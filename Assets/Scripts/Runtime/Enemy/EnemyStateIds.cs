namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// EnemyBase FSM 5상태 ID (stage-c-architecture.md §2-2).
    /// </summary>
    public static class EnemyStateIds
    {
        public const string Patrol = "Patrol";
        public const string Chase = "Chase";
        public const string Attack = "Attack";
        public const string Stagger = "Stagger";
        public const string Dead = "Dead";
    }
}
