using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 스킬 카드 표시 규약의 단일 기준점(SoT). 축 태그는 <see cref="SynergyAxis"/>가,
    /// 방 타입은 <see cref="Abyss.Runtime.Stage.RoomTypeDisplay"/>가 담당하는 것과 같은 역할이다.
    ///
    /// 드래프트 카드(<see cref="Abyss.Runtime.UI.SkillCardView"/>)의 private static 헬퍼로 있던 것을 승격했다.
    /// 도감(완주 루프 계획 3-1)이 같은 별 표기·배경색·분류 줄을 써야 하는 <b>두 번째 소비자</b>가 되면서
    /// 카드 파일에 두면 복제가 생기는 시점이 됐다 — 희귀도가 늘 때 한쪽만 고치면 같은 스킬이
    /// 화면마다 다른 등급으로 보인다.
    ///
    /// 카테고리도 여기서 한글로 바꾼다. 그전까지는 enum을 그대로 찍어 카드에 <c>Synergy</c>가 노출됐는데,
    /// 이는 축 태그가 <c>fire</c>로 새어 나가 <see cref="SynergyAxis"/>를 만들게 한 것과 같은 종류의 결함이다.
    /// </summary>
    public static class SkillDisplay
    {
        private static readonly Color NeutralBackground = new(0.15f, 0.15f, 0.18f);

        /// <summary>별 4칸 고정폭 표기. 폰트가 확실히 가진 기호만 쓴다(RoomTypeDisplay.Glyph와 같은 방침).</summary>
        public static string RarityLabel(SkillRarity rarity) => rarity switch
        {
            SkillRarity.Common => "★☆☆☆",
            SkillRarity.Rare => "★★☆☆",
            SkillRarity.Epic => "★★★☆",
            SkillRarity.Legendary => "★★★★",
            _ => string.Empty
        };

        /// <summary>카드·타일 배경색. 채도가 낮은 어두운 색조라 위에 흰 글씨를 올릴 수 있다.</summary>
        public static Color RarityBackground(SkillRarity rarity) => rarity switch
        {
            SkillRarity.Common => new Color(0.18f, 0.2f, 0.22f),
            SkillRarity.Rare => new Color(0.15f, 0.22f, 0.32f),
            SkillRarity.Epic => new Color(0.28f, 0.18f, 0.32f),
            SkillRarity.Legendary => new Color(0.4f, 0.32f, 0.1f),
            _ => NeutralBackground
        };

        /// <summary>카테고리 표시명. 미등록 값은 열거형 이름으로 폴백한다(화면이 비지 않게).</summary>
        public static string CategoryLabel(SkillCategory category) => category switch
        {
            SkillCategory.Active => "액티브",
            SkillCategory.Passive => "패시브",
            SkillCategory.Augment => "강화",
            SkillCategory.Synergy => "시너지",
            _ => category.ToString()
        };

        /// <summary>희귀도 · 카테고리 · [축] 한 줄. 드래프트 카드와 도감이 같은 문구를 쓴다.</summary>
        public static string Headline(SkillData skill)
        {
            if (skill == null) return string.Empty;
            return $"{RarityLabel(skill.rarity)} · {CategoryLabel(skill.category)} · [{SynergyAxis.GetDisplayName(skill.synergyTag)}]";
        }
    }
}
