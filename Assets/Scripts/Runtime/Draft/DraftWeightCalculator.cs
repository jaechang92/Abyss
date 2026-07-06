using System.Collections.Generic;
using System.Linq;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 희귀도별 추첨 가중치 묶음(불변). 순수 계산기(DraftWeightCalculator)가 RunConfig에 직접
    /// 의존하지 않도록, 설정값은 이 값 타입으로 주입한다. RunConfig→RarityWeights 변환은 호출부
    /// (DraftPoolManager)가 담당해 계산기의 테스트 가능성을 보존한다.
    /// </summary>
    public readonly struct RarityWeights
    {
        public readonly float Common;
        public readonly float Rare;
        public readonly float Epic;
        public readonly float Legendary;

        public RarityWeights(float common, float rare, float epic, float legendary)
        {
            Common = common;
            Rare = rare;
            Epic = epic;
            Legendary = legendary;
        }

        /// <summary>Analyst 확정 프로토 분포: Common 60 / Rare 28 / Epic 10 / Legendary 2.</summary>
        public static readonly RarityWeights Default = new RarityWeights(60f, 28f, 10f, 2f);

        public float For(SkillRarity rarity) => rarity switch
        {
            SkillRarity.Common => Common,
            SkillRarity.Rare => Rare,
            SkillRarity.Epic => Epic,
            SkillRarity.Legendary => Legendary,
            _ => 0f
        };
    }

    /// <summary>
    /// 희귀도 가중치 계산. 순수 함수로 유지해 EditMode 테스트 가능(P-08 완료 조건).
    /// 희귀도 분포는 RarityWeights로 주입(기본값 미지정 시 Analyst 프로토 분포 Default).
    /// 실제 값은 RunConfig(commonWeight 등)가 SoT — DraftPoolManager가 RunConfigProvider에서 읽어 주입.
    /// 시너지 우대 배율은 프로토 상수(SYNERGY_MULTIPLIER). 보스 처치 부스트·soft pity는 후속 태스크.
    /// </summary>
    public static class DraftWeightCalculator
    {
        /// <summary>보유 시너지 태그와 일치할 때 곱하는 우대 배율.</summary>
        private const float SYNERGY_MULTIPLIER = 1.3f;

        /// <summary>기본 분포(Default) 기준 희귀도 가중치. 하위호환용.</summary>
        public static float GetBaseWeight(SkillRarity rarity) => RarityWeights.Default.For(rarity);

        /// <summary>주입 분포 기준 희귀도 가중치.</summary>
        public static float GetBaseWeight(SkillRarity rarity, in RarityWeights weights) => weights.For(rarity);

        /// <summary>
        /// 스킬 1개의 실제 가중치(기본 분포). 폼 귀속 필터·시너지 우대 반영. 하위호환용 오버로드.
        /// </summary>
        public static float CalculateWeight(SkillData skill, string currentFormId, IReadOnlyCollection<string> ownedSynergyTags)
            => CalculateWeight(skill, currentFormId, ownedSynergyTags, RarityWeights.Default);

        /// <summary>
        /// 스킬 1개의 실제 가중치. 주입된 희귀도 분포(weights)를 기준으로 폼 귀속 필터·시너지 우대를 반영한다.
        /// </summary>
        public static float CalculateWeight(SkillData skill, string currentFormId, IReadOnlyCollection<string> ownedSynergyTags, in RarityWeights weights)
        {
            if (skill == null) return 0f;

            if (!string.IsNullOrEmpty(skill.formBound) && skill.formBound != currentFormId)
            {
                return 0f;
            }

            float weight = weights.For(skill.rarity);

            if (ownedSynergyTags != null && !string.IsNullOrEmpty(skill.synergyTag) && ownedSynergyTags.Contains(skill.synergyTag))
            {
                weight *= SYNERGY_MULTIPLIER;
            }

            return weight;
        }
    }
}
