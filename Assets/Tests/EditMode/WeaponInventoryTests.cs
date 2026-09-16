using Abyss.Runtime.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 무기 보유 상태(<see cref="WeaponInventory"/>). <b>획득 3창구가 전부 여기에 쓰므로
    /// 여기가 틀리면 세 창구가 같이 틀린다</b>(14-weapon-equipment-system.md §3-B).
    ///
    /// 🔴 고정하는 성질은 전부 <b>오류가 안 나는</b> 종류다 — 보상이 조용히 증발하거나,
    /// 강화 단계가 조용히 초기화되거나, 폼을 바꾼 뒤 무기만 안 따라온다.
    /// </summary>
    public sealed class WeaponInventoryTests
    {
        private static WeaponData MakeWeapon(string weaponId, string formBound, float baseMult = 1f,
                                             float step = 0.1f)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponId = weaponId;
            weapon.formBound = formBound;
            weapon.attackMultiplier = baseMult;
            weapon.upgradeMultiplierStep = step;
            return weapon;
        }

        [Test]
        public void 처음_획득하면_그_폼이_그_무기를_든다()
        {
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");
            var basic = MakeWeapon("rusted_blade", "swordsman");

            Assert.IsTrue(inventory.Grant(sword), "첫 획득은 true(신규)여야 한다");
            Assert.AreSame(sword, inventory.ResolveFor("swordsman", basic));
        }

        [Test]
        public void 안_가진_폼은_기본_무기로_물러난다()
        {
            // 🔑 런 시작 시점에는 전부 이 경로다 — 폴백이 끊기면 맨손이 된다.
            var inventory = new WeaponInventory();
            var basic = MakeWeapon("rusted_blade", "swordsman");

            Assert.AreSame(basic, inventory.ResolveFor("swordsman", basic));
            Assert.AreSame(basic, inventory.ResolveFor(null, basic));
        }

        [Test]
        public void 중복_획득은_강화_단계를_올린다()
        {
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");

            inventory.Grant(sword);
            Assert.AreEqual(0, inventory.UpgradeLevelOf(sword), "첫 획득은 0단계다");

            Assert.IsFalse(inventory.Grant(sword), "중복 획득은 false(강화)여야 한다");
            inventory.Grant(sword);
            Assert.AreEqual(2, inventory.UpgradeLevelOf(sword));
        }

        [Test]
        public void 강화_단계는_배율_식_하나로만_환산된다()
        {
            // ⚠️ 식은 WeaponData.MultiplierAt 하나뿐이다. 인벤토리는 단계만 센다.
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman", baseMult: 1.2f, step: 0.1f);

            inventory.Grant(sword);
            inventory.Grant(sword);

            Assert.AreEqual(1.3f, sword.MultiplierAt(inventory.UpgradeLevelOf(sword)), 0.0001f);
        }

        [Test]
        public void 폼마다_따로_기억한다()
        {
            // 🔑 「폼마다 기억」이 확정 정책이다(2026-09-16). 검사로 돌아오면 아까 주운 검이 그대로다.
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");
            var bow = MakeWeapon("long_bow", "archer");
            var swordBasic = MakeWeapon("rusted_blade", "swordsman");
            var bowBasic = MakeWeapon("worn_bow", "archer");

            inventory.Grant(sword);
            inventory.Grant(bow);

            Assert.AreSame(sword, inventory.ResolveFor("swordsman", swordBasic));
            Assert.AreSame(bow, inventory.ResolveFor("archer", bowBasic));
        }

        [Test]
        public void 갈아탔다_돌아와도_강화_단계가_남는다()
        {
            // 🔴 단계를 폼에 붙였다면 여기서 증발한다 — 그래서 표를 둘로 나눴다.
            var inventory = new WeaponInventory();
            var first = MakeWeapon("keen_blade", "swordsman");
            var second = MakeWeapon("great_blade", "swordsman");

            inventory.Grant(first);
            inventory.Grant(first);          // first = 1단계
            inventory.Grant(second);         // 장착이 second 로 넘어간다
            inventory.Grant(first);          // 다시 first — 단계가 이어져야 한다

            Assert.AreEqual(2, inventory.UpgradeLevelOf(first));
            Assert.AreEqual(0, inventory.UpgradeLevelOf(second));
            Assert.AreSame(first, inventory.ResolveFor("swordsman", null));
        }

        [Test]
        public void formBound_가_비면_안_꽂힌다()
        {
            // 🔴 빈 문자열을 키로 넣으면 formId 없는 폼이 그걸 집어 든다. 보유만 남긴다.
            var inventory = new WeaponInventory();
            var orphan = MakeWeapon("nameless", formBound: "");

            Assert.IsTrue(inventory.Grant(orphan));
            Assert.IsTrue(inventory.Owns(orphan));
            Assert.IsNull(inventory.ResolveFor("", null));
        }

        [Test]
        public void 빈_무기는_보유로_안_잡힌다()
        {
            var inventory = new WeaponInventory();
            var noId = MakeWeapon("", "swordsman");

            Assert.IsFalse(inventory.Grant(null));
            Assert.IsFalse(inventory.Grant(noId));
            Assert.IsFalse(inventory.Owns(noId));
            Assert.AreEqual(0, inventory.UpgradeLevelOf(null));
        }

        [Test]
        public void 런이_새로_시작되면_비워진다()
        {
            // 무기는 런 스코프다 — 유물과 달리 런을 넘어가지 않는다.
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");
            var basic = MakeWeapon("rusted_blade", "swordsman");

            inventory.Grant(sword);
            inventory.Clear();

            Assert.IsFalse(inventory.Owns(sword));
            Assert.AreEqual(0, inventory.UpgradeLevelOf(sword));
            Assert.AreSame(basic, inventory.ResolveFor("swordsman", basic));
        }

        [Test]
        public void 시작_무기를_들이면_같은_무기_획득이_강화가_된다()
        {
            // 🔴 들이지 않으면 첫 획득이 0단계 — 손에 든 무기·배율이 그대로인 빈손 보상이다.
            var inventory = new WeaponInventory();
            var basic = MakeWeapon("rusted_blade", "swordsman");

            Assert.IsTrue(inventory.Adopt(basic));
            Assert.IsTrue(inventory.Owns(basic), "시작 무기는 보유다 — 상점·제단이 「강화」로 불러야 한다");
            Assert.AreEqual(0, inventory.UpgradeLevelOf(basic));

            Assert.IsFalse(inventory.Grant(basic), "들인 뒤의 획득은 강화(false)다");
            Assert.AreEqual(1, inventory.UpgradeLevelOf(basic));
        }

        [Test]
        public void 들이기는_쌓인_단계와_장착을_건드리지_않는다()
        {
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");
            var basic = MakeWeapon("rusted_blade", "swordsman");

            inventory.Grant(basic);
            inventory.Grant(basic); // 1단계
            inventory.Grant(sword); // 손은 keen_blade

            int version = inventory.Version;
            Assert.IsFalse(inventory.Adopt(basic), "이미 가진 무기는 다시 안 들인다");
            Assert.AreEqual(1, inventory.UpgradeLevelOf(basic), "쌓아 둔 단계가 0으로 돌아가면 안 된다");
            Assert.AreSame(sword, inventory.ResolveFor("swordsman", basic), "손의 무기가 시작 무기로 돌아가면 안 된다");
            Assert.AreEqual(version, inventory.Version);
        }

        [Test]
        public void 들이기는_판_번호를_올리지_않는다()
        {
            // 🔑 뷰가 다시 그리며 들이기를 부른다 — 여기서 판이 오르면 매 프레임 다시 그린다.
            var inventory = new WeaponInventory();
            int start = inventory.Version;

            inventory.Adopt(MakeWeapon("rusted_blade", "swordsman"));
            inventory.Adopt(null);

            Assert.AreEqual(start, inventory.Version);
        }

        [Test]
        public void 판_번호는_바뀔_때만_오른다()
        {
            // 🔑 뷰가 이 숫자만 보고 다시 그린다. 안 바뀌었는데 오르면 매 프레임 다시 그리고,
            //    바뀌었는데 안 오르면 방금 얻은 무기가 손에 안 들린다.
            var inventory = new WeaponInventory();
            var sword = MakeWeapon("keen_blade", "swordsman");

            int start = inventory.Version;
            inventory.Grant(null);
            Assert.AreEqual(start, inventory.Version, "실패한 획득은 판을 올리지 않는다");

            inventory.Grant(sword);
            Assert.Greater(inventory.Version, start);

            int afterGrant = inventory.Version;
            inventory.Clear();
            Assert.Greater(inventory.Version, afterGrant);

            int afterClear = inventory.Version;
            inventory.Clear();
            Assert.AreEqual(afterClear, inventory.Version, "빈 상태를 또 비워도 판은 그대로다");
        }
    }
}
