using System.Collections.Generic;
using Abyss.Runtime.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 상점 무기 좌판(<see cref="WeaponShop"/>). 획득 3창구 중 둘째다(§3-B).
    ///
    /// 🔴 여기서 나오는 <b>값이 곧 표시 가격이자 차감액</b>이다. 두 곳이 각자 계산하면
    /// 「살 수 있다고 떴는데 안 사진다」가 되고, 그건 오류가 안 난다.
    /// </summary>
    public sealed class WeaponShopTests
    {
        private static readonly float[] Weights = { 38f, 25f, 16f, 10f, 6f, 3f, 2f };
        private static readonly int[] Prices = { 55, 85, 120, 170, 240, 330, 450 };

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
        public void 값은_등급이_정한다()
        {
            Assert.AreEqual(55, WeaponShop.PriceOf(WeaponRarity.Common, Prices));
            Assert.AreEqual(450, WeaponShop.PriceOf(WeaponRarity.Ancient, Prices));
        }

        [Test]
        public void 표에_없는_등급은_0이다()
        {
            // 🔑 0은 「무료」로 보인다 — 그래서 Build 가 0인 자리를 아예 안 내놓는다(아래 테스트).
            var shortTable = new[] { 55, 85 };
            Assert.AreEqual(0, WeaponShop.PriceOf(WeaponRarity.Ancient, shortTable));
            Assert.AreEqual(0, WeaponShop.PriceOf(WeaponRarity.Common, null));
        }

        [Test]
        public void 현재_폼이_쓸_수_있는_무기만_진열된다()
        {
            var sword = Make("keen_blade", "swordsman");
            var catalog = new List<WeaponData> { sword, Make("long_bow", "archer") };

            var offers = WeaponShop.Build(catalog, "swordsman", Weights, Prices, 1);

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(sword, offers[0].Weapon);
            Assert.AreEqual(55, offers[0].Price);
        }

        [Test]
        public void 같은_무기가_두_자리에_안_놓인다()
        {
            // 값만 두 번 보이고 살 이유는 한 번뿐이라 고장으로 읽힌다.
            var a = Make("blade_a", "swordsman");
            var b = Make("blade_b", "swordsman");
            var catalog = new List<WeaponData> { a, b };

            var offers = WeaponShop.Build(catalog, "swordsman", Weights, Prices, 2, new System.Random(7));

            Assert.AreEqual(2, offers.Count);
            Assert.AreNotSame(offers[0].Weapon, offers[1].Weapon);
        }

        [Test]
        public void 후보가_모자라면_채운_만큼만_준다()
        {
            // 호출자는 빈 자리를 감춘다 — "품절"로 두면 팔다가 떨어진 것처럼 보인다.
            var catalog = new List<WeaponData> { Make("keen_blade", "swordsman") };

            Assert.AreEqual(1, WeaponShop.Build(catalog, "swordsman", Weights, Prices, 3).Count);
            Assert.AreEqual(0, WeaponShop.Build(catalog, "archer", Weights, Prices, 2).Count);
            Assert.AreEqual(0, WeaponShop.Build(null, "swordsman", Weights, Prices, 2).Count);
            Assert.AreEqual(0, WeaponShop.Build(catalog, "swordsman", Weights, Prices, 0).Count);
        }

        [Test]
        public void 값이_0인_등급은_공짜로_안_내놓는다()
        {
            // 🔴 RunConfig 를 덜 채운 경우다. 공짜 무기가 조용히 나오는 쪽이 훨씬 나쁘다.
            var ancient = Make("god_blade", "swordsman", WeaponRarity.Ancient);
            var catalog = new List<WeaponData> { ancient };
            var shortTable = new[] { 55, 85 };   // Ancient 자리가 없다

            Assert.AreEqual(0, WeaponShop.Build(catalog, "swordsman", Weights, shortTable, 1).Count);
        }

        [Test]
        public void 등급_가중치가_전부_0이면_비운다()
        {
            var catalog = new List<WeaponData> { Make("keen_blade", "swordsman") };
            var zero = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };

            Assert.AreEqual(0, WeaponShop.Build(catalog, "swordsman", zero, Prices, 2).Count);
        }
    }
}
