using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
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

        [Tooltip("설정 시 이 방 클리어 후 폼 보상 제단이 활성화된다(비우면 없음). StageDirector가 게이트 처리.")]
        public FormData formReward;
    }

    [System.Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData data;
        [Min(1)] public int count = 1;
    }
}
