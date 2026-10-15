namespace Abyss.Runtime.Combat
{
    /// <summary>Visual result of the existing hit roll; never rolls damage itself.</summary>
    public interface IProjectileCriticalFeedback
    {
        bool LastHitWasCritical { get; }
    }
}
