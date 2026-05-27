using Abyss.Runtime.Run;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// RunStats POCO EditMode 테스트.
    /// GetDominantFormId · GetFormRatio · Reset · FormExclusiveDraftRatio 가드.
    /// </summary>
    public sealed class RunStatsTests
    {
        [Test]
        public void GetDominantFormId_EmptyStats_ReturnsEmpty()
        {
            var stats = new RunStats();
            Assert.AreEqual(string.Empty, stats.GetDominantFormId());
        }

        [Test]
        public void GetDominantFormId_PicksMaxPlaytime()
        {
            var stats = new RunStats();
            stats.formPlaytimeSeconds["dark_blade"] = 12.5f;
            stats.formPlaytimeSeconds["void_archer"] = 30f;
            Assert.AreEqual("void_archer", stats.GetDominantFormId());
        }

        [Test]
        public void GetFormRatio_ZeroElapsed_ReturnsZero()
        {
            var stats = new RunStats();
            stats.formPlaytimeSeconds["dark_blade"] = 10f;
            Assert.AreEqual(0f, stats.GetFormRatio("dark_blade"));
        }

        [Test]
        public void GetFormRatio_NullOrEmptyForm_ReturnsZero()
        {
            var stats = new RunStats();
            stats.totalElapsedSeconds = 30f;
            stats.formPlaytimeSeconds["dark_blade"] = 15f;
            Assert.AreEqual(0f, stats.GetFormRatio(string.Empty));
            Assert.AreEqual(0f, stats.GetFormRatio(null));
        }

        [Test]
        public void GetFormRatio_ReturnsPlaytimeOverElapsed()
        {
            var stats = new RunStats();
            stats.totalElapsedSeconds = 40f;
            stats.formPlaytimeSeconds["dark_blade"] = 10f;
            Assert.AreEqual(0.25f, stats.GetFormRatio("dark_blade"), 0.0001f);
        }

        [Test]
        public void GetFormRatio_UnknownForm_ReturnsZero()
        {
            var stats = new RunStats();
            stats.totalElapsedSeconds = 40f;
            Assert.AreEqual(0f, stats.GetFormRatio("nonexistent_form"));
        }

        [Test]
        public void FormExclusiveDraftRatio_ZeroTotal_ReturnsZero()
        {
            var stats = new RunStats();
            Assert.AreEqual(0f, stats.FormExclusiveDraftRatio);
        }

        [Test]
        public void FormExclusiveDraftRatio_CalculatesCorrectly()
        {
            var stats = new RunStats();
            stats.totalDraftCount = 5;
            stats.formExclusiveDraftCount = 2;
            Assert.AreEqual(0.4f, stats.FormExclusiveDraftRatio, 0.0001f);
        }

        [Test]
        public void Reset_ClearsAllFields()
        {
            var stats = new RunStats
            {
                enemiesKilled = 10,
                maxCombo = 5,
                totalElapsedSeconds = 90f,
                stageReached = "stage_3",
                totalDraftCount = 4,
                formExclusiveDraftCount = 1
            };
            stats.formsUsed.Add("dark_blade");
            stats.draftedSkillIds.Add("ember_strike");
            stats.formPlaytimeSeconds["dark_blade"] = 50f;

            stats.Reset();

            Assert.AreEqual(0, stats.enemiesKilled);
            Assert.AreEqual(0, stats.maxCombo);
            Assert.AreEqual(0f, stats.totalElapsedSeconds);
            Assert.AreEqual(string.Empty, stats.stageReached);
            Assert.IsEmpty(stats.formsUsed);
            Assert.IsEmpty(stats.draftedSkillIds);
            Assert.IsEmpty(stats.formPlaytimeSeconds);
            Assert.AreEqual(0, stats.totalDraftCount);
            Assert.AreEqual(0, stats.formExclusiveDraftCount);
        }
    }
}
