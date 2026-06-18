namespace Abyss.Runtime.Skill
{
    /// <summary>
    /// 범용 어빌리티(GenericAbility)가 데이터로 분기하는 효과 타입.
    /// GenericAbilityData.effectType이 이 값을 들고, GenericAbility가 대응 IAbilityEffect로 위임한다.
    /// 효과 추가 시 enum 값 + 효과 핸들러 파일만 늘리면 되어 500줄 규칙을 자연히 충족한다.
    /// </summary>
    public enum AbilityEffectType
    {
        /// <summary>전방 박스 광역 근접 타격(프리팹 불필요, 즉시 플레이 가능).</summary>
        MeleeArea,

        /// <summary>전방으로 발사체 발사(GenericAbilityData.projectilePrefab 필요).</summary>
        Projectile,

        /// <summary>자기 자신 대상 효과(체력 회복 등).</summary>
        Buff
    }
}
