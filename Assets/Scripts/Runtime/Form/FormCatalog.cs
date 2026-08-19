using System.Collections.Generic;
using Abyss.Runtime.Meta;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 폼 카탈로그. Resources/Data/Forms/의 모든 FormData SO를 지연 로드해 캐시한다.
    /// 새 폼은 이 폴더에 에셋만 추가하면 자동 편입 — 에디터 메뉴 재실행이 필요 없다.
    /// (SkillCatalog / MetaUpgrades / RunConfigProvider와 동형: 폴더가 SoT, Resources.LoadAll이 자동 동기화 계층.)
    /// </summary>
    public static class FormCatalog
    {
        private const string RESOURCES_FOLDER = "Data/Forms";
        private static FormData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static FormData[] All => all ??= Resources.LoadAll<FormData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        /// <summary>formId로 폼을 조회한다. 미발견 시 null.</summary>
        public static FormData GetById(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return null;

            var catalog = All;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i] != null && catalog[i].formId == formId) return catalog[i];
            }
            return null;
        }

        /// <summary>
        /// 제외 목록에 없는 폼들을 반환한다(미보유 폼 우선 제시용).
        /// 결과가 비면 빈 리스트를 돌려주므로 호출부가 전체 폴백을 결정한다.
        ///
        /// <b>메타 해금이 필요한 폼은 해금 전까지 안 나온다</b>(3-2). 잠금은 <b>옵트인</b>이라
        /// <c>requiresMetaUnlock</c>이 false인 폼 — 지금 4종 전부 — 은 그대로 나온다.
        /// </summary>
        public static List<FormData> GetExcluding(IReadOnlyList<FormData> excluded)
        {
            var result = new List<FormData>();
            var catalog = All;
            var meta = MetaSaveService.Instance;

            for (int i = 0; i < catalog.Length; i++)
            {
                var form = catalog[i];
                if (form == null) continue;
                if (Contains(excluded, form)) continue;
                // meta가 없으면(에디터 단독 플레이 등) 잠긴 폼만 빼고 나머지는 그대로 쓴다.
                if (form.requiresMetaUnlock && (meta == null || !meta.IsFormUnlocked(form))) continue;
                result.Add(form);
            }
            return result;
        }

        private static bool Contains(IReadOnlyList<FormData> list, FormData form)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == form) return true;
            }
            return false;
        }

        // 도메인 리로드 비활성화(Enter Play Mode Options) 대비 정적 캐시 리셋.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all = null;
    }
}
