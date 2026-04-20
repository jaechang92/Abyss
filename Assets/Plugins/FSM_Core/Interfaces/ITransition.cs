using System;
using System.Collections.Generic;

namespace FSM.Core
{
    public interface ITransition
    {
        string Id { get; }
        string FromStateId { get; }
        string ToStateId { get; }
        int Priority { get; }
        bool IsEnabled { get; set; }

        IReadOnlyList<ICondition> Conditions { get; }

        void AddCondition(ICondition condition);
        void RemoveCondition(ICondition condition);
        bool CanTransition();

        event Action<ITransition> OnTransitionTriggered;
    }
}