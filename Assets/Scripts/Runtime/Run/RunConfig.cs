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

        [Header("P04 폼 연계 시범 (A/B/C 각자 켜고 끈다 · 전부 끄면 기존 동작)")]
        [Tooltip("A: 저스트 가드 성공 뒤 창 안의 교체 입력을 1회 예약한다. 쿨다운·교체 가능 조건은 그대로")]
        public bool isGuardSwapReserveEnabled = true;
        [Tooltip("A: 저스트 가드 성공 뒤 교체 입력을 받는 창(초). 이 안의 입력만 즉시 교체되거나 예약된다(이름은 호환용으로 유지)")]
        [Min(0f)] public float guardSwapReserveWindow = 1.5f;
        [Tooltip("A: 교체 입력이 예약된 순간부터 교체 조건이 열리길 기다리는 시간(초). 반복 입력으로 늘어나지 않는다")]
        [Min(0f)] public float guardSwapExecutionWait = 1.8f;

        [Tooltip("B: 원거리 → 근접 폼 교체 완료 때 전진 입력을 유지하면 지상에서 1회 짧게 접근한다")]
        public bool isRangedToMeleeLungeEnabled = true;
        [Tooltip("B: 교체 완료 뒤 접근을 기다리는 창(초)")]
        [Min(0f)] public float lungeReadyWindow = 0.5f;
        [Tooltip("B: 접근 최대 거리(유닛). 벽·낭떠러지 앞에서는 덜 간다")]
        [Min(0f)] public float lungeDistance = 1.5f;
        [Tooltip("B: 접근에 걸리는 시간(초)")]
        [Min(0.01f)] public float lungeDuration = 0.15f;

        [Tooltip("C: 원거리 기본 발사체 첫 적중으로 적 1명에 표식, 다른 폼의 근접 기본 공격이 소비해 짧게 끊는다")]
        public bool isRangedMarkEnabled = true;
        [Tooltip("C: 표식 유지 시간(초)")]
        [Min(0f)] public float rangedMarkDuration = 3f;
        [Tooltip("C: 표식 소비 때 일반 적의 기존 경직을 최소 이 시간(초)으로 늘린다. 추가 피해 없음")]
        [Min(0f)] public float rangedMarkInterruptDuration = 0.2f;

        [Tooltip("A/B/C 발생·소비·실패 원인을 콘솔에 남긴다")]
        public bool isFormComboLogEnabled = true;

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

        [Header("무기 (§3-B 획득 3창구)")]
        [Tooltip("무기 등급별 추첨 가중치. 색인이 WeaponRarity 값이다(일반→고대). " +
                 "🔴 enum 순서를 바꾸면 확률이 조용히 뒤바뀐다. " +
                 "항목 수로 나눠 정규화되므로(WeaponDraw) 에셋을 넣고 빼도 등급 분포는 그대로다.")]
        public float[] weaponRarityWeights = { 38f, 25f, 16f, 10f, 6f, 3f, 2f };

        [Tooltip("무기 등급별 상점 가격(골드). 색인이 WeaponRarity 값이다(일반→고대). " +
                 "소모품(25~60)보다 비싸게 잡는다 — 무기는 런 내내 남는다. " +
                 "🔴 표시 가격과 차감액은 한 값에서 나온다(ShopRoomPanel 이 슬롯별로 한 번 계산해 들고 있다).")]
        public int[] weaponPriceByRarity = { 55, 85, 120, 170, 240, 330, 450 };

        [Header("경험치 곡선")]
        [Min(1)] public int baseExpToLevel = 100;
        [Min(1f)] public float expGrowthPerLevel = 1.2f;

        [Header("밸런스 가드레일")]
        [Tooltip("단일 런 내 전투 시간 비율 기준 단일 폼 사용률 경보 임계값 (MF-5)")]
        [Range(0.5f, 1f)] public float formBiasThreshold = 0.6f;

        [Header("메타 진행 (P-21)")]
        [Tooltip("런 종료 시 goldShards → abyss_shards 환산 비율. 0.2 = gold 10 → abyss 2")]
        [Range(0f, 1f)] public float abyssShardsConversionRate = 0.2f;
    }
}
