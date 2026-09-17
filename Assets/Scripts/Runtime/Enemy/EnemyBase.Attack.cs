using System;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Events;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using FSM.Core;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 공격 파트 — 공격 상태 진입(예비동작) · 타격 · 근접/직진탄/연사/곡사 실행.
    /// 📌 2026-09-18 <c>EnemyBase.cs</c> 가 500줄을 넘어 나눴다(시간제 공격 추가). 판단(언제 공격하나)은 본체의
    /// <c>EvaluateTransitions</c> 에 남고, 여기는 들어간 뒤 무엇을 하느냐만 갖는다.
    /// </summary>
    public partial class EnemyBase
    {
        /// <summary>
        /// 공격 상태 진입. 예비동작이 0 이면 옛 동작(즉발)이고, 아니면 타격 시각을 잡아 두고
        /// <see cref="EvaluateTransitions"/> 가 그 시각에 <see cref="Strike"/> 를 부른다.
        /// </summary>
        private void BeginAttack()
        {
            if (target != null) visuals?.Face(target.position.x - transform.position.x);

            float windup = data != null ? data.attackWindup : 0f;
            float recovery = data != null ? data.attackRecovery : 0f;
            timedStateExitTime = Time.time + windup + recovery;
            strikeTime = Time.time + windup;
            hasStruck = false;

            if (windup <= 0f)
            {
                hasStruck = true;
                PerformAttack();
            }
        }

        /// <summary>
        /// 예비동작 끝의 타격. 🔴 <b>근접은 사거리를 다시 본다</b> — 예비동작은 피하라고 보여 주는 것이라,
        /// 들어갈 때의 판정으로 때리면 물러나도 맞는다. 원거리는 그 순간의 조준으로 쏜다.
        /// </summary>
        private void Strike()
        {
            hasStruck = true;
            if (target == null || data == null) return;

            bool isMelee = !data.isRanged;
            if (isMelee && Vector2.Distance(transform.position, target.position) > data.attackRange) return;

            PerformAttack();
        }

        private void PerformAttack()
        {
            if (target == null || data == null) return;

            // 곡사가 우선한다. 프리팹 연결이 없으면 아래 직진탄으로 자연히 폴백되므로,
            // 배선이 빠져도 이 적이 무해해지지는 않는다.
            if (data.isRanged && data.usesArcProjectile && data.arcProjectilePrefab != null)
            {
                FireArcShell();
                return;
            }

            // 원거리 적: 발사체 발사(즉발 대신). projectilePrefab 미연결 시 근접으로 폴백.
            if (data.isRanged && data.projectilePrefab != null)
            {
                if (data.burstCount > 1) FireBurstAsync();
                else FireProjectile();
                return;
            }

            var player = target.GetComponent<PlayerCharacter>();
            if (player != null && !player.IsDead)
            {
                int damage = GetAttackDamage();
                // 출처 = 이 적 — 방패병 가드의 정면 판정(16-shield-guard §5)
                player.TakeDamage(damage, transform.position);
            }
        }

        /// <summary>
        /// 타겟 방향으로 발사체 1발 발사(원거리 적 직격용).
        /// </summary>
        private void FireProjectile()
        {
            Vector2 toTarget = (Vector2)target.position - (Vector2)transform.position;
            SpawnProjectile(toTarget);
        }

        /// <summary>
        /// 연사. 탄 사이 간격은 Coroutine 금지 규약(ADR-002)에 따라 Awaitable로 벌린다.
        ///
        /// 매 발 <see cref="FireProjectile"/>로 <b>조준을 다시 한다</b> — 첫 발 방향으로 세 발을
        /// 몰아 쏘면 옆으로 한 걸음만 움직여도 전부 빗나가 연사라는 위협이 성립하지 않는다.
        /// 대신 사이를 벌려 두었으므로 계속 움직이면 뒷발은 피할 수 있다.
        /// </summary>
        private async void FireBurstAsync()
        {
            try
            {
                int shots = Mathf.Max(1, data.burstCount);
                for (int i = 0; i < shots; i++)
                {
                    // 연사 도중 죽거나 타겟이 사라질 수 있다 — 대기 구간을 사이에 두면
                    // "그동안 세상이 바뀌었을 수 있다"를 매번 확인해야 한다.
                    if (isDead || target == null || data == null) return;

                    FireProjectile();

                    if (i < shots - 1 && data.burstInterval > 0f)
                    {
                        await Awaitable.WaitForSecondsAsync(data.burstInterval, destroyCancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 파괴·씬 전환으로 취소됨. 남은 탄은 쏘지 않는다.
            }
        }

        /// <summary>
        /// 곡사 폭발탄 1발. 조준점은 <b>발사 시점의 플레이어 위치</b>다 —
        /// 착탄까지 <see cref="EnemyData.arcFlightTime"/>초가 걸리므로 그 자리에 서 있으면 맞고,
        /// 움직이면 피한다. 이것이 이 적의 유일한 회피 규칙이라 예고 링과 함께 읽히게 했다.
        /// </summary>
        private void FireArcShell()
        {
            if (data.arcProjectilePrefab == null || target == null) return;

            // 자기 콜라이더 위에서 출발 — 발밑에서 나오면 발사 즉시 지형에 닿아 터진다.
            Vector2 spawnPos = (Vector2)transform.position + Vector2.up * 0.7f;

            var shell = PoolManager.Instance.Get(data.arcProjectilePrefab, (Vector3)spawnPos, Quaternion.identity);
            shell.Launch((Vector2)target.position, GetAttackDamage(),
                         data.arcFlightTime, data.arcExplosionRadius, data.arcProjectilePrefab);
        }

        /// <summary>
        /// 지정 방향으로 발사체를 풀에서 꺼내 발사하는 공용 진입점(원거리 직격·보스 탄막 공유).
        /// 자기 콜라이더와 겹치지 않도록 사거리의 일부만큼 앞에서 생성하고,
        /// Projectile 측에서도 EnemyBase를 통과 처리한다. projectilePrefab 미연결 시 무동작.
        /// 데미지는 GetAttackDamage()를 사용하므로 보스 페이즈 배율이 그대로 반영된다.
        /// </summary>
        protected void SpawnProjectile(Vector2 direction)
        {
            if (data == null || data.projectilePrefab == null) return;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 spawnPos = (Vector2)transform.position + dir * (data.attackRange * 0.3f);

            var proj = PoolManager.Instance.Get(data.projectilePrefab, (Vector3)spawnPos, Quaternion.identity);
            proj.Launch(dir, GetAttackDamage(), data.projectileSpeed, data.projectileLifetime, data.projectilePrefab);
        }
    }
}
