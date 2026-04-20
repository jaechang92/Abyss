using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 모든 어빌리티의 베이스 클래스
    /// Fire Magic, Ice Magic 등이 이 클래스를 상속하여 구현
    /// </summary>
    public abstract class Ability : MonoBehaviour, IAbility
    {
        // ====== 필드 ======

        /// <summary>
        /// 어빌리티 이름
        /// </summary>
        protected string abilityName;

        /// <summary>
        /// 쿨다운 관리 객체
        /// </summary>
        protected AbilityCooldown cooldown;

        /// <summary>
        /// 게임플레이 컨텍스트 (소유자)
        /// </summary>
        protected IGameplayContext context;

        /// <summary>
        /// 현재 실행 중 여부
        /// </summary>
        protected bool isExecuting;


        // ====== 프로퍼티 (IAbility 구현) ======

        /// <summary>
        /// 어빌리티 고유 식별자
        /// </summary>
        public string AbilityName => abilityName;

        /// <summary>
        /// 쿨다운 관리 객체
        /// </summary>
        public AbilityCooldown Cooldown => cooldown;

        /// <summary>
        /// 현재 실행 중인지 여부
        /// </summary>
        public bool IsExecuting => isExecuting;


        // ====== Unity 생명주기 ======

        /// <summary>
        /// 초기화 (서브클래스에서 override 가능)
        /// </summary>
        protected virtual void Awake()
        {
            // 쿨다운 초기화는 서브클래스에서 수행
            // AbilityData를 로드한 후 cooldown.Initialize(duration) 호출
        }


        // ====== IAbility 구현 (추상 메서드) ======

        /// <summary>
        /// 어빌리티 실행 가능 여부 확인
        /// </summary>
        /// <param name="context">게임플레이 컨텍스트</param>
        /// <returns>true: 실행 가능, false: 불가능</returns>
        /// <remarks>
        /// 서브클래스에서 override하여 추가 조건 검사 가능
        /// 예: 마나 확인, 특수 상태 확인 등
        /// </remarks>
        public abstract bool CanExecute(IGameplayContext context);

        /// <summary>
        /// 어빌리티 비동기 실행
        /// </summary>
        /// <param name="context">게임플레이 컨텍스트</param>
        /// <returns>Awaitable (완료 대기 가능)</returns>
        /// <remarks>
        /// 서브클래스에서 반드시 구현해야 함
        /// 예: Fire Magic - 발사체 생성 및 비행 처리
        /// </remarks>
        public abstract Awaitable ExecuteAsync(IGameplayContext context);

        /// <summary>
        /// 실행 중인 어빌리티 취소
        /// </summary>
        /// <remarks>
        /// 서브클래스에서 override하여 취소 로직 구현
        /// 기본 구현: isExecuting = false
        /// </remarks>
        public virtual void Cancel()
        {
            if (!isExecuting)
            {
                return;
            }

            isExecuting = false;
            Debug.Log($"[Ability] {abilityName} 취소됨");
        }


        // ====== 헬퍼 메서드 ======

        /// <summary>
        /// 기본 실행 조건 검증 (서브클래스에서 사용)
        /// </summary>
        /// <param name="context">게임플레이 컨텍스트</param>
        /// <returns>true: 기본 조건 충족, false: 불충족</returns>
        /// <remarks>
        /// 검증 항목:
        /// - context != null
        /// - context.IsAlive == true
        /// - context.CanAct == true
        /// - Cooldown == null || Cooldown.CanUse() == true
        /// </remarks>
        protected bool ValidateBasicConditions(IGameplayContext context)
        {
            // 1. 컨텍스트 유효성 확인
            if (context == null)
            {
                Debug.LogWarning($"[Ability] {abilityName}: context가 null입니다.");
                return false;
            }

            // 2. 생존 확인
            if (!context.IsAlive)
            {
                Debug.Log($"[Ability] {abilityName}: 소유자가 사망 상태입니다.");
                return false;
            }

            // 3. 행동 가능 확인
            if (!context.CanAct)
            {
                Debug.Log($"[Ability] {abilityName}: 소유자가 행동 불가 상태입니다 (스턴, 경직 등).");
                return false;
            }

            // 4. 쿨다운 확인
            if (cooldown != null && !cooldown.CanUse())
            {
                Debug.Log($"[Ability] {abilityName}: 쿨다운 중입니다. 남은 시간: {cooldown.RemainingTime:F1}초");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 어빌리티 실행 시작 (서브클래스 ExecuteAsync() 시작 시 호출)
        /// </summary>
        protected void BeginExecution()
        {
            isExecuting = true;
            Debug.Log($"[Ability] {abilityName} 실행 시작");
        }

        /// <summary>
        /// 어빌리티 실행 완료 (서브클래스 ExecuteAsync() 종료 시 호출)
        /// </summary>
        protected void EndExecution()
        {
            isExecuting = false;

            // 쿨다운 시작
            if (cooldown != null)
            {
                cooldown.StartCooldown();
                Debug.Log($"[Ability] {abilityName} 실행 완료. 쿨다운 시작: {cooldown.Duration}초");
            }
            else
            {
                Debug.Log($"[Ability] {abilityName} 실행 완료 (쿨다운 없음)");
            }
        }


        // ====== 디버그 ======

        /// <summary>
        /// 어빌리티 정보 출력 (디버그용)
        /// </summary>
        public void DebugPrintInfo()
        {
            Debug.Log($"[Ability] {abilityName}");
            Debug.Log($"  - IsExecuting: {isExecuting}");

            if (cooldown != null)
            {
                Debug.Log($"  - Cooldown: {cooldown.Duration}초");
                Debug.Log($"  - RemainingTime: {cooldown.RemainingTime:F1}초");
                Debug.Log($"  - CanUse: {cooldown.CanUse()}");
            }
            else
            {
                Debug.Log($"  - Cooldown: 없음");
            }
        }
    }
}
