using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 전투 입력 훅 + 근접 공격 판정. 프로토 범위:
    /// Physics2D.OverlapBoxAll 로 `attackPoint` 기준 박스 안 EnemyBase 감지 → TakeDamage.
    /// GAS Ability 본격 연결은 후속. 현재는 PlayerCharacter가 직접 데미지 로직 수행.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("공격 쿨다운")]
        [SerializeField, Min(0f)] private float attackCooldownLight = 0.3f;
        [SerializeField, Min(0f)] private float attackCooldownHeavy = 0.8f;

        [Header("공격 판정")]
        [Tooltip("공격 범위 중심이 될 자식 Transform (플레이어 앞쪽 배치)")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private Vector2 attackBoxSize = new(1.6f, 1.2f);
        [SerializeField, Min(0)] private int lightAttackDamage = 15;
        [SerializeField, Min(0)] private int heavyAttackDamage = 35;

        [Header("공격 피드백")]
        [SerializeField] private AttackEffect attackEffect;
        [SerializeField, Min(0f)] private float lightHitstop = 0.05f;
        [SerializeField, Min(0f)] private float heavyHitstop = 0.10f;
        [SerializeField] private Vector2 lightShake = new(0.12f, 0.1f);
        [SerializeField] private Vector2 heavyShake = new(0.25f, 0.2f);

        private float lastAttackLightTime = -999f;
        private float lastAttackHeavyTime = -999f;
        private CameraShake cachedCameraShake;
        private bool cameraShakeLookupAttempted;

        private static readonly List<EnemyBase> reusableHitList = new();

        public bool CanAttackLight => Time.time >= lastAttackLightTime + attackCooldownLight;
        public bool CanAttackHeavy => Time.time >= lastAttackHeavyTime + attackCooldownHeavy;

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackLight) return;

            lastAttackLightTime = Time.time;
            stateMachine?.TriggerAttackLight();
            PerformAttack(lightAttackDamage, lightHitstop, lightShake, isHeavy: false);
        }

        private void OnAttackHeavy(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackHeavy) return;

            lastAttackHeavyTime = Time.time;
            stateMachine?.TriggerAttackHeavy();
            PerformAttack(heavyAttackDamage, heavyHitstop, heavyShake, isHeavy: true);
        }

        /// <summary>
        /// 공격 판정 + 시각·촉각 피드백.
        /// attackPoint 기준 OverlapBox 로 적 수집, 중복 히트 방지 후 TakeDamage.
        /// 최소 1히트 시 히트스탑·쉐이크 발생.
        /// </summary>
        private void PerformAttack(int damage, float hitstop, Vector2 shake, bool isHeavy)
        {
            if (isHeavy) attackEffect?.PlayHeavy();
            else attackEffect?.PlayLight();

            if (attackPoint == null) return;

            int hitCount = CollectAndDamageEnemies(damage);
            if (hitCount <= 0) return;

            if (HitstopController.HasInstance)
            {
                HitstopController.Instance.Trigger(hitstop);
            }
            TriggerShake(shake);
        }

        private int CollectAndDamageEnemies(int damage)
        {
            var hits = Physics2D.OverlapBoxAll(attackPoint.position, attackBoxSize, 0f);
            reusableHitList.Clear();

            foreach (var col in hits)
            {
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (reusableHitList.Contains(enemy)) continue;
                reusableHitList.Add(enemy);
            }

            foreach (var enemy in reusableHitList)
            {
                enemy.TakeDamage(damage);
            }

            return reusableHitList.Count;
        }

        private void TriggerShake(Vector2 magDuration)
        {
            var shake = ResolveCameraShake();
            if (shake != null) shake.Shake(magDuration.x, magDuration.y);
        }

        private CameraShake ResolveCameraShake()
        {
            if (cachedCameraShake != null) return cachedCameraShake;
            if (cameraShakeLookupAttempted) return null;

            cameraShakeLookupAttempted = true;
            var mainCam = UnityEngine.Camera.main;
            cachedCameraShake = mainCam != null ? mainCam.GetComponent<CameraShake>() : null;
            if (cachedCameraShake == null)
            {
                cachedCameraShake = FindAnyObjectByType<CameraShake>();
            }
            return cachedCameraShake;
        }

        private void OnDrawGizmosSelectedCombat()
        {
            if (attackPoint == null) return;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(attackPoint.position, new Vector3(attackBoxSize.x, attackBoxSize.y, 0f));
        }
    }
}
