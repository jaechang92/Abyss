namespace Abyss.Runtime.Player
{
    /// <summary>
    /// PlayerStateMachine의 상태 ID 상수. 9상태 (Analyst 확정, stage-c-architecture §2-2) + Guard(16-shield-guard).
    /// </summary>
    public static class PlayerStateIds
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

        /// <summary>방패병 가드(누르고 있는 동안). 2026-09-17 추가 — 10번째 상태.</summary>
        public const string Guard = "Guard";
    }
}
