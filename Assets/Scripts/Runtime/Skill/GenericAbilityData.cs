using Abyss.Runtime.Combat;
using GAS.Core;
using UnityEngine;

namespace Abyss.Runtime.Skill
{
    /// <summary>
    /// 데이터 구동 범용 어빌리티 정의. AbilityData(이름·쿨다운·실행조건)를 상속하고
    /// effectType + 효과별 수치를 추가한다. SkillData.relatedAbility에 이 SO를 연결하면
    /// 드래프트 Active 스킬이 실제 어빌리티로 실행된다.
    /// 단일 GenericAbility가 effectType으로 효과 핸들러를 분기하므로, 어빌리티마다
    /// 클래스를 새로 만들 필요 없이 SO만 추가하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "GenericAbilityData", menuName = "Abyss/Data/Generic Ability")]
    public sealed class GenericAbilityData : AbilityData
    {
        [Header("효과 타입")]
        public AbilityEffectType effectType = AbilityEffectType.MeleeArea;

        [Header("공통 수치")]
        [Tooltip("적에게 가하는 피해(MeleeArea/Projectile).")]
        [Min(0)] public int damage = 20;

        [Header("MeleeArea")]
        [Tooltip("전방 타격 박스 크기.")]
        public Vector2 meleeBoxSize = new(2.2f, 1.6f);

        [Tooltip("플레이어 중심에서 전방으로 박스를 밀어내는 거리.")]
        [Min(0f)] public float meleeForwardOffset = 1.1f;

        [Header("Projectile")]
        [Tooltip("발사할 발사체 프리팹. 미연결 시 Projectile 효과는 무동작.")]
        public Projectile projectilePrefab;

        [Min(0f)] public float projectileSpeed = 12f;
        [Min(0f)] public float projectileLifetime = 2f;

        [Tooltip("플레이어 중심에서 발사체 생성 지점까지의 전방 오프셋.")]
        [Min(0f)] public float projectileSpawnOffset = 0.6f;

        [Tooltip("한 번 발동에 발사할 발사체 수. 2 이상이면 부채꼴로 분산 발사.")]
        [Min(1)] public int projectileCount = 1;

        [Tooltip("다발 발사 시 전체 부채꼴 확산각(도). 0이면 모두 직선. projectileCount가 1이면 무시.")]
        [Min(0f)] public float projectileSpreadAngle = 0f;

        [Header("Buff")]
        [Tooltip("자기 회복량(Buff). 즉시 적용.")]
        [Min(0)] public int healAmount = 25;

        [Tooltip("이동속도 배율(Buff, 1 = 변화 없음). buffDuration 동안 적용.")]
        [Min(0f)] public float buffMoveSpeedMultiplier = 1f;

        [Tooltip("공격력 배율(Buff, 1 = 변화 없음). buffDuration 동안 적용.")]
        [Min(0f)] public float buffAttackMultiplier = 1f;

        [Tooltip("받는 피해 배율(Buff, 1 = 변화 없음, <1 = 피해 감소). buffDuration 동안 적용. 방패병 '철벽 방어' 등 방어 스킬용.")]
        [Min(0f)] public float buffDefenseMultiplier = 1f;

        [Tooltip("버프 지속시간(초, 0 = 즉시 효과(Heal)만).")]
        [Min(0f)] public float buffDuration = 0f;

        [Header("연소 (불꽃 축)")]
        [Tooltip("명중한 적에게 부여할 연소 스택. 0이면 연소를 걸지 않는다(불꽃 축이 아닌 스킬).")]
        [Min(0)] public int burnStacks = 0;

        [Tooltip("연소 지속시간(초).")]
        [Min(0f)] public float burnDuration = 4f;

        [Tooltip("연소 스택 1개가 1초에 주는 피해. 실제 값은 연소 강화(Passive) 배율이 곱해져 확정된다.")]
        [Min(0f)] public float burnDamagePerStack = 2f;

        [Header("연출")]
        [Tooltip("발동 시 원형 링 이펙트 표시 여부.")]
        public bool showHitEffect = true;

        [Tooltip("이펙트 색상.")]
        public Color effectColor = new(1f, 0.6f, 0.2f, 1f);

        [Tooltip("발동음. 비어 있으면 무음(ContentBuilder.WireAbilitySfx가 자동 연결).")]
        public AudioClip castSfx;

        public override bool Validate()
        {
            if (!base.Validate()) return false;

            if (effectType == AbilityEffectType.Projectile && projectilePrefab == null)
            {
                Debug.LogWarning($"[GenericAbilityData] {abilityName}: Projectile 타입인데 projectilePrefab이 비어있습니다.");
            }
            return true;
        }

        /// <summary>
        /// 이 어빌리티가 부여할 연소 명세. burnStacks가 0이면 <see cref="BurnPayload.HasBurn"/>이 false라
        /// 소비처(근접·발사체 효과)가 별도 분기 없이 그대로 넘겨도 된다.
        /// </summary>
        public BurnPayload BuildBurnPayload() => new(burnStacks, burnDuration, burnDamagePerStack);
    }
}
