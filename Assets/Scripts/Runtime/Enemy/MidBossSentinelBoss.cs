using System;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// Stage2 중간보스 "감시자 거인"(midboss_sentinel) 전용 패턴 — 회전베기.
    /// 근접 광역 베기로, 페이즈가 오를수록 연속 베기 횟수가 늘고(P1:1 / P2:2 / P3:3)
    /// 페이즈3에서는 사거리·데미지가 강화된다. 탄막(projectilePrefab) 없이 근접만 수행.
    ///
    /// EnemyData.isElite=true이지만 PrefabBuilder가 enemyId로 이 컴포넌트를 부착해
    /// BossEnemy 페이즈 시스템(데미지 배율·HP 임계 전환)을 그대로 획득한다(중간보스 승격).
    /// 연속 베기 사이 간격은 Coroutine 금지 규약(ADR-002)에 따라 Awaitable로 처리한다.
    /// </summary>
    public sealed class MidBossSentinelBoss : BossEnemy
    {
        [Header("회전베기")]
        [Tooltip("회전베기 광역 반경(근접 타격 범위)")]
        [SerializeField, Min(0.5f)] private float spinRadius = 2.5f;
        [Tooltip("회전베기 사이클 쿨다운(초)")]
        [SerializeField, Min(0.2f)] private float spinCooldown = 2.5f;
        [Tooltip("연속 베기 사이 간격(초)")]
        [SerializeField, Min(0.05f)] private float comboGap = 0.3f;
        [Tooltip("회전베기 발동을 위한 트리거 여유 거리(spinRadius에 가산)")]
        [SerializeField, Min(0f)] private float triggerMargin = 0.6f;

        [Header("페이즈3 강화")]
        [Tooltip("페이즈3 회전베기 데미지 배율(페이즈 배율과 별도로 곱)")]
        [SerializeField, Min(1f)] private float finalDamageMultiplier = 1.3f;
        [Tooltip("페이즈3 회전베기 반경 배율")]
        [SerializeField, Min(1f)] private float finalRadiusMultiplier = 1.3f;

        // 회전베기 링 이펙트 색(감시자 외눈과 동일한 청록).
        private static readonly Color SpinColor = new Color(0.45f, 0.9f, 1f);

        private float lastSpinTime = -999f;
        private bool isSpinning;

        /// <summary>
        /// 타겟이 회전베기 사거리 안이고 쿨다운이 지났을 때 회전베기 콤보를 발동한다.
        /// 연속 베기가 진행 중이면(중첩 방지) 무시한다.
        /// </summary>
        protected override void TickPattern()
        {
            if (IsDead || Target == null || Data == null || isSpinning) return;

            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > spinRadius + triggerMargin) return;
            if (Time.time < lastSpinTime + spinCooldown) return;

            lastSpinTime = Time.time;
            PerformSpinSlash();
        }

        /// <summary>
        /// 페이즈 수만큼 연속 회전베기를 수행(P1:1 / P2:2 / P3:3). 베기 사이는 comboGap 대기.
        /// 페이즈3에서는 데미지·반경이 추가 강화된다. 파괴 시 토큰 취소로 안전하게 종료.
        /// </summary>
        private async void PerformSpinSlash()
        {
            isSpinning = true;
            try
            {
                int hits = Mathf.Clamp(CurrentPhase, 1, 3);
                bool isFinalPhase = CurrentPhase >= 3;
                float radius = isFinalPhase ? spinRadius * finalRadiusMultiplier : spinRadius;
                float damageMul = isFinalPhase ? finalDamageMultiplier : 1f;

                Debug.Log($"[감시자 거인] 회전베기 발동 — 페이즈 {CurrentPhase}, {hits}연속 (반경 {radius:F1})");

                for (int i = 0; i < hits; i++)
                {
                    if (IsDead) return;

                    FlashVisual();
                    SpawnAreaEffect(radius, SpinColor);
                    ShakeCamera(0.22f, 0.18f);
                    MeleeAreaStrike(radius, damageMul);

                    if (i < hits - 1)
                        await Awaitable.WaitForSecondsAsync(comboGap, destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 베기 도중 파괴됨 — 정상 종료.
            }
            finally
            {
                isSpinning = false;
            }
        }
    }
}
