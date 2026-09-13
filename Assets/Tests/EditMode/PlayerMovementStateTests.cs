using Abyss.Runtime.Player;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 움직임으로 정해지는 상태(대시·상승·하강·달리기·서기) 가드.
    ///
    /// 🔴 <b>대시가 끝나도 Dash 에서 못 나갔다</b>(2026-09-13). 「현재가 Dash 면 그대로 둔다」는 한 줄이
    /// FSM 을 만들 때부터 있었고, 폴백이 Run 그림을 보여 줘 Dash 클립을 붙이기 전까지 안 드러났다.
    /// Hit 이 한 틱만 유지되던 결손(Bug-040)의 반대 모양이다 — 둘 다 「상태가 얼마나 오래 현재인가」 문제다.
    /// </summary>
    public sealed class PlayerMovementStateTests
    {
        [Test]
        public void 대시_중이면_어디서든_Dash다()
        {
            Assert.AreEqual(PlayerStateIds.Dash, PlayerStateMachine.ResolveMovementState(true, true, 0f, 0f));
            Assert.AreEqual(PlayerStateIds.Dash, PlayerStateMachine.ResolveMovementState(true, false, 5f, 10f));
        }

        [Test]
        public void 대시가_아니면_절대_Dash가_아니다()
        {
            // 🔑 이번 결손의 모양 그대로다 — 대시가 끝난 뒤의 모든 경우를 훑는다.
            foreach (bool isGrounded in new[] { true, false })
            foreach (float vy in new[] { -5f, 0f, 5f })
            foreach (float vx in new[] { -8f, 0f, 8f })
            {
                Assert.AreNotEqual(PlayerStateIds.Dash,
                    PlayerStateMachine.ResolveMovementState(false, isGrounded, vy, vx),
                    $"grounded={isGrounded} vy={vy} vx={vx}");
            }
        }

        [Test]
        public void 공중에서는_올라가면_Jump_아니면_Fall()
        {
            Assert.AreEqual(PlayerStateIds.Jump, PlayerStateMachine.ResolveMovementState(false, false, 3f, 0f));
            Assert.AreEqual(PlayerStateIds.Fall, PlayerStateMachine.ResolveMovementState(false, false, -3f, 0f));
            // 정점(속도 0)은 Fall 이다 — 원래 코드의 `> 0.01f` 경계를 그대로 지킨다.
            Assert.AreEqual(PlayerStateIds.Fall, PlayerStateMachine.ResolveMovementState(false, false, 0f, 0f));
        }

        [Test]
        public void 지상에서는_움직이면_Run_아니면_Idle()
        {
            Assert.AreEqual(PlayerStateIds.Run, PlayerStateMachine.ResolveMovementState(false, true, 0f, 4f));
            Assert.AreEqual(PlayerStateIds.Run, PlayerStateMachine.ResolveMovementState(false, true, 0f, -4f));
            Assert.AreEqual(PlayerStateIds.Idle, PlayerStateMachine.ResolveMovementState(false, true, 0f, 0.05f));
        }
    }
}
