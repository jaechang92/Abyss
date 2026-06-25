using System.Threading;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Feedback;
using GAS.Core;
using ObjectPool_Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 전방으로 발사체 발사. 기존 적 발사체 자산(Projectile + PoolManager)을 재사용하되,
    /// ProjectileFaction.HitsEnemies로 발사해 플레이어는 통과하고 적에게 피해를 준다.
    /// projectileCount가 2 이상이면 projectileSpreadAngle 범위에 균등 분산해 부채꼴로 일제 발사한다(연사).
    /// projectilePrefab 미연결 시 무동작(적 SpawnProjectile과 동일한 안전 처리).
    /// </summary>
    public sealed class ProjectileEffect : IAbilityEffect
    {
        public async Awaitable ApplyAsync(IGameplayContext context, GenericAbilityData data, CancellationToken token)
        {
            if (data.projectilePrefab == null || !PoolManager.HasInstance)
            {
                if (data.projectilePrefab == null)
                    Debug.LogWarning($"[ProjectileEffect] {data.abilityName}: projectilePrefab이 비어 있어 무동작.");
                await Awaitable.NextFrameAsync(token);
                return;
            }

            Vector2 baseDir = (Vector2)context.Forward;
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector2.right;
            baseDir.Normalize();

            int count = Mathf.Max(1, data.projectileCount);
            // count발을 [-spread/2, +spread/2]에 균등 배치. 1발이면 정중앙(직선).
            float spread = data.projectileSpreadAngle;
            float startAngle = count > 1 ? -spread * 0.5f : 0f;
            float step = count > 1 ? spread / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * baseDir);
                Vector2 spawnPos = (Vector2)context.Position + dir * data.projectileSpawnOffset;

                var proj = PoolManager.Instance.Get(data.projectilePrefab, (Vector3)spawnPos, Quaternion.identity);
                proj.Launch(dir, data.damage, data.projectileSpeed, data.projectileLifetime,
                            data.projectilePrefab, ProjectileFaction.HitsEnemies);

                // 발사 지점 머즐 플래시(작은 링).
                if (data.showHitEffect)
                {
                    BossAreaEffect.Spawn((Vector3)spawnPos, 0.5f, data.effectColor, 0.18f);
                }
            }

            await Awaitable.NextFrameAsync(token);
        }
    }
}
