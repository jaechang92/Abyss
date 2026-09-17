namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 FSM 의 <b>시간제 상태</b> 판정 — 공격(예비동작 → 타격 → 회복)과 경직.
    ///
    /// 🔴 <b>왜 생겼나</b>(2026-09-18 근접 병사 애니메이션). 적의 공격·경직은 <b>한 판정만</b> 그 상태였다 —
    /// 공격은 들어가는 순간 피해를 주고 다음 프레임에 추적으로 돌아갔다. 그림이 없을 때는 드러나지 않았지만
    /// 클립을 붙이면 첫 프레임만 보인다(플레이어 Bug-040 과 같은 모양).
    ///
    /// 📌 <c>Time.time</c> 을 읽지 않고 인자로 받는다 — EditMode 에서 경계를 고정하기 위해서다.
    /// 🔑 시간이 0 인 적(애니메이션 없는 11종)은 <b>옛 동작 그대로</b>다: 즉발 · 한 판정 경직.
    /// </summary>
    public static class EnemyTimedState
    {
        /// <summary>지속시간이 끝나기 전이라 현재 상태를 붙잡아야 하는가. 사망·재경직은 이것보다 먼저 판정된다.</summary>
        public static bool IsHeld(string currentStateId, float now, float exitTime)
        {
            bool isTimedState = currentStateId == EnemyStateIds.Attack
                || currentStateId == EnemyStateIds.Stagger;
            return isTimedState && now < exitTime;
        }

        /// <summary>아직 안 때렸고 타격 시각이 됐는가. 예비동작이 0 이면 들어가는 순간 참이다.</summary>
        public static bool IsStrikeDue(bool hasStruck, float now, float strikeTime)
        {
            return !hasStruck && now >= strikeTime;
        }
    }
}
