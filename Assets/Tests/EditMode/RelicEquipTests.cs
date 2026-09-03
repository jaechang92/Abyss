using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 유물 <b>장착</b> 규칙. 보유와 장착을 나눈 설계가 여기서 지켜진다 —
    /// 보유는 뽑기의 결과라 되돌릴 수 없고, 장착은 언제든 바꾸는 빌드 선택이다.
    /// 효과 합산이 장착만 보므로(<see cref="MetaUpgrades"/>), 슬롯 규칙이 곧 밸런스 규칙이다.
    ///
    /// 보유는 세이브를 직접 채워 만든다 — <c>TryDrawRelic</c>은 Resources 카탈로그를 읽어
    /// 테스트가 콘텐츠에 묶이고, 게다가 확률이라 원하는 유물을 지목할 수 없다.
    /// </summary>
    public sealed class RelicEquipTests
    {
        private MetaSaveService service;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("[Test] MetaSaveService");
            service = go.AddComponent<MetaSaveService>();
            service.ResetAll(autoSave: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null) Object.DestroyImmediate(service.gameObject);
        }

        private void Grant(string relicId, int level = 1)
        {
            service.Current.relics.Add(new RelicEntry { relicId = relicId, level = level });
        }

        // ───────────────────────── 슬롯 목록 ─────────────────────────

        /// <summary>
        /// 유물을 모르던 세이브는 장착 목록이 비어 있다. 읽는 쪽에서 길이를 맞춰 두지 않으면
        /// 슬롯을 그리는 UI가 첫 프레임에 인덱스 예외를 낸다.
        /// </summary>
        [Test]
        public void 옛_세이브도_슬롯_길이가_맞춰진다()
        {
            service.Current.equippedRelicIds.Clear();

            Assert.AreEqual(MetaSave.EquippedRelicSlots, service.EquippedRelicIds.Count);
            foreach (var id in service.EquippedRelicIds) Assert.IsEmpty(id);
        }

        // ───────────────────────── 장착 ─────────────────────────

        [Test]
        public void 보유하지_않은_유물은_장착할_수_없다()
        {
            Assert.IsFalse(service.TryEquipRelic("relic_unknown", 0, autoSave: false));
            Assert.IsEmpty(service.EquippedRelicIds[0]);
        }

        [Test]
        public void 슬롯_범위_밖은_장착할_수_없다()
        {
            Grant("relic_a");

            Assert.IsFalse(service.TryEquipRelic("relic_a", -1, autoSave: false));
            Assert.IsFalse(service.TryEquipRelic("relic_a", MetaSave.EquippedRelicSlots, autoSave: false));
        }

        [Test]
        public void 보유한_유물은_빈_슬롯에_들어간다()
        {
            Grant("relic_a");

            Assert.IsTrue(service.TryEquipRelic("relic_a", 1, autoSave: false));
            Assert.AreEqual("relic_a", service.EquippedRelicIds[1]);
            Assert.IsTrue(service.IsRelicEquipped("relic_a"));
        }

        /// <summary>
        /// 🔴 같은 유물이 두 슬롯에 앉으면 효과가 두 번 더해진다 — 슬롯 3칸으로 유물 하나를
        /// 3배로 쓰는 길이 열린다. 그래서 새 슬롯에 넣을 때 <b>옛 자리를 비운다</b>(자리 이동).
        /// </summary>
        [Test]
        public void 같은_유물이_두_슬롯에_동시에_있을_수_없다()
        {
            Grant("relic_a");
            service.TryEquipRelic("relic_a", 0, autoSave: false);

            Assert.IsTrue(service.TryEquipRelic("relic_a", 2, autoSave: false));

            Assert.IsEmpty(service.EquippedRelicIds[0], "옛 슬롯이 비워져야 한다.");
            Assert.AreEqual("relic_a", service.EquippedRelicIds[2]);
        }

        [Test]
        public void 슬롯에_다른_유물을_넣으면_덮인다()
        {
            Grant("relic_a");
            Grant("relic_b");
            service.TryEquipRelic("relic_a", 0, autoSave: false);

            Assert.IsTrue(service.TryEquipRelic("relic_b", 0, autoSave: false));

            Assert.AreEqual("relic_b", service.EquippedRelicIds[0]);
            Assert.IsFalse(service.IsRelicEquipped("relic_a"), "덮인 유물은 장착 해제 상태다.");
        }

        /// <summary>장착을 빼도 보유는 남는다 — 뽑기의 결과가 장착 실수로 사라지면 안 된다.</summary>
        [Test]
        public void 장착을_빼도_보유는_남는다()
        {
            Grant("relic_a", level: 3);
            service.TryEquipRelic("relic_a", 0, autoSave: false);

            Assert.IsTrue(service.UnequipRelicSlot(0, autoSave: false));

            Assert.IsEmpty(service.EquippedRelicIds[0]);
            Assert.IsTrue(service.OwnsRelic("relic_a"));
            Assert.AreEqual(3, service.GetRelicLevel("relic_a"), "레벨도 그대로여야 한다.");
        }

        [Test]
        public void 빈_슬롯을_빼면_아무_일도_없다()
        {
            Assert.IsFalse(service.UnequipRelicSlot(0, autoSave: false));
        }

        // ───────────────────────── 보유 레벨 ─────────────────────────

        [Test]
        public void 미보유_유물의_레벨은_0이다()
        {
            Assert.AreEqual(0, service.GetRelicLevel("relic_none"));
            Assert.IsFalse(service.OwnsRelic("relic_none"));
        }
    }
}
