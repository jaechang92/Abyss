namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 폼의 기본 공격 방식. 무기가 아니라 <b>폼</b>이 정한다
    /// (<c>14-weapon-equipment-system.md</c> §8-3 「무기는 얼마나 세게, 폼은 어떤 동작」).
    ///
    /// 🔴 <b>순서가 곧 직렬화 값이다.</b> 기존 폼 에셋은 필드가 없어 0 = <see cref="Melee"/> 로 읽힌다 —
    /// 앞에 값을 끼우면 검사·방패가 조용히 원거리가 된다.
    /// </summary>
    public enum FormAttackStyle
    {
        /// <summary>몸 앞 판정 박스(<c>PlayerCharacter.attackBoxSize</c>).</summary>
        Melee = 0,

        /// <summary>발사체(<see cref="RangedAttackSpec"/>).</summary>
        Ranged = 1
    }
}
