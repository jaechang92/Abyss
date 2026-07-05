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
            // 최대 HP = RunConfig.baseHp(SoT) + 메타 업그레이드 보너스. 메타 배율도 이 시점에 확정.
            maxHp = ResolveConfigBaseHp() + MetaUpgrades.MaxHpBonus();
            metaAttackMult = MetaUpgrades.AttackMultiplier();
            currentHp = maxHp;
            isDead = false;
        }

        /// <summary>
        /// 기본 최대 HP를 RunConfig(SoT)에서 읽는다. RunManager 미준비(Bootstrap 미경유 등) 시
        /// RunConfig를 Resources에서 직접 폴백 로드해 초기화 순서와 무관하게 같은 값을 쓴다. 최후엔 로컬 baseHp.
        /// </summary>
        private int ResolveConfigBaseHp()
        {
            if (RunManager.HasInstance && RunManager.Instance.Config != null)
                return RunManager.Instance.Config.baseHp;

            var cfg = Resources.Load<RunConfig>("Data/RunConfig");
            return cfg != null ? cfg.baseHp : baseHp;
        }

        public void TakeDamage(int amount)
        {
            if (isDead || amount <= 0 || DebugInvincible) return;

            int previous = currentHp;
            currentHp = Mathf.Max(0, currentHp - amount);
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
