using System;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// EnemyBase를 상속한 보스 구현. HP 임계값 기반 3페이즈 전환.
    /// 페이즈별 데미지 배율 + 부채꼴 탄막 볼리(발사체 재사용). 페이즈 상승 시 발사 수↑·주기↓.
    /// 처치 시 EnemyBase.Die가 RunManager.NotifyBossKilled() 호출 (EnemyData.isBoss=true).
    /// </summary>
    public sealed class BossEnemy : EnemyBase
    {
        [Header("페이즈 전환 HP 비율")]
        [Range(0f, 1f), SerializeField] private float phase2HpThreshold = 0.66f;
        [Range(0f, 1f), SerializeField] private float phase3HpThreshold = 0.33f;

        [Header("페이즈별 보정")]
        [SerializeField] private float damageMultiplierPhase2 = 1.2f;
        [SerializeField] private float damageMultiplierPhase3 = 1.5f;

        [Header("페이즈 탄막 (부채꼴 볼리 — 발사체 재사용)")]
        [Tooltip("볼리 발사 주기(초). 실제 주기는 interval/currentPhase로 페이즈 상승 시 단축")]
        [SerializeField, Min(0.5f)] private float volleyInterval = 3f;
        [Tooltip("페이즈 1 기준 발사 수. 페이즈마다 +1발")]
        [SerializeField, Min(1)] private int baseVolleyCount = 3;
        [Tooltip("부채꼴 전체 확산 각도(도)")]
        [SerializeField, Min(0f)] private float spreadAngle = 40f;

        private int currentPhase = 1;
        private float lastVolleyTime = -999f;

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
            TryFireVolley();
        }

        /// <summary>
        /// 타겟이 감지 범위 안일 때 주기적으로 부채꼴 탄막을 발사한다(근접 공격과 병행).
        /// 페이즈가 오를수록 주기가 짧아지고(interval/phase) 발사 수가 늘어난다.
        /// projectilePrefab 미연결 보스는 무동작(근접만 수행).
        /// </summary>
        private void TryFireVolley()
        {
            if (IsDead || Target == null || Data == null || Data.projectilePrefab == null) return;

            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > Data.detectionRange) return;

            float interval = volleyInterval / currentPhase;
            if (Time.time < lastVolleyTime + interval) return;
            lastVolleyTime = Time.time;

            FireVolley();
        }

        private void FireVolley()
        {
            int count = baseVolleyCount + (currentPhase - 1);

            Vector2 toTarget = (Vector2)Target.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector2.right;
            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

            float start = count > 1 ? baseAngle - spreadAngle * 0.5f : baseAngle;
            float step = count > 1 ? spreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angRad = (start + step * i) * Mathf.Deg2Rad;
                SpawnProjectile(new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad)));
            }
        }

        protected override int GetAttackDamage()
        {
            int baseDamage = base.GetAttackDamage();
            return Mathf.RoundToInt(baseDamage * CurrentDamageMultiplier);
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
