using System;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// Stage2 최종보스 "화염 뱀"(boss_flame_serpent) 전용 패턴 — 거리 기반 화염브레스 / 꼬리치기 전환.
    ///
    /// - 원거리: 화염브레스 — 타겟 방향 좁은 부채꼴 연사(FireFan). 페이즈 상승 시 주기↓·발사 수↑.
    /// - 근거리: 꼬리치기 — 근접 광역 강타(MeleeAreaStrike, 데미지 배율↑).
    ///
    /// 화염브레스는 projectilePrefab을 사용하므로 PrefabBuilder가 isBoss 보스에 발사체를 연결해야 한다
    /// (LinkProjectileToRangedEnemies — isRanged||isBoss). 데미지는 GetAttackDamage()로 페이즈 배율 반영.
    /// </summary>
    public sealed class FlameSerpentBoss : BossEnemy
    {
        [Header("꼬리치기 (근접 광역)")]
        [Tooltip("꼬리치기 광역 반경. 이 거리 이내면 브레스 대신 꼬리치기로 전환")]
        [SerializeField, Min(0.5f)] private float tailRange = 2.8f;
        [Tooltip("꼬리치기 쿨다운(초)")]
        [SerializeField, Min(0.2f)] private float tailCooldown = 2f;
        [Tooltip("꼬리치기 데미지 배율(페이즈 배율과 별도로 곱)")]
        [SerializeField, Min(1f)] private float tailDamageMultiplier = 1.5f;

        [Header("화염브레스 (원거리 연사)")]
        [Tooltip("브레스 발사 주기(초). 실제 주기는 interval/currentPhase로 페이즈 상승 시 단축")]
        [SerializeField, Min(0.3f)] private float breatheInterval = 2.5f;
        [Tooltip("페이즈1 기준 브레스 발사 수. 페이즈마다 +1발")]
        [SerializeField, Min(1)] private int breatheBaseCount = 3;
        [Tooltip("브레스 부채꼴 확산 각도(도) — 좁을수록 직선형 화염 줄기")]
        [SerializeField, Min(0f)] private float breatheSpread = 16f;

        [Header("연출")]
        [Tooltip("꼬리치기 전 예고 시간(초) — 붉은 틴트 + 범위 미리보기로 회피 안내")]
        [SerializeField, Min(0f)] private float telegraphTime = 0.4f;
        [Tooltip("예고 효과음(차지)")]
        [SerializeField] private AudioClip telegraphSfx;
        [Tooltip("꼬리치기 효과음(강타)")]
        [SerializeField] private AudioClip smashSfx;
        [Tooltip("화염브레스 효과음")]
        [SerializeField] private AudioClip breatheSfx;

        // 꼬리치기 링 이펙트 색(화염 주황).
        private static readonly Color TailColor = new Color(1f, 0.45f, 0.12f);
        // 예고 틴트 색(붉은 경고).
        private static readonly Color TelegraphColor = new Color(1f, 0.3f, 0.3f);

        private float lastTailTime = -999f;
        private float lastBreatheTime = -999f;
        private bool isTailSwinging;

        /// <summary>
        /// 타겟 거리에 따라 패턴을 선택한다. 꼬리치기 사거리 안이면 근접 강타,
        /// 그 밖이고 감지 범위 안이면 화염브레스 연사. 각 패턴은 독립 쿨다운으로 관리.
        /// </summary>
        protected override void TickPattern()
        {
            if (IsDead || Target == null || Data == null || isTailSwinging) return;

            float distance = Vector2.Distance(transform.position, Target.position);

            if (distance <= tailRange)
            {
                if (Time.time < lastTailTime + tailCooldown) return;
                lastTailTime = Time.time;
                PerformTailStrike();
                return;
            }

            if (distance <= Data.detectionRange)
            {
                float interval = breatheInterval / CurrentPhase;
                if (Time.time < lastBreatheTime + interval) return;
                lastBreatheTime = Time.time;

                int count = breatheBaseCount + (CurrentPhase - 1);
                Debug.Log($"[화염 뱀] 화염브레스 발동 — 페이즈 {CurrentPhase}, {count}발");
                PlaySfx(breatheSfx);
                FlashVisual();
                PunchVisual(0.1f, 0.2f);
                ShakeCamera(0.12f, 0.15f);
                FireFan(count, breatheSpread);
            }
        }

        /// <summary>
        /// 꼬리치기 — 예고(붉은 틴트 + 타격 범위 고정 링 + 차지음) 후 근접 광역 강타.
        /// 즉발 광역이라 예고로 회피 여지를 준다. 예고 대기는 Awaitable(Coroutine 금지).
        /// </summary>
        private async void PerformTailStrike()
        {
            isTailSwinging = true;
            try
            {
                Debug.Log($"[화염 뱀] 꼬리치기 발동 — 페이즈 {CurrentPhase} (반경 {tailRange:F1})");

                if (telegraphTime > 0f)
                {
                    PlaySfx(telegraphSfx);
                    TintVisual(TelegraphColor, telegraphTime);
                    SpawnAreaEffect(tailRange, TailColor, telegraphTime, BossAreaEffect.Mode.Telegraph);
                    await Awaitable.WaitForSecondsAsync(telegraphTime, destroyCancellationToken);
                    if (IsDead) return;
                }

                PlaySfx(smashSfx);
                FlashVisual();
                PunchVisual(0.2f, 0.25f);
                SpawnAreaEffect(tailRange, TailColor);
                ShakeCamera(0.3f, 0.25f);
                MeleeAreaStrike(tailRange, tailDamageMultiplier);
            }
            catch (OperationCanceledException)
            {
                // 꼬리치기 도중 파괴됨 — 정상 종료.
            }
            finally
            {
                isTailSwinging = false;
            }
        }
    }
}
