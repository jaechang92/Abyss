using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 유물 등급. 가차 추첨 가중치와 표시 색의 기준이다.
    ///
    /// ⚠️ 숫자가 직렬화되지는 않지만(세이브는 relicId 문자열만 든다) 가중치 배열
    /// <c>RelicGacha.RARITY_WEIGHTS</c>가 <b>이 순서를 인덱스로</b> 쓴다. 순서를 바꾸면
    /// 확률이 조용히 뒤바뀐다 — 새 등급은 뒤에 붙이고 가중치도 함께 늘릴 것.
    /// </summary>
    public enum RelicRarity
    {
        Common,
        Rare,
        Epic
    }

    /// <summary>
    /// 유물 정의 SO. 로비 상점에서 <b>확률로</b> 얻으며, 장착 슬롯에 끼운 것만 런에 반영된다.
    /// Resources/Data/Relics/ 하위에 배치 → <see cref="RelicCatalog"/>가 로드(폴더가 SoT).
    ///
    /// 🔑 <b>효과 어휘로 <see cref="MetaUpgradeType"/>을 재사용한다.</b>
    /// 상점(런)이 쓰는 <c>EventEffectType</c>이 아니다 — 그쪽은 골드·HP·드래프트처럼
    /// <i>살아 있는 플레이어에게 지금 일어나는 일</i>이고, 유물은 제단 업그레이드와 같이
    /// <i>런을 시작할 때 이미 적용돼 있는 것</i>이다. 어휘를 새로 만들면 같은 뜻이 두 벌이 되고,
    /// <see cref="MetaUpgrades"/>의 합산도 두 갈래로 갈라진다.
    ///
    /// ⚠️ <see cref="MetaUpgradeType.UnlockForm"/>·<see cref="MetaUpgradeType.UnlockSkill"/>은
    /// 유물 효과로 쓰지 않는다. 해금은 "샀다/안 샀다"의 1회성이라 레벨 개념이 없고,
    /// 장착을 빼면 해금이 풀리는 이상한 상태가 된다. <see cref="IsUsableEffect"/>가 걸러낸다.
    /// </summary>
    [CreateAssetMenu(fileName = "Relic", menuName = "Abyss/Data/Relic")]
    public sealed class RelicData : ScriptableObject
    {
        [Header("식별자")]
        [Tooltip("고유 ID. MetaSave 보유·장착 키. 예: relic_ember_core")]
        public string relicId;

        [Header("표시")]
        [Tooltip("이름 StringKey (GameText.csv).")]
        public string nameKey;

        [Tooltip("설명 StringKey (GameText.csv).")]
        public string descKey;

        [Tooltip("도감·장착 슬롯에 그릴 아이콘. 비우면 이름 첫 글자로 폴백한다.")]
        public Sprite icon;

        [Header("등급")]
        public RelicRarity rarity = RelicRarity.Common;

        [Header("효과")]
        [Tooltip("효과 종류. 제단 업그레이드와 같은 어휘를 쓴다(해금 2종은 제외).")]
        public MetaUpgradeType effectType = MetaUpgradeType.MaxHp;

        [Tooltip("레벨당 효과 증분. MaxHp=HP량, AttackMultiplier=배율(0.05=+5%), StartingGold=골드, FreeReroll=횟수.")]
        public float valuePerLevel = 1f;

        [Tooltip("중복 획득으로 올릴 수 있는 최대 레벨. 최대치에 닿은 유물도 추첨 후보에는 남는다.")]
        [Min(1)] public int maxLevel = 5;

        /// <summary>이 효과 종류를 유물이 쓸 수 있는가(해금 2종은 불가).</summary>
        public bool IsUsableEffect =>
            effectType != MetaUpgradeType.UnlockForm && effectType != MetaUpgradeType.UnlockSkill;

        /// <summary>지정 레벨에서의 누적 효과값. 레벨 0(미보유·미장착)이면 0.</summary>
        public float EffectAtLevel(int level) => valuePerLevel * Mathf.Clamp(level, 0, maxLevel);
    }
}
