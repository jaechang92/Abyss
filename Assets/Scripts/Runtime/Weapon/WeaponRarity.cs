namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 무기 등급 7단계. 위로 갈수록 강하다.
    ///
    /// ⚠️ <b>이름을 이렇게 고른 이유가 있다</b>(<c>14-weapon-equipment-system.md</c> §3-1) —
    /// 초안의 「유물」·「레어」에서 바뀌었다:
    /// <list type="bullet">
    /// <item><b>「유물」을 안 쓴다</b> — <c>RelicData</c>·<c>RelicGacha</c>·로비 좌판이 전부 「유물」이고
    /// 그건 <b>심연 조각으로 뽑는 별개 물건</b>이다. 등급 이름으로 쓰면 화면에서
    /// 「유물 등급 검」과 「유물」이 섞인다 → <b>신화</b>로 대체</item>
    /// <item><b>「희귀」와 「레어」를 같이 안 쓴다</b> — 같은 말이라 어느 쪽이 위인지 못 읽는다 →
    /// 「레어」 자리를 <b>영웅</b>으로</item>
    /// </list>
    /// 🔑 위로 갈수록 커지는 어휘로만 세워 <b>순서가 말로 읽히게</b> 했다.
    ///
    /// 📌 <b>이 순서가 곧 값이다.</b> 중간에 끼워 넣으면 직렬화된 에셋의 등급이 통째로 밀린다 —
    /// 새 등급이 필요하면 <b>끝에 붙이거나</b> 마이그레이션을 쓸 것.
    /// </summary>
    public enum WeaponRarity
    {
        Common = 0,      // 일반
        Uncommon = 1,    // 고급
        Rare = 2,        // 희귀
        Heroic = 3,      // 영웅
        Legendary = 4,   // 전설
        Mythic = 5,      // 신화
        Ancient = 6,     // 고대
    }
}
