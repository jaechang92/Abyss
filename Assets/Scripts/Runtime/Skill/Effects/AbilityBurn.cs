using Abyss.Runtime.Combat;
using Abyss.Runtime.Player;
using GAS.Core;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 어빌리티가 부여할 연소 명세를 <b>시전 시점에 확정</b>하는 공통 진입점.
    ///
    /// 근접(<see cref="MeleeAreaEffect"/>)과 발사체(<see cref="ProjectileEffect"/>) 두 곳이 같은 규칙을
    /// 필요로 해서 여기로 모았다 — 한쪽이 다른 쪽의 헬퍼를 호출하면 근접과 발사체 사이에 없는 의존이 생긴다.
    /// </summary>
    public static class AbilityBurn
    {
        /// <summary>
        /// 어빌리티 데이터의 연소 수치에 시전자 보정(연소 강화 Passive)을 곱한 확정 명세.
        /// 시전자가 플레이어가 아니면 무보정. burnStacks가 0이면 HasBurn이 false라 소비처는 분기 없이 넘기면 된다.
        /// </summary>
        public static BurnPayload Build(IGameplayContext context, GenericAbilityData data)
        {
            if (data == null) return default;
            return data.BuildBurnPayload().Scaled(ResolveMultiplier(context));
        }

        /// <summary>시전자의 연소 피해 배율(연소 강화). 플레이어가 아니면 1.</summary>
        private static float ResolveMultiplier(IGameplayContext context)
        {
            var player = context?.Owner != null ? context.Owner.GetComponent<PlayerCharacter>() : null;
            return player != null ? player.BurnDamageMultiplier : 1f;
        }
    }
}
