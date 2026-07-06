using System;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 플레이어의 2슬롯 폼 관리. Analyst MF-8 확정 — FSM 대신 enum 상태 + float 쿨다운.
    /// 프로토 스펙: 2슬롯 고정, 교체 CD 1.5s, 교체 연출 0.3s (Docs/game-design/02-form-change-system.md).
    /// 실제 입력 연결은 P-09 InputRouter에서. 지금은 RequestSwap() public API만 노출.
    /// </summary>
    public sealed class FormController : MonoBehaviour
    {
        public enum FormState
        {
            Ready,
            Swapping
        }

        [Header("슬롯 (2슬롯 고정)")]
        [SerializeField] private FormData[] slots = new FormData[2];
        [SerializeField] private int activeSlot;

        [Header("런 설정 (P-14 — RunConfig SO 참조)")]
        [Tooltip("비워두면 Resources/Data/RunConfig를 자동 로드")]
        [SerializeField] private RunConfig config;

        // config 미할당·로드 실패 시 폴백 기본값
        private const float DEFAULT_SWAP_COOLDOWN = 1.5f;
        private const float DEFAULT_SWAP_ANIM = 0.3f;

        private FormState state = FormState.Ready;
        private float currentCooldown;
        private float currentSwapTimer;

        private float SwapCooldown => config != null ? config.formSwapCooldown : DEFAULT_SWAP_COOLDOWN;
        private float SwapAnimationDuration => config != null ? config.formSwapAnimationDuration : DEFAULT_SWAP_ANIM;

        private void Awake()
        {
            // RunConfig SoT: SerializeField 오버라이드 우선, 없으면 공유 RunConfigProvider.Current.
            config = RunConfigProvider.Resolve(config);

            ApplyStartingForm();
        }

        /// <summary>
        /// 로비에서 선택한 시작 폼(RunStartContext)을 활성 슬롯으로 반영한다.
        /// 슬롯 폼 구성은 씬 직렬화 그대로 두고 activeSlot만 선택한다.
        /// 미선택이거나 슬롯에 없는 폼이면 씬 기본 활성 슬롯을 유지한다.
        /// </summary>
        private void ApplyStartingForm()
        {
            if (!RunStartContext.HasStartingForm) return;

            string id = RunStartContext.StartingFormId;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].formId == id)
                {
                    activeSlot = i;
                    Debug.Log($"[FormController] 시작 폼 적용: {id} (slot {i})");
                    return;
                }
            }
            Debug.LogWarning($"[FormController] 시작 폼 '{id}'이(가) 슬롯에 없음 — 기본 활성 슬롯({activeSlot}) 유지.");
        }

        /// <summary>
        /// 이 런의 시작 폼을 반환한다. RunStartContext 선택을 우선 반영하되, ApplyStartingForm의
        /// Awake 실행 순서와 무관하게 슬롯 직렬화만으로 해석한다(HP 초기화가 Awake에서 시작 폼 배율을
        /// 순서 안전하게 읽기 위함). 미선택·부재 시 씬 기본 활성 슬롯을 반환한다.
        /// </summary>
        public FormData ResolveStartingForm()
        {
            if (RunStartContext.HasStartingForm)
            {
                string id = RunStartContext.StartingFormId;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].formId == id) return slots[i];
                }
            }
            return (activeSlot >= 0 && activeSlot < slots.Length) ? slots[activeSlot] : null;
        }

        public FormState State => state;
        public FormData CurrentForm => slots[activeSlot];
        public FormData OtherForm => slots[1 - activeSlot];
        public int ActiveSlot => activeSlot;
        public float CurrentCooldown => currentCooldown;
        public float CooldownProgress => SwapCooldown <= 0f ? 1f : 1f - (currentCooldown / SwapCooldown);
        public bool CanSwap => state == FormState.Ready && currentCooldown <= 0f && OtherForm != null;

        public event Action<FormData, FormData> OnSwapStarted;
        public event Action<FormData, FormData> OnSwapCompleted;

        /// <summary>
        /// 특정 슬롯에 폼을 배정. 런 시작·해금 해제·디버그용.
        /// </summary>
        public void AssignSlot(int slotIndex, FormData form)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            slots[slotIndex] = form;
        }

        /// <summary>
        /// 폼 교체 요청. 쿨다운·상태 가드 통과 시 즉시 전환 시작.
        /// 성공 시 OnSwapStarted + GameEvents.OnFormSwapped 발행.
        /// </summary>
        public bool RequestSwap()
        {
            if (!CanSwap)
            {
                Debug.Log($"[FormController] Swap 거부 — state={state}, cooldown={currentCooldown:F2}s, other={(OtherForm != null ? OtherForm.formId : "null (slot 미할당)")}");
                return false;
            }

            var previous = CurrentForm;
            activeSlot = 1 - activeSlot;
            var next = CurrentForm;

            state = FormState.Swapping;
            currentSwapTimer = SwapAnimationDuration;
            currentCooldown = SwapCooldown;

            Debug.Log($"[FormController] Swap 실행: {previous?.formId ?? "?"} → {next?.formId ?? "?"}");
            OnSwapStarted?.Invoke(previous, next);
            GameEvents.RaiseFormSwapped(previous, next);
            return true;
        }

        private void Update()
        {
            if (currentCooldown > 0f)
            {
                currentCooldown = Mathf.Max(0f, currentCooldown - Time.deltaTime);
            }

            if (state == FormState.Swapping)
            {
                currentSwapTimer -= Time.deltaTime;
                if (currentSwapTimer <= 0f)
                {
                    state = FormState.Ready;
                    OnSwapCompleted?.Invoke(OtherForm, CurrentForm);
                }
            }

            if (CurrentForm != null && RunManager.HasInstance)
            {
                RunManager.Instance.RegisterFormPlaytime(CurrentForm.formId, Time.deltaTime);
            }
        }

        [ContextMenu("Debug: Request Swap")]
        private void DebugRequestSwap()
        {
            bool ok = RequestSwap();
            Debug.Log($"[FormController] Debug Swap → {(ok ? "성공" : "차단됨")} (CD={currentCooldown:F2}s, state={state})");
        }
    }
}
