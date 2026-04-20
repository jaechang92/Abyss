using UnityEngine;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// 런 전역 설정 SO. RunManager·FormController·DraftSessionController가 참조.
    /// 기본값은 Analyst 확정 스펙(stage-d-analyst.md §2) 기준.
    /// </summary>
    [CreateAssetMenu(fileName = "RunConfig", menuName = "Abyss/Data/Run Config")]
    public sealed class RunConfig : ScriptableObject
    {
        [Header("플레이어")]
        [Min(1)] public int baseHp = 100;

        [Header("폼체인지")]
        [Min(0f)] public float formSwapCooldown = 1.5f;
        [Min(0f)] public float formSwapAnimationDuration = 0.3f;
        [Min(0f)] public float formSwapHitstopMs = 120f;

        [Header("드래프트 (MF-3/MF-4)")]
        [Min(1)] public int draftOptionCount = 3;
        public int[] rerollCostLadder = { 15, 30 };
        [Min(0)] public int skipReward = 10;
        [Min(1)] public int activeSlotLimit = 2;

        [Header("희귀도 분포 (합 100%)")]
        [Min(0f)] public float commonWeight = 60f;
        [Min(0f)] public float rareWeight = 28f;
        [Min(0f)] public float epicWeight = 10f;
        [Min(0f)] public float legendaryWeight = 2f;

        [Header("경험치 곡선")]
        [Min(1)] public int baseExpToLevel = 100;
        [Min(1f)] public float expGrowthPerLevel = 1.2f;

        [Header("밸런스 가드레일")]
        [Tooltip("단일 런 내 전투 시간 비율 기준 단일 폼 사용률 경보 임계값 (MF-5)")]
        [Range(0.5f, 1f)] public float formBiasThreshold = 0.6f;
    }
}
