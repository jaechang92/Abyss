namespace Abyss.Runtime.Player
{
    /// <summary>
    /// PlayerStateMachine의 상태 ID 상수. 9상태 (Analyst 확정, stage-c-architecture §2-2).
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
    }
}
