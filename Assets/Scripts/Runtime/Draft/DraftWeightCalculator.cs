using System.Collections.Generic;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 희귀도 가중치 계산. 순수 함수로 유지해 EditMode 테스트 가능(P-08 완료 조건).
    /// 프로토 분포(Analyst): Common 60 / Rare 28 / Epic 10 / Legendary 2.
    /// 보스 처치 후 Rare+ 부스트·연속 미등장 soft pity는 후속 태스크(Week 2)에서 추가.
    /// </summary>
    public static class DraftWeightCalculator
    {
        private static readonly IReadOnlyDictionary<SkillRarity, float> defaultWeights = new Dictionary<SkillRarity, float>
        {
            { SkillRarity.Common, 60f },
            { SkillRarity.Rare, 28f },
            { SkillRarity.Epic, 10f },
            { SkillRarity.Legendary, 2f }
        };

        public static float GetBaseWeight(SkillRarity rarity) => defaultWeights[rarity];

        /// <summary>
        /// 스킬 1개의 실제 가중치. 폼 귀속 필터, 시너지 우대 등을 반영.
        /// </summary>
        public static float CalculateWeight(SkillData skill, string currentFormId, IReadOnlyCollection<string> ownedSynergyTags)
        {
            if (skill == null) return 0f;

            if (!string.IsNullOrEmpty(skill.formBound) && skill.formBound != currentFormId)
            {
                return 0f;
            }

            float weight = GetBaseWeight(skill.rarity);

            if (ownedSynergyTags != null && !string.IsNullOrEmpty(skill.synergyTag) && ownedSynergyTags.Contains(skill.synergyTag))
            {
                weight *= 1.3f;
            }

            return weight;
        }
    }
}
