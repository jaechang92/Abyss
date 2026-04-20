using UnityEngine;

namespace FSM.Core
{
    public abstract class Condition : ICondition
    {
        protected GameObject owner;
        protected IStateMachine stateMachine;

        public string Id { get; protected set; }
        public bool IsInverted { get; set; }
        public bool IsEnabled { get; set; } = true;

        public virtual void Initialize(GameObject owner, IStateMachine stateMachine)
        {
            this.owner = owner;
            this.stateMachine = stateMachine;
        }

        public abstract bool Evaluate(GameObject owner, IStateMachine stateMachine);

        public virtual void Reset() { }
    }

    public class TimeCondition : Condition
    {
        private float duration;
        private float startTime;
        private bool hasStarted;

        public TimeCondition(string id, float duration)
        {
            Id = id;
            this.duration = duration;
        }

        public override void Initialize(GameObject owner, IStateMachine stateMachine)
        {
            base.Initialize(owner, stateMachine);
            Reset();
        }

        public override bool Evaluate(GameObject owner, IStateMachine stateMachine)
        {
            if (!hasStarted)
            {
                startTime = Time.time;
                hasStarted = true;
            }

            return Time.time - startTime >= duration;
        }

        public override void Reset()
        {
            hasStarted = false;
            startTime = 0f;
        }
    }

    public class FunctionCondition : Condition
    {
        private System.Func<bool> conditionFunc;

        public FunctionCondition(string id, System.Func<bool> condition)
        {
            Id = id;
            conditionFunc = condition;
        }

        public override bool Evaluate(GameObject owner, IStateMachine stateMachine)
        {
            return conditionFunc?.Invoke() ?? false;
        }
    }
}