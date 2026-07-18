using System;
using System.Collections.Generic;
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
        /// 로비에서 선택한 시작 폼(RunStartContext)을 활성 슬롯에 반영한다.
        /// 이미 슬롯에 있는 폼이면 그 슬롯을 활성화(스왑 대상 보존)하고, 슬롯에 없는 신규 폼(예: FormC)
        /// 이면 활성 슬롯(0)에 주입한다 — 나머지 슬롯은 스왑 대상으로 유지. 미선택이면 씬 기본 유지.
        /// (미드런 폼 보상은 EquipForm으로 슬롯을 지정 주입하는 동일 경로를 쓴다.)
        /// </summary>
        private void ApplyStartingForm()
        {
            if (!RunStartContext.HasStartingForm) return;

            var form = RunStartContext.StartingForm;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].formId == form.formId)
                {
                    activeSlot = i;
                    Debug.Log($"[FormController] 시작 폼 적용: {form.formId} (기존 slot {i})");
                    return;
                }
            }

            // 슬롯에 없는 신규 시작 폼 → 활성 슬롯(0)에 주입.
            EquipForm(form, 0, activate: true);
            Debug.Log($"[FormController] 시작 폼 주입: {form.formId} → slot 0 (스왑 대상: {(OtherForm != null ? OtherForm.formId : "없음")})");
        }

        /// <summary>
        /// 이 런의 시작 폼을 반환한다. RunStartContext 선택을 우선 반영하며, ApplyStartingForm의
        /// Awake 실행 순서와 무관하게 컨텍스트 에셋을 직접 돌려준다(HP 초기화가 Awake에서 시작 폼 배율을
        /// 순서 안전하게 읽기 위함). 미선택·부재 시 씬 기본 활성 슬롯을 반환한다.
        /// </summary>
        public FormData ResolveStartingForm()
        {
            if (RunStartContext.HasStartingForm) return RunStartContext.StartingForm;
            return (activeSlot >= 0 && activeSlot < slots.Length) ? slots[activeSlot] : null;
        }

        public FormState State => state;
        public FormData CurrentForm => slots[activeSlot];
        public FormData OtherForm => slots[1 - activeSlot];
        public int ActiveSlot => activeSlot;
        public int SlotCount => slots.Length;

        /// <summary>슬롯 index의 폼(범위 밖이면 null). 폼 보상 슬롯 선택 모달의 표시용.</summary>
        public FormData GetSlot(int index) => (index >= 0 && index < slots.Length) ? slots[index] : null;

        /// <summary>
        /// 현재 슬롯에 장착된(=이 런에서 보유 중인) 폼 목록. 별도 인벤토리가 없으므로 슬롯이 곧 보유 상태다.
        /// 폼 보상 룸의 '미보유 폼 우선 제시' 판정에 쓰인다.
        /// 호출 빈도가 룸 클리어당 1회라 캐시 재사용(별칭 위험)보다 매번 새 리스트를 주는 쪽을 택했다.
        /// </summary>
        public List<FormData> GetOwnedForms()
        {
            var owned = new List<FormData>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && !owned.Contains(slots[i])) owned.Add(slots[i]);
            }
            return owned;
        }
        public float CurrentCooldown => currentCooldown;
        public float CooldownProgress => SwapCooldown <= 0f ? 1f : 1f - (currentCooldown / SwapCooldown);
        public bool CanSwap => state == FormState.Ready && currentCooldown <= 0f && OtherForm != null && IsGateOpen;

        /// <summary>
        /// 외부(플레이어 FSM) 교체 허용 게이트. 미등록이면 항상 허용해 로비·테스트 씬에서 폼 단독 동작을 보장한다.
        /// FormController가 Player 네임스페이스를 역참조하지 않도록 델리게이트로 주입받는다(의존 방향 유지).
        /// </summary>
        private Func<bool> swapGate;
        private bool IsGateOpen => swapGate == null || swapGate();

        /// <summary>
        /// 교체 허용 조건을 주입한다(피격 경직·시전 중·사망 등 상태 차단용).
        /// PlayerStateMachine이 OnEnable에서 등록한다.
        /// </summary>
        public void SetSwapGate(Func<bool> gate) => swapGate = gate;

        /// <summary>
        /// 자신이 등록한 게이트만 해제한다. 등록자가 여럿일 때(프리팹 중복 인스턴스·테스트 씬)
        /// 먼저 비활성화되는 쪽이 남의 게이트까지 지우는 것을 막는다.
        /// </summary>
        public void ClearSwapGate(Func<bool> expected)
        {
            if (swapGate == expected) swapGate = null;
        }

        public event Action<FormData, FormData> OnSwapStarted;
        public event Action<FormData, FormData> OnSwapCompleted;

        /// <summary>
        /// 폼을 지정 슬롯에 주입한다. 로비 시작 폼 적용과 미드런 폼 보상(이벤트/보상 획득) 공용 진입점.
        /// activate=true면 해당 슬롯을 활성 슬롯으로 만든다(현재 폼이 새 폼으로 즉시 전환).
        /// 미드런에서 '플레이어가 슬롯 선택' UI는 이 API에 선택한 slotIndex를 넘겨 재사용한다.
        /// </summary>
        public void EquipForm(FormData form, int slotIndex, bool activate = true)
        {
            if (form == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            slots[slotIndex] = form;
            if (activate) activeSlot = slotIndex;
        }

        /// <summary>
        /// 폼 교체 요청. 쿨다운·상태 가드 통과 시 즉시 전환 시작.
        /// 성공 시 OnSwapStarted + GameEvents.OnFormSwapped 발행.
        /// </summary>
        public bool RequestSwap()
        {
            if (!CanSwap)
            {
                Debug.Log($"[FormController] Swap 거부 — state={state}, cooldown={currentCooldown:F2}s, other={(OtherForm != null ? OtherForm.formId : "null (slot 미할당)")}, gate={(IsGateOpen ? "open" : "blocked (피격/시전/사망)")}");
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
