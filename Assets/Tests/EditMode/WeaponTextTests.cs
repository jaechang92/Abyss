using Abyss.Runtime.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 무기를 부르는 말(<see cref="WeaponText"/>). 제단·상점·가차 셋이 여기서만 문자열을 얻는다.
    ///
    /// 🔴 <b>「이미 가진 것」을 알리는 말이 갈리면</b> 중복이 어디서는 강화로, 어디서는 꽝으로 읽힌다.
    /// 무기는 중복이 강화라(§7) 그 사실이 말로 보여야 살 이유·주울 이유가 남는다.
    /// </summary>
    public sealed class WeaponTextTests
    {
        private static WeaponData Make(string weaponId, string displayName = null, string description = null)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponId = weaponId;
            weapon.displayName = displayName;
            weapon.description = description;
            return weapon;
        }

        [Test]
        public void 이름이_비면_아이디로_물러난다()
        {
            // 빈 칸으로 두면 값을 못 읽은 것처럼 보인다.
            Assert.AreEqual("녹슨 검", WeaponText.NameOf(Make("rusted_blade", "녹슨 검")));
            Assert.AreEqual("rusted_blade", WeaponText.NameOf(Make("rusted_blade")));
            Assert.AreEqual("rusted_blade", WeaponText.NameOf(Make("rusted_blade", "")));
            Assert.AreEqual("무기", WeaponText.NameOf(null));
        }

        [Test]
        public void 보유_중이면_강화로_읽힌다()
        {
            Assert.AreEqual("획득", WeaponText.VerbFor(false));
            Assert.AreEqual("강화", WeaponText.VerbFor(true));
        }

        [Test]
        public void 제단_프롬프트는_이름과_무슨_일인지를_같이_말한다()
        {
            var sword = Make("rusted_blade", "녹슨 검");

            Assert.AreEqual("녹슨 검 획득 (G)", WeaponText.AltarPrompt(sword, owned: false));
            Assert.AreEqual("녹슨 검 강화 (G)", WeaponText.AltarPrompt(sword, owned: true));
        }

        [Test]
        public void 획득_줄은_신규와_강화를_구분한다()
        {
            // 🔑 가차는 결과 문구가 에셋에 미리 쓰여 있어 「무엇이 나왔는지」를 못 담는다.
            //    이 줄이 그 자리를 메운다 — 없으면 상자를 열고도 뭘 얻었는지 모른 채 넘어간다.
            var sword = Make("rusted_blade", "녹슨 검");

            StringAssert.Contains("녹슨 검", WeaponText.GainLine(sword, wasNew: true));
            StringAssert.Contains("녹슨 검", WeaponText.GainLine(sword, wasNew: false));
            Assert.AreNotEqual(WeaponText.GainLine(sword, true), WeaponText.GainLine(sword, false));
        }

        [Test]
        public void 상점_설명은_보유_중임을_밝힌다()
        {
            var sword = Make("rusted_blade", "녹슨 검", "무디지만 손에 익는다.");

            Assert.AreEqual("무디지만 손에 익는다.", WeaponText.ShopDescription(sword, owned: false));
            StringAssert.Contains("강화", WeaponText.ShopDescription(sword, owned: true));
            StringAssert.Contains("무디지만 손에 익는다.", WeaponText.ShopDescription(sword, owned: true));
        }

        [Test]
        public void 설명이_비면_최소_정보라도_준다()
        {
            var bare = Make("rusted_blade", "녹슨 검");

            Assert.IsNotEmpty(WeaponText.ShopDescription(bare, owned: false));
            Assert.IsNotEmpty(WeaponText.ShopDescription(null, owned: false));
        }
    }
}
