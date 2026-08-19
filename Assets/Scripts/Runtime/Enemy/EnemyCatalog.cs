using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 카탈로그. Resources/Data/Enemies/의 모든 EnemyData SO를 지연 로드해 캐시한다.
    /// 새 적은 이 폴더에 에셋만 추가하면 자동 편입 — 에디터 메뉴 재실행이 필요 없다.
    /// (FormCatalog / SkillCatalog / MetaUpgrades와 동형: 폴더가 SoT, Resources.LoadAll이 자동 동기화 계층.)
    ///
    /// 적 SO는 방(RoomData)이 직접 참조하므로 원래 카탈로그가 필요 없었다. 도감(완주 루프 계획 3-1)이
    /// <b>타이틀 씬에서도 열려야</b> 하면서 필요해졌다 — 그 씬에는 StageData·RoomData 참조가 하나도
    /// 로드되지 않으므로 "게임에 존재하는 적 전체"를 아는 수단이 Resources.LoadAll뿐이다.
    /// </summary>
    public static class EnemyCatalog
    {
        private const string RESOURCES_FOLDER = "Data/Enemies";
        private static EnemyData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static EnemyData[] All => all ??= Resources.LoadAll<EnemyData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        /// <summary>enemyId로 적을 조회한다. 미발견 시 null.</summary>
        public static EnemyData GetById(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;

            var catalog = All;
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i] != null && catalog[i].enemyId == enemyId) return catalog[i];
            }
            return null;
        }

        /// <summary>
        /// 도감의 '적' 탭과 '보스' 탭이 같은 카탈로그를 나눠 쓴다.
        /// <b>'보스' 탭은 <see cref="EnemyTier.Boss"/>와 <see cref="EnemyTier.MidBoss"/>를 함께</b> 담는다.
        ///
        /// 전에는 <c>isBoss</c> 하나로 갈랐고, 미드보스는 엘리트 보상 트리거를 재활용하느라
        /// <c>isBoss=0, isElite=1</c>이라 <b>'적' 탭에 엘리트로 나왔다.</b> 그렇다고 <c>isBoss</c>를 켜면
        /// 처치 판정·프리팹 크기·발사체 연결이 함께 바뀐다 — <b>동작 스위치와 분류가 한 플래그에 얹혀 있었다.</b>
        /// 등급을 <see cref="EnemyTier"/>로 뽑고 나서야 도감만 정확해지는 수정이 가능해졌다.
        ///
        /// 이름 규칙(<c>MidBoss*</c>)에는 여전히 기대지 않는다 — 등급은 에셋이 들고 있다.
        /// </summary>
        public static List<EnemyData> GetByBossTab(bool bossTab)
        {
            var result = new List<EnemyData>();
            var catalog = All;

            for (int i = 0; i < catalog.Length; i++)
            {
                var enemy = catalog[i];
                if (enemy == null) continue;
                bool belongsToBossTab = enemy.tier == EnemyTier.Boss || enemy.tier == EnemyTier.MidBoss;
                if (belongsToBossTab != bossTab) continue;
                result.Add(enemy);
            }
            return result;
        }

        // 도메인 리로드 비활성화(Enter Play Mode Options) 대비 정적 캐시 리셋.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all = null;
    }
}
