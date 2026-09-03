using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 유물 카탈로그. Resources/Data/Relics/의 모든 SO를 지연 로드해 캐시한다.
    /// 새 유물은 이 폴더에 에셋만 추가하면 자동 편입 — 에디터 메뉴 재실행이 필요 없다.
    /// (<see cref="MetaUpgrades"/> / FormCatalog / SkillCatalog와 동형: 폴더가 SoT.)
    /// </summary>
    public static class RelicCatalog
    {
        private const string RESOURCES_FOLDER = "Data/Relics";
        private static RelicData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static RelicData[] All => all ??= Resources.LoadAll<RelicData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        /// <summary>relicId로 유물을 조회한다. 미발견 시 null.</summary>
        public static RelicData GetById(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return null;

            var catalog = All;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i] != null && catalog[i].relicId == relicId) return catalog[i];
            }
            return null;
        }

        // 도메인 리로드 비활성화(Enter Play Mode Options) 대비 정적 캐시 리셋.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all = null;
    }
}
