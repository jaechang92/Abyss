using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 게임플레이 컨텍스트 인터페이스
    /// 어빌리티 시스템이 게임 상황을 파악하기 위한 인터페이스
    /// </summary>
    public interface IGameplayContext
    {
        // 기본 정보
        GameObject Owner { get; }
        Transform Transform { get; }

        // 상태 정보
        bool IsAlive { get; }
        bool CanAct { get; }

        // 위치/방향 정보
        Vector3 Position { get; }
        Vector3 Forward { get; }

        // 게임 상태 확인
        bool IsInState(string stateName);
        void SetState(string stateName, bool value);

        // 타겟팅
        Transform GetTarget();
        void SetTarget(Transform target);

        // 커스텀 데이터
        T GetCustomData<T>(string key) where T : class;
        void SetCustomData<T>(string key, T data) where T : class;
    }
}