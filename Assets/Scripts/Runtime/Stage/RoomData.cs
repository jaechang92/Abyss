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

        [Tooltip("체크 시 이 방 클리어 후 폼 보상 제단이 활성화된다. 보상 폼은 FormCatalog에서 미보유 우선으로 추첨된다.")]
        public bool hasFormReward;

        [Tooltip("보상 폼 고정 지정(선택). 설정 시 추첨 대신 이 폼을 제시한다. 비우면 미보유 우선 추첨. " +
                 "설정되어 있으면 hasFormReward와 무관하게 보상 룸으로 취급된다(레거시 호환).")]
        public FormData formReward;

        /// <summary>이 방이 폼 보상 룸인지. 플래그 또는 고정 폼 지정 중 하나라도 있으면 보상 룸.</summary>
        public bool IsFormRewardRoom => hasFormReward || formReward != null;
    }

    [System.Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData data;
        [Min(1)] public int count = 1;
    }
}
