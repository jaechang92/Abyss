using System.Threading;
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

    public sealed class RunActiveState : State
    {
        public override string Name => GameFlowStateIds.RunActive;

        protected override Awaitable OnEnterState(CancellationToken cancellationToken)
        {
            Time.timeScale = 1f;
            Debug.Log("[GameFlow] RunActive 진입 (timeScale=1)");
            return AwaitableHelper.CompletedTask;
        }
    }

    public sealed class DraftOpenState : State
    {
        public override string Name => GameFlowStateIds.DraftOpen;

        protected override Awaitable OnEnterState(CancellationToken cancellationToken)
        {
            Time.timeScale = 0f;
            Debug.Log("[GameFlow] DraftOpen 진입 (timeScale=0)");
            return AwaitableHelper.CompletedTask;
        }

        protected override Awaitable OnExitState(CancellationToken cancellationToken)
        {
            Time.timeScale = 1f;
            return AwaitableHelper.CompletedTask;
        }
    }

    public sealed class ResultState : State
    {
        public override string Name => GameFlowStateIds.Result;

        protected override Awaitable OnEnterState(CancellationToken cancellationToken)
        {
            Time.timeScale = 0f;
            Debug.Log("[GameFlow] Result 진입");
            return AwaitableHelper.CompletedTask;
        }
    }
}
