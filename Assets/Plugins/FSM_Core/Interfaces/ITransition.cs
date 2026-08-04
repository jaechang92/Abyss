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

        /// <summary>
        /// 전환 가능 여부. <b>부작용이 없어야 한다(순수 조회)</b> —
        /// StateMachine이 우선순위를 고르느라 여러 후보를 평가하고, CanTransitionTo 같은 질의도 이 메서드를 쓴다.
        /// 소비성 처리(이벤트 트리거 소모 등)는 <see cref="NotifyTaken"/>에서 한다.
        /// </summary>
        bool CanTransition();

        /// <summary>
        /// 이 전환이 실제로 채택됐을 때 StateMachine이 1회 호출한다.
        /// 1회성 부작용(이벤트 소비)과 <see cref="OnTransitionTriggered"/> 발행을 여기서 처리한다.
        /// </summary>
        void NotifyTaken();

        event Action<ITransition> OnTransitionTriggered;
    }
}