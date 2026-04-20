using System;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// EnemyBase를 상속한 보스 구현. HP 임계값 기반 3페이즈 전환.
    /// 실제 공격 패턴·페이즈별 연출은 스텁(Debug.Log). Week 2 마감 후 스크립트 확장.
    /// 처치 시 EnemyBase.Die가 RunManager.NotifyBossKilled() 호출 (EnemyData.isBoss=true).
    /// </summary>
    public sealed class BossEnemy : EnemyBase
    {
        [Header("페이즈 전환 HP 비율")]
        [Range(0f, 1f), SerializeField] private float phase2HpThreshold = 0.66f;
        [Range(0f, 1f), SerializeField] private float phase3HpThreshold = 0.33f;

        [Header("페이즈별 보정 (프로토 스텁 — 실제 패턴 교체 시 확장)")]
        [SerializeField] private float damageMultiplierPhase2 = 1.2f;
        [SerializeField] private float damageMultiplierPhase3 = 1.5f;

        private int currentPhase = 1;

        public int CurrentPhase => currentPhase;
        public event Action<int> OnPhaseChanged;

        public float CurrentDamageMultiplier => currentPhase switch
        {
            1 => 1f,
            2 => damageMultiplierPhase2,
            3 => damageMultiplierPhase3,
            _ => 1f
        };

        protected override void Update()
        {
            base.Update();
            CheckPhaseTransition();
        }

        private void CheckPhaseTransition()
        {
            if (IsDead || Data == null || Data.baseHp <= 0) return;

            float ratio = (float)CurrentHp / Data.baseHp;

            int nextPhase = currentPhase;
            if (ratio <= phase3HpThreshold && currentPhase < 3) nextPhase = 3;
            else if (ratio <= phase2HpThreshold && currentPhase < 2) nextPhase = 2;

            if (nextPhase != currentPhase)
            {
                currentPhase = nextPhase;
                Debug.Log($"[Boss:{Data.enemyId}] Phase {currentPhase} 진입 (HP {ratio:P0})");
                OnPhaseChanged?.Invoke(currentPhase);
            }
        }
    }
}
