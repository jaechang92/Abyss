using Abyss.Runtime.Audio;
using Abyss.Runtime.Skill.Effects;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill
{
    /// <summary>
    /// 데이터 구동 범용 어빌리티. AbilityData만으로는 인스턴스화할 수 없는 GAS 구조상
    /// 소유자(플레이어) GameObject에 AddComponent된 뒤 Configure로 데이터·컨텍스트를 주입받는다.
    /// ExecuteAsync는 얇은 디스패처로, effectType에 대응하는 IAbilityEffect에 실행을 위임한다.
    /// </summary>
    public sealed class GenericAbility : Ability
    {
        private GenericAbilityData data;
        private IAbilityEffect effect;

        /// <summary>
        /// AddComponent 직후 호출하는 주입 진입점. 쿨다운까지 초기화한다.
        /// registeredName은 슬롯 단위 고유 키(예: "slot0:fireball")로, 이름 충돌을 막는다.
        /// </summary>
        public void Configure(GenericAbilityData abilityData, IGameplayContext ownerContext, string registeredName)
        {
            data = abilityData;
            context = ownerContext;
            abilityName = registeredName;

            cooldown = new AbilityCooldown();
            cooldown.Initialize(abilityData != null ? abilityData.cooldownDuration : 0f);

            effect = CreateEffect(abilityData != null ? abilityData.effectType : AbilityEffectType.MeleeArea);
            isExecuting = false;
        }

        public override bool CanExecute(IGameplayContext context)
        {
            if (data == null) return false;
            return ValidateBasicConditions(context);
        }

        public override async Awaitable ExecuteAsync(IGameplayContext context)
        {
            BeginExecution();
            try
            {
                // data 미주입 시 각 효과가 data.* 즉시 역참조로 NRE → 단일 진입점에서 차단.
                if (data == null)
                {
                    Debug.LogWarning($"[GenericAbility] {abilityName}: data 미주입 — 실행 skip");
                    return;
                }

                // 발동음 — 효과 종류 무관 단일 지점 재생(무음 가드). 효과별 세분화는 후속.
                if (data.castSfx != null && AudioManager.HasInstance)
                {
                    AudioManager.Instance.PlaySfx(data.castSfx);
                }

                // 피의 서약 대가 — 효과보다 먼저 치른다(발동이 확정된 뒤라 쿨다운·조건으로 막힌 시도에는 안 든다).
                PayHpCost(context);

                if (effect != null)
                {
                    await effect.ApplyAsync(context, data, destroyCancellationToken);
                }
            }
            finally
            {
                // 예외·취소 경로에서도 실행 플래그 해제 + 쿨다운 시작을 보장(무한 연사 방지).
                EndExecution();
            }
        }

        /// <summary>
        /// 현재 HP × hpCostRatio(내림, 최소 1)를 바친다. <see cref="Abyss.Runtime.Player.PlayerCharacter.PayHpCost"/>가
        /// 최소 1을 남기므로 대가로 죽지 않는다 — HP 1에서도 발동은 되고 대가만 사라진다.
        /// </summary>
        private void PayHpCost(IGameplayContext ownerContext)
        {
            if (data.hpCostRatio <= 0f || ownerContext?.Owner == null) return;
            if (!ownerContext.Owner.TryGetComponent(out Abyss.Runtime.Player.PlayerCharacter player)) return;

            int cost = Mathf.Max(1, Mathf.FloorToInt(player.CurrentHp * data.hpCostRatio));
            player.PayHpCost(cost);
        }

        private static IAbilityEffect CreateEffect(AbilityEffectType type)
        {
            switch (type)
            {
                case AbilityEffectType.Projectile: return new ProjectileEffect();
                case AbilityEffectType.Buff: return new BuffEffect();
                case AbilityEffectType.MeleeArea:
                default: return new MeleeAreaEffect();
            }
        }
    }
}
