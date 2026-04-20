using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FSM.Core
{
    /// <summary>
    /// 하이브리드 FSM State 인터페이스
    /// 동기/비동기 모두 지원
    /// </summary>
    public interface IState
    {
        string Id { get; }
        string Name { get; }
        bool IsActive { get; }
        GameObject Owner { get; }

        // === 동기 메서드 (Combat용 - 즉시 실행) ===
        void OnEnterSync();
        void OnExitSync();

        // === 비동기 메서드 (GameFlow용 - 대기 가능) ===
        Awaitable OnEnter(CancellationToken cancellationToken = default);
        Awaitable OnExit(CancellationToken cancellationToken = default);

        // === Update (공통) ===
        void OnUpdate(float deltaTime);

        void Initialize(string id, GameObject owner, IStateMachine stateMachine);

        event Action<IState> OnEntered;
        event Action<IState> OnExited;
    }
}