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
            GameEvents.OnPauseRequested += HandlePauseRequested;
            GameEvents.OnResumeRequested += HandleResumeRequested;
        }

        private void OnDisable()
        {
            GameEvents.OnDraftOpened -= HandleDraftOpened;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnPauseRequested -= HandlePauseRequested;
            GameEvents.OnResumeRequested -= HandleResumeRequested;
        }

        /// <summary>현재 일시정지 상태인지. UI·입력이 정지 토글 여부를 판단할 때 참조한다.</summary>
        public bool IsPaused => fsm != null && fsm.CurrentStateId == GameFlowStateIds.Paused;

        private void Start()
        {
            fsm.StartStateMachine(GameFlowStateIds.RunActive);
        }

        private void RegisterStates()
        {
            fsm.AddState(new RunActiveState());
            fsm.AddState(new DraftOpenState());
            fsm.AddState(new ResultState());
            fsm.AddState(new PausedState());
        }

        // 동기 전이(ForceTransitionTo → OnEnterSync)로 상태를 바꾼다. 상태의 OnEnterStateSync가 정지/재개를
        // 즉시 적용하므로, 드래프트/런종료 진입 순간 플레이어·적·발사체가 함께 멈춘다. async 전이는 Unity
        // Awaitable 지연 실행 탓에 정지가 늦어 적이 계속 움직이는 버그가 있어 사용하지 않는다.
        // ForceTransitionTo는 내부에서 예외를 처리하므로 async void·try/catch가 불필요하다.
        private void HandleDraftOpened() => fsm.ForceTransitionTo(GameFlowStateIds.DraftOpen);
        private void HandleDraftClosed() => fsm.ForceTransitionTo(GameFlowStateIds.RunActive);
        private void HandleRunEnded() => fsm.ForceTransitionTo(GameFlowStateIds.Result);

        /// <summary>
        /// 정지는 RunActive에서만 받는다. 드래프트·결과 화면은 이미 정지 상태이므로 여기서 정지를 겹치면
        /// 해제할 때 RunActive로 복귀해 그쪽 정지가 풀려버린다(드래프트 창이 열린 채 게임이 돌아감).
        /// </summary>
        private void HandlePauseRequested()
        {
            if (fsm.CurrentStateId != GameFlowStateIds.RunActive) return;
            fsm.ForceTransitionTo(GameFlowStateIds.Paused);
            GameEvents.RaiseGamePaused(); // 수락된 뒤에만 '사실'을 알린다
        }

        /// <summary>해제도 Paused에서만 받는다 — 다른 상태에서 들어온 해제 요청이 정지를 풀지 않게.</summary>
        private void HandleResumeRequested()
        {
            if (fsm.CurrentStateId != GameFlowStateIds.Paused) return;
            fsm.ForceTransitionTo(GameFlowStateIds.RunActive);
            GameEvents.RaiseGameResumed();
        }
    }
}
