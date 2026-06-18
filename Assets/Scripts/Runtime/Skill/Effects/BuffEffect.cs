using System.Threading;
using Abyss.Runtime.Player;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 자기 자신 대상 효과. 즉시 체력 회복 + 시간제 버프(이동속도/공격력 일시 상승).
    /// 지속시간·원복은 PlayerCharacter(ApplyTimedBuff/UpdateBuff)가 관리하므로 여기선 적용 요청만 한다.
    /// </summary>
    public sealed class BuffEffect : IAbilityEffect
    {
        public async Awaitable ApplyAsync(IGameplayContext context, GenericAbilityData data, CancellationToken token)
        {
            var player = context.Owner != null ? context.Owner.GetComponent<PlayerCharacter>() : null;
            if (player != null)
            {
                // 즉시 회복(있으면) + 시간제 버프(buffDuration>0일 때) 적용.
                if (data.healAmount > 0) player.Heal(data.healAmount);
                player.ApplyTimedBuff(data.buffMoveSpeedMultiplier, data.buffAttackMultiplier, data.buffDuration);
            }

            await Awaitable.NextFrameAsync(token);
        }
    }
}
