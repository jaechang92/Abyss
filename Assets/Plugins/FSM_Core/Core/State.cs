using FSM.Utils;
using System;
using System.Threading;
using UnityEngine;

namespace FSM.Core
{
    /// <summary>
    /// 하이브리드 FSM State 기본 클래스
    /// 동기/비동기 모두 지원
    /// </summary>
    public abstract class State : IState
    {
        private GameObject owner;
        protected IStateMachine stateMachine;

        public string Id { get; private set; }
        public virtual string Name => Id;
        public bool IsActive { get; private set; }
        public GameObject Owner => owner;

        public event Action<IState> OnEntered;
        public event Action<IState> OnExited;

        public virtual void Initialize(string id, GameObject owner, IStateMachine stateMachine)
        {
            this.Id = id;
            this.owner = owner;
            this.stateMachine = stateMachine;
        }

        // === 동기 메서드 (Combat용 - 기본 구현) ===
        public virtual void OnEnterSync()
        {
            IsActive = true;
            OnEnterStateSync();
            OnEntered?.Invoke(this);
        }

        public virtual void OnExitSync()
        {
            IsActive = false;
            OnExitStateSync();
            OnExited?.Invoke(this);
        }

        // === 비동기 메서드 (GameFlow용 - 기본 구현) ===
        public virtual async Awaitable OnEnter(CancellationToken cancellationToken = default)
        {
            IsActive = true;
            await OnEnterState(cancellationToken);
            OnEntered?.Invoke(this);
        }

        public virtual async Awaitable OnExit(CancellationToken cancellationToken = default)
        {
            IsActive = false;
            await OnExitState(cancellationToken);
            OnExited?.Invoke(this);
        }

        public virtual void OnUpdate(float deltaTime)
        {
            if (IsActive)
            {
                OnUpdateState(deltaTime);
            }
        }

        // === 하위 클래스에서 오버라이드할 메서드들 ===
        protected virtual void OnEnterStateSync() { } // 동기 진입 (Combat용)
        protected virtual void OnExitStateSync() { } // 동기 종료 (Combat용)
        protected virtual Awaitable OnEnterState(CancellationToken cancellationToken) => AwaitableHelper.CompletedTask; // 비동기 진입 (GameFlow용)
        protected virtual Awaitable OnExitState(CancellationToken cancellationToken) => AwaitableHelper.CompletedTask; // 비동기 종료 (GameFlow용)
        protected virtual void OnUpdateState(float deltaTime) { }
    }

    /// <summary>
    /// 간단한 상태 구현 (Action 기반)
    /// </summary>
    public class SimpleState : State
    {
        private Action onEnterAction;
        private Action<float> onUpdateAction;
        private Action onExitAction;

        public SimpleState(Action onEnter = null, Action<float> onUpdate = null, Action onExit = null)
        {
            onEnterAction = onEnter;
            onUpdateAction = onUpdate;
            onExitAction = onExit;
        }

        // 동기 버전 (Combat용)
        protected override void OnEnterStateSync()
        {
            onEnterAction?.Invoke();
        }

        protected override void OnExitStateSync()
        {
            onExitAction?.Invoke();
        }

        // 비동기 버전 (GameFlow용) - 동기 호출
        protected override Awaitable OnEnterState(CancellationToken cancellationToken)
        {
            onEnterAction?.Invoke();
            return AwaitableHelper.CompletedTask;
        }

        protected override Awaitable OnExitState(CancellationToken cancellationToken)
        {
            onExitAction?.Invoke();
            return AwaitableHelper.CompletedTask;
        }

        protected override void OnUpdateState(float deltaTime)
        {
            onUpdateAction?.Invoke(deltaTime);
        }
    }
}
