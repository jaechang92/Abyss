using System.Collections.Generic;
using Abyss.Runtime.Weapon;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 등급 정규화 추첨. <b>이 시스템의 요구사항 자체가 「에셋을 넣고 빼도 등급 분포가
    /// 안 흔들린다」</b>(14-weapon-equipment-system.md §3-2·§4)라, 그 성질을 테스트로 고정한다.
    ///
    /// 🔴 흔들리면 <b>오류가 안 난다</b> — 확률이 조용히 바뀔 뿐이다. 잡을 수 있는 것은 여기서 잡는다.
    /// </summary>
    public sealed class WeaponDrawTests
    {
        // 색인이 WeaponRarity 값이다. 일반 → 고대.
        private static readonly float[] Weights = { 38f, 25f, 16f, 10f, 6f, 3f, 2f };

        private static float RarityShare(IReadOnlyList<WeaponRarity> rarities, WeaponRarity target)
        {
            float[] w = WeaponDraw.NormalizedWeights(rarities, Weights);
            float total = 0f, mine = 0f;
            for (int i = 0; i < w.Length; i++)
            {
                total += w[i];
                if (rarities[i] == target) mine += w[i];
            }
            return total > 0f ? mine / total : 0f;
        }

        [Test]
        public void 등급_확률은_항목_개수에_안_흔들린다()
        {
            // 🔑 같은 두 등급인데 한쪽 항목 수만 늘린다. 정규화가 없으면 늘린 쪽 확률이 커진다.
            var few = new[] { WeaponRarity.Common, WeaponRarity.Rare };
            var many = new[]
            {
                WeaponRarity.Common,
                WeaponRarity.Rare, WeaponRarity.Rare, WeaponRarity.Rare, WeaponRarity.Rare,
            };

            float expected = 16f / (38f + 16f);      // 희귀 가중치 / (일반 + 희귀)
            Assert.AreEqual(expected, RarityShare(few, WeaponRarity.Rare), 1e-5f);
            Assert.AreEqual(expected, RarityShare(many, WeaponRarity.Rare), 1e-5f,
                            "희귀 항목을 4배로 늘렸는데 희귀 등급 확률이 변했다");
        }

        [Test]
        public void 등급_안에서는_균등하다()
        {
            var rarities = new[] { WeaponRarity.Rare, WeaponRarity.Rare, WeaponRarity.Common };
            float[] w = WeaponDraw.NormalizedWeights(rarities, Weights);
            Assert.AreEqual(w[0], w[1], 1e-5f, "같은 등급인데 가중치가 다르다");
            Assert.AreEqual(16f / 2f, w[0], 1e-5f, "등급 가중치를 그 등급 항목 수로 나눠야 한다");
        }

        [Test]
        public void 항목이_없는_등급은_롤에_안_들어간다()
        {
            // 🔑 단일 롤이라 「뽑힌 등급에 후보 0개」 실패가 원리적으로 없다는 것을 고정한다.
            var rarities = new[] { WeaponRarity.Ancient };
            float[] w = WeaponDraw.NormalizedWeights(rarities, Weights);
            Assert.AreEqual(2f, w[0], 1e-5f);
            Assert.AreEqual(0, WeaponDraw.DrawIndex(rarities, Weights, 0.0));
            Assert.AreEqual(0, WeaponDraw.DrawIndex(rarities, Weights, 0.999));
        }

        [Test]
        public void DrawIndex_는_롤_전_구간에서_범위_안이다()
        {
            var rarities = new[]
            {
                WeaponRarity.Common, WeaponRarity.Common,
                WeaponRarity.Legendary, WeaponRarity.Ancient,
            };
            for (int i = 0; i <= 1000; i++)
            {
                int index = WeaponDraw.DrawIndex(rarities, Weights, i / 1000.0);
                Assert.That(index, Is.InRange(0, rarities.Length - 1), $"roll {i / 1000.0} 에서 벗어났다");
            }
        }

        [Test]
        public void 후보가_없으면_실패를_돌려준다()
        {
            Assert.AreEqual(-1, WeaponDraw.DrawIndex(new WeaponRarity[0], Weights, 0.5));
            Assert.AreEqual(-1, WeaponDraw.DrawIndex(null, Weights, 0.5));
            Assert.IsNull(WeaponDraw.Draw(null, Weights));
        }

        [Test]
        public void 가중치가_전부_0이면_뽑지_않는다()
        {
            // ⚠️ 0 으로 나눈 값이 섞여 아무거나 뽑히는 것을 막는다.
            var zero = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            var rarities = new[] { WeaponRarity.Common, WeaponRarity.Rare };
            Assert.AreEqual(-1, WeaponDraw.DrawIndex(rarities, zero, 0.5));
        }
    }
}
