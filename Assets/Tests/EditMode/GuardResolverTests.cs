using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 방패병 가드 판정(16-shield-guard.md) — 정면 판정 · 저스트 창 경계 · 재무장 · 피해 배율.
    ///
    /// 🔑 저스트 창 0.2초가 어긋나도 오류는 안 난다 — 「잘 안 된다」는 체감으로만 나타난다. 경계를 여기서 고정한다.
    /// </summary>
    public sealed class GuardResolverTests
    {
        // ----- 정면 판정 -----

        [Test]
        public void IsFromFront_SourceOnFacingSide_True()
        {
            Assert.IsTrue(GuardResolver.IsFromFront(0f, 2f, 1));
            Assert.IsTrue(GuardResolver.IsFromFront(0f, -2f, -1));
        }

        [Test]
        public void IsFromFront_SourceBehind_False()
        {
            Assert.IsFalse(GuardResolver.IsFromFront(0f, -2f, 1));
            Assert.IsFalse(GuardResolver.IsFromFront(0f, 2f, -1));
        }

        [Test]
        public void IsFromFront_OverheadWithinDeadZone_TreatedAsFront()
        {
            // 머리 위에서 터진 곡사탄 — 소수점 차이로 「뒤」가 되면 안 된다.
            Assert.IsTrue(GuardResolver.IsFromFront(0f, -0.05f, 1));
            Assert.IsTrue(GuardResolver.IsFromFront(0f, 0.05f, -1));
        }

        // ----- 저스트 창 · 재무장 -----

        // 🔴 경계 테스트는 2진수로 정확히 표현되는 값(0.25 · 0.5 · 10.25 …)만 쓴다.
        //    10.2f 는 10.1999998 이라 10.2f - 10f = 0.19999981 < 0.2f 가 되어 「경계에서 닫힌다」가 거짓으로 실패했다.
        //    판정 자체는 맞다 — 1e-7초 차이는 플레이에 안 보인다. 틀린 것은 경계값의 표현이었다.

        [Test]
        public void JustGuardOpen_InsideWindow_True_AtBoundary_False()
        {
            Assert.IsTrue(GuardResolver.IsJustGuardOpen(true, 10f, 10.125f, 0.25f));
            Assert.IsFalse(GuardResolver.IsJustGuardOpen(true, 10f, 10.25f, 0.25f));
        }

        [Test]
        public void JustGuardOpen_NotArmed_AlwaysFalse()
        {
            Assert.IsFalse(GuardResolver.IsJustGuardOpen(false, 10f, 10f, 0.2f));
        }

        [Test]
        public void JustGuardArmed_RepressTooSoon_False()
        {
            // 뗀 지 0.25초 만에 다시 누름 — 연타로 저스트 창을 계속 여는 것을 막는다. 딱 지연만큼 지나면 무장.
            Assert.IsFalse(GuardResolver.IsJustGuardArmed(10.25f, 10f, 0.5f));
            Assert.IsTrue(GuardResolver.IsJustGuardArmed(10.5f, 10f, 0.5f));
        }

        [Test]
        public void JustGuardArmed_NeverReleasedBefore_True()
        {
            Assert.IsTrue(GuardResolver.IsJustGuardArmed(0.05f, -999f, 0.3f));
        }

        // ----- 판정 -----

        [Test]
        public void Resolve_NotGuarding_None()
        {
            Assert.AreEqual(GuardOutcome.None, GuardResolver.Resolve(false, true, true));
        }

        [Test]
        public void Resolve_FromBehind_NoneEvenInJustWindow()
        {
            Assert.AreEqual(GuardOutcome.None, GuardResolver.Resolve(true, false, true));
        }

        [Test]
        public void Resolve_FrontInsideWindow_JustGuarded_OutsideWindow_Guarded()
        {
            Assert.AreEqual(GuardOutcome.JustGuarded, GuardResolver.Resolve(true, true, true));
            Assert.AreEqual(GuardOutcome.Guarded, GuardResolver.Resolve(true, true, false));
        }

        // ----- 피해 -----

        [Test]
        public void ApplyGuard_JustGuard_Zero()
        {
            Assert.AreEqual(0, GuardResolver.ApplyGuard(40, GuardOutcome.JustGuarded, 0.3f));
        }

        [Test]
        public void ApplyGuard_Guarded_ScaledWithMinimumOne()
        {
            Assert.AreEqual(12, GuardResolver.ApplyGuard(40, GuardOutcome.Guarded, 0.3f));
            // 0 이면 누르고만 있어도 무적이다.
            Assert.AreEqual(1, GuardResolver.ApplyGuard(2, GuardOutcome.Guarded, 0.3f));
            Assert.AreEqual(1, GuardResolver.ApplyGuard(40, GuardOutcome.Guarded, 0f));
        }

        [Test]
        public void ApplyGuard_None_Unchanged()
        {
            Assert.AreEqual(40, GuardResolver.ApplyGuard(40, GuardOutcome.None, 0.3f));
        }

        [Test]
        public void GuardSpec_DefaultIsDisabled()
        {
            // 필드가 없는 기존 폼 에셋 — 꺼져 있어야 검사 · 궁수 · 투척사의 X 가 강공격으로 남는다.
            Assert.IsFalse(default(FormGuardSpec).isEnabled);
        }

        [Test]
        public void GuardState_HasAnimationChainEndingInIdle()
        {
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.Guard);
            Assert.AreEqual(PlayerStateIds.Guard, chain[0]);
            Assert.AreEqual(PlayerAnimationIds.Idle, chain[chain.Count - 1]);
            Assert.IsFalse(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.Guard), "가드는 누르는 동안 이어지는 루프다");
        }

        [Test]
        public void GuardState_IsNotTimed()
        {
            // 시간으로 붙잡으면 X 를 떼도 가드 그림이 남는다 — 가드는 누름이 정한다.
            Assert.IsFalse(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.Guard, 0f, 1f));
        }
    }
}
