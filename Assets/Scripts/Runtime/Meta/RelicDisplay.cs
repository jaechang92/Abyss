using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 유물 등급의 <b>표시 규약</b> SoT. 상점 패널과 도감이 같은 색을 쓴다.
    ///
    /// 색을 패널 안에 두지 않은 이유가 둘이다.
    /// ① 같은 등급이 화면마다 다른 색이면 플레이어가 등급을 색으로 읽을 수 없다.
    /// ② 도감(<c>Abyss.Runtime.UI</c>)이 색 하나 때문에 로비 패널을 참조하게 되면
    ///    <b>의존 방향이 뒤집힌다</b> — 도감은 로비를 몰라야 한다.
    ///
    /// <c>Abyss.Runtime.Draft.SkillDisplay</c>와 같은 자리·같은 역할이다(데이터 옆에 표시 규약).
    /// 색 계열도 스킬 등급과 맞춰 "흔한 것 → 귀한 것"의 방향이 화면 전체에서 일치하게 한다.
    /// </summary>
    public static class RelicDisplay
    {
        private static readonly Color Common = new(0.30f, 0.34f, 0.40f);
        private static readonly Color Rare = new(0.24f, 0.40f, 0.58f);
        private static readonly Color Epic = new(0.44f, 0.28f, 0.56f);

        /// <summary>등급 배경색. 정의되지 않은 등급은 Common 으로 본다(색이 사라지는 것보다 낫다).</summary>
        public static Color RarityColor(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Rare => Rare,
            RelicRarity.Epic => Epic,
            _ => Common
        };
    }
}
