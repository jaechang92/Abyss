using Abyss.Runtime.Combat;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 정의 ScriptableObject. 프로토 스펙 — 근접 2·원거리 1·엘리트 1 구성.
    /// 원거리/엘리트 구분은 `isRanged`/`isElite` 플래그로 처리(별도 클래스 분기 없음).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Abyss/Data/Enemy Data")]
    public sealed class EnemyData : ScriptableObject
    {
        [Header("식별자")]
        public string enemyId;
        public string displayName;

        [Header("전투")]
        [Min(1)] public int baseHp = 30;
        [Min(0)] public int baseDamage = 10;
        [Min(0f)] public float moveSpeed = 3f;
        [Min(0f)] public float detectionRange = 6f;
        [Min(0f)] public float attackRange = 1.2f;
        [Min(0.1f)] public float attackCooldown = 1.5f;

        [Header("순찰")]
        [Tooltip("스폰 지점 기준 좌우 이동 반경. 0이면 Patrol 시 정지 (보스 등)")]
        [Min(0f)] public float patrolRadius = 2f;
        [Tooltip("moveSpeed 대비 순찰 이동 속도 배율")]
        [Range(0f, 1f)] public float patrolSpeedMultiplier = 0.5f;

        [Header("보상")]
        [Min(0)] public int expReward = 20;
        [Min(0)] public int goldReward = 5;

        [Header("분류")]
        [Tooltip("적 등급(분류의 SoT). 전투·보상 동작은 이 값에서 파생한다 — IsBoss/IsElite 참조.")]
        public EnemyTier tier = EnemyTier.Normal;
        [Tooltip("원거리 여부. 등급과 직교한다 — 일반 적도 보스도 원거리일 수 있다.")]
        public bool isRanged;

        /// <summary>
        /// 보스 처치 판정·프리팹 크기·발사체 연결에 쓰는 동작 스위치.
        /// <b>중간보스는 false다</b> — 보스 처치 수는 기록자 챕터 해금 조건이라
        /// 미드보스로 오르면 연재 순서가 무너진다.
        /// </summary>
        public bool IsBoss => tier == EnemyTier.Boss;

        /// <summary>
        /// 엘리트 보상(EliteBonus 드래프트) 트리거에 쓰는 동작 스위치.
        /// <b>중간보스도 true다</b> — 중간보스가 엘리트라서가 아니라 <b>같은 보상 트리거를
        /// 재활용</b>하기 때문이다. 이 둘을 구분해야 하는 곳(도감)은 <see cref="tier"/>를 본다.
        /// </summary>
        public bool IsElite => tier == EnemyTier.Elite || tier == EnemyTier.MidBoss;

        [Header("원거리 (isRanged 전용 — PrefabBuilder가 projectilePrefab 자동 연결)")]
        [Tooltip("isRanged=true이고 이 값이 있으면 근접 즉발 대신 발사체를 발사")]
        public Projectile projectilePrefab;
        [Min(0f)] public float projectileSpeed = 8f;
        [Min(0.1f)] public float projectileLifetime = 3f;

        [Tooltip("한 번의 공격에 쏘는 탄 수. 1이면 단발. 매 발 조준을 다시 하므로 연사 중 플레이어를 따라간다.")]
        [Min(1)] public int burstCount = 1;
        [Tooltip("연사 탄 사이 간격(초). burstCount가 1이면 무시된다.")]
        [Min(0f)] public float burstInterval = 0.12f;

        [Header("곡사 (체크 시 직진탄 대신 포물선 폭발탄)")]
        [Tooltip("이 적이 곡사병인지. PrefabBuilder가 이 플래그를 보고 arcProjectilePrefab을 연결한다. " +
                 "플래그만 켜고 프리팹 연결이 실패하면 직진탄으로 폴백한다(무력화되지 않는다).")]
        public bool usesArcProjectile;
        public ArcProjectile arcProjectilePrefab;
        [Tooltip("발사에서 착탄까지의 시간(초). 거리와 무관하게 일정해 예고 리듬이 고정된다.")]
        [Min(0.1f)] public float arcFlightTime = 1.1f;
        [Min(0.1f)] public float arcExplosionRadius = 1.8f;

        [Header("스폰 프리팹 (StageDirector가 Instantiate)")]
        public GameObject spawnPrefab;
    }
}
