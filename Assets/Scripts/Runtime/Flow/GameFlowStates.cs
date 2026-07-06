using System.Threading;
using Abyss.Runtime.Feedback;
using FSM.Core;
using FSM.Utils;
using UnityEngine;

namespace Abyss.Runtime.Flow
{
    public static class GameFlowStateIds
    {
        public const string RunActive = "RunActive";
        public const string DraftOpen = "DraftOpen";
        public const string Result = "Result";
    }

    /// <summary>
    /// 게임 흐름 상태의 공통 전역 정지/재개. 정지 진입 시 진행 중인 히트스탑을 원복 없이 취소해,
    /// 히트스탑의 지연 원복(timeScale=1)이 정지를 덮어써 적이 다시 움직이는 버그를 막는다
    /// (사망 킬링블로우 히트스탑이 Result 정지를 풀던 문제).
    /// </summary>
    internal static class GameFlowTime
    {
        public static void Pause()
        {
            HitstopController.GetInstanceSafe()?.CancelActive();
            Time.timeScale = 0f;
        }

        public static void Resume()
        {
            Time.timeScale = 1f;
        }
    }

    // 게임 흐름 제어(전역 정지/재개)는 FSM 상태가 소유한다(SoT). 단, GameFlowController는 '동기' 전이
    // (ForceTransitionTo → OnEnterSync)로 진입시켜 정지가 이벤트 시점에 즉시 적용되게 한다.
    // (async 전이 ForceTransitionToAsync는 Unity Awaitable 지연 실행 탓에 정지가 늦어져 적이 계속 움직인다)

    public sealed class RunActiveState : State
    {
        public override string Name => GameFlowStateIds.RunActive;

        // 드래프트 종료 등 정상 재개는 동기 전이로 진입.
        protected override void OnEnterStateSync()
        {
            GameFlowTime.Resume();
            Debug.Log("[GameFlow] RunActive 진입 (timeScale=1)");
        }

        // 최초 StartStateMachine은 비동기 경로로 초기 상태를 진입시키므로, 시작·복구 안전망으로 async에도 둔다.
        protected override Awaitable OnEnterState(CancellationToken cancellationToken)
        {
            GameFlowTime.Resume();
            return AwaitableHelper.CompletedTask;
        }
    }

    public sealed class DraftOpenState : State
    {
        public override string Name => GameFlowStateIds.DraftOpen;

        protected override void OnEnterStateSync()
        {
            GameFlowTime.Pause();
            Debug.Log("[GameFlow] DraftOpen 진입 (timeScale=0)");
        }
    }

    public sealed class ResultState : State
    {
        public override string Name => GameFlowStateIds.Result;

        protected override void OnEnterStateSync()
        {
            GameFlowTime.Pause();
            Debug.Log("[GameFlow] Result 진입 (timeScale=0)");
        }
    }
}
