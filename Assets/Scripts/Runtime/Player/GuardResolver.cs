using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>가드 판정 결과.</summary>
    public enum GuardOutcome
    {
        /// <summary>가드가 안 됐다 — 가드 중이 아니거나 뒤에서 맞았다. 평소대로 맞는다.</summary>
        None = 0,

        /// <summary>일반 가드 — 피해를 줄이고 경직이 없다.</summary>
        Guarded = 1,

        /// <summary>저스트 가드 — 피해 0.</summary>
        JustGuarded = 2
    }

    /// <summary>
    /// 방패병 가드 판정. 기획: <c>Docs/game-design/16-shield-guard.md</c>.
    ///
    /// 📌 <b>Unity 시간을 읽지 않는 순수 함수</b>다(<c>PlayerStateMachine.IsTimedStateHeld</c> 와 같은 태도).
    /// 시각은 인자로 받아야 저스트 창의 경계를 EditMode 에서 고정할 수 있다 — 0.2초 창이 0.19초로
    /// 어긋나도 오류는 안 나고 「잘 안 된다」는 체감으로만 나타난다.
    /// </summary>
    public static class GuardResolver
    {
        /// <summary>
        /// 출처가 플레이어와 거의 같은 x 면 정면으로 친다(유닛). 머리 위에서 터진 곡사탄이
        /// 소수점 차이로 「뒤」가 되지 않게 한다.
        /// </summary>
        public const float FRONT_DEAD_ZONE = 0.1f;

        /// <summary>공격이 바라보는 쪽에서 왔는가.</summary>
        public static bool IsFromFront(float playerX, float sourceX, int facingSign)
        {
            float dx = sourceX - playerX;
            if (Mathf.Abs(dx) < FRONT_DEAD_ZONE) return true;
            return (dx > 0f ? 1 : -1) == (facingSign >= 0 ? 1 : -1);
        }

        /// <summary>
        /// 이번 누름이 저스트 창을 열 자격이 있는가. 🔴 뗀 뒤 <paramref name="rearmDelay"/> 가 지나야 한다 —
        /// 연타로 저스트 창을 계속 여는 것을 막는다.
        /// </summary>
        /// <param name="lastReleaseTime">마지막으로 뗀 시각. 한 번도 안 뗐으면 아주 작은 값.</param>
        public static bool IsJustGuardArmed(float pressTime, float lastReleaseTime, float rearmDelay)
        {
            return pressTime - lastReleaseTime >= rearmDelay;
        }

        /// <summary>저스트 창이 아직 열려 있는가. 창은 누른 순간부터 <paramref name="window"/> 초.</summary>
        public static bool IsJustGuardOpen(bool isArmed, float guardStartTime, float now, float window)
        {
            return isArmed && now - guardStartTime < window;
        }

        /// <summary>들어온 공격 하나를 판정한다.</summary>
        public static GuardOutcome Resolve(bool isGuarding, bool isFromFront, bool isJustGuardOpen)
        {
            if (!isGuarding || !isFromFront) return GuardOutcome.None;
            return isJustGuardOpen ? GuardOutcome.JustGuarded : GuardOutcome.Guarded;
        }

        /// <summary>
        /// 판정 결과대로 피해를 줄인다. 일반 가드는 <b>최소 1</b> — 0 이면 누르고만 있어도 무적이다
        /// (방어 버프의 최소 1 규약과 같다). 저스트 가드만 0 이다.
        /// </summary>
        public static int ApplyGuard(int amount, GuardOutcome outcome, float holdDamageScale)
        {
            if (amount <= 0) return 0;
            return outcome switch
            {
                GuardOutcome.JustGuarded => 0,
                GuardOutcome.Guarded => Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(0f, holdDamageScale))),
                _ => amount,
            };
        }
    }
}
