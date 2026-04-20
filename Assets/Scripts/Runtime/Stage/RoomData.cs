using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 방 1개 정의 SO. 스폰할 적 목록 + 타입 + 클리어 보상.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomData", menuName = "Abyss/Data/Room Data")]
    public sealed class RoomData : ScriptableObject
    {
        [Header("식별자")]
        public string roomId;
        public RoomType roomType = RoomType.Combat;

        [Header("적 스폰")]
        public List<EnemySpawnEntry> enemies = new();

        [Header("클리어 보상")]
        [Min(0)] public int clearGoldReward;
    }

    [System.Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData data;
        [Min(1)] public int count = 1;
    }
}
