using System.Collections.Generic;
using Abyss.Runtime.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 제단이 내놓을 무기를 정하는 규칙(<see cref="WeaponReward"/>). 획득 3창구 중 첫째다(§3-B).
    ///
    /// 🔴 여기가 <c>null</c>을 주면 <b>게이트가 조용히 스킵된다</b> — 방은 정상으로 넘어가고
    /// 보상만 안 나온다. 오류도 로그 없이는 안 보이는 종류라, 조건을 테스트로 못박는다.
    /// </summary>
    public sealed class WeaponRewardTests
    {
        // 색인이 WeaponRarity 값이다(RunConfig.weaponRarityWeights 기본값과 같다).
        private static readonly float[] Weights = { 38f, 25f, 16f, 10f, 6f, 3f, 2f };

        private static WeaponData Make(string weaponId, string formBound,
                                       WeaponRarity rarity = WeaponRarity.Common)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponId = weaponId;
            weapon.formBound = formBound;
            weapon.rarity = rarity;
            return weapon;
        }

        [Test]
        public void 고정_지정이_있으면_추첨하지_않는다()
        {
            // 보스 방처럼 무엇이 나올지 정해 둔 자리. 폼이 안 맞아도 그대로 준다 —
            // 고정 지정은 기획이 일부러 놓은 것이라 여기서 되묻지 않는다.
            var fixedReward = Make("boss_blade", "swordsman");
            var catalog = new List<WeaponData> { Make("other", "archer") };

            Assert.AreSame(fixedReward, WeaponReward.Resolve(fixedReward, catalog, "archer", Weights));
        }

        [Test]
        public void 현재_폼이_쓸_수_있는_무기만_나온다()
        {
            // 🔴 무기는 폼 전용이다 — 검사에게 활이 나오면 손에 안 붙는다.
            var sword = Make("keen_blade", "swordsman");
            var catalog = new List<WeaponData> { sword, Make("long_bow", "archer"), Make("tower", "shield") };

            for (int i = 0; i < 20; i++)
            {
                Assert.AreSame(sword, WeaponReward.Resolve(null, catalog, "swordsman", Weights));
            }
        }

        [Test]
        public void 후보가_없으면_null_이다()
        {
            // 호출자는 이걸 받으면 게이트를 걸지 않는다. 방이 안 넘어가는 것보다 가벼운 고장이다.
            var catalog = new List<WeaponData> { Make("long_bow", "archer") };

            Assert.IsNull(WeaponReward.Resolve(null, catalog, "swordsman", Weights));
            Assert.IsNull(WeaponReward.Resolve(null, catalog, null, Weights));
            Assert.IsNull(WeaponReward.Resolve(null, new List<WeaponData>(), "archer", Weights));
            Assert.IsNull(WeaponReward.Resolve(null, null, "archer", Weights));
        }

        [Test]
        public void 등급_가중치가_전부_0이면_null_이다()
        {
            // 🔑 RunConfig 를 잘못 채운 경우다. 임의의 기본값으로 굴러가는 것보다 안 나오는 편이 낫다 —
            //    조용히 굴러가면 「확률을 설정한 적이 없다」가 영영 안 드러난다.
            var catalog = new List<WeaponData> { Make("keen_blade", "swordsman") };
            var zero = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };

            Assert.IsNull(WeaponReward.Resolve(null, catalog, "swordsman", zero));
        }

        [Test]
        public void 같은_폼_후보가_여럿이면_전부_나올_수_있다()
        {
            // 한 자루만 계속 나오면 '운'이 아니라 고장으로 읽힌다(RelicGacha 와 같은 판단).
            var a = Make("blade_a", "swordsman", WeaponRarity.Common);
            var b = Make("blade_b", "swordsman", WeaponRarity.Common);
            var catalog = new List<WeaponData> { a, b, Make("long_bow", "archer") };

            var seen = new HashSet<WeaponData>();
            var rng = new System.Random(12345);
            for (int i = 0; i < 100; i++)
            {
                seen.Add(WeaponReward.Resolve(null, catalog, "swordsman", Weights, rng));
            }

            Assert.AreEqual(2, seen.Count, "같은 등급 후보 둘이 모두 나와야 한다");
            Assert.IsTrue(seen.Contains(a) && seen.Contains(b));
        }
    }
}
