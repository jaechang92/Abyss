using System.Threading;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill.Effects
{
    /// <summary>
    /// 범용 어빌리티의 실제 효과 실행 단위. GenericAbility가 effectType으로 구현체를 골라
    /// ApplyAsync로 위임한다. 효과별 클래스를 분리해 GenericAbility 본문을 짧게 유지한다.
    /// </summary>
    public interface IAbilityEffect
    {
        /// <summary>
        /// 효과 실행. context는 소유자(플레이어), data는 수치, token은 소유자 파괴 시 취소 신호.
        /// </summary>
        Awaitable ApplyAsync(IGameplayContext context, GenericAbilityData data, CancellationToken token);
    }
}
