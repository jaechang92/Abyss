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

        [Header("표시 (노드 맵 분기 선택용)")]
        [Tooltip("분기 선택지에 표시할 방 이름. 비우면 방 타입 라벨(전투·이벤트 등)이 쓰인다.")]
        public string displayName;

        [Tooltip("선택 근거가 되는 한 줄 힌트. 예: \"보상 골드 18\" · \"미지의 대가\". 비우면 표시하지 않는다.")]
        public string hint;

        [Header("적 스폰")]
        public List<EnemySpawnEntry> enemies = new();

        [Header("클리어 보상")]
        [Min(0)] public int clearGoldReward;

        [Tooltip("체크 시 이 방 클리어 후 폼 보상 제단이 활성화된다. 보상 폼은 FormCatalog에서 미보유 우선으로 추첨된다.")]
        public bool hasFormReward;

        [Tooltip("보상 폼 고정 지정(선택). 설정 시 추첨 대신 이 폼을 제시한다. 비우면 미보유 우선 추첨. " +
                 "설정되어 있으면 hasFormReward와 무관하게 보상 룸으로 취급된다(레거시 호환).")]
        public FormData formReward;

        [Header("이벤트 (비전투 방)")]
        [Tooltip("설정 시 이 방은 이벤트 방이 된다. 적 목록은 비워 둘 것 — 적이 있으면 전투가 끝나야 이벤트가 열린다.")]
        public EventData eventData;

        [Header("상점 (비전투 방)")]
        [Tooltip("설정 시 이 방은 상점 방이 된다. eventData와 동시에 설정하지 말 것 — 이벤트가 먼저 잡힌다.")]
        public ShopData shopData;

        /// <summary>이 방이 폼 보상 룸인지. 플래그 또는 고정 폼 지정 중 하나라도 있으면 보상 룸.</summary>
        public bool IsFormRewardRoom => hasFormReward || formReward != null;

        /// <summary>이 방이 이벤트 방인지. <see cref="roomType"/>이 아니라 데이터 유무로 판정한다(보상 룸과 같은 규약).</summary>
        public bool IsEventRoom => eventData != null;

        /// <summary>이 방이 상점 방인지. 이벤트 방과 같은 규약(데이터 유무로 판정).</summary>
        public bool IsShopRoom => shopData != null;

        /// <summary>분기 선택지에 쓸 제목. 방 이름이 없으면 타입 라벨로 폴백한다.</summary>
        public string ChoiceTitle =>
            string.IsNullOrEmpty(displayName) ? RoomTypeDisplay.Label(roomType) : displayName;
    }

    [System.Serializable]
    public sealed class EnemySpawnEntry
    {
        public EnemyData data;
        [Min(1)] public int count = 1;
    }
}
