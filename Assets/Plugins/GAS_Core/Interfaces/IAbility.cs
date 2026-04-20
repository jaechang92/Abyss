using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 모든 어빌리티가 구현해야 하는 인터페이스
    /// Fire Magic, Ice Magic 등 모든 어빌리티의 기본 계약
    /// </summary>
    public interface IAbility
    {
        // ====== 프로퍼티 ======

        /// <summary>
        /// 어빌리티 고유 식별자 (예: "FireMagic")
        /// </summary>
        string AbilityName { get; }

        /// <summary>
        /// 현재 실행 중인지 여부
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// 쿨다운 관리 객체 (null 가능)
        /// </summary>
        AbilityCooldown Cooldown { get; }


        // ====== 메서드 ======

        /// <summary>
        /// 어빌리티 실행 가능 여부 확인
        /// </summary>
        /// <param name="context">게임플레이 컨텍스트 (소유자, 타겟 등)</param>
        /// <returns>true: 실행 가능, false: 불가능</returns>
        /// <remarks>
        /// 검증 항목:
        /// - context.IsAlive == true
        /// - context.CanAct == true
        /// - Cooldown == null || Cooldown.CanUse() == true
        /// </remarks>
        bool CanExecute(IGameplayContext context);

        /// <summary>
        /// 어빌리티 비동기 실행 (Awaitable 패턴, Coroutine 금지)
        /// </summary>
        /// <param name="context">게임플레이 컨텍스트</param>
        /// <returns>Awaitable (완료 대기 가능)</returns>
        /// <remarks>
        /// 전제조건: CanExecute(context) == true
        /// 후제조건: Cooldown.StartCooldown() 호출됨
        /// </remarks>
        Awaitable ExecuteAsync(IGameplayContext context);

        /// <summary>
        /// 실행 중인 어빌리티 취소
        /// </summary>
        /// <remarks>
        /// FSM 상태 전환 시 호출됨
        /// </remarks>
        void Cancel();
    }
}
