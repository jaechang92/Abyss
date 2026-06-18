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

        [Header("Buff")]
        [Tooltip("자기 회복량(Buff).")]
        [Min(0)] public int healAmount = 25;

        [Header("연출")]
        [Tooltip("발동 시 원형 링 이펙트 표시 여부.")]
        public bool showHitEffect = true;

        [Tooltip("이펙트 색상.")]
        public Color effectColor = new(1f, 0.6f, 0.2f, 1f);

        public override bool Validate()
        {
            if (!base.Validate()) return false;

            if (effectType == AbilityEffectType.Projectile && projectilePrefab == null)
            {
                Debug.LogWarning($"[GenericAbilityData] {abilityName}: Projectile 타입인데 projectilePrefab이 비어있습니다.");
            }
            return true;
        }
    }
}
