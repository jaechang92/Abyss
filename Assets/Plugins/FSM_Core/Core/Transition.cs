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

        /// <summary>
        /// 채택 통지. 기본 구현은 <see cref="OnTransitionTriggered"/> 발행뿐이다.
        /// (이 이벤트는 선언만 되어 있고 아무도 발행하지 않던 것을 여기서 연결했다.)
        /// </summary>
        public virtual void NotifyTaken()
        {
            OnTransitionTriggered?.Invoke(this);
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

        /// <summary>
        /// 트리거 여부를 <b>조회만</b> 한다. 예전에는 여기서 ConsumeEvent로 소비까지 했는데 두 가지가 깨졌다 —
        /// ① 우선순위 비교를 위해 여러 후보를 평가하면 채택되지 않은 전환이 트리거를 먹어 치운다
        /// ② <c>CanTransitionTo</c> 같은 '질의'가 상태를 바꿔 버린다.
        /// 소비는 실제로 채택된 뒤 <see cref="NotifyTaken"/>에서 한다.
        /// </summary>
        public override bool CanTransition()
        {
            if (!IsEnabled) return false;
            return stateMachine != null && stateMachine.IsEventTriggered(eventId);
        }

        public override void NotifyTaken()
        {
            stateMachine?.ConsumeEvent(eventId);
            base.NotifyTaken();
        }
    }
}