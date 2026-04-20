using System;
using System.Collections.Generic;
using Singleton_Core;
using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 어빌리티 시스템 싱글톤 매니저
    /// 어빌리티 등록, 실행, 쿨다운 관리
    /// </summary>
    public class AbilitySystem : SingletonManager<AbilitySystem>, IAbilitySystem
    {
        // ====== 필드 ======

        /// <summary>
        /// 등록된 어빌리티 딕셔너리 (이름 → 어빌리티)
        /// </summary>
        private Dictionary<string, IAbility> registeredAbilities = new Dictionary<string, IAbility>();

        /// <summary>
        /// 소유자의 게임플레이 컨텍스트
        /// </summary>
        private IGameplayContext ownerContext;

        /// <summary>
        /// 현재 실행 중인 어빌리티
        /// </summary>
        private IAbility currentExecutingAbility;


        // ====== 이벤트 (IAbilitySystem 구현) ======

        /// <summary>
        /// 어빌리티 실행 성공 시 발생
        /// </summary>
        public event Action<string> OnAbilityExecuted;

        /// <summary>
        /// 어빌리티 실행 실패 시 발생
        /// </summary>
        public event Action<string> OnAbilityFailed;

        /// <summary>
        /// 쿨다운 시작 시 발생
        /// </summary>
        public event Action<string, float> OnCooldownStarted;


        // ====== Unity 생명주기 ======

        protected override void OnAwake()
        {
            Debug.Log("[AbilitySystem] 초기화 완료");
        }

        private void Update()
        {
            // 모든 등록된 어빌리티의 쿨다운 업데이트
            UpdateCooldowns();
        }


        // ====== IAbilitySystem 구현 - 생명주기 ======

        /// <summary>
        /// 시스템 초기화
        /// </summary>
        /// <param name="context">소유자의 게임플레이 컨텍스트</param>
        public void Initialize(IGameplayContext context)
        {
            if (context == null)
            {
                Debug.LogError("[AbilitySystem] Initialize(): context가 null입니다.");
                return;
            }

            ownerContext = context;
            Debug.Log($"[AbilitySystem] 초기화: Owner = {context.Owner.name}");
        }

        /// <summary>
        /// 쿨다운 업데이트 (매 프레임)
        /// </summary>
        void IAbilitySystem.Update()
        {
            UpdateCooldowns();
        }

        private void UpdateCooldowns()
        {
            foreach (var kvp in registeredAbilities)
            {
                IAbility ability = kvp.Value;

                if (ability?.Cooldown != null)
                {
                    ability.Cooldown.Update(Time.deltaTime);
                }
            }
        }


        // ====== IAbilitySystem 구현 - 어빌리티 관리 ======

        /// <summary>
        /// 어빌리티 등록
        /// </summary>
        /// <param name="ability">등록할 어빌리티 인스턴스</param>
        public void RegisterAbility(IAbility ability)
        {
            if (ability == null)
            {
                Debug.LogError("[AbilitySystem] RegisterAbility(): ability가 null입니다.");
                return;
            }

            if (string.IsNullOrEmpty(ability.AbilityName))
            {
                Debug.LogError("[AbilitySystem] RegisterAbility(): AbilityName이 비어있습니다.");
                return;
            }

            // 중복 확인
            if (registeredAbilities.ContainsKey(ability.AbilityName))
            {
                Debug.LogWarning($"[AbilitySystem] RegisterAbility(): '{ability.AbilityName}'는 이미 등록되어 있습니다. 덮어씁니다.");
            }

            registeredAbilities[ability.AbilityName] = ability;
            Debug.Log($"[AbilitySystem] 어빌리티 등록: {ability.AbilityName} (총 {registeredAbilities.Count}개)");
        }

        /// <summary>
        /// 어빌리티 등록 해제
        /// </summary>
        /// <param name="abilityName">제거할 어빌리티 이름</param>
        public void UnregisterAbility(string abilityName)
        {
            if (string.IsNullOrEmpty(abilityName))
            {
                return;
            }

            if (registeredAbilities.Remove(abilityName))
            {
                Debug.Log($"[AbilitySystem] 어빌리티 등록 해제: {abilityName} (남은 개수: {registeredAbilities.Count})");
            }
            // 없는 이름은 조용히 무시
        }

        /// <summary>
        /// 어빌리티 존재 확인
        /// </summary>
        /// <param name="abilityName">확인할 어빌리티 이름</param>
        /// <returns>true: 등록됨, false: 미등록</returns>
        public bool HasAbility(string abilityName)
        {
            return registeredAbilities.ContainsKey(abilityName);
        }

        /// <summary>
        /// 등록된 어빌리티 가져오기
        /// </summary>
        /// <param name="abilityName">가져올 어빌리티 이름</param>
        /// <returns>IAbility 인스턴스 (없으면 null)</returns>
        public IAbility GetAbility(string abilityName)
        {
            if (registeredAbilities.TryGetValue(abilityName, out IAbility ability))
            {
                return ability;
            }

            return null;
        }


        // ====== IAbilitySystem 구현 - 어빌리티 실행 ======

        /// <summary>
        /// 어빌리티 실행 시도 (비동기)
        /// </summary>
        /// <param name="abilityName">실행할 어빌리티 이름</param>
        /// <returns>true: 실행 성공, false: 실행 실패</returns>
        public async Awaitable<bool> TryExecuteAbilityAsync(string abilityName)
        {
            // 1. 어빌리티 존재 확인
            if (!HasAbility(abilityName))
            {
                Debug.LogWarning($"[AbilitySystem] TryExecuteAbilityAsync(): '{abilityName}'는 등록되지 않은 어빌리티입니다.");
                OnAbilityFailed?.Invoke(abilityName);
                return false;
            }

            IAbility ability = GetAbility(abilityName);

            // 2. CanExecute 검증
            if (!ability.CanExecute(ownerContext))
            {
                Debug.Log($"[AbilitySystem] TryExecuteAbilityAsync(): '{abilityName}' 실행 조건 불만족");
                OnAbilityFailed?.Invoke(abilityName);
                return false;
            }

            // 3. 어빌리티 실행
            try
            {
                currentExecutingAbility = ability;

                // 비동기 실행
                await ability.ExecuteAsync(ownerContext);

                // 성공 이벤트 발생
                OnAbilityExecuted?.Invoke(abilityName);

                // 쿨다운 시작 이벤트 발생
                if (ability.Cooldown != null)
                {
                    OnCooldownStarted?.Invoke(abilityName, ability.Cooldown.Duration);
                }

                Debug.Log($"[AbilitySystem] '{abilityName}' 실행 완료");

                currentExecutingAbility = null;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AbilitySystem] TryExecuteAbilityAsync(): '{abilityName}' 실행 중 예외 발생: {ex.Message}");
                OnAbilityFailed?.Invoke(abilityName);

                currentExecutingAbility = null;
                return false;
            }
        }

        /// <summary>
        /// 모든 실행 중인 어빌리티 취소
        /// </summary>
        public void CancelAllAbilities()
        {
            if (currentExecutingAbility != null)
            {
                currentExecutingAbility.Cancel();
                Debug.Log($"[AbilitySystem] 현재 실행 중인 어빌리티 취소: {currentExecutingAbility.AbilityName}");
                currentExecutingAbility = null;
            }

            // 모든 어빌리티에 대해 Cancel 호출 (안전성)
            foreach (var kvp in registeredAbilities)
            {
                if (kvp.Value.IsExecuting)
                {
                    kvp.Value.Cancel();
                }
            }

            Debug.Log("[AbilitySystem] 모든 어빌리티 취소 완료");
        }


        // ====== 디버그 ======

        /// <summary>
        /// 등록된 모든 어빌리티 정보 출력
        /// </summary>
        public void DebugPrintAllAbilities()
        {
            Debug.Log($"[AbilitySystem] ========== 등록된 어빌리티 목록 ({registeredAbilities.Count}개) ==========");

            int index = 1;
            foreach (var kvp in registeredAbilities)
            {
                IAbility ability = kvp.Value;
                Debug.Log($"[AbilitySystem] {index}. {ability.AbilityName}");
                Debug.Log($"     - IsExecuting: {ability.IsExecuting}");

                if (ability.Cooldown != null)
                {
                    Debug.Log($"     - Cooldown: {ability.Cooldown.Duration}초");
                    Debug.Log($"     - Remaining: {ability.Cooldown.RemainingTime:F1}초");
                    Debug.Log($"     - CanUse: {ability.Cooldown.CanUse()}");
                }
                else
                {
                    Debug.Log($"     - Cooldown: 없음");
                }

                index++;
            }

            Debug.Log("[AbilitySystem] ================================================");
        }
    }
}
