using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>환경형 종별 시간/거리. 전투 공통 수치는 EnemyData에서 읽는다.</summary>
    [CreateAssetMenu(menuName = "Abyss/Enemy/Environmental Profile")]
    public sealed class EnvironmentalEnemyProfile : ScriptableObject
    {
        [Header("Scaled seconds")]
        [SerializeField, Min(0.1f)] private float ceilingWait = 3f;
        [SerializeField, Min(0.1f)] private float buriedWait = 2.5f;
        [SerializeField, Min(0.1f)] private float stuckTimeout = 3f;
        [SerializeField, Min(0.1f)] private float recovery = 1.1f;
        [SerializeField, Min(0.1f)] private float windup = 0.6f;
        [SerializeField, Min(0.01f)] private float dropStrikeDuration = 1.2f;
        [SerializeField, Min(0.01f)] private float dashStrikeDuration = 0.3f;
        [Header("World units / units per second")]
        [SerializeField, Min(0.1f)] private float attachmentProbeDistance = 8f;
        [SerializeField, Min(0.1f)] private float maxDropDistance = 6f;
        [SerializeField, Min(0.1f)] private float dropSpeed = 8f;
        [SerializeField, Min(0.1f)] private float hoverHeight = 1f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 5f;
        [SerializeField, Min(0.01f)] private float movementProgressDistance = 0.1f;
        [Header("Conservative connected-floor resolver")]
        [SerializeField, Min(0.1f)] private float searchRadius = 8f;
        [SerializeField, Min(0.1f)] private float candidateSpacing = 0.5f;
        [SerializeField, Range(1, 64)] private int maxCandidates = 33;
        [SerializeField, Range(0.05f, 0.25f)] private float connectionSpacing = 0.2f;
        [SerializeField, Range(1, 512)] private int maxConnectionSamples = 256;
        [SerializeField, Range(0.01f, 0.2f)] private float maxFloorStep = 0.1f;
        [SerializeField, Range(0.005f, 0.05f)] private float floorSkin = 0.02f;
        [SerializeField, Range(0.8f, 1f)] private float minimumGroundNormalY = 0.95f;

        public float CeilingWait => Safe(ceilingWait, 3f, 0.1f, 3f);
        public float BuriedWait => Safe(buriedWait, 2.5f, 0.1f, 2.5f);
        public float StuckTimeout => Safe(stuckTimeout, 3f, 0.1f, 3f);
        public float Recovery => Safe(recovery, 1.1f, 0.1f, 3f);
        public float Windup => Safe(windup, 0.6f, 0.1f, 3f);
        public float DropStrikeDuration => Safe(dropStrikeDuration, 1.2f, 0.01f, 1.2f);
        public float DashStrikeDuration => Safe(dashStrikeDuration, 0.3f, 0.01f, 0.3f);
        public float AttachmentProbeDistance => Safe(attachmentProbeDistance, 8f, 0.1f, 50f);
        public float MaxDropDistance => Safe(maxDropDistance, 6f, 0.1f, 50f);
        public float DropSpeed => Safe(dropSpeed, 8f, 0.1f, 8f);
        public float HoverHeight => Safe(hoverHeight, 1f, 0.1f, 10f);
        public float DashSpeed => Safe(dashSpeed, 5f, 0.1f, 5f);
        public float MovementProgressDistance => Safe(movementProgressDistance, 0.1f, 0.01f, 1f);
        public float SearchRadius => Safe(searchRadius, 8f, 0.1f, 50f);
        public float CandidateSpacing => Safe(candidateSpacing, 0.5f, 0.1f, 2f);
        public int MaxCandidates => Mathf.Clamp(maxCandidates, 1, 64);
        public float ConnectionSpacing => Safe(connectionSpacing, 0.2f, 0.05f, 0.25f);
        public int MaxConnectionSamples => Mathf.Clamp(maxConnectionSamples, 1, 512);
        public float MaxFloorStep => Safe(maxFloorStep, 0.1f, 0.01f, 0.2f);
        public float FloorSkin => Safe(floorSkin, 0.02f, 0.005f, 0.05f);
        public float MinimumGroundNormalY => Safe(minimumGroundNormalY, 0.95f, 0.8f, 1f);

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Safe(float value, float fallback, float min, float max)
        {
            return IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
        }
    }
}
