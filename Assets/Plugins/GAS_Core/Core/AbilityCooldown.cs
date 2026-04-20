using System;
using UnityEngine;

namespace GAS.Core
{
    /// <summary>
    /// 어빌리티 쿨다운 관리 클래스
    /// </summary>
    [Serializable]
    public class AbilityCooldown
    {
        private float cooldownDuration;
        private float remainingTime;
        private bool isOnCooldown;
        private float lastUsedTime;

        // 이벤트
        public event Action OnCooldownStarted;
        public event Action OnCooldownCompleted;
        public event Action<float> OnCooldownUpdated;

        // 프로퍼티
        public float Duration => cooldownDuration;
        public float RemainingTime => remainingTime;
        public bool IsOnCooldown => isOnCooldown;
        public float Progress => cooldownDuration > 0 ? 1f - (remainingTime / cooldownDuration) : 1f;

        /// <summary>
        /// 쿨다운 초기화
        /// </summary>
        public void Initialize(float duration)
        {
            cooldownDuration = duration;
            remainingTime = 0f;
            isOnCooldown = false;
            lastUsedTime = -duration; // 즉시 사용 가능하도록
        }

        /// <summary>
        /// 쿨다운 시작
        /// </summary>
        public void StartCooldown()
        {
            if (cooldownDuration <= 0) return;

            remainingTime = cooldownDuration;
            isOnCooldown = true;
            lastUsedTime = Time.time;

            OnCooldownStarted?.Invoke();
            Debug.Log($"Cooldown started: {cooldownDuration}s");
        }

        /// <summary>
        /// 쿨다운 시작 (커스텀 시간)
        /// </summary>
        public void StartCooldown(float customDuration)
        {
            if (customDuration <= 0) return;

            cooldownDuration = customDuration;
            remainingTime = customDuration;
            isOnCooldown = true;
            lastUsedTime = Time.time;

            OnCooldownStarted?.Invoke();
            Debug.Log($"Cooldown started with custom duration: {customDuration}s");
        }

        /// <summary>
        /// 쿨다운 업데이트 (매 프레임 호출)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!isOnCooldown) return;

            remainingTime -= deltaTime;
            OnCooldownUpdated?.Invoke(remainingTime);

            if (remainingTime <= 0)
            {
                CompleteCooldown();
            }
        }

        /// <summary>
        /// 쿨다운 즉시 완료
        /// </summary>
        public void CompleteCooldown()
        {
            if (!isOnCooldown) return;

            remainingTime = 0f;
            isOnCooldown = false;

            OnCooldownCompleted?.Invoke();
            Debug.Log("Cooldown completed");
        }

        /// <summary>
        /// 쿨다운 리셋
        /// </summary>
        public void Reset()
        {
            remainingTime = 0f;
            isOnCooldown = false;
            lastUsedTime = -cooldownDuration;

            Debug.Log("Cooldown reset");
        }

        /// <summary>
        /// 쿨다운 감소 (아이템/스킬 효과)
        /// </summary>
        public void ReduceCooldown(float amount)
        {
            if (!isOnCooldown || amount <= 0) return;

            remainingTime = Mathf.Max(0, remainingTime - amount);

            if (remainingTime <= 0)
            {
                CompleteCooldown();
            }
            else
            {
                OnCooldownUpdated?.Invoke(remainingTime);
            }

            Debug.Log($"Cooldown reduced by {amount}s, remaining: {remainingTime}s");
        }

        /// <summary>
        /// 쿨다운 퍼센트 감소 (0~1)
        /// </summary>
        public void ReduceCooldownByPercent(float percent)
        {
            if (!isOnCooldown || percent <= 0) return;

            float reduction = remainingTime * Mathf.Clamp01(percent);
            ReduceCooldown(reduction);
        }

        /// <summary>
        /// 쿨다운 시간 수정 (버프/디버프 효과)
        /// </summary>
        public void ModifyCooldownDuration(float newDuration)
        {
            cooldownDuration = Mathf.Max(0, newDuration);

            // 현재 쿨다운 중이면 비율 유지
            if (isOnCooldown && cooldownDuration > 0)
            {
                float progressRatio = Progress;
                remainingTime = cooldownDuration * (1f - progressRatio);
            }
        }

        /// <summary>
        /// 쿨다운 감소율 적용 (0~1, 0.2 = 20% 감소)
        /// </summary>
        public void ApplyCooldownReduction(float reductionRate)
        {
            float reduction = Mathf.Clamp01(reductionRate);
            ModifyCooldownDuration(cooldownDuration * (1f - reduction));
        }

        /// <summary>
        /// 사용 가능 여부 확인
        /// </summary>
        public bool CanUse()
        {
            return !isOnCooldown;
        }

        /// <summary>
        /// 마지막 사용 후 경과 시간
        /// </summary>
        public float GetTimeSinceLastUse()
        {
            return Time.time - lastUsedTime;
        }
    }
}