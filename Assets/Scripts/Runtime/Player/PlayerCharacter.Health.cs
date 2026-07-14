using System;
using Abyss.Runtime.Events;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    public sealed partial class PlayerCharacter
    {
        [Header("체력 (P-14: RunConfig.baseHp가 SoT, 아래 값은 폴백)")]
        [SerializeField, Min(1)] private int baseHp = 100;

        private int maxHp;          // 메타 업그레이드 보너스가 반영된 실효 최대 HP (런 시작 시 확정)
        private int currentHp;
        private bool isDead;
        private float metaAttackMult = 1f;  // 메타 공격 배율 캐시 (Combat.cs에서 사용)

        /// <summary>실효 최대 HP(메타 보너스 반영). 초기화 전에는 직렬화 baseHp로 폴백. HUD/CheatMenu 최대치.</summary>
        public int BaseHp => maxHp > 0 ? maxHp : baseHp;
        public int CurrentHp => currentHp;
        public bool IsDead => isDead;

        /// <summary>메타 업그레이드 공격 배율(1 = 무보정). Combat 데미지 계산에 곱한다.</summary>
        public float MetaAttackMult => metaAttackMult;

        /// <summary>치트/디버그용 무적. true면 TakeDamage 무시.</summary>
        public bool DebugInvincible { get; set; }

        /// <summary>HP 변화 로컬 이벤트. (previousHp, currentHp).</summary>
        public event Action<int, int> OnHpChanged;

        private void InitializeHealth()
        {
            // 최대 HP = RunConfig.baseHp(SoT) × 시작 폼 hpMultiplier + 메타 업그레이드 보너스.
            // 폼별 HP 차별화는 '시작 폼 고정' 정책: 런 시작 폼 기준으로 여기서 1회 확정하고,
            // 이후 폼 스왑(FormController.RequestSwap)에도 maxHp는 불변이다(악용·회복 없음).
            maxHp = Mathf.RoundToInt(ResolveConfigBaseHp() * ResolveStartingFormHpMultiplier()) + MetaUpgrades.MaxHpBonus();
            metaAttackMult = MetaUpgrades.AttackMultiplier();
            currentHp = maxHp;
            isDead = false;
        }

        /// <summary>
        /// 시작 폼의 hpMultiplier(1 = 무보정). formController 미참조 시 1배로 폴백한다.
        /// Awake 실행 순서와 무관하도록 FormController.ResolveStartingForm으로 해석한다.
        /// </summary>
        private float ResolveStartingFormHpMultiplier()
        {
            var startForm = formController != null ? formController.ResolveStartingForm() : null;
            return startForm != null ? startForm.hpMultiplier : 1f;
        }

        /// <summary>
        /// 기본 최대 HP를 RunConfig(SoT)에서 읽는다. RunManager가 준비돼 있으면 그 config(에디터 오버라이드
        /// 반영본)를 우선하고, 미준비(Bootstrap 미경유 등) 시 공유 RunConfigProvider.Current로 폴백해
        /// 초기화 순서와 무관하게 같은 값을 쓴다. 최후엔 직렬화 로컬 baseHp.
        /// </summary>
        private int ResolveConfigBaseHp()
        {
            var cfg = (RunManager.HasInstance && RunManager.Instance.Config != null)
                ? RunManager.Instance.Config
                : RunConfigProvider.Current;
            return cfg != null ? cfg.baseHp : baseHp;
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0 || DebugInvincible) return;

            // 방어 버프(철벽 방어 등) 적용 — 받는 피해 배율. 배율 적용 후에도 최소 1 피해 보장(약공 무효화 방지).
            int mitigated = DefenseMultiplier < 1f
                ? Mathf.Max(1, Mathf.RoundToInt(amount * DefenseMultiplier))
                : amount;

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - mitigated);
            OnHpChanged?.Invoke(previous, currentHp);

            if (currentHp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;

            int previous = currentHp;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            OnHpChanged?.Invoke(previous, currentHp);
        }

        private void Die()
        {
            isDead = true;
            Debug.Log("[Player] 사망 — OnPlayerDead 발행");
            GameEvents.RaisePlayerDead();
        }

        [ContextMenu("Debug: Take 25 Damage")]
        private void DebugTakeDamage()
        {
            TakeDamage(25);
        }

        [ContextMenu("Debug: Heal 25")]
        private void DebugHeal()
        {
            Heal(25);
        }

        [ContextMenu("Debug: Kill")]
        private void DebugKill()
        {
            TakeDamage(currentHp);
        }
    }
}
