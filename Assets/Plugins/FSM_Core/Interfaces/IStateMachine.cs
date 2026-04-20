using System;
using System.Collections.Generic;
using UnityEngine;

namespace FSM.Core
{
    public interface IStateMachine
    {
        string CurrentStateId { get; }
        IState CurrentState { get; }
        bool IsRunning { get; }

        IReadOnlyDictionary<string, IState> States { get; }
        IReadOnlyList<ITransition> Transitions { get; }

        void AddState(IState state);
        void RemoveState(string stateId);
        bool HasState(string stateId);
        bool TryGetState(string stateId, out IState state);

        void AddTransition(ITransition transition);
        void RemoveTransition(ITransition transition);

        bool CanTransitionTo(string stateId);
        bool TryTransitionTo(string stateId);
        void ForceTransitionTo(string stateId);
        Awaitable ForceTransitionToAsync(string stateId);

        void StartStateMachine(string initialStateId = null);
        void Stop();
        void Update();

        event Action<string, string> OnStateChanged;
        event Action<ITransition> OnTransitionStarted;
        event Action<ITransition> OnTransitionCompleted;
        event Action OnStarted;
        event Action OnStopped;
    }
}