using Abyss.Runtime.Form;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// PlayerCharacter 상태 관리 9상태 FSM. FSM_Core.StateMachine을 구성 요소로 사용.
    /// 전이 조건은 매 프레임 PlayerCharacter의 public 속성으로 평가 (이벤트 기반 ICondition 대신).
    /// FormController의 OnSwapStarted/Completed로 폼 교체 중 상태 전이 차단.
    /// </summary>
    [RequireComponent(typeof(StateMachine))]
    public sealed class PlayerStateMachine : MonoBehaviour
    {
        [SerializeField] private PlayerCharacter player;
        [SerializeField] private FormController formController;

        /// <summary>
        /// 공격 상태 기본 지속시간.
        ///
        /// 🔴 <b>공격 클립의 길이가 이 값을 따른다</b> — <c>PlayerAnimationBuilder</c>가 한 번 재생하고 끝나는
        /// 클립의 프레임레이트를 <b>프레임 수 ÷ 이 값</b>으로 역산한다. 클립이 더 길면 끝을 못 보고 잘리고,
        /// 더 짧으면 마지막 그림으로 서 있는 시간이 생긴다. 어느 쪽도 오류가 아니라 <b>어색함</b>으로만 나타난다.
        ///
        /// ⚠️ 인스펙터에서 값을 바꾸면 <b>그 폼의 공격 클립을 다시 구워야</b> 어긋나지 않는다.
        /// 직렬화된 값이 여기의 상수를 이기므로, 상수만 고치는 것으로는 프리팹이 안 따라온다.
        /// </summary>
        public const float DefaultAttackLightDuration = 0.25f;

        /// <inheritdoc cref="DefaultAttackLightDuration"/>
        public const float DefaultAttackHeavyDuration = 0.6f;

        [Header("공격 상태 지속시간 (이후 일반 전이로 복귀)")]
        [SerializeField, Min(0.01f)] private float attackLightDuration = DefaultAttackLightDuration;
        [SerializeField, Min(0.01f)] private float attackHeavyDuration = DefaultAttackHeavyDuration;

        [Header("디버그")]
        [SerializeField] private bool logStateChanges;

        private StateMachine fsm;
        private bool isFormSwapping;
        private bool hitQueued;
        private float attackStateExitTime;
        private System.Func<bool> swapGate;  // 해제 시 동일 인스턴스 비교용(남의 게이트 삭제 방지)

        public StateMachine Machine => fsm;
        public string CurrentStateId => fsm != null ? fsm.CurrentStateId : string.Empty;

        /// <summary>
        /// 현재 상태에서 폼 교체가 허용되는지. 피격 경직(Hit)·시전 중(AttackLight/Heavy)·사망(Dead)에서 차단한다.
        /// Dash는 허용 — 대시 캔슬 교체는 의도된 조작감(기획 02-form-change-system.md).
        /// FormController.SetSwapGate로 주입되어 RequestSwap 가드에 합류한다.
        /// </summary>
        public bool CanSwapForm
        {
            get
            {
                if (player != null && player.IsDead) return false;
                if (fsm == null || !fsm.IsRunning) return true;  // FSM 미가동 시엔 폼 단독 동작 보장

                string id = fsm.CurrentStateId;
                return id != PlayerStateIds.Hit
                    && id != PlayerStateIds.Dead
                    && id != PlayerStateIds.AttackLight
                    && id != PlayerStateIds.AttackHeavy;
            }
        }

        private void Awake()
        {
            fsm = GetComponent<StateMachine>();
            if (player == null) player = GetComponent<PlayerCharacter>();
            if (formController == null) formController = GetComponent<FormController>();

            RegisterStates();
        }

        private void OnEnable()
        {
            if (formController != null)
            {
                formController.OnSwapStarted += HandleSwapStarted;
                formController.OnSwapCompleted += HandleSwapCompleted;
                swapGate ??= () => CanSwapForm;
                formController.SetSwapGate(swapGate);
            }
            if (player != null)
            {
                player.OnHpChanged += HandleHpChanged;
            }
        }

        private void OnDisable()
        {
            if (formController != null)
            {
                formController.OnSwapStarted -= HandleSwapStarted;
                formController.OnSwapCompleted -= HandleSwapCompleted;
                formController.ClearSwapGate(swapGate);
            }
            if (player != null)
            {
                player.OnHpChanged -= HandleHpChanged;
            }
        }

        private void Start()
        {
            fsm.StartStateMachine(PlayerStateIds.Idle);
        }

        private void Update()
        {
            if (fsm == null || !fsm.IsRunning) return;
            EvaluateTransitions();
        }

        /// <summary>외부에서 AttackLight 상태 진입 유도. PlayerCharacter.Combat의 OnAttack에서 호출.</summary>
        public void TriggerAttackLight()
        {
            if (!CanAttack()) return;
            attackStateExitTime = Time.time + attackLightDuration;
            fsm.ForceTransitionTo(PlayerStateIds.AttackLight);
        }

        /// <summary>외부에서 AttackHeavy 상태 진입 유도.</summary>
        public void TriggerAttackHeavy()
        {
            if (!CanAttack()) return;
            attackStateExitTime = Time.time + attackHeavyDuration;
            fsm.ForceTransitionTo(PlayerStateIds.AttackHeavy);
        }

        private bool CanAttack()
        {
            if (fsm == null || !fsm.IsRunning) return false;
            if (player != null && player.IsDead) return false;
            if (isFormSwapping) return false;
            return true;
        }

        private void RegisterStates()
        {
            fsm.AddState(new NamedState(PlayerStateIds.Idle, () => LogEnter(PlayerStateIds.Idle)));
            fsm.AddState(new NamedState(PlayerStateIds.Run, () => LogEnter(PlayerStateIds.Run)));
            fsm.AddState(new NamedState(PlayerStateIds.Jump, () => LogEnter(PlayerStateIds.Jump)));
            fsm.AddState(new NamedState(PlayerStateIds.Fall, () => LogEnter(PlayerStateIds.Fall)));
            fsm.AddState(new NamedState(PlayerStateIds.Dash, () => LogEnter(PlayerStateIds.Dash)));
            fsm.AddState(new NamedState(PlayerStateIds.AttackLight, () => LogEnter(PlayerStateIds.AttackLight)));
            fsm.AddState(new NamedState(PlayerStateIds.AttackHeavy, () => LogEnter(PlayerStateIds.AttackHeavy)));
            fsm.AddState(new NamedState(PlayerStateIds.Hit, () => LogEnter(PlayerStateIds.Hit)));
            fsm.AddState(new NamedState(PlayerStateIds.Dead, () => LogEnter(PlayerStateIds.Dead)));
        }

        private void LogEnter(string stateId)
        {
            if (logStateChanges) Debug.Log($"[PlayerFSM] {stateId}");
        }

        private void EvaluateTransitions()
        {
            if (player == null) return;

            string current = fsm.CurrentStateId;

            if (player.IsDead)
            {
                if (current != PlayerStateIds.Dead) fsm.ForceTransitionTo(PlayerStateIds.Dead);
                return;
            }
            if (current == PlayerStateIds.Dead) return;

            if (hitQueued)
            {
                hitQueued = false;
                fsm.ForceTransitionTo(PlayerStateIds.Hit);
                return;
            }

            if (isFormSwapping) return;

            if (current == PlayerStateIds.AttackLight || current == PlayerStateIds.AttackHeavy)
            {
                if (Time.time < attackStateExitTime) return;
            }

            if (player.IsDashing)
            {
                if (current != PlayerStateIds.Dash) fsm.ForceTransitionTo(PlayerStateIds.Dash);
                return;
            }
            if (current == PlayerStateIds.Dash) return;

            if (!player.IsGrounded)
            {
                if (player.Velocity.y > 0.01f)
                {
                    if (current != PlayerStateIds.Jump) fsm.ForceTransitionTo(PlayerStateIds.Jump);
                }
                else
                {
                    if (current != PlayerStateIds.Fall) fsm.ForceTransitionTo(PlayerStateIds.Fall);
                }
                return;
            }

            if (Mathf.Abs(player.Velocity.x) > 0.1f)
            {
                if (current != PlayerStateIds.Run) fsm.ForceTransitionTo(PlayerStateIds.Run);
            }
            else
            {
                if (current != PlayerStateIds.Idle) fsm.ForceTransitionTo(PlayerStateIds.Idle);
            }
        }

        private void HandleSwapStarted(FormData previous, FormData next) => isFormSwapping = true;
        private void HandleSwapCompleted(FormData previous, FormData next) => isFormSwapping = false;

        private void HandleHpChanged(int previousHp, int currentHp)
        {
            if (currentHp < previousHp && currentHp > 0)
            {
                hitQueued = true;
            }
        }
    }
}
