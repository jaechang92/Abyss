using System;
using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// Stage3 최종보스 "왕좌의 영혼"(boss_thronebound) 전용 패턴 — 순간이동 + 광역 슬래시.
    /// 08-content-roadmap.md M2-Q2 항목.
    ///
    /// 앞선 두 보스와 <b>플레이어에게 요구하는 것</b>이 다르다.
    /// - <see cref="MidBossSentinelBoss"/>(회전베기): 제자리 연속 광역 — <i>붙어 있는 것</i>을 벌한다.
    /// - <see cref="FlameSerpentBoss"/>(브레스/꼬리): 거리별 패턴 전환 — 거리 유지를 요구한다.
    /// - 왕좌의 영혼: <b>거리를 무의미하게 만든다.</b> 멀어지면 등 뒤로 순간이동해 벤다.
    ///   도망이 통하지 않으니 <b>예고를 읽고 빠져나가는 것</b>만이 답이다.
    ///
    /// 그래서 이 보스는 예고에 모든 것을 건다 — 착지 지점에 링을 먼저 띄우고,
    /// 예고 중에는 <see cref="FixedUpdate"/>에서 이동을 멈춘다(예고와 실제 타격 지점이 어긋나면
    /// 회피가 운이 된다). 페이즈3에서는 벤 직후 사방으로 영혼을 터뜨려 "붙어서 피하기"까지 닫는다.
    /// </summary>
    public sealed class ThroneboundBoss : BossEnemy
    {
        [Header("광역 슬래시")]
        [Tooltip("슬래시 광역 반경(근접 타격 범위)")]
        [SerializeField, Min(0.5f)] private float slashRadius = 3f;
        [Tooltip("이 거리 안이면 순간이동 없이 제자리에서 벤다(slashRadius에 가산)")]
        [SerializeField, Min(0f)] private float triggerMargin = 0.8f;
        [Tooltip("슬래시 사이클 쿨다운(초). 실제 주기는 cooldown/currentPhase로 페이즈 상승 시 단축")]
        [SerializeField, Min(0.2f)] private float strikeCooldown = 3f;
        [Tooltip("슬래시 데미지 배율(페이즈 배율과 별도로 곱)")]
        [SerializeField, Min(1f)] private float slashDamageMultiplier = 1.4f;

        [Header("순간이동")]
        [Tooltip("플레이어로부터 이 거리만큼 떨어진 곳에 나타난다. 슬래시 반경보다 작아야 착지 즉시 타격권에 든다")]
        [SerializeField, Min(0.5f)] private float blinkOffset = 2.2f;

        [Header("페이즈3 — 영혼 파열")]
        [Tooltip("슬래시 직후 사방으로 터뜨리는 탄 수. 0이면 비활성")]
        [SerializeField, Min(0)] private int burstCount = 8;

        [Header("연출")]
        [Tooltip("착지 지점 예고 시간(초) — 순간이동은 예고 없이는 회피가 불가능한 공격이다")]
        [SerializeField, Min(0f)] private float telegraphTime = 0.45f;
        [Tooltip("예고 효과음(차지)")]
        [SerializeField] private AudioClip telegraphSfx;
        [Tooltip("순간이동 효과음")]
        [SerializeField] private AudioClip blinkSfx;
        [Tooltip("슬래시 효과음")]
        [SerializeField] private AudioClip slashSfx;

        // 부채꼴을 완전한 360도로 주면 첫 탄과 마지막 탄이 겹친다 — 한 칸 덜 돌린다.
        private const float BURST_SPREAD = 330f;

        // 왕좌 금색(슬래시 링).
        private static readonly Color ThroneColor = new Color(0.88f, 0.76f, 0.35f);
        // 예고 틴트 색(붉은 경고) — 다른 보스와 같은 어휘를 쓴다.
        private static readonly Color TelegraphColor = new Color(1f, 0.3f, 0.3f);

        private float lastStrikeTime = -999f;
        private bool isStriking;

        /// <summary>
        /// 쿨다운이 지나고 타겟이 감지 범위 안이면 슬래시를 발동한다.
        /// 슬래시 사거리 <b>밖</b>이면 순간이동으로 붙고, 이미 붙어 있으면 제자리에서 벤다 —
        /// 거리로 패턴이 갈리지만 <b>어느 쪽이든 맞는다</b>는 점이 이 보스의 성격이다.
        /// </summary>
        protected override void TickPattern()
        {
            if (IsDead || Target == null || Data == null || isStriking) return;
            if (Time.time < lastStrikeTime + strikeCooldown / CurrentPhase) return;

            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > Data.detectionRange) return;

            lastStrikeTime = Time.time;
            PerformStrike(blink: distance > slashRadius + triggerMargin);
        }

        /// <summary>
        /// 예고 중에는 추격을 멈춘다.
        ///
        /// 멈추지 않으면 예고 링을 띄운 지점과 실제 타격 지점이 어긋나 <b>회피가 운이 된다</b>.
        /// 예고로 회피를 유도하는 보스가 예고를 지키지 않으면 예고가 아니라 노이즈다.
        /// 중력은 유지해야 하므로 수평 속도만 죽인다.
        /// </summary>
        protected override void FixedUpdate()
        {
            if (isStriking)
            {
                if (Body != null) Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
                return;
            }
            base.FixedUpdate();
        }

        /// <summary>
        /// 예고 → (순간이동) → 광역 슬래시 → (페이즈3) 영혼 파열.
        /// 예고 대기는 Coroutine 금지 규약(ADR-002)에 따라 Awaitable로 처리한다.
        /// </summary>
        private async void PerformStrike(bool blink)
        {
            isStriking = true;
            try
            {
                Vector3 center = blink ? ResolveBlinkDestination() : transform.position;

                Debug.Log($"[왕좌의 영혼] {(blink ? "순간이동 슬래시" : "제자리 슬래시")} 발동 — 페이즈 {CurrentPhase}");

                // 예고는 '지금 서 있는 곳'이 아니라 '벨 곳'에 띄운다.
                if (telegraphTime > 0f)
                {
                    PlaySfx(telegraphSfx);
                    TintVisual(TelegraphColor, telegraphTime);
                    SpawnAreaEffectAt(center, slashRadius, ThroneColor, telegraphTime, BossAreaEffect.Mode.Telegraph);
                    await Awaitable.WaitForSecondsAsync(telegraphTime, destroyCancellationToken);
                    if (IsDead) return;
                }

                if (blink)
                {
                    PlaySfx(blinkSfx);
                    TeleportTo(center);
                    PunchVisual(0.22f, 0.2f);
                }

                PlaySfx(slashSfx);
                FlashVisual();
                SpawnAreaEffect(slashRadius, ThroneColor);
                ShakeCamera(0.28f, 0.22f);
                MeleeAreaStrike(slashRadius, slashDamageMultiplier);

                // 페이즈3 — 벤 직후 사방으로 영혼 파열. 붙어서 피하는 선택지까지 닫는다.
                if (CurrentPhase >= 3 && burstCount > 0)
                {
                    FireFan(burstCount, BURST_SPREAD);
                }
            }
            catch (OperationCanceledException)
            {
                // 예고 도중 파괴됨 — 정상 종료.
            }
            finally
            {
                isStriking = false;
            }
        }

        /// <summary>
        /// 착지 지점 — 플레이어를 사이에 두고 <b>반대편</b>이다. 도망치는 방향의 앞을 막는 셈이라
        /// "거리를 벌리면 안전하다"는 판단을 무너뜨린다.
        ///
        /// Y는 건드리지 않는다. 중력이 있는 2D 플랫포머라 높이를 옮기면 공중에서 낙하하거나
        /// 지형에 박힌다 — 수평 이동만으로 의도는 충분히 전달된다.
        /// </summary>
        private Vector3 ResolveBlinkDestination()
        {
            float side = Target.position.x >= transform.position.x ? 1f : -1f;
            return new Vector3(Target.position.x + side * blinkOffset, transform.position.y, transform.position.z);
        }

        /// <summary>
        /// 순간이동. Transform만 옮기면 물리 바디가 다음 스텝에 원위치로 되돌리므로 둘 다 옮기고,
        /// 이동 관성도 지운다(순간이동인데 착지 후 미끄러지면 안 된다).
        /// </summary>
        private void TeleportTo(Vector3 destination)
        {
            transform.position = destination;

            if (Body != null)
            {
                Body.position = destination;
                Body.linearVelocity = Vector2.zero;
            }
        }
    }
}
