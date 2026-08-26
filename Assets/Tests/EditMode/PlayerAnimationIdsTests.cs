using Abyss.Runtime.Player;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// FSM 상태 → 애니메이션 폴백 사슬 가드.
    ///
    /// 🔑 폴백이 존재하는 이유는 <b>리깅 초기에 클립이 셋뿐</b>이라서다.
    /// 사슬이 끊기면 없는 상태를 재생하려다 그림이 직전 클립에 얼어붙는데,
    /// 그것도 오류가 아니라 경고라 조용히 지나간다.
    /// </summary>
    public sealed class PlayerAnimationIdsTests
    {
        [Test]
        public void 모든_FSM_상태에_사슬이_있다()
        {
            foreach (string stateId in AllPlayerStates())
            {
                var chain = PlayerAnimationIds.FallbackChain(stateId);
                Assert.IsNotNull(chain, stateId);
                Assert.Greater(chain.Count, 0, stateId);
            }
        }

        [Test]
        public void 모든_사슬은_Idle로_끝난다()
        {
            // 리그가 있는 폼이면 idle은 반드시 있다. 끝이 idle이 아니면 갈 곳이 없는 상태가 생긴다.
            foreach (string stateId in AllPlayerStates())
            {
                var chain = PlayerAnimationIds.FallbackChain(stateId);
                Assert.AreEqual(PlayerAnimationIds.Idle, chain[chain.Count - 1], stateId);
            }
        }

        [Test]
        public void 사슬의_첫_후보는_자기_이름이다()
        {
            // 클립이 다 갖춰지면 폴백이 한 번도 안 타야 한다.
            foreach (string stateId in AllPlayerStates())
            {
                Assert.AreEqual(stateId, PlayerAnimationIds.FallbackChain(stateId)[0], stateId);
            }
        }

        [Test]
        public void 사슬_안에서_같은_이름이_반복되지_않는다()
        {
            foreach (string stateId in AllPlayerStates())
            {
                var chain = PlayerAnimationIds.FallbackChain(stateId);
                for (int i = 0; i < chain.Count; i++)
                {
                    for (int j = i + 1; j < chain.Count; j++)
                    {
                        Assert.AreNotEqual(chain[i], chain[j], $"{stateId} 사슬에 {chain[i]}가 두 번 있다");
                    }
                }
            }
        }

        [Test]
        public void 모르는_상태는_Idle로_떨어진다()
        {
            var chain = PlayerAnimationIds.FallbackChain("존재하지_않는_상태");
            Assert.AreEqual(1, chain.Count);
            Assert.AreEqual(PlayerAnimationIds.Idle, chain[0]);
        }

        [Test]
        public void 클립이_Idle_Run_AttackLight_뿐일_때_모든_상태가_재생된다()
        {
            // 🔴 이것이 폴백의 실제 첫 사용처다 — 리깅 1차 목표가 정확히 이 셋이다.
            var available = new[]
            {
                PlayerAnimationIds.Idle, PlayerAnimationIds.Run, PlayerAnimationIds.AttackLight
            };

            foreach (string stateId in AllPlayerStates())
            {
                Assert.IsNotNull(FirstAvailable(stateId, available),
                    $"{stateId}가 재생할 클립을 못 찾는다");
            }
        }

        [Test]
        public void 대시는_Idle보다_Run을_먼저_고른다()
        {
            var available = new[] { PlayerAnimationIds.Idle, PlayerAnimationIds.Run };
            Assert.AreEqual(PlayerAnimationIds.Run, FirstAvailable(PlayerStateIds.Dash, available));
        }

        [Test]
        public void 사망은_Idle보다_Hit를_먼저_고른다()
        {
            // 죽는 그림이 없을 때 idle로 서 있으면 "죽었는데 멀쩡하다"는 버그로 읽힌다.
            var available = new[] { PlayerAnimationIds.Idle, PlayerAnimationIds.Hit };
            Assert.AreEqual(PlayerAnimationIds.Hit, FirstAvailable(PlayerStateIds.Dead, available));
        }

        [Test]
        public void 한_번_재생_상태만_재진입에_재시작한다()
        {
            Assert.IsTrue(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.AttackLight));
            Assert.IsTrue(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.AttackHeavy));
            Assert.IsTrue(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.Hit));
            Assert.IsTrue(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.Dead));

            Assert.IsFalse(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.Idle));
            Assert.IsFalse(PlayerAnimationIds.IsOneShot(PlayerAnimationIds.Run));

            // 2연타가 성립하려면 공격이 재진입에 다시 시작해야 한다.
            Assert.IsTrue(PlayerAnimationIds.RestartsOnReenter(PlayerAnimationIds.AttackLight));
            Assert.IsFalse(PlayerAnimationIds.RestartsOnReenter(PlayerAnimationIds.Run));
        }

        private static string FirstAvailable(string playerStateId, string[] available)
        {
            var chain = PlayerAnimationIds.FallbackChain(playerStateId);
            for (int i = 0; i < chain.Count; i++)
            {
                for (int j = 0; j < available.Length; j++)
                {
                    if (chain[i] == available[j]) return chain[i];
                }
            }
            return null;
        }

        private static string[] AllPlayerStates() => new[]
        {
            PlayerStateIds.Idle, PlayerStateIds.Run, PlayerStateIds.Jump, PlayerStateIds.Fall,
            PlayerStateIds.Dash, PlayerStateIds.AttackLight, PlayerStateIds.AttackHeavy,
            PlayerStateIds.Hit, PlayerStateIds.Dead,
        };
    }
}
