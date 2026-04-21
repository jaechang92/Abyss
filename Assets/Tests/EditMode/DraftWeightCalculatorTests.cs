using Abyss.Runtime.Draft;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// DraftWeightCalculator 순수 함수 EditMode 테스트.
    /// 희귀도 분포 · 폼 귀속 필터 · 시너지 부스트 세 가지 가드.
    /// </summary>
    public sealed class DraftWeightCalculatorTests
    {
        [Test]
        public void GetBaseWeight_Common_Returns60()
        {
            Assert.AreEqual(60f, DraftWeightCalculator.GetBaseWeight(SkillRarity.Common));
        }

        [Test]
        public void GetBaseWeight_Legendary_Returns2()
        {
            Assert.AreEqual(2f, DraftWeightCalculator.GetBaseWeight(SkillRarity.Legendary));
        }

        [Test]
        public void CalculateWeight_FormBoundSkill_ReturnsZero_WhenWrongForm()
        {
            var skill = CreateSkill(SkillRarity.Rare, formBound: "dark_blade", synergyTag: null);
            float weight = DraftWeightCalculator.CalculateWeight(skill, currentFormId: "void_archer", ownedSynergyTags: null);
            Assert.AreEqual(0f, weight);
            Object.DestroyImmediate(skill);
        }

        [Test]
        public void CalculateWeight_NoFormBound_ReturnsBaseWeight()
        {
            var skill = CreateSkill(SkillRarity.Epic, formBound: string.Empty, synergyTag: null);
            float weight = DraftWeightCalculator.CalculateWeight(skill, currentFormId: "dark_blade", ownedSynergyTags: null);
            Assert.AreEqual(10f, weight);
            Object.DestroyImmediate(skill);
        }

        [Test]
        public void CalculateWeight_MatchingSynergy_BoostsBy1_3x()
        {
            var skill = CreateSkill(SkillRarity.Common, formBound: string.Empty, synergyTag: "fire");
            var ownedTags = new[] { "fire" };
            float weight = DraftWeightCalculator.CalculateWeight(skill, currentFormId: "dark_blade", ownedSynergyTags: ownedTags);
            Assert.AreEqual(60f * 1.3f, weight, 0.0001f);
            Object.DestroyImmediate(skill);
        }

        [Test]
        public void CalculateWeight_NullSkill_ReturnsZero()
        {
            float weight = DraftWeightCalculator.CalculateWeight(null, currentFormId: "dark_blade", ownedSynergyTags: null);
            Assert.AreEqual(0f, weight);
        }

        private static SkillData CreateSkill(SkillRarity rarity, string formBound, string synergyTag)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillId = "test_skill";
            skill.rarity = rarity;
            skill.formBound = formBound;
            skill.synergyTag = synergyTag;
            return skill;
        }
    }
}
