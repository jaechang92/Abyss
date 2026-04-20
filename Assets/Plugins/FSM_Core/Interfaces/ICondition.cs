using UnityEngine;

namespace FSM.Core
{
    public interface ICondition
    {
        string Id { get; }
        bool IsInverted { get; set; }
        bool IsEnabled { get; set; }

        bool Evaluate(GameObject owner, IStateMachine stateMachine);
        void Initialize(GameObject owner, IStateMachine stateMachine);
        void Reset();
    }

    public enum ConditionOperator
    {
        And,
        Or
    }
}