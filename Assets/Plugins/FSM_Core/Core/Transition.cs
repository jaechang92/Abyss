using System;
using System.Collections.Generic;
using System.Linq;

namespace FSM.Core
{
    public class Transition : ITransition
    {
        private List<ICondition> conditions = new List<ICondition>();

        public string Id { get; private set; }
        public string FromStateId { get; private set; }
        public string ToStateId { get; private set; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; } = true;

        public IReadOnlyList<ICondition> Conditions => conditions;

        public event Action<ITransition> OnTransitionTriggered;

        public Transition(string id, string fromStateId, string toStateId, int priority = 0)
        {
            Id = id;
            FromStateId = fromStateId;
            ToStateId = toStateId;
            Priority = priority;
        }

        public void AddCondition(ICondition condition)
        {
            if (condition != null && !conditions.Contains(condition))
            {
                conditions.Add(condition);
            }
        }

        public void RemoveCondition(ICondition condition)
        {
            conditions.Remove(condition);
        }

        public virtual bool CanTransition()
        {
            if (!IsEnabled || conditions.Count == 0) return false;

            return conditions.All(condition => condition.IsEnabled &&
                (condition.IsInverted ? !condition.Evaluate(null, null) : condition.Evaluate(null, null)));
        }
    }

    public class ConditionalTransition : Transition
    {
        private readonly Func<bool> conditionFunc;

        public ConditionalTransition(string id, string fromStateId, string toStateId,
            Func<bool> condition, int priority = 0)
            : base(id, fromStateId, toStateId, priority)
        {
            conditionFunc = condition;
        }

        public override bool CanTransition()
        {
            if (!IsEnabled) return false;
            return conditionFunc?.Invoke() ?? false;
        }
    }

    public class EventBasedTransition : Transition
    {
        private readonly string eventId;
        private readonly StateMachine stateMachine;

        public EventBasedTransition(string id, string fromStateId, string toStateId,
            string eventId, StateMachine stateMachine, int priority = 0)
            : base(id, fromStateId, toStateId, priority)
        {
            this.eventId = eventId;
            this.stateMachine = stateMachine;
        }

        public override bool CanTransition()
        {
            if (!IsEnabled) return false;
            return stateMachine?.ConsumeEvent(eventId) ?? false;
        }
    }
}