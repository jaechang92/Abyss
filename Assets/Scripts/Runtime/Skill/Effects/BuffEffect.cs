using System.Threading;
using Abyss.Runtime.Player;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 자기 자신 대상 효과. 프로토 범위에서는 즉시 체력 회복만 구현한다.
    /// 버프(이동속도/공격력 일시 상승) 확장은 후속.
    /// </summary>
    public sealed class BuffEffect : IAbilityEffect
    {
        public async Awaitable ApplyAsync(IGameplayContext context, GenericAbilityData data, CancellationToken token)
        {
            var player = context.Owner != null ? context.Owner.GetComponent<PlayerCharacter>() : null;
            if (player != null && data.healAmount > 0)
            {
                player.Heal(data.healAmount);
            }

            await Awaitable.NextFrameAsync(token);
        }
    }
}
