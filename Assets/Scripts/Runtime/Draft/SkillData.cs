using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 스킬 정의 ScriptableObject. 프로토 스펙 (03-skill-draft-system.md §6).
    /// 프로토 9개: 불꽃 5 + 심연 4. 피의 서약 축은 EA 이월.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData", menuName = "Abyss/Data/Skill Data")]
    public sealed class SkillData : ScriptableObject
    {
        [Header("식별자")]
        public string skillId;

        [Header("표시 정보")]
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("분류")]
        public SkillRarity rarity = SkillRarity.Common;
        public SkillCategory category = SkillCategory.Passive;

        [Tooltip("시너지 축 태그. 예: fire, abyss")]
        public string synergyTag;

        [Tooltip("폼 귀속. 빈 문자열이면 any (런 전역).")]
        public string formBound;

        [Header("수치 (툴팁 F2 대응)")]
        public float flatBonus;
        public float multiplier = 1f;
        [TextArea(1, 3), Tooltip("수식 유형 명시. 예: (flatBonus + base) * multiplier")]
        public string formulaDescription;

        [Header("연결 어빌리티 (Active일 때)")]
        public AbilityData relatedAbility;

        [Header("등장 조건")]
        public string[] tags;
    }
}
