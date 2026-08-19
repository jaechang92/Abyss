using System.Linq;
using Abyss.Runtime.Enemy;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 적 등급의 불변식. 개별 적의 수치가 아니라 <b>등급이 동작과 표시를 올바로 갈라 주는가</b>를 본다.
    ///
    /// 이 테스트가 생긴 계기: <c>isElite</c>·<c>isBoss</c> 두 bool이 <b>상호배타적 4단계</b>
    /// (일반/엘리트/중간보스/보스)를 표현하려 하고 있었다. 중간보스는 엘리트 보상 트리거를
    /// 재활용하려고 <c>isElite=1</c>로 둔 것이지 엘리트라서가 아니었는데,
    /// <b>분류를 묻는 소비자(도감)가 그 플래그를 읽어</b> 미드보스를 '적' 탭의 엘리트로 내보냈다.
    ///
    /// 고치겠다고 <c>isBoss</c>를 켜면 처치 판정·프리팹 크기·발사체 연결까지 함께 바뀐다 —
    /// <b>동작 스위치와 분류가 한 플래그에 얹혀 있었다.</b> 그래서 등급을 <see cref="EnemyTier"/>로 뽑고
    /// 동작 스위치를 파생시켰다. 이 테스트가 지키는 것은 그 파생 규칙이다.
    ///
    /// ⚠️ <c>Resources/Data/Enemies</c>의 <b>실제 에셋</b>을 읽는다.
    /// 새 적을 추가했다면 <c>Tools > Abyss > Generate Content</c>를 먼저 실행할 것.
    /// </summary>
    public sealed class EnemyTierTests
    {
        private EnemyData[] catalog;

        [SetUp]
        public void SetUp()
        {
            EnemyCatalog.Reload();
            catalog = EnemyCatalog.All;
            Assert.IsNotNull(catalog, "EnemyCatalog가 비었다 — Resources/Data/Enemies 경로 확인.");
            Assert.Greater(catalog.Length, 0);
        }

        // ───────────────────────── 파생 규칙 ─────────────────────────

        [Test]
        public void 보스_처치_판정은_최종보스에만_붙는다()
        {
            foreach (var enemy in catalog)
            {
                Assert.AreEqual(enemy.tier == EnemyTier.Boss, enemy.IsBoss,
                    $"{enemy.enemyId}: IsBoss는 tier == Boss와 같아야 한다.");
            }
        }

        /// <summary>
        /// 🔴 이 규칙이 개정 전 동작을 그대로 보존한다. 중간보스가 엘리트 보상을 못 받게 되면
        /// <b>드래프트가 한 번 사라지는데 오류는 안 난다.</b>
        /// </summary>
        [Test]
        public void 엘리트_보상은_엘리트와_중간보스_둘_다_받는다()
        {
            foreach (var enemy in catalog)
            {
                bool expected = enemy.tier == EnemyTier.Elite || enemy.tier == EnemyTier.MidBoss;
                Assert.AreEqual(expected, enemy.IsElite,
                    $"{enemy.enemyId}: 중간보스도 엘리트 보상 트리거를 재활용한다.");
            }
        }

        [Test]
        public void 중간보스는_보스_처치_수에_들어가지_않는다()
        {
            var midBosses = catalog.Where(e => e.tier == EnemyTier.MidBoss).ToArray();
            Assert.IsNotEmpty(midBosses, "중간보스 에셋이 하나도 없다 — 등급 이관이 빠졌을 수 있다.");

            foreach (var enemy in midBosses)
            {
                Assert.IsFalse(enemy.IsBoss,
                    $"{enemy.enemyId}: 보스 처치 수는 기록자 챕터 해금 조건이라 중간보스로 오르면 연재가 무너진다.");
                Assert.IsTrue(enemy.IsElite, $"{enemy.enemyId}: 엘리트 보상은 그대로 받아야 한다.");
            }
        }

        // ───────────────────────── 도감 분류 ─────────────────────────

        /// <summary>🔴 이 결손을 고치려고 등급을 뽑았다.</summary>
        [Test]
        public void 중간보스는_보스_탭에_나온다()
        {
            var bossTab = EnemyCatalog.GetByBossTab(bossTab: true);
            var enemyTab = EnemyCatalog.GetByBossTab(bossTab: false);

            foreach (var enemy in catalog.Where(e => e.tier == EnemyTier.MidBoss))
            {
                Assert.Contains(enemy, bossTab, $"{enemy.enemyId}는 '보스' 탭에 있어야 한다.");
                Assert.IsFalse(enemyTab.Contains(enemy), $"{enemy.enemyId}가 '적' 탭에 남아 있다.");
            }
        }

        [Test]
        public void 두_탭이_카탈로그를_빠짐없이_나눈다()
        {
            var bossTab = EnemyCatalog.GetByBossTab(bossTab: true);
            var enemyTab = EnemyCatalog.GetByBossTab(bossTab: false);

            Assert.AreEqual(catalog.Length, bossTab.Count + enemyTab.Count,
                "두 탭의 합이 카탈로그와 다르다 — 어느 탭에도 안 나오는 적이 생겼다.");
            Assert.IsEmpty(bossTab.Intersect(enemyTab), "두 탭에 동시에 나오는 적이 있다.");
        }

        // ───────────────────────── 에셋 구성 ─────────────────────────

        /// <summary>
        /// 등급을 안 정한 에셋이 있으면 조용히 `Normal`로 떨어진다(enum 0). 오류가 안 나므로 여기서 잡는다.
        /// </summary>
        [Test]
        public void 이름이_MidBoss로_시작하는_적은_등급도_중간보스다()
        {
            foreach (var enemy in catalog)
            {
                if (enemy.enemyId == null || !enemy.enemyId.StartsWith("midboss_")) continue;
                Assert.AreEqual(EnemyTier.MidBoss, enemy.tier,
                    $"{enemy.enemyId}: id는 미드보스인데 tier가 다르다(이관 누락 의심).");
            }
        }

        [Test]
        public void 이름이_boss로_시작하는_적은_등급도_보스다()
        {
            foreach (var enemy in catalog)
            {
                if (enemy.enemyId == null || !enemy.enemyId.StartsWith("boss_")) continue;
                Assert.AreEqual(EnemyTier.Boss, enemy.tier,
                    $"{enemy.enemyId}: id는 보스인데 tier가 다르다(이관 누락 의심).");
            }
        }

        [Test]
        public void 등급별로_적어도_하나씩은_있다()
        {
            foreach (EnemyTier tier in System.Enum.GetValues(typeof(EnemyTier)))
            {
                Assert.IsTrue(catalog.Any(e => e.tier == tier),
                    $"{tier} 등급의 적이 하나도 없다 — 등급을 늘렸으면 콘텐츠도 늘려야 한다.");
            }
        }
    }
}
