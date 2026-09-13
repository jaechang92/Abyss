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

        /// <summary>
        /// 피격 상태 기본 지속시간. 규칙은 공격과 같다 — <b>Hit 클립의 길이가 이 값을 따른다.</b>
        ///
        /// 🔴 <b>이 값이 없던 동안 Hit 은 판정 한 번만 유지됐다</b>(2026-09-13 발견). 들어간 바로 다음 판정에서
        /// Idle·Run 으로 나가 클립의 첫 프레임만 보이고 끝났다. 클립이 없어서 안 드러났을 뿐이다.
        ///
        /// ⚠️ 이것은 <b>애니메이션 상태</b>만 붙잡는다. 이동·공격 입력을 막는 경직은 별도 규칙이다.
        /// 단 <see cref="CanSwapForm"/> 은 Hit 을 차단하므로 <b>피격 후 이 시간 동안 폼 교체가 막힌다</b>(기획 의도).
        /// </summary>
        public const float DefaultHitDuration = 0.3f;

        [Header("시간제 상태 지속시간 (이후 일반 전이로 복귀)")]
        [SerializeField, Min(0.01f)] private float attackLightDuration = DefaultAttackLightDuration;
        [SerializeField, Min(0.01f)] private float attackHeavyDuration = DefaultAttackHeavyDuration;
        [SerializeField, Min(0.01f)] private float hitDuration = DefaultHitDuration;

        [Header("디버그")]
        [SerializeField] private bool logStateChanges;

        private StateMachine fsm;
        private bool isFormSwapping;
        private bool hitQueued;
        private float timedStateExitTime;  // 공격·피격 공용 — 한 번에 한 상태만 현재이므로 시각 하나로 충분하다
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
            timedStateExitTime = Time.time + attackLightDuration;
            fsm.ForceTransitionTo(PlayerStateIds.AttackLight);
        }

        /// <summary>외부에서 AttackHeavy 상태 진입 유도.</summary>
        public void TriggerAttackHeavy()
        {
            if (!CanAttack()) return;
            timedStateExitTime = Time.time + attackHeavyDuration;
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

            // 피격은 시간제 게이트보다 먼저 본다 — 공격 중에도, 피격 중 다시 맞아도 끊고 들어가 시간을 새로 잰다.
            if (hitQueued)
            {
                hitQueued = false;
                timedStateExitTime = Time.time + hitDuration;
                fsm.ForceTransitionTo(PlayerStateIds.Hit);
                return;
            }

            if (isFormSwapping) return;

            if (IsTimedStateHeld(current, Time.time, timedStateExitTime)) return;

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

        /// <summary>
        /// 지속시간이 끝나기 전이라 <b>현재 상태를 붙잡아야 하는가</b>. 시간제 상태는 공격 2종과 피격이다.
        ///
        /// 📌 순수 함수로 뺀 이유: Hit 이 이 규칙에서 빠져 있었는데 오류도 로그도 없었다. <c>Time.time</c> 을 읽지 않고
        /// 인자로 받아야 EditMode 에서 고정할 수 있다. 사망·재피격이 이것보다 <b>먼저</b> 판정된다는 순서는
        /// <see cref="EvaluateTransitions"/> 가 지킨다.
        /// </summary>
        public static bool IsTimedStateHeld(string currentStateId, float now, float exitTime)
        {
            bool isTimedState = currentStateId == PlayerStateIds.AttackLight
                || currentStateId == PlayerStateIds.AttackHeavy
                || currentStateId == PlayerStateIds.Hit;
            return isTimedState && now < exitTime;
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
