using System;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    public sealed partial class PlayerCharacter
    {
        [Header("체력 (P-14 RunConfig로 교체 예정)")]
        [SerializeField, Min(1)] private int baseHp = 100;

        private int currentHp;
        private bool isDead;

        public int BaseHp => baseHp;
        public int CurrentHp => currentHp;
        public bool IsDead => isDead;

        /// <summary>치트/디버그용 무적. true면 TakeDamage 무시.</summary>
        public bool DebugInvincible { get; set; }

        /// <summary>HP 변화 로컬 이벤트. (previousHp, currentHp).</summary>
        public event Action<int, int> OnHpChanged;

        private void InitializeHealth()
        {
            currentHp = baseHp;
            isDead = false;
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
            currentHp = Mathf.Min(baseHp, currentHp + amount);
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
