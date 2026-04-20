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

        [Header("디버그")]
        [SerializeField] private bool logStateChanges;

        private StateMachine fsm;
        private bool isFormSwapping;
        private bool hitQueued;

        public StateMachine Machine => fsm;
        public string CurrentStateId => fsm != null ? fsm.CurrentStateId : string.Empty;

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
            if (CanAttack()) fsm.ForceTransitionTo(PlayerStateIds.AttackLight);
        }

        /// <summary>외부에서 AttackHeavy 상태 진입 유도.</summary>
        public void TriggerAttackHeavy()
        {
            if (CanAttack()) fsm.ForceTransitionTo(PlayerStateIds.AttackHeavy);
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
                return;
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
