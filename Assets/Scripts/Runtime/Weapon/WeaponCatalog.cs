using UnityEngine;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 무기 카탈로그. <c>Resources/Data/Weapons/</c> 의 모든 <see cref="WeaponData"/>를 지연 로드해 캐시한다.
    /// 새 무기는 이 폴더에 에셋만 추가하면 자동 편입 — 에디터 메뉴 재실행이 필요 없다.
    /// (<c>SkillCatalog</c> / <c>MetaUpgrades</c> / <c>RunConfigProvider</c> 와 동형:
    /// 폴더가 SoT, <c>Resources.LoadAll</c> 이 자동 동기화 계층.)
    ///
    /// 🔑 <b>종수를 문서에 고정하지 않는 것이 요구사항이다</b>(§4). 그래서 추첨도 개수에
    /// 끌려가면 안 되고, <see cref="WeaponDraw"/> 가 등급 가중치를 정규화한다.
    /// </summary>
    public static class WeaponCatalog
    {
        private const string RESOURCES_FOLDER = "Data/Weapons";
        private static WeaponData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static WeaponData[] All => all ??= Resources.LoadAll<WeaponData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        // 도메인 리로드 비활성화(Enter Play Mode Options) 대비 정적 캐시 리셋.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all = null;
    }
}
