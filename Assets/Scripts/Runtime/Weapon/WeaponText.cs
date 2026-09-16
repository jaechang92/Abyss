namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 무기를 화면에 부르는 말의 단일 출처(SoT).
    ///
    /// 🔑 <b>부르는 곳이 셋이다</b> — 제단 프롬프트 · 상점 진열 · 가차 결과(§3-B 획득 3창구).
    /// 셋이 각자 문자열을 만들면 같은 무기가 자리마다 다르게 읽힌다. 특히
    /// <b>「이미 가진 것」을 알리는 말</b>이 갈리면 중복이 어디서는 강화로, 어디서는 꽝으로 보인다.
    /// 이 저장소가 UI 정렬 순위·화폐 표기에서 이미 겪은 파편화 모양이라 여기로 모은다.
    ///
    /// ⚠️ <b>Unity 타입을 안 쓴다</b>(<see cref="WeaponData"/> 참조뿐) — 씬 없이 테스트한다.
    /// </summary>
    public static class WeaponText
    {
        /// <summary>
        /// 화면에 찍을 이름. <see cref="WeaponData.displayName"/>이 비면 <c>weaponId</c>로 물러난다 —
        /// 빈 칸으로 두면 값을 못 읽은 것처럼 보인다(<c>ShopItem.CostText</c>가 「무료」로 물러나는 것과 같은 판단).
        /// </summary>
        public static string NameOf(WeaponData weapon)
        {
            if (weapon == null) return "무기";
            return string.IsNullOrEmpty(weapon.displayName) ? weapon.weaponId : weapon.displayName;
        }

        /// <summary>
        /// 「무엇이 일어나는가」 한 낱말. 🔴 <b>중복이 꽝으로 읽히면 안 된다</b> —
        /// 무기는 중복이 강화이고(§7), 그 사실이 말로 보여야 살 이유·주울 이유가 남는다.
        /// </summary>
        public static string VerbFor(bool owned) => owned ? "강화" : "획득";

        /// <summary>제단 프롬프트. 예: <c>"녹슨 검 강화 (G)"</c></summary>
        public static string AltarPrompt(WeaponData weapon, bool owned)
            => $"{NameOf(weapon)} {VerbFor(owned)} (G)";

        /// <summary>
        /// 무기를 손에 넣은 뒤 알리는 한 줄. 가차처럼 <b>결과 문구가 고정</b>인 자리에 덧붙는다 —
        /// 에셋에 미리 적은 문장은 「어느 무기」를 못 담기 때문이다(<c>EventEffect</c>와 같은 한계).
        /// 예: <c>"녹슨 검을(를) 손에 넣었다."</c>
        /// </summary>
        public static string GainLine(WeaponData weapon, bool wasNew)
        {
            string name = NameOf(weapon);
            return wasNew ? $"{name}을(를) 손에 넣었다." : $"{name}이(가) 한 단계 벼려졌다.";
        }

        /// <summary>
        /// 상점 진열의 설명 꼬리표. 이미 가진 무기면 <b>사면 강화된다</b>는 것을 밝힌다.
        /// 설명이 비면 폼 전용이라는 최소 정보라도 준다.
        /// </summary>
        public static string ShopDescription(WeaponData weapon, bool owned)
        {
            string body = weapon != null && !string.IsNullOrEmpty(weapon.description)
                ? weapon.description
                : "이 폼이 드는 무기다.";
            return owned ? $"{body} (보유 중 — 사면 강화된다)" : body;
        }
    }
}
