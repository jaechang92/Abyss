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

        [Header("보상")]
        [Min(0)] public int expReward = 20;
        [Min(0)] public int goldReward = 5;

        [Header("분류")]
        public bool isElite;
        public bool isRanged;

        [Header("스폰 프리팹 (StageDirector가 Instantiate)")]
        public GameObject spawnPrefab;
    }
}
