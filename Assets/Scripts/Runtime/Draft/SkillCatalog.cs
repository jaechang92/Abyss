using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 드래프트 스킬 카탈로그. Resources/Data/Skills/의 모든 SkillData SO를 지연 로드해 캐시한다.
    /// 새 스킬은 이 폴더에 에셋만 추가하면 자동 편입 — 에디터 메뉴 재실행이 필요 없다.
    /// (MetaUpgrades / RunConfigProvider와 동형: 폴더가 SoT, Resources.LoadAll이 자동 동기화 계층.)
    /// </summary>
    public static class SkillCatalog
    {
        private const string RESOURCES_FOLDER = "Data/Skills";
        private static SkillData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static SkillData[] All => all ??= Resources.LoadAll<SkillData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        // 도메인 리로드 비활성화(Enter Play Mode Options) 대비 정적 캐시 리셋.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all = null;
    }
}
