using System;
using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 어빌리티 시스템 관리 인터페이스
    /// 어빌리티 등록, 실행, 생명주기 관리
    /// </summary>
    public interface IAbilitySystem
    {
        // ====== 이벤트 ======

        /// <summary>
        /// 어빌리티 실행 성공 시 발생
        /// </summary>
        event Action<string> OnAbilityExecuted; // abilityName

        /// <summary>
        /// 어빌리티 실행 실패 시 발생 (CanExecute 실패)
        /// </summary>
        event Action<string> OnAbilityFailed; // abilityName

        /// <summary>
        /// 쿨다운 시작 시 발생
        /// </summary>
        event Action<string, float> OnCooldownStarted; // (abilityName, duration)


        // ====== 생명주기 ======

        /// <summary>
        /// 시스템 초기화
        /// </summary>
        /// <param name="context">소유자의 게임플레이 컨텍스트</param>
        /// <remarks>
        /// 호출 시점: MonoBehaviour.Start() 또는 Awake()
        /// </remarks>
        void Initialize(IGameplayContext context);

        /// <summary>
        /// 매 프레임 업데이트 (쿨다운 갱신)
        /// </summary>
        /// <remarks>
        /// 호출 시점: MonoBehaviour.Update()
        /// </remarks>
        void Update();


        // ====== 어빌리티 관리 ======

        /// <summary>
        /// 어빌리티 등록
        /// </summary>
        /// <param name="ability">등록할 어빌리티 인스턴스</param>
        /// <remarks>
        /// 중복 처리: 동일 이름이 이미 존재하면 덮어씀 (경고 로그)
        /// </remarks>
        void RegisterAbility(IAbility ability);

        /// <summary>
        /// 어빌리티 등록 해제
        /// </summary>
        /// <param name="abilityName">제거할 어빌리티 이름</param>
        /// <remarks>
        /// 없는 이름 처리: 조용히 무시 (로그 없음)
        /// </remarks>
        void UnregisterAbility(string abilityName);

        /// <summary>
        /// 어빌리티 존재 확인
        /// </summary>
        /// <param name="abilityName">확인할 어빌리티 이름</param>
        /// <returns>true: 등록됨, false: 미등록</returns>
        bool HasAbility(string abilityName);

        /// <summary>
        /// 등록된 어빌리티 가져오기
        /// </summary>
        /// <param name="abilityName">가져올 어빌리티 이름</param>
        /// <returns>IAbility 인스턴스 (없으면 null)</returns>
        IAbility GetAbility(string abilityName);


        // ====== 어빌리티 실행 ======

        /// <summary>
        /// 어빌리티 실행 시도 (비동기)
        /// </summary>
        /// <param name="abilityName">실행할 어빌리티 이름</param>
        /// <returns>true: 실행 성공, false: 실행 실패</returns>
        /// <remarks>
        /// 실행 플로우:
        /// 1. HasAbility(abilityName) 확인 → false면 return false
        /// 2. ability.CanExecute(context) 확인 → false면 OnAbilityFailed 발생, return false
        /// 3. await ability.ExecuteAsync(context) 실행
        /// 4. OnAbilityExecuted 이벤트 발생
        /// 5. return true
        /// </remarks>
        Awaitable<bool> TryExecuteAbilityAsync(string abilityName);

        /// <summary>
        /// 모든 실행 중인 어빌리티 취소
        /// </summary>
        /// <remarks>
        /// 사용 시나리오: 플레이어 사망, 게임 일시정지, FSM 상태 강제 전환
        /// </remarks>
        void CancelAllAbilities();
    }
}
