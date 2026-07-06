using Abyss.Runtime.Events;
using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 게임 흐름 3상태(RunActive/DraftOpen/Result) FSM 컨트롤러.
    /// GameEvents를 구독해 상태 전이를 유발한다. Analyst MF-8 확정 — 폼 FSM 제거·게임플로우만 FSM 사용.
    /// 전역 정지/재개는 FSM 상태(GameFlowStates)가 소유하며, 여기서는 '동기' 전이로 진입시켜 즉시 적용한다.
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

        // 동기 전이(ForceTransitionTo → OnEnterSync)로 상태를 바꾼다. 상태의 OnEnterStateSync가 정지/재개를
        // 즉시 적용하므로, 드래프트/런종료 진입 순간 플레이어·적·발사체가 함께 멈춘다. async 전이는 Unity
        // Awaitable 지연 실행 탓에 정지가 늦어 적이 계속 움직이는 버그가 있어 사용하지 않는다.
        // ForceTransitionTo는 내부에서 예외를 처리하므로 async void·try/catch가 불필요하다.
        private void HandleDraftOpened() => fsm.ForceTransitionTo(GameFlowStateIds.DraftOpen);
        private void HandleDraftClosed() => fsm.ForceTransitionTo(GameFlowStateIds.RunActive);
        private void HandleRunEnded() => fsm.ForceTransitionTo(GameFlowStateIds.Result);
    }
}
