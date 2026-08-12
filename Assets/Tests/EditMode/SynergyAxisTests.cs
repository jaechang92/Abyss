using System.Collections.Generic;
using Abyss.Runtime.Draft;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 시너지 발동 판정(<see cref="SynergyAxis.IsSynergyActive"/>) EditMode 테스트.
    /// 실제 에셋을 읽지 않고 <see cref="SkillData"/>를 직접 만들어 규칙만 본다
    /// (풀 구성 자체는 <see cref="SkillPoolCompositionTests"/> 담당).
    /// </summary>
    public sealed class SynergyAxisTests
    {
        private readonly List<SkillData> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var skill in created)
            {
                if (skill != null) Object.DestroyImmediate(skill);
            }
            created.Clear();
        }

        /// <summary>
        /// 개수에는 스킬 자신이 포함된다 — 그래서 임계 2는 "시너지 스킬 + 같은 축 하나 더"다.
        /// HUD 칩이 보유 전부를 세므로, 여기서만 자신을 빼면 칩이 켜졌는데 효과가 없는 상태가 된다.
        /// </summary>
        [Test]
        public void Synergy_WithOneCompanionInSameAxis_Activates()
        {
            var owned = new List<SkillData>
            {
                Skill("skill_counter_stance", SynergyAxis.AXIS_GUARD),
                Skill("skill_shield_bash", SynergyAxis.AXIS_GUARD)
            };

            Assert.IsTrue(SynergyAxis.IsSynergyActive(owned, "skill_counter_stance"));
        }

        [Test]
        public void Synergy_Alone_DoesNotActivate()
        {
            var owned = new List<SkillData> { Skill("skill_counter_stance", SynergyAxis.AXIS_GUARD) };

            Assert.IsFalse(SynergyAxis.IsSynergyActive(owned, "skill_counter_stance"));
        }

        /// <summary>다른 축은 아무리 많아도 임계를 채우지 못한다.</summary>
        [Test]
        public void Synergy_CompanionsInOtherAxes_DoNotCount()
        {
            var owned = new List<SkillData>
            {
                Skill("skill_counter_stance", SynergyAxis.AXIS_GUARD),
                Skill("skill_fireball", SynergyAxis.AXIS_FIRE),
                Skill("skill_afterimage", SynergyAxis.AXIS_ABYSS)
            };

            Assert.IsFalse(SynergyAxis.IsSynergyActive(owned, "skill_counter_stance"));
        }

        /// <summary>보유하지 않은 시너지 스킬은 같은 축이 아무리 쌓여도 발동하지 않는다.</summary>
        [Test]
        public void Synergy_NotOwned_DoesNotActivate()
        {
            var owned = new List<SkillData>
            {
                Skill("skill_shield_bash", SynergyAxis.AXIS_GUARD),
                Skill("skill_iron_guard", SynergyAxis.AXIS_GUARD)
            };

            Assert.IsFalse(SynergyAxis.IsSynergyActive(owned, "skill_counter_stance"));
        }

        /// <summary>수호 축이 표시 규약(한글명·색)에 등록돼 있어야 HUD 칩이 raw 태그를 노출하지 않는다.</summary>
        [Test]
        public void GuardAxis_IsRegisteredForDisplay()
        {
            Assert.IsTrue(SynergyAxis.IsRegistered(SynergyAxis.AXIS_GUARD));
            Assert.AreEqual("수호", SynergyAxis.GetDisplayName(SynergyAxis.AXIS_GUARD));
        }

        private SkillData Skill(string skillId, string axis)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillId = skillId;
            skill.synergyTag = axis;
            created.Add(skill);
            return skill;
        }
    }
}
