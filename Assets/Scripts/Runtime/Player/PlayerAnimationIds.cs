using System.Collections.Generic;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 애니메이터 상태 이름 SoT + <see cref="PlayerStateIds"/>에서 애니메이션으로 가는 <b>폴백 사슬</b>.
    ///
    /// 🔑 <b>이름이 같은데 왜 따로 두는가</b> — 지금은 9:9로 겹치지만 <b>둘은 다른 계약</b>이다.
    /// FSM 상태는 게임 로직이 정하고, 애니메이션 상태는 그 폼에 <b>그려진 클립이 있느냐</b>가 정한다.
    /// FSM 쪽 상수를 그대로 <c>Animator.Play</c>에 넘기면 없는 상태를 재생하려다
    /// <b>경고만 나고 화면은 직전 클립에 얼어붙는다</b> — 오류로 안 잡히는 종류의 결손이다.
    ///
    /// 그래서 상태마다 <b>대체 순서</b>를 명시한다. 없는 것은 있는 것으로 내려간다.
    /// 사슬의 끝은 언제나 <see cref="Idle"/>이다 — 그림이 있는 폼이면 idle은 반드시 있다.
    ///
    /// 🔴 <b>프레임 애니메이션에서는 이 사슬이 오래 쓰인다.</b>
    /// 상태 9종 × 폼 4종 = <b>클립 36개</b>를 한꺼번에 그릴 수는 없다.
    /// 상태 하나를 6~8프레임으로 그리는 동안 나머지 여덟 상태는 갈 곳이 없고,
    /// 그 기간이 <b>클립을 다 채울 때까지 이어진다.</b>
    ///
    /// 📌 <b>폴백은 임시방편이 아니라 규약이다.</b> 클립이 다 갖춰진 뒤에도 남는다 —
    /// 다섯 번째 폼이 들어올 때 그 폼만 클립이 모자란 상황이 정확히 다시 온다.
    /// </summary>
    public static class PlayerAnimationIds
    {
        public const string Idle = "Idle";
        public const string Run = "Run";
        public const string Jump = "Jump";
        public const string Fall = "Fall";
        public const string Dash = "Dash";
        public const string AttackLight = "AttackLight";
        public const string AttackHeavy = "AttackHeavy";
        public const string Hit = "Hit";
        public const string Dead = "Dead";

        private static readonly string[] idleChain = { Idle };
        private static readonly string[] runChain = { Run, Idle };

        // 공중 두 상태는 서로를 먼저 본다 — 한 장만 그렸으면 상승·하강에 같이 쓰는 게 자연스럽다.
        private static readonly string[] jumpChain = { Jump, Fall, Idle };
        private static readonly string[] fallChain = { Fall, Jump, Idle };

        // 대시는 달리기 쪽이 idle보다 훨씬 덜 어색하다.
        private static readonly string[] dashChain = { Dash, Run, Idle };

        private static readonly string[] attackLightChain = { AttackLight, AttackHeavy, Idle };
        private static readonly string[] attackHeavyChain = { AttackHeavy, AttackLight, Idle };

        private static readonly string[] hitChain = { Hit, Idle };

        // 🔴 사망이 Hit를 거치는 것은 의도다. 죽는 그림이 없을 때 idle로 서 있는 것보다
        //    피격 자세로 굳는 편이 낫다 — "죽었는데 멀쩡히 서 있다"는 버그로 읽힌다.
        private static readonly string[] deadChain = { Dead, Hit, Idle };

        private static readonly string[] emptyChain = { Idle };

        /// <summary>
        /// FSM 상태 하나에 대해 <b>먼저 시도할 순서대로</b> 애니메이션 상태 이름을 돌려준다.
        /// 호출자는 앞에서부터 재생 가능한 첫 번째를 고른다.
        /// </summary>
        public static IReadOnlyList<string> FallbackChain(string playerStateId)
        {
            return playerStateId switch
            {
                PlayerStateIds.Idle => idleChain,
                PlayerStateIds.Run => runChain,
                PlayerStateIds.Jump => jumpChain,
                PlayerStateIds.Fall => fallChain,
                PlayerStateIds.Dash => dashChain,
                PlayerStateIds.AttackLight => attackLightChain,
                PlayerStateIds.AttackHeavy => attackHeavyChain,
                PlayerStateIds.Hit => hitChain,
                PlayerStateIds.Dead => deadChain,
                _ => emptyChain,
            };
        }

        /// <summary>
        /// 그 상태가 <b>한 번 재생하고 멈춰야 하는가</b>(루프가 아닌가).
        /// 클립 생성기와 재생기가 같은 답을 써야 해서 여기 둔다.
        /// </summary>
        public static bool IsOneShot(string animationStateId)
        {
            return animationStateId is AttackLight or AttackHeavy or Hit or Dead;
        }

        /// <summary>
        /// 같은 상태로 다시 들어왔을 때 <b>처음부터 다시 재생해야 하는가</b>.
        ///
        /// 연속 공격이 이것 때문에 존재한다 — <c>PlayerStateMachine.TriggerAttackLight</c>는
        /// 이미 AttackLight일 때도 <c>ForceTransitionTo</c>를 부르지만, 상태 이름은 안 바뀐다.
        /// 이름만 보고 재생을 결정하면 <b>두 번째 공격에 그림이 안 움직인다.</b>
        /// </summary>
        public static bool RestartsOnReenter(string animationStateId) => IsOneShot(animationStateId);
    }
}
