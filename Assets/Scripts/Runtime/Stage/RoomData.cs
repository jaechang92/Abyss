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

        [Header("스크롤 맵 (17-stage-flow-boss-presentation §1)")]
        [Tooltip("맵 가로 길이(유닛). 0이면 옛 아레나 방 — 즉시 일괄 스폰 + 갈림길 모달. " +
                 "0보다 크면 맵 방 — 맵은 x=0을 중심으로 좌우 절반씩, 입구는 왼쪽 끝, 클리어 뒤 오른쪽 끝에 보상 문이 선다. " +
                 "Run 씬 바닥 폭이 45.6이라 지금은 44 이하로 둘 것(더 긴 맵은 바닥 확장이 필요하다).")]
        [Min(0f)] public float mapLength;
        [Tooltip("맵 방 진입 즉시 스폰할 적과 x 좌표(맵 중심 0 기준). 비우면 enemies를 맵 전체에 고르게 흩어 스폰한다.")]
        public List<EnemyPlacement> placements = new();
        [Tooltip("조건이 되면 추가로 소환하는 무리. 배치 무리를 다 잡았는데 남아 있으면 즉시 소환된다(방이 막히지 않게).")]
        public List<Reinforcement> reinforcements = new();

        [Header("클리어 보상")]
        [Min(0)] public int clearGoldReward;

        [Tooltip("체크 시 이 방 클리어 후 폼 보상 제단이 활성화된다. 보상 폼은 FormCatalog에서 미보유 우선으로 추첨된다.")]
        public bool hasFormReward;

        [Tooltip("보상 폼 고정 지정(선택). 설정 시 추첨 대신 이 폼을 제시한다. 비우면 미보유 우선 추첨. " +
                 "설정되어 있으면 hasFormReward와 무관하게 보상 룸으로 취급된다(레거시 호환).")]
        public FormData formReward;

        [Tooltip("체크 시 이 방 클리어 후 무기 보상 제단이 활성화된다. 보상 무기는 현재 폼이 쓸 수 있는 " +
                 "무기 중에서 등급 가중 추첨된다(이미 가진 무기도 후보다 — 중복은 강화가 된다). " +
                 "🔴 폼 보상과 동시에 설정하지 말 것 — 폼 보상이 먼저 잡히고 무기 보상은 그 방에서 안 열린다.")]
        public bool hasWeaponReward;

        [Tooltip("보상 무기 고정 지정(선택). 설정 시 추첨 대신 이 무기를 준다. " +
                 "설정되어 있으면 hasWeaponReward와 무관하게 무기 보상 룸으로 취급된다(폼 보상과 같은 규약).")]
        public Weapon.WeaponData weaponReward;

        [Header("이벤트 (비전투 방)")]
        [Tooltip("설정 시 이 방은 이벤트 방이 된다. 적 목록은 비워 둘 것 — 적이 있으면 전투가 끝나야 이벤트가 열린다.")]
        public EventData eventData;

        [Header("상점 (비전투 방)")]
        [Tooltip("설정 시 이 방은 상점 방이 된다. eventData와 동시에 설정하지 말 것 — 이벤트가 먼저 잡힌다.")]
        public ShopData shopData;

        /// <summary>이 방이 폼 보상 룸인지. 플래그 또는 고정 폼 지정 중 하나라도 있으면 보상 룸.</summary>
        public bool IsFormRewardRoom => hasFormReward || formReward != null;

        /// <summary>이 방이 무기 보상 룸인지. 폼 보상과 같은 규약(플래그 또는 고정 지정).</summary>
        public bool IsWeaponRewardRoom => hasWeaponReward || weaponReward != null;

        /// <summary>이 방이 이벤트 방인지. <see cref="roomType"/>이 아니라 데이터 유무로 판정한다(보상 룸과 같은 규약).</summary>
        public bool IsEventRoom => eventData != null;

        /// <summary>이 방이 상점 방인지. 이벤트 방과 같은 규약(데이터 유무로 판정).</summary>
        public bool IsShopRoom => shopData != null;

        /// <summary>스크롤 맵 방인지. <see cref="mapLength"/>가 0이면 옛 아레나 방이다(하위 호환).</summary>
        public bool IsMapRoom => mapLength > 0f;

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

    /// <summary>맵 방의 적 한 마리 자리. x는 맵 중심(0) 기준, 높이는 스폰 지점 높이를 쓴다.</summary>
    [System.Serializable]
    public sealed class EnemyPlacement
    {
        public EnemyData data;
        public float x;
    }

    public enum ReinforcementTrigger
    {
        /// <summary>플레이어가 x ≥ value 에 도달.</summary>
        ReachX,
        /// <summary>이 방에서 누적 value 마리 처치.</summary>
        KillCount
    }

    /// <summary>조건부 추가 소환 한 무리(스컬의 「맵 시스템이 부르는 추가 무리」).</summary>
    [System.Serializable]
    public sealed class Reinforcement
    {
        public ReinforcementTrigger trigger = ReinforcementTrigger.KillCount;
        [Tooltip("ReachX = 맵 x 좌표 · KillCount = 누적 처치 수")]
        public float value;
        public List<EnemyPlacement> enemies = new();
    }
}
