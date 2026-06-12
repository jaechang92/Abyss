using System;
using Abyss.Runtime.Events;
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
            if (config == null)
            {
                config = Resources.Load<RunConfig>("Data/RunConfig");
                if (config == null)
                {
                    Debug.LogWarning("[FormController] RunConfig 로드 실패 — 폴백 기본값 사용. Assets/Resources/Data/RunConfig.asset 확인 필요.");
                }
            }
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
