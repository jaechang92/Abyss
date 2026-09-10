using System;
using System.Collections.Generic;
using Abyss.Runtime.Player;
using Anim.Core;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 폴백 사슬 해석기 가드.
    ///
    /// 🔑 <b>이 테스트가 성립하는 것 자체가 설계의 요점이다.</b> 해석기가 Unity 타입을 안 쓰기 때문에
    /// <c>Animator</c> 없이, 플레이 모드 없이 재생 판정을 전부 검증할 수 있다.
    /// 애니메이션 결손은 오류가 아니라 <b>화면으로만</b> 드러나는 종류라, 잡을 수 있는 것은 여기서 잡는다.
    /// </summary>
    public sealed class AnimationChainResolverTests
    {
        /// <summary>가진 클립 목록만 답하는 재생기. 실제로 재생하지는 않는다.</summary>
        private sealed class FakeDriver : IAnimationDriver
        {
            private readonly HashSet<string> clips;

            public FakeDriver(params string[] availableClips)
            {
                clips = new HashSet<string>(availableClips);
            }

            public string CurrentAnimationId { get; private set; } = string.Empty;

            public bool HasClip(string animationId) => clips.Contains(animationId);

            public void Play(string animationId, bool restart, bool immediate)
            {
                CurrentAnimationId = animationId;
            }

            // 이 테스트는 클립 구성을 바꾸지 않는다. 빈 접근자로 두어 미사용 경고를 피한다.
            public event Action OnClipsChanged { add { } remove { } }
        }

        [Test]
        public void 첫_후보가_있으면_그것을_고른다()
        {
            var driver = new FakeDriver(PlayerAnimationIds.Dash, PlayerAnimationIds.Run, PlayerAnimationIds.Idle);
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.Dash);

            Assert.AreEqual(PlayerAnimationIds.Dash, AnimationChainResolver.Resolve(chain, driver));
        }

        [Test]
        public void 첫_후보가_없으면_다음으로_내려간다()
        {
            // 대시 그림이 아직 없는 폼. 사슬이 의도한 대로 달리기로 물러나야 한다.
            var driver = new FakeDriver(PlayerAnimationIds.Run, PlayerAnimationIds.Idle);
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.Dash);

            Assert.AreEqual(PlayerAnimationIds.Run, AnimationChainResolver.Resolve(chain, driver));
        }

        [Test]
        public void 아무것도_없으면_null이다()
        {
            // 그림이 하나도 없는 대상이 실제로 있다(프로토 단계). 임의로 무언가 고르지 않는다.
            var driver = new FakeDriver();
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.Run);

            Assert.IsNull(AnimationChainResolver.Resolve(chain, driver));
        }

        [Test]
        public void 인자가_비어_있으면_null이다()
        {
            var driver = new FakeDriver(PlayerAnimationIds.Idle);

            Assert.IsNull(AnimationChainResolver.Resolve(null, driver));
            Assert.IsNull(AnimationChainResolver.Resolve(new[] { PlayerAnimationIds.Idle }, null));
        }

        [Test]
        public void 빈_이름은_건너뛴다()
        {
            var driver = new FakeDriver(PlayerAnimationIds.Idle);

            Assert.AreEqual(
                PlayerAnimationIds.Idle,
                AnimationChainResolver.Resolve(new[] { null, string.Empty, PlayerAnimationIds.Idle }, driver));
        }

        [Test]
        public void Idle_하나뿐인_폼도_모든_상태가_갈_곳이_있다()
        {
            // 🔴 이것이 폴백이 존재하는 이유다. 상태 9종 × 폼 4종 = 클립 36개를 한꺼번에 그릴 수 없어서,
            //    한 상태를 그리는 동안 나머지 여덟은 갈 곳이 없다. 그 기간이 클립을 다 채울 때까지 이어진다.
            var driver = new FakeDriver(PlayerAnimationIds.Idle);

            foreach (string stateId in AllPlayerStates())
            {
                var chain = PlayerAnimationIds.FallbackChain(stateId);
                Assert.AreEqual(PlayerAnimationIds.Idle, AnimationChainResolver.Resolve(chain, driver), stateId);
            }
        }

        [Test]
        public void 사망은_그림이_없으면_피격_자세로_굳는다()
        {
            // "죽었는데 멀쩡히 서 있다"는 버그로 읽힌다 — idle보다 피격 자세가 낫다는 의도된 순서.
            var driver = new FakeDriver(PlayerAnimationIds.Hit, PlayerAnimationIds.Idle);
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.Dead);

            Assert.AreEqual(PlayerAnimationIds.Hit, AnimationChainResolver.Resolve(chain, driver));
        }

        [Test]
        public void 공중_두_상태는_서로를_먼저_본다()
        {
            // 한 장만 그렸으면 상승·하강에 같이 쓰는 편이 idle로 떨어지는 것보다 자연스럽다.
            var onlyJump = new FakeDriver(PlayerAnimationIds.Jump, PlayerAnimationIds.Idle);
            var onlyFall = new FakeDriver(PlayerAnimationIds.Fall, PlayerAnimationIds.Idle);

            Assert.AreEqual(
                PlayerAnimationIds.Jump,
                AnimationChainResolver.Resolve(PlayerAnimationIds.FallbackChain(PlayerStateIds.Fall), onlyJump));
            Assert.AreEqual(
                PlayerAnimationIds.Fall,
                AnimationChainResolver.Resolve(PlayerAnimationIds.FallbackChain(PlayerStateIds.Jump), onlyFall));
        }

        [Test]
        public void 고른_결과의_재생_방식이_상태와_맞는다()
        {
            // 폴백으로 내려간 뒤에는 원래 상태가 아니라 <b>실제로 재생할 것</b>을 기준으로 판정해야 한다.
            // 공격이 idle로 물러났는데 one-shot으로 틀면 idle이 한 번 돌고 멈춘다.
            var driver = new FakeDriver(PlayerAnimationIds.Idle);
            var chain = PlayerAnimationIds.FallbackChain(PlayerStateIds.AttackLight);
            string resolved = AnimationChainResolver.Resolve(chain, driver);

            Assert.AreEqual(PlayerAnimationIds.Idle, resolved);
            Assert.IsFalse(PlayerAnimationIds.IsOneShot(resolved));
            Assert.IsFalse(PlayerAnimationIds.RestartsOnReenter(resolved));
        }

        private static IEnumerable<string> AllPlayerStates()
        {
            yield return PlayerStateIds.Idle;
            yield return PlayerStateIds.Run;
            yield return PlayerStateIds.Jump;
            yield return PlayerStateIds.Fall;
            yield return PlayerStateIds.Dash;
            yield return PlayerStateIds.AttackLight;
            yield return PlayerStateIds.AttackHeavy;
            yield return PlayerStateIds.Hit;
            yield return PlayerStateIds.Dead;
        }
    }
}
