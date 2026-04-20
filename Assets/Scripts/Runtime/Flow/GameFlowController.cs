using Abyss.Runtime.Events;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 게임 흐름 3상태(RunActive/DraftOpen/Result) FSM 컨트롤러.
    /// GameEvents를 구독해 상태 전이를 유발한다. Analyst MF-8 확정 — 폼 FSM 제거·게임플로우만 FSM 사용.
    /// </summary>
    [RequireComponent(typeof(StateMachine))]
    public sealed class GameFlowController : MonoBehaviour
    {
        private StateMachine fsm;

        private void Awake()
        {
            fsm = GetComponent<StateMachine>();
            RegisterStates();
        }

        private void OnEnable()
        {
            GameEvents.OnDraftOpened += HandleDraftOpened;
            GameEvents.OnDraftClosed += HandleDraftClosed;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnDraftOpened -= HandleDraftOpened;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void Start()
        {
            fsm.StartStateMachine(GameFlowStateIds.RunActive);
        }

        private void RegisterStates()
        {
            fsm.AddState(new RunActiveState());
            fsm.AddState(new DraftOpenState());
            fsm.AddState(new ResultState());
        }

        private async void HandleDraftOpened()
        {
            await fsm.ForceTransitionToAsync(GameFlowStateIds.DraftOpen);
        }

        private async void HandleDraftClosed()
        {
            await fsm.ForceTransitionToAsync(GameFlowStateIds.RunActive);
        }

        private async void HandleRunEnded()
        {
            await fsm.ForceTransitionToAsync(GameFlowStateIds.Result);
        }
    }
}
