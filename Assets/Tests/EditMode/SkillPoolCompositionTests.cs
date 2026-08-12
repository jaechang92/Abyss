using System.Collections.Generic;
using Abyss.Runtime.Draft;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 스킬 풀 구성의 불변식. 개별 스킬의 효과가 아니라 <b>풀 전체가 드래프트로서 성립하는가</b>를 본다.
    ///
    /// 이 테스트가 생긴 계기: 스킬 15종 시점에 풀의 Common이 3장뿐이었다. 추첨은 등급을 먼저 고르는
    /// 2단계가 아니라 <b>스킬마다 등급 가중치를 붙인 단일 가중 추첨</b>이라(<see cref="DraftPoolManager"/>),
    /// Common 3장이 실효 점유의 절반 이상을 가져갔다(암흑검사 기준 180/330 ≈ 55%).
    /// 3지선다마다 같은 카드가 반복됐는데, <b>스킬을 하나씩 볼 때는 아무 문제가 없어 보인다</b> —
    /// 결손이 개별 에셋이 아니라 분포에 있었다.
    ///
    /// ⚠️ 이 테스트는 <c>Resources/Data/Skills</c>의 <b>실제 에셋</b>을 읽는다.
    /// 새 스킬을 추가했다면 <c>Tools > Abyss > Generate Content</c>를 <b>먼저</b> 실행할 것 —
    /// 에셋이 없으면 여기서 실패한다(그게 이 테스트가 하려는 일이다).
    /// </summary>
    public sealed class SkillPoolCompositionTests
    {
        private SkillData[] pool;

        [SetUp]
        public void SetUp()
        {
            SkillCatalog.Reload();
            pool = SkillCatalog.All;
            Assert.IsNotNull(pool, "SkillCatalog가 비었다 — Resources/Data/Skills 경로 확인.");
            Assert.Greater(pool.Length, 0, "스킬 에셋이 하나도 없다 — ContentBuilder를 먼저 실행할 것.");
        }

        /// <summary>
        /// 가장 무거운 가중치를 가진 등급의 후보가 너무 적으면 드래프트가 매번 같은 카드를 보여준다.
        /// 3지선다 한 판을 서로 다른 카드로 채우는 것이 하한(3)이고, <b>매번 같은 세 장</b>이 되지
        /// 않으려면 그보다 여유가 있어야 한다.
        /// </summary>
        [Test]
        public void CommonPool_HasEnoughCandidatesForDraft()
        {
            int common = CountByRarity(SkillRarity.Common);
            Assert.GreaterOrEqual(common, 5,
                $"Common 스킬이 {common}장뿐이다. 가중치가 가장 높은 등급이라 드래프트가 반복된다.");
        }

        /// <summary>
        /// 전역(any) 스킬이 과반은 돼야 한다. 폼 전용은 그 폼을 장착한 런에서만 후보가 되므로
        /// (<see cref="DraftWeightCalculator"/>가 formBound 불일치를 가중치 0으로 거른다),
        /// 비율이 뒤집히면 <b>어떤 폼을 골랐는지가 스킬 다양성을 좌우</b>하게 된다.
        /// 로드맵(08 §2-5)의 "폼 전용 30%+"는 <b>하한</b>이지 목표치가 아니다.
        /// </summary>
        [Test]
        public void FormAgnosticSkills_StayInMajority()
        {
            int anyForm = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && string.IsNullOrEmpty(pool[i].formBound)) anyForm += 1;
            }

            Assert.Greater(anyForm * 2, pool.Length,
                $"전역 스킬이 {anyForm}/{pool.Length}장 — 폼 전용이 과반이면 폼 선택이 스킬 다양성을 결정해 버린다.");
        }

        /// <summary>
        /// Active 스킬은 <see cref="SkillData.relatedAbility"/>가 없으면 <b>뽑히지만 아무 일도 하지 않는다.</b>
        /// 에러도 나지 않으므로 배선 누락은 조용히 통과한다 — 신규 Active를 추가할 때 가장 잊기 쉬운 절차다.
        /// </summary>
        [Test]
        public void EveryActiveSkill_HasRelatedAbility()
        {
            var missing = new List<string>();
            for (int i = 0; i < pool.Length; i++)
            {
                var skill = pool[i];
                if (skill == null || skill.category != SkillCategory.Active) continue;
                if (skill.relatedAbility == null) missing.Add(skill.skillId);
            }

            CollectionAssert.IsEmpty(missing,
                "relatedAbility 미연결 Active 스킬 — ContentBuilder의 WireActiveAbilities에 항목을 추가할 것.");
        }

        /// <summary>
        /// 전용 발동 코드가 참조하는 스킬 ID(<see cref="SkillIds"/>)는 실제 에셋에 존재해야 한다.
        /// 오타는 예외가 아니라 <b>'미보유' 판정</b>으로 나타나 효과가 조용히 꺼진다 —
        /// SkillIds 자신의 주석이 경고하는 위험이고, 그 경고를 테스트로 고정한다.
        /// </summary>
        [Test]
        public void EverySkillIdConstant_ResolvesToAnAsset()
        {
            var ids = new[]
            {
                SkillIds.EXPLOSIVE_THEOLOGY,
                SkillIds.ABYSS_ALLY,
                SkillIds.COUNTER_STANCE,
                SkillIds.BURN_ENHANCEMENT,
                SkillIds.FLAME_ARMOR,
                SkillIds.AFTERIMAGE,
                SkillIds.ABYSS_CHARGE,
                SkillIds.SOUL_RECLAIM
            };

            var missing = new List<string>();
            foreach (var id in ids)
            {
                if (!Exists(id)) missing.Add(id);
            }

            CollectionAssert.IsEmpty(missing, "SkillIds 상수에 대응하는 스킬 에셋이 없다.");
        }

        /// <summary>
        /// skillId 중복은 보유 판정·시너지 집계를 통째로 흔든다(같은 ID 두 장이면 축이 2로 세어져
        /// 시너지가 혼자 발동한다). 파일명이 달라도 ID는 유일해야 한다.
        /// </summary>
        [Test]
        public void SkillIds_AreUnique()
        {
            var seen = new HashSet<string>();
            var duplicated = new List<string>();

            for (int i = 0; i < pool.Length; i++)
            {
                var skill = pool[i];
                if (skill == null || string.IsNullOrEmpty(skill.skillId)) continue;
                if (!seen.Add(skill.skillId)) duplicated.Add(skill.skillId);
            }

            CollectionAssert.IsEmpty(duplicated, "중복된 skillId가 있다.");
        }

        /// <summary>
        /// 시너지 스킬은 축이 없으면 <see cref="SynergyAxis.IsSynergyActive"/>가 항상 false를 돌려준다
        /// (축 태그로 같은 축 개수를 세기 때문). 즉 <b>영영 발동하지 않는 카드</b>가 된다.
        /// </summary>
        [Test]
        public void EverySynergySkill_HasRegisteredAxis()
        {
            var invalid = new List<string>();
            for (int i = 0; i < pool.Length; i++)
            {
                var skill = pool[i];
                if (skill == null || skill.category != SkillCategory.Synergy) continue;
                if (!SynergyAxis.IsRegistered(skill.synergyTag)) invalid.Add(skill.skillId);
            }

            CollectionAssert.IsEmpty(invalid, "축이 없거나 미등록인 시너지 스킬 — 발동 조건을 영영 못 채운다.");
        }

        /// <summary>
        /// 시너지 스킬이 발동하려면 같은 축이 임계(2)만큼 필요하다. 그러므로 <b>같은 축에 다른 스킬이
        /// 최소 하나</b>는 있어야 한다. 축에 시너지 카드 하나뿐이면 그 카드는 절대 켜지지 않는다.
        /// </summary>
        [Test]
        public void EverySynergySkill_HasCompanionInSameAxis()
        {
            var orphan = new List<string>();
            for (int i = 0; i < pool.Length; i++)
            {
                var skill = pool[i];
                if (skill == null || skill.category != SkillCategory.Synergy) continue;

                int sameAxis = 0;
                for (int j = 0; j < pool.Length; j++)
                {
                    if (pool[j] != null && pool[j].synergyTag == skill.synergyTag) sameAxis += 1;
                }

                if (sameAxis < SynergyAxis.ACTIVATION_THRESHOLD) orphan.Add(skill.skillId);
            }

            CollectionAssert.IsEmpty(orphan, "같은 축에 동료 스킬이 없어 발동 임계를 채울 수 없는 시너지 스킬.");
        }

        private int CountByRarity(SkillRarity rarity)
        {
            int n = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].rarity == rarity) n += 1;
            }
            return n;
        }

        private bool Exists(string skillId)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].skillId == skillId) return true;
            }
            return false;
        }
    }
}
