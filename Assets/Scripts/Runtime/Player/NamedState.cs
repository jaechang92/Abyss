using System;
using FSM.Core;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 이름을 생성자로 주입받는 경량 State. 9상태를 클래스 9개 만들지 않기 위한 헬퍼.
    /// 동기 Enter/Exit 콜백만 지원 (Combat용).
    /// </summary>
    public sealed class NamedState : State
    {
        private readonly string stateName;
        private readonly Action onEnter;
        private readonly Action onExit;

        public NamedState(string name, Action onEnter = null, Action onExit = null)
        {
            stateName = name;
            this.onEnter = onEnter;
            this.onExit = onExit;
        }

        public override string Name => stateName;

        protected override void OnEnterStateSync()
        {
            onEnter?.Invoke();
        }

        protected override void OnExitStateSync()
        {
            onExit?.Invoke();
        }
    }
}
