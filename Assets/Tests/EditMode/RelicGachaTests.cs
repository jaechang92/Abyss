using System.Collections.Generic;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 유물 가차 추첨(<see cref="RelicGacha"/>) 규칙. 에셋을 읽지 않고 후보 목록을 직접 만들어
    /// 규칙만 본다 — 카탈로그 자체의 무결성은 <see cref="RelicCatalogTests"/> 담당이다.
    /// </summary>
    public sealed class RelicGachaTests
    {
        private readonly List<RelicData> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var relic in created)
            {
                if (relic != null) Object.DestroyImmediate(relic);
            }
            created.Clear();
        }

        private RelicData Relic(string id, RelicRarity rarity, MetaUpgradeType type = MetaUpgradeType.MaxHp)
        {
            var relic = ScriptableObject.CreateInstance<RelicData>();
            relic.relicId = id;
            relic.rarity = rarity;
            relic.effectType = type;
            created.Add(relic);
            return relic;
        }

        [Test]
        public void 후보가_없으면_null_이다()
        {
            Assert.IsNull(RelicGacha.Draw(new List<RelicData>()));
            Assert.IsNull(RelicGacha.Draw(null));
        }

        /// <summary>
        /// 해금 2종은 유물 효과로 쓸 수 없다(레벨 개념이 없고, 장착을 빼면 해금이 풀리는
        /// 이상한 상태가 된다). 가중치 0으로 걸러 <b>뽑히지 않게</b> 한다.
        /// </summary>
        [Test]
        public void 해금_효과_유물은_가중치가_0이라_안_뽑힌다()
        {
            var unlockForm = Relic("relic_unlock_form", RelicRarity.Epic, MetaUpgradeType.UnlockForm);
            var unlockSkill = Relic("relic_unlock_skill", RelicRarity.Epic, MetaUpgradeType.UnlockSkill);

            Assert.AreEqual(0, RelicGacha.WeightOf(unlockForm));
            Assert.AreEqual(0, RelicGacha.WeightOf(unlockSkill));

            // 가중치 합이 0이면 뽑을 것이 없다 — 임의의 하나를 돌려주지 않는다.
            Assert.IsNull(RelicGacha.Draw(new List<RelicData> { unlockForm, unlockSkill }));
        }

        [Test]
        public void 등급이_높을수록_가중치가_낮다()
        {
            int common = RelicGacha.WeightOf(Relic("c", RelicRarity.Common));
            int rare = RelicGacha.WeightOf(Relic("r", RelicRarity.Rare));
            int epic = RelicGacha.WeightOf(Relic("e", RelicRarity.Epic));

            Assert.Greater(common, rare, "Common 이 Rare 보다 흔해야 한다.");
            Assert.Greater(rare, epic, "Rare 가 Epic 보다 흔해야 한다.");
        }

        /// <summary>
        /// ⚠️ <b>가중치 60이 곧 확률 60%는 아니다.</b> 유물 단위 단일 가중 추첨이라
        /// 실효 확률은 등급별 <i>개수</i>에 따라 달라진다 — Common 이 둘이고 Epic 이 하나면
        /// Common 쪽 몫은 120/130 이다. 스킬 풀 작업에서 확인한 것과 같은 구조라,
        /// "등급 가중치 = 등급 확률"로 읽지 않도록 여기 고정해 둔다.
        /// </summary>
        [Test]
        public void 실효_확률은_등급별_개수에_비례한다()
        {
            var pool = new List<RelicData>
            {
                Relic("c1", RelicRarity.Common),
                Relic("c2", RelicRarity.Common),
                Relic("e1", RelicRarity.Epic)
            };

            int commonWeight = RelicGacha.WeightOf(pool[0]) + RelicGacha.WeightOf(pool[1]);
            int epicWeight = RelicGacha.WeightOf(pool[2]);

            Assert.AreEqual(commonWeight + epicWeight, TotalWeight(pool));
            Assert.Greater(commonWeight, epicWeight * 2,
                "Common 이 둘이면 Epic 하나보다 훨씬 자주 나와야 한다 — 등급 가중치만으로 읽으면 안 된다.");
        }

        /// <summary>시드를 고정하면 같은 결과가 나온다 — 분포 검증의 전제.</summary>
        [Test]
        public void 시드가_같으면_같은_것을_뽑는다()
        {
            var pool = new List<RelicData>
            {
                Relic("a", RelicRarity.Common),
                Relic("b", RelicRarity.Rare),
                Relic("c", RelicRarity.Epic)
            };

            var first = RelicGacha.Draw(pool, new System.Random(1234));
            var second = RelicGacha.Draw(pool, new System.Random(1234));

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        /// <summary>가중치대로 대략 갈리는지 — 표본 수를 크게 잡아 우연을 배제한다.</summary>
        [Test]
        public void 많이_뽑으면_흔한_등급이_더_많이_나온다()
        {
            var common = Relic("c", RelicRarity.Common);
            var epic = Relic("e", RelicRarity.Epic);
            var pool = new List<RelicData> { common, epic };

            var rng = new System.Random(7);
            int commonCount = 0;
            const int TRIALS = 2000;
            for (int i = 0; i < TRIALS; i++)
            {
                if (ReferenceEquals(RelicGacha.Draw(pool, rng), common)) commonCount++;
            }

            // 기대 60/(60+10) = 약 86%. 넉넉한 구간으로 두어 시드 교체에도 깨지지 않게 한다.
            Assert.Greater(commonCount, TRIALS * 0.75f);
            Assert.Less(commonCount, TRIALS * 0.95f);
        }

        private static int TotalWeight(IReadOnlyList<RelicData> pool)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++) total += RelicGacha.WeightOf(pool[i]);
            return total;
        }
    }
}
