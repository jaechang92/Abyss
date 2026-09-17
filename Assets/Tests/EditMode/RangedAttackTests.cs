using Abyss.Runtime.Combat;
using Abyss.Runtime.Form;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 원거리 기본 공격(15-ranged-basic-attack.md) 순수 로직 테스트.
    /// ProjectileHitLedger(관통 수 · 중복 적중 · 첫 적중) · RangedAttackSpec(피해 식 · 사거리 · 유효성).
    ///
    /// 📌 심연 충전 「첫 적중 1회」는 청취자가 <c>isFirstHit</c> 로만 가른다 — 그 값의 정확성을 여기서 고정한다.
    /// </summary>
    public sealed class RangedAttackTests
    {
        private sealed class Target { }

        // ----- ProjectileHitLedger -----

        [Test]
        public void Ledger_NoPierce_HitsOnceThenExhausted()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(0);

            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
            Assert.IsTrue(ledger.IsExhausted);
            Assert.IsFalse(ledger.TryRegister(new Target(), out _));
        }

        [Test]
        public void Ledger_PierceTwo_HitsThreeDistinctTargets()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(2);

            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
            Assert.IsFalse(ledger.IsExhausted);
            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
            Assert.IsTrue(ledger.IsExhausted);
            Assert.IsFalse(ledger.TryRegister(new Target(), out _));
            Assert.AreEqual(3, ledger.HitCount);
        }

        [Test]
        public void Ledger_SameTargetTwice_SecondIsRejectedAndDoesNotSpendPierce()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(1);
            var enemy = new Target();

            Assert.IsTrue(ledger.TryRegister(enemy, out _));
            Assert.IsFalse(ledger.TryRegister(enemy, out _));
            Assert.IsFalse(ledger.IsExhausted, "같은 적 재진입이 관통 수를 깎으면 두 번째 적을 못 맞힌다");
            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
        }

        [Test]
        public void Ledger_FirstHitFlag_OnlyOnFirstRegisteredHit()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(2);
            var first = new Target();

            ledger.TryRegister(first, out bool isFirst1);
            ledger.TryRegister(first, out bool isFirstDuplicate);
            ledger.TryRegister(new Target(), out bool isFirst2);

            Assert.IsTrue(isFirst1);
            Assert.IsFalse(isFirstDuplicate);
            Assert.IsFalse(isFirst2);
        }

        [Test]
        public void Ledger_Reset_ClearsPreviousLaunch()
        {
            var ledger = new ProjectileHitLedger<Target>();
            var enemy = new Target();
            ledger.Reset(0);
            ledger.TryRegister(enemy, out _);

            // 풀에서 다시 나온 발사체 — 지난 발사에서 맞힌 적도 다시 맞혀야 한다.
            ledger.Reset(0);

            Assert.IsFalse(ledger.IsExhausted);
            Assert.IsTrue(ledger.TryRegister(enemy, out bool isFirst));
            Assert.IsTrue(isFirst);
        }

        [Test]
        public void Ledger_NullTarget_Rejected()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(0);

            Assert.IsFalse(ledger.TryRegister(null, out _));
            Assert.IsFalse(ledger.IsExhausted);
        }

        [Test]
        public void Ledger_NegativePierce_TreatedAsZero()
        {
            var ledger = new ProjectileHitLedger<Target>();
            ledger.Reset(-3);

            Assert.IsTrue(ledger.TryRegister(new Target(), out _));
            Assert.IsTrue(ledger.IsExhausted);
        }

        // ----- RangedAttackSpec -----

        [Test]
        public void ScaleDamage_DesignValues()
        {
            // 15-ranged-basic-attack §3-1 표: 약 15 → 9 · 강 35 → 21
            Assert.AreEqual(9, RangedAttackSpec.ScaleDamage(15, 0.6f));
            Assert.AreEqual(21, RangedAttackSpec.ScaleDamage(35, 0.6f));
        }

        [Test]
        public void ScaleDamage_NeverDropsToZero()
        {
            Assert.AreEqual(1, RangedAttackSpec.ScaleDamage(1, 0.1f));
            Assert.AreEqual(1, RangedAttackSpec.ScaleDamage(15, 0f));
        }

        [Test]
        public void Range_IsSpeedTimesLifetime()
        {
            var spec = new RangedAttackSpec { speed = 16f, lifetime = 0.45f };
            Assert.AreEqual(7.2f, spec.Range, 0.0001f);
        }

        [Test]
        public void IsValid_FalseWithoutPrefab()
        {
            // 빌더를 안 돌린 에셋 — 런타임이 근접으로 물러나는 조건이다.
            var spec = new RangedAttackSpec { speed = 16f, lifetime = 0.45f };
            Assert.IsFalse(spec.IsValid);
        }

        [Test]
        public void AttackStyle_DefaultIsMelee()
        {
            // 필드가 없는 기존 폼 에셋은 0 으로 읽힌다 — 그 값이 Melee 여야 검사 · 방패가 그대로다.
            Assert.AreEqual(FormAttackStyle.Melee, default(FormAttackStyle));
        }
    }
}
