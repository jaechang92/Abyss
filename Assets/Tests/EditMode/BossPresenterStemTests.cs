using Abyss.Runtime.UI;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 보스 대사 키 줄기(<see cref="BossPresenter.Stem"/>). 틀리면 키가 표에서 안 찾혀 연출이 <b>조용히</b> 빠진다 —
    /// 에러가 아니라 「이름 카드 없이 체력바만」으로 보이므로 여기서 고정한다. 기대값은 GameText.csv의 Boss_* 행.
    /// </summary>
    public sealed class BossPresenterStemTests
    {
        [TestCase("boss_abyss_keeper", "AbyssKeeper")]
        [TestCase("boss_flame_serpent", "FlameSerpent")]
        [TestCase("boss_thronebound", "Thronebound")]
        [TestCase("midboss_sentinel", "Sentinel")]
        [TestCase("midboss_throne_warden", "ThroneWarden")]
        public void Stem_MatchesCsvKeys(string enemyId, string expected)
        {
            Assert.AreEqual(expected, BossPresenter.Stem(enemyId));
        }

        [Test]
        public void Stem_EmptyId_IsEmpty()
        {
            Assert.AreEqual(string.Empty, BossPresenter.Stem(null));
            Assert.AreEqual(string.Empty, BossPresenter.Stem(string.Empty));
        }
    }
}
