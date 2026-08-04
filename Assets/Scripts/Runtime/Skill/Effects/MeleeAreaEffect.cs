using System.Collections.Generic;
using System.Threading;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 전방 박스 광역 근접 타격. 소유자 facing 방향으로 박스를 밀어내 OverlapBox(ContactFilter2D, 버퍼)로 적을 수집,
    /// 중복 제거 후 일괄 피해. 프리팹이 필요 없어 가장 빠르게 플레이 가능한 효과.
    /// </summary>
    public sealed class MeleeAreaEffect : IAbilityEffect
    {
        private static readonly List<EnemyBase> hitBuffer = new();

        // OverlapBoxAll의 매 호출 배열 할당(GC)을 피하기 위한 무할당 버퍼. 32개면 실전 동시 히트 수 충분.
        private static readonly Collider2D[] overlapResults = new Collider2D[32];
        // useTriggers를 매 호출 갱신해야 하므로 readonly 불가 (struct 필드 직접 대입).
        // NoFilter()는 정적이 아닌 인스턴스 메서드라 기본 인스턴스를 만들어 호출한다.
        private static ContactFilter2D overlapFilter = new ContactFilter2D().NoFilter();

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

            // OverlapBoxAll과 동일하게 전역 트리거 감지 설정을 따르도록 매 호출 동기화(설정이 런타임에 바뀔 수 있음).
            overlapFilter.useTriggers = Physics2D.queriesHitTriggers;
            int hitCount = Physics2D.OverlapBox(center, data.meleeBoxSize, 0f, overlapFilter, overlapResults);

            hitBuffer.Clear();
            // hitCount만큼만 순회 — 버퍼에 남은 이전 호출 잔존값은 무시. 버퍼 초과분(32개 이상 동시 히트)은 누락될 수 있음.
            for (int i = 0; i < hitCount; i++)
            {
                var col = overlapResults[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (hitBuffer.Contains(enemy)) continue;
                hitBuffer.Add(enemy);
            }

            // 연소 명세는 부여 시점에 확정한다 — 시전자의 연소 강화 배율을 여기서 곱해 둬야
            // 적이 tick마다 플레이어를 되묻지 않는다(BurnPayload 참조).
            var burn = AbilityBurn.Build(context, data);

            foreach (var enemy in hitBuffer)
            {
                if (burn.HasBurn) enemy.ApplyBurn(burn);
                enemy.TakeDamage(data.damage);
            }

            // 한 프레임 양보 — 실행 플래그/연출 타이밍 정리(취소 토큰 전파).
            await Awaitable.NextFrameAsync(token);
        }
    }
}
