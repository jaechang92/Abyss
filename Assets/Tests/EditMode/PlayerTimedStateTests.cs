using Abyss.Runtime.Player;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 시간제 상태(공격 2종 · 피격) 게이트 가드.
    ///
    /// 🔴 <b>Hit 이 이 게이트에서 빠져 있었다</b>(2026-09-13). 들어간 다음 판정에서 곧바로 나가
    /// 클립의 첫 프레임만 보였는데 오류도 로그도 없었다. 클립이 생기기 전에는 드러날 수 없는 결손이라,
    /// 「한 번 재생하는 클립이면 지속시간 게이트가 있다」를 규칙으로 고정한다.
    /// </summary>
    public sealed class PlayerTimedStateTests
    {
        private const float ExitTime = 10f;

        [Test]
        public void 피격은_지속시간_동안_붙잡힌다()
        {
            Assert.IsTrue(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.Hit, ExitTime - 0.01f, ExitTime));
        }

        [Test]
        public void 지속시간이_끝나면_놓는다()
        {
            // 종료 시각과 같은 순간에는 이미 놓는다 — 공격이 원래 쓰던 `now < exitTime` 과 같은 경계다.
            Assert.IsFalse(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.Hit, ExitTime, ExitTime));
            Assert.IsFalse(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.AttackLight, ExitTime + 1f, ExitTime));
        }

        [Test]
        public void 공격_2종도_같은_게이트를_쓴다()
        {
            Assert.IsTrue(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.AttackLight, 0f, ExitTime));
            Assert.IsTrue(PlayerStateMachine.IsTimedStateHeld(PlayerStateIds.AttackHeavy, 0f, ExitTime));
        }

        [Test]
        public void 시간제가_아닌_상태는_시각이_남아도_안_붙잡는다()
        {
            // 공격 직후 남은 종료 시각이 Idle·Run 을 붙잡으면 입력이 먹통처럼 보인다.
            foreach (string stateId in new[]
            {
                PlayerStateIds.Idle, PlayerStateIds.Run, PlayerStateIds.Jump,
                PlayerStateIds.Fall, PlayerStateIds.Dash, PlayerStateIds.Dead,
            })
            {
                Assert.IsFalse(PlayerStateMachine.IsTimedStateHeld(stateId, 0f, ExitTime), stateId);
            }
        }

        [Test]
        public void 한_번_재생_클립은_사망을_빼고_전부_시간제_상태다()
        {
            // 🔑 이번 결손의 모양 그대로다 — 한 번 재생하는 클립인데 FSM 이 시간을 안 주면 첫 프레임만 보인다.
            //    사망은 예외다: 스스로 안 나가는 종착 상태라 게이트가 필요 없다.
            foreach (string stateId in new[]
            {
                PlayerStateIds.Idle, PlayerStateIds.Run, PlayerStateIds.Jump, PlayerStateIds.Fall,
                PlayerStateIds.Dash, PlayerStateIds.AttackLight, PlayerStateIds.AttackHeavy,
                PlayerStateIds.Hit, PlayerStateIds.Dead,
            })
            {
                if (!PlayerAnimationIds.IsOneShot(stateId) || stateId == PlayerStateIds.Dead) continue;
                Assert.IsTrue(PlayerStateMachine.IsTimedStateHeld(stateId, 0f, ExitTime),
                    $"{stateId} 는 한 번 재생 클립인데 지속시간 게이트가 없다");
            }
        }

        [Test]
        public void 피격_기본_지속시간은_0점3초다()
        {
            // Hit 클립 fps 가 이 값에서 역산된다(9프레임 → 30fps). 바꾸면 클립 메뉴를 다시 돌릴 것.
            Assert.AreEqual(0.3f, PlayerStateMachine.DefaultHitDuration, 0.0001f);
        }
    }
}
