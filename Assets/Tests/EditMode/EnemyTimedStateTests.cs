using System.Linq;
using Abyss.Runtime.Enemy;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 적 시간제 상태(공격 · 경직) 게이트와 애니메이션 사슬.
    ///
    /// 🔴 <b>2026-09-18 근접 병사에 클립을 붙이며 발견</b> — 적의 공격·경직은 한 판정만 그 상태였다.
    /// 플레이어 Bug-040(Hit 첫 프레임만 보임)과 같은 모양이라 「한 번 재생 클립이면 시간제 상태다」를 같은 식으로 고정한다.
    /// 경계값은 2진수로 정확한 값만 쓴다(메모리 feedback_float_boundary_tests).
    /// </summary>
    public sealed class EnemyTimedStateTests
    {
        private const float ExitTime = 10f;

        [Test]
        public void 공격과_경직은_지속시간_동안_붙잡힌다()
        {
            Assert.IsTrue(EnemyTimedState.IsHeld(EnemyStateIds.Attack, ExitTime - 0.25f, ExitTime));
            Assert.IsTrue(EnemyTimedState.IsHeld(EnemyStateIds.Stagger, ExitTime - 0.25f, ExitTime));
        }

        [Test]
        public void 종료_시각에는_놓는다()
        {
            Assert.IsFalse(EnemyTimedState.IsHeld(EnemyStateIds.Attack, ExitTime, ExitTime));
            Assert.IsFalse(EnemyTimedState.IsHeld(EnemyStateIds.Stagger, ExitTime + 0.5f, ExitTime));
        }

        [Test]
        public void 시간제가_아닌_상태는_시각이_남아도_안_붙잡는다()
        {
            // 공격 직후 남은 종료 시각이 추적·순찰을 붙잡으면 적이 멈춰 선다.
            foreach (string stateId in new[] { EnemyStateIds.Patrol, EnemyStateIds.Chase, EnemyStateIds.Dead })
            {
                Assert.IsFalse(EnemyTimedState.IsHeld(stateId, 0f, ExitTime), stateId);
            }
        }

        [Test]
        public void 동작_시간이_0이면_옛_동작이다()
        {
            // 애니메이션 없는 11종: 종료 시각 = 들어간 시각 → 다음 판정에서 바로 놓는다.
            Assert.IsFalse(EnemyTimedState.IsHeld(EnemyStateIds.Attack, ExitTime, ExitTime));
            // 예비동작 0 → 들어가는 순간 타격.
            Assert.IsTrue(EnemyTimedState.IsStrikeDue(false, ExitTime, ExitTime));
        }

        [Test]
        public void 타격은_예비동작이_끝나야_한_번만_나간다()
        {
            const float strikeTime = 5.5f;
            Assert.IsFalse(EnemyTimedState.IsStrikeDue(false, strikeTime - 0.25f, strikeTime));
            Assert.IsTrue(EnemyTimedState.IsStrikeDue(false, strikeTime, strikeTime));
            Assert.IsFalse(EnemyTimedState.IsStrikeDue(true, strikeTime + 0.5f, strikeTime));
        }

        [Test]
        public void 한_번_재생_클립은_사망을_빼고_전부_시간제_상태에서_튼다()
        {
            // 🔑 결손의 모양 그대로 — 한 번 재생 클립인데 FSM 이 시간을 안 주면 첫 프레임만 보인다.
            foreach (string stateId in new[]
            {
                EnemyStateIds.Patrol, EnemyStateIds.Chase, EnemyStateIds.Attack, EnemyStateIds.Stagger,
            })
            {
                string first = EnemyAnimationIds.FallbackChain(stateId)[0];
                if (!EnemyAnimationIds.IsOneShot(first)) continue;
                Assert.IsTrue(EnemyTimedState.IsHeld(stateId, 0f, ExitTime), $"{stateId} → {first}");
            }
        }

        [Test]
        public void 사망_사슬은_움직임으로_곧장_가지_않는다()
        {
            // 죽었는데 기어 다니는 그림은 버그로 읽힌다 — 피격 자세가 먼저다.
            var chain = EnemyAnimationIds.FallbackChain(EnemyStateIds.Dead);
            Assert.AreEqual(EnemyAnimationIds.Dead, chain[0]);
            Assert.AreEqual(EnemyAnimationIds.Hit, chain[1]);
        }

        [Test]
        public void 모든_사슬의_이름은_컨트롤러_상태에_있다()
        {
            // 컨트롤러에 없는 이름은 어떤 오버라이드도 채울 수 없다 — HasClip 이 영원히 false.
            foreach (string stateId in new[]
            {
                EnemyStateIds.Patrol, EnemyStateIds.Chase, EnemyStateIds.Attack,
                EnemyStateIds.Stagger, EnemyStateIds.Dead, "Unknown",
            })
            {
                foreach (string animationId in EnemyAnimationIds.FallbackChain(stateId))
                {
                    Assert.IsTrue(EnemyAnimationIds.All.Contains(animationId), $"{stateId} → {animationId}");
                }
            }
        }
    }
}
