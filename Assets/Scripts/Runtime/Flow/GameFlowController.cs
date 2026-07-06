using System;
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

        // GameEvents 콜백(동기 Action 시그니처)이라 fire-and-forget async void가 불가피하다.
        // 미관측 예외가 프로세스로 전파되지 않도록 반드시 SafeTransition의 try/catch를 경유한다.
        private void HandleDraftOpened() => SafeTransition(GameFlowStateIds.DraftOpen);
        private void HandleDraftClosed() => SafeTransition(GameFlowStateIds.RunActive);
        private void HandleRunEnded() => SafeTransition(GameFlowStateIds.Result);

        /// <summary>
        /// 비동기 FSM 전이를 fire-and-forget으로 실행하되 async void의 미관측 예외를 가드한다.
        /// 파괴/씬 전환 중 취소(OperationCanceledException)는 정상 흐름으로 흡수, 그 외 예외는 로깅.
        /// ForceTransitionToAsync는 CancellationToken 인자를 노출하지 않으나(FSM_Core는 Plugins
        /// Copy-as-is, 수정 금지) StateMachine이 내부 CTS로 종료 취소를 처리한다. 전이 완료 후 이
        /// 컴포넌트를 건드리지 않으므로 파괴 후 continuation도 안전하다.
        /// </summary>
        private async void SafeTransition(string stateId)
        {
            try
            {
                await fsm.ForceTransitionToAsync(stateId);
            }
            catch (OperationCanceledException)
            {
                // FSM 종료/씬 전환 중 전이 취소 — 정상.
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
    }
}
