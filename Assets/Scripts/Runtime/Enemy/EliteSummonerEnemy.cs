using System;
using System.Collections.Generic;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 엘리트 변종 ② 소환사(2026-10-06). 기본 공격은 빌려 온 공허 술사의 3연사 그대로이고,
    /// 플레이어를 감지한 동안 주기적으로 졸개를 부른다.
    ///
    /// 플레이어에게 묻는 것: 「졸개를 치울 것인가, 뚫고 들어가 소환사를 먼저 잡을 것인가」.
    /// 답을 분명하게 하려고 <b>소환사가 죽으면 졸개도 함께 사라진다</b>(<see cref="EnemyBase.Dismiss"/>).
    ///
    /// 🔴 졸개는 <see cref="StageDirector.TrySpawnSummoned"/>로 부른다 — 방 클리어 판정에 등록돼야 한다.
    /// 🔴 졸개는 보상이 없다(<see cref="EnemyBase.IsSummoned"/>) · 동시 수·총수 상한이 있다 — 파밍을 막는다.
    /// 소환 예고(색 + 예고 링)는 실제 등장 자리에 띄운다. 예고 없이 적이 생기면 억울한 피격이 된다.
    /// </summary>
    public sealed class EliteSummonerEnemy : EnemyBase
    {
        [Header("소환")]
        [Tooltip("부를 졸개. PrefabBuilder가 근접 병사를 연결한다. 비면 소환하지 않는다.")]
        [SerializeField] private EnemyData minionData;
        [Tooltip("소환 주기(초). 플레이어를 감지한 동안만 흐른다.")]
        [SerializeField, Min(1f)] private float summonInterval = 6f;
        [Tooltip("첫 소환까지(초) — 만나자마자 부르면 소환사에게 다가갈 틈이 없다.")]
        [SerializeField, Min(0f)] private float firstSummonDelay = 2.5f;
        [Tooltip("동시에 살아 있을 수 있는 졸개 수")]
        [SerializeField, Min(1)] private int maxAliveMinions = 2;
        [Tooltip("이 소환사가 평생 부를 수 있는 졸개 수")]
        [SerializeField, Min(1)] private int maxTotalMinions = 4;
        [Tooltip("예고에서 등장까지(초)")]
        [SerializeField, Min(0.1f)] private float summonWindup = 0.8f;
        [Tooltip("소환 자리 — 소환사 기준 플레이어 반대쪽으로 이만큼(유닛)")]
        [SerializeField, Min(0f)] private float summonSideOffset = 1.6f;

        [Header("소환 연출")]
        [SerializeField] private Color summonColor = new(0.7f, 0.45f, 1f, 1f);
        [SerializeField, Min(0f)] private float summonRingRadius = 1.1f;
        [SerializeField] private AudioClip summonSfx;

        private readonly List<EnemyBase> minions = new();
        private StageDirector director;
        private bool isDirectorLookupDone;
        private float nextSummonTime = -1f;
        private int totalSummoned;
        private bool isSummoning;

        /// <summary>지금 살아 있는 졸개 수(디버그·스탯 창 조회용).</summary>
        public int AliveMinionCount
        {
            get
            {
                minions.RemoveAll(m => m == null || m.IsDead);
                return minions.Count;
            }
        }

        protected override void Update()
        {
            base.Update();
            TickSummon();
        }

        private void TickSummon()
        {
            if (IsDead || isSummoning || minionData == null || Data == null || Target == null) return;
            if (totalSummoned >= maxTotalMinions) return;

            // 감지 범위 밖에서는 시계가 멈춘다 — 방 반대편에서 졸개를 쌓아 두지 않는다.
            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > Data.detectionRange)
            {
                nextSummonTime = -1f;
                return;
            }

            if (nextSummonTime < 0f)
            {
                nextSummonTime = Time.time + firstSummonDelay;
                return;
            }
            if (Time.time < nextSummonTime) return;

            nextSummonTime = Time.time + summonInterval;
            if (AliveMinionCount >= maxAliveMinions) return;

            SummonAsync();
        }

        /// <summary>예고 → 대기 → 등장. 대기는 Coroutine 금지 규약(ADR-002)에 따라 Awaitable.</summary>
        private async void SummonAsync()
        {
            isSummoning = true;
            try
            {
                Vector3 position = ResolveSummonPosition();
                TintVisual(summonColor, summonWindup);
                if (summonRingRadius > 0f)
                {
                    BossAreaEffect.Spawn(position, summonRingRadius, summonColor, summonWindup, BossAreaEffect.Mode.Telegraph);
                }

                await Awaitable.WaitForSecondsAsync(summonWindup, destroyCancellationToken);

                // 예고 중에 죽었으면 부르지 않는다 — 죽은 소환사의 졸개가 늦게 나오면 「죽으면 사라진다」가 깨진다.
                if (IsDead) return;

                var minion = ResolveDirector()?.TrySpawnSummoned(minionData, position);
                if (minion == null) return;

                minion.MarkSummoned();
                minions.Add(minion);
                totalSummoned += 1;
                if (summonSfx != null && AudioManager.HasInstance) AudioManager.Instance.PlaySfx(summonSfx);
            }
            catch (OperationCanceledException)
            {
                // 파괴·씬 전환으로 취소됨. 아무것도 부르지 않는다.
            }
            finally
            {
                isSummoning = false;
            }
        }

        /// <summary>소환사 옆, 플레이어 반대쪽. 높이는 소환사와 같다(졸개는 중력으로 내려선다).</summary>
        private Vector3 ResolveSummonPosition()
        {
            float away = Target != null && Target.position.x > transform.position.x ? -1f : 1f;
            return transform.position + new Vector3(away * summonSideOffset, 0f, 0f);
        }

        private StageDirector ResolveDirector()
        {
            if (director != null || isDirectorLookupDone) return director;
            isDirectorLookupDone = true;
            director = FindAnyObjectByType<StageDirector>();
            if (director == null)
            {
                Debug.LogWarning("[EliteSummoner] StageDirector가 없어 소환하지 않는다 — 방 클리어 판정에 못 넣는 적은 부르지 않는다.");
            }
            return director;
        }

        /// <summary>소환사가 죽는 순간 졸개를 거둔다. 처치 이벤트보다 먼저 불린다(<see cref="EnemyBase.OnDying"/>).</summary>
        protected override void OnDying()
        {
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null) minions[i].Dismiss();
            }
            minions.Clear();
        }
    }
}
