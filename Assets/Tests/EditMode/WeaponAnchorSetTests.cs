using Abyss.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 앵커 샘플링. <see cref="WeaponAnchorSet.FrameIndexOf"/>가 Unity 타입을 안 쓰는 순수 함수라
    /// <c>Animator</c> 없이 잰다 — <c>AnimationChainResolver</c>와 같은 태도다.
    ///
    /// 🔴 무기가 한 프레임 어긋나는 것은 <b>오류가 아니라 화면으로만</b> 드러난다.
    /// 잡을 수 있는 것은 여기서 잡는다.
    /// </summary>
    public sealed class WeaponAnchorSetTests
    {
        [Test]
        public void FrameIndexOf_진행도를_프레임에_고르게_나눈다()
        {
            Assert.AreEqual(0, WeaponAnchorSet.FrameIndexOf(0f, 4, isLooping: false));
            Assert.AreEqual(1, WeaponAnchorSet.FrameIndexOf(0.25f, 4, isLooping: false));
            Assert.AreEqual(2, WeaponAnchorSet.FrameIndexOf(0.5f, 4, isLooping: false));
            Assert.AreEqual(3, WeaponAnchorSet.FrameIndexOf(0.75f, 4, isLooping: false));
        }

        [Test]
        public void FrameIndexOf_한번재생은_끝에서_마지막_프레임에_머문다()
        {
            // 🔑 여기서 되감으면 공격이 끝나는 순간 무기가 시작 자세로 튄다.
            Assert.AreEqual(8, WeaponAnchorSet.FrameIndexOf(1f, 9, isLooping: false));
            Assert.AreEqual(8, WeaponAnchorSet.FrameIndexOf(1.5f, 9, isLooping: false));
        }

        [Test]
        public void FrameIndexOf_루프는_되감는다()
        {
            Assert.AreEqual(0, WeaponAnchorSet.FrameIndexOf(1f, 8, isLooping: true));
            Assert.AreEqual(4, WeaponAnchorSet.FrameIndexOf(2.5f, 8, isLooping: true));
        }

        [Test]
        public void FrameIndexOf_음수_진행도에도_범위를_안_벗어난다()
        {
            // Animator 가 음수 normalizedTime 을 줄 일은 없지만, 범위를 벗어나면
            // IndexOutOfRange 로 터지는 게 아니라 조용히 잘못된 프레임이 나온다.
            Assert.AreEqual(0, WeaponAnchorSet.FrameIndexOf(-0.3f, 5, isLooping: false));
            Assert.AreEqual(3, WeaponAnchorSet.FrameIndexOf(-0.3f, 5, isLooping: true));
        }

        [Test]
        public void FrameIndexOf_프레임이_하나뿐이면_항상_0()
        {
            Assert.AreEqual(0, WeaponAnchorSet.FrameIndexOf(0.9f, 1, isLooping: true));
            Assert.AreEqual(0, WeaponAnchorSet.FrameIndexOf(0.9f, 0, isLooping: false));
        }

        [Test]
        public void TrySample_없는_애니메이션이면_실패한다()
        {
            WeaponAnchorSet set = Build();

            // 🔑 실패를 false 로 돌려 호출자가 무기를 숨기게 한다.
            //    빈 앵커를 주면 무기가 원점(발밑)에 박힌다.
            Assert.IsFalse(set.TrySample("없는상태", 0.5f, out _));
        }

        [Test]
        public void TrySample_프레임이_비면_실패한다()
        {
            var set = ScriptableObject.CreateInstance<WeaponAnchorSet>();
            set.clips = new[]
            {
                new WeaponAnchorClip { animationId = PlayerAnimationIds.Idle, frames = new WeaponAnchorFrame[0] },
            };

            Assert.IsFalse(set.TrySample(PlayerAnimationIds.Idle, 0f, out _));
        }

        [Test]
        public void TrySample_진행도에_맞는_프레임을_준다()
        {
            WeaponAnchorSet set = Build();

            Assert.IsTrue(set.TrySample(PlayerAnimationIds.AttackLight, 0f, out WeaponAnchorFrame first));
            Assert.AreEqual(new Vector2(0f, 0f), first.position);
            Assert.IsFalse(first.isKey);

            Assert.IsTrue(set.TrySample(PlayerAnimationIds.AttackLight, 0.6f, out WeaponAnchorFrame mid));
            Assert.AreEqual(new Vector2(1f, 1f), mid.position);
            Assert.IsTrue(mid.isKey);
        }

        [Test]
        public void TrySample_클립_오프셋이_모든_프레임에_더해진다()
        {
            // 🔑 검출은 앞쪽 손만 잡는데 이 캐릭터는 뒤쪽 손으로 쥔다. 그 차이를 클립 하나에
            //    한 값으로 메운다 — 프레임마다 다르게 주면 무기가 떤다.
            WeaponAnchorSet set = Build();
            set.clips[0].handOffset = new Vector2(-0.5f, -0.25f);

            Assert.IsTrue(set.TrySample(PlayerAnimationIds.AttackLight, 0f, out WeaponAnchorFrame first));
            Assert.AreEqual(new Vector2(-0.5f, -0.25f), first.position);

            Assert.IsTrue(set.TrySample(PlayerAnimationIds.AttackLight, 0.6f, out WeaponAnchorFrame mid));
            Assert.AreEqual(new Vector2(0.5f, 0.75f), mid.position);
        }

        [Test]
        public void TrySample_오프셋이_원본_프레임을_안_망친다()
        {
            // frames 는 배열이고 WeaponAnchorFrame 은 struct 다. 복사본에 더해야지
            // 원본에 누적되면 샘플할수록 무기가 밀려난다 — 한 번만 봐서는 안 드러난다.
            WeaponAnchorSet set = Build();
            set.clips[0].handOffset = new Vector2(1f, 0f);

            set.TrySample(PlayerAnimationIds.AttackLight, 0f, out _);
            set.TrySample(PlayerAnimationIds.AttackLight, 0f, out WeaponAnchorFrame second);

            Assert.AreEqual(new Vector2(1f, 0f), second.position);
        }

        private static WeaponAnchorSet Build()
        {
            var set = ScriptableObject.CreateInstance<WeaponAnchorSet>();
            set.formId = "dark_blade";
            set.clips = new[]
            {
                new WeaponAnchorClip
                {
                    animationId = PlayerAnimationIds.AttackLight,
                    isLooping = false,
                    frames = new[]
                    {
                        new WeaponAnchorFrame { position = Vector2.zero, isKey = false },
                        new WeaponAnchorFrame { position = Vector2.one, isKey = true },
                    },
                },
            };
            return set;
        }
    }
}
