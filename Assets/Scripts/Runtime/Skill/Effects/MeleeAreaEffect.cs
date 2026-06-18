using System.Collections.Generic;
using System.Threading;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 전방 박스 광역 근접 타격. 소유자 facing 방향으로 박스를 밀어내 OverlapBox로 적을 수집,
    /// 중복 제거 후 일괄 피해. 프리팹이 필요 없어 가장 빠르게 플레이 가능한 효과.
    /// </summary>
    public sealed class MeleeAreaEffect : IAbilityEffect
    {
        private static readonly List<EnemyBase> hitBuffer = new();

        public async Awaitable ApplyAsync(IGameplayContext context, GenericAbilityData data, CancellationToken token)
        {
            Vector2 dir = (Vector2)context.Forward;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            Vector2 center = (Vector2)context.Position + dir * data.meleeForwardOffset;

            // 타격 범위 시각화 — 프리팹 불필요한 자기완결 링(BossAreaEffect 재사용).
            if (data.showHitEffect)
            {
                float radius = Mathf.Max(data.meleeBoxSize.x, data.meleeBoxSize.y) * 0.5f;
                BossAreaEffect.Spawn((Vector3)center, radius, data.effectColor, 0.3f);
            }

            var hits = Physics2D.OverlapBoxAll(center, data.meleeBoxSize, 0f);

            hitBuffer.Clear();
            foreach (var col in hits)
            {
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (hitBuffer.Contains(enemy)) continue;
                hitBuffer.Add(enemy);
            }

            foreach (var enemy in hitBuffer)
            {
                enemy.TakeDamage(data.damage);
            }

            // 한 프레임 양보 — 실행 플래그/연출 타이밍 정리(취소 토큰 전파).
            await Awaitable.NextFrameAsync(token);
        }
    }
}
