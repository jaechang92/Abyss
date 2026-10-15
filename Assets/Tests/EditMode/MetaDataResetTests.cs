using System;
using System.Collections.Generic;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 데이터 초기화 후보 생성(<see cref="MetaSaveService.CreateResetCandidate"/>) — 디스크·싱글톤 없이 순수 계산만 본다.
    /// 실제 저장·되돌림은 PlayMode(DataResetPlayModeTests)가 임시 폴더에서 확인한다.
    ///
    /// 묶음마다 「고른 묶음은 새 세이브 값, 고르지 않은 묶음은 원본 값」을 JSON 값으로 비교한다.
    /// 프롤로그 시청 여부는 이야기 묶음 소속이라 도감·기록 묶음을 지워도 남아야 한다.
    /// </summary>
    public sealed class MetaDataResetTests
    {
        private static readonly MetaResetCategory[] Groups =
        {
            MetaResetCategory.Relics,
            MetaResetCategory.Currency,
            MetaResetCategory.PermanentUpgrades,
            MetaResetCategory.Unlocks,
            MetaResetCategory.CodexAndRunRecords,
            MetaResetCategory.StoryAndTutorial,
            MetaResetCategory.Settings
        };

        /// <summary>묶음 하나의 값만 담는 비교용 그릇. 담지 않은 필드는 기본값이라 JSON 비교에서 서로 같다.</summary>
        [Serializable]
        public sealed class Probe
        {
            public List<RelicEntry> relics = new();
            public List<string> equippedRelicIds = new();
            public int abyssShardsTotal;
            public List<MetaUpgradeEntry> upgradeLevels = new();
            public List<string> unlockedFormIds = new();
            public List<string> unlockedSkillIds = new();
            public List<string> discoveredFormIds = new();
            public List<string> discoveredSkillIds = new();
            public List<string> discoveredEnemyIds = new();
            public RunSummary lastRunSummary = new();
            public MetaRecords records = new();
            public List<StoryProgressEntry> storyProgress = new();
            public int storyStage;
            public int storyRunSnapshot;
            public int storyBossSnapshot;
            public List<string> discoveredPassageIds = new();
            public List<string> viewedPassageReactionIds = new();
            public bool hasCompletedFirstPlayTutorial;
            public bool hasSkippedFirstPlayTutorial;
            public bool hasSeenPrologue;
            public bool hasSeenEnding;
            public MetaSettings settings = new();
        }

        private static string ProbeJson(MetaSave save, MetaResetCategory group)
        {
            var probe = new Probe();
            switch (group)
            {
                case MetaResetCategory.Relics:
                    probe.relics = save.relics;
                    probe.equippedRelicIds = save.equippedRelicIds;
                    break;
                case MetaResetCategory.Currency:
                    probe.abyssShardsTotal = save.abyssShardsTotal;
                    break;
                case MetaResetCategory.PermanentUpgrades:
                    probe.upgradeLevels = save.upgradeLevels;
                    break;
                case MetaResetCategory.Unlocks:
                    probe.unlockedFormIds = save.unlockedFormIds;
                    probe.unlockedSkillIds = save.unlockedSkillIds;
                    break;
                case MetaResetCategory.CodexAndRunRecords:
                    probe.discoveredFormIds = save.discoveredFormIds;
                    probe.discoveredSkillIds = save.discoveredSkillIds;
                    probe.discoveredEnemyIds = save.discoveredEnemyIds;
                    probe.lastRunSummary = save.lastRunSummary;
                    probe.records = JsonUtility.FromJson<MetaRecords>(JsonUtility.ToJson(save.records));
                    probe.records.hasSeenPrologue = false;   // 이야기 묶음 소속
                    probe.records.hasSeenEnding = false;
                    break;
                case MetaResetCategory.StoryAndTutorial:
                    probe.storyProgress = save.storyProgress;
                    probe.storyStage = save.storyStage;
                    probe.storyRunSnapshot = save.storyRunSnapshot;
                    probe.storyBossSnapshot = save.storyBossSnapshot;
                    probe.discoveredPassageIds = save.discoveredPassageIds;
                    probe.viewedPassageReactionIds = save.viewedPassageReactionIds;
                    probe.hasCompletedFirstPlayTutorial = save.hasCompletedFirstPlayTutorial;
                    probe.hasSkippedFirstPlayTutorial = save.hasSkippedFirstPlayTutorial;
                    probe.hasSeenPrologue = save.records.hasSeenPrologue;
                    probe.hasSeenEnding = save.records.hasSeenEnding;
                    break;
                case MetaResetCategory.Settings:
                    probe.settings = save.settings;
                    break;
            }
            return JsonUtility.ToJson(probe);
        }

        private static MetaSave CreatePopulatedSave()
        {
            var save = MetaSave.CreateNew();
            save.saveTimeUtc = "2026-01-02T03:04:05.0000000Z";
            save.unlockedFormIds.Add("form_ash");
            save.unlockedSkillIds.Add("skill_ember");
            save.discoveredFormIds.Add("form_ash");
            save.discoveredSkillIds.Add("skill_ember");
            save.discoveredEnemyIds.Add("enemy_wisp");
            save.lastRunSummary.hasRecord = true;
            save.lastRunSummary.enemiesKilled = 17;
            save.lastRunSummary.formsUsed.Add("form_ash");
            save.abyssShardsTotal = 321;
            save.upgradeLevels.Add(new MetaUpgradeEntry { upgradeId = "upgrade_hp", level = 2 });
            save.relics.Add(new RelicEntry { relicId = "relic_eye", level = 3 });
            save.equippedRelicIds.AddRange(new[] { "relic_eye", string.Empty, string.Empty });
            save.storyProgress.Add(new StoryProgressEntry { speakerId = "recorder", viewedChapterStages = { 1, 2 }, runSnapshot = 3, bossSnapshot = 1 });
            save.storyStage = 2;
            save.storyRunSnapshot = 4;
            save.storyBossSnapshot = 1;
            save.discoveredPassageIds.Add("passage_1");
            save.viewedPassageReactionIds.Add("passage_1");
            save.hasCompletedFirstPlayTutorial = true;
            save.hasSkippedFirstPlayTutorial = true;
            save.records.totalRunCount = 9;
            save.records.totalBossKillCount = 2;
            save.records.bestReach = new StageReach { stageNumber = 2, stepNumber = 3, stageName = "Stage 2" };
            save.records.bestRunDurationSeconds = 12.5f;
            save.records.bestGoldShards = 40;
            save.records.hasSeenEnding = true;
            save.records.hasSeenPrologue = true;
            var settings = save.settings;
            settings.masterVolume = 0.25f;
            settings.bgmVolume = 0.5f;
            settings.sfxVolume = 0.75f;
            settings.screenWidth = 1280;
            settings.screenHeight = 720;
            settings.isFullscreen = false;
            settings.language = "English";
            settings.keyboardBindingOverrides = "{\"bindings\":[]}";
            settings.cameraShakeScale = 0.5f;
            settings.reduceBossScreenFlashes = true;
            settings.narrativeTextScale = 1.25f;
            settings.highContrastNarrative = true;
            settings.menuTextScale = 1.5f;
            return save;
        }

        private static IEnumerable<MetaResetCategory> Selections()
        {
            foreach (var group in Groups) yield return group;
            yield return MetaResetCategory.CodexAndRunRecords | MetaResetCategory.StoryAndTutorial;
            yield return MetaResetCategory.Relics | MetaResetCategory.Settings;
            yield return MetaResetCategory.Currency | MetaResetCategory.PermanentUpgrades | MetaResetCategory.Unlocks;
        }

        [TestCaseSource(nameof(Selections))]
        public void SelectedGroupsBecomeFreshAndOthersStayEqual(MetaResetCategory categories)
        {
            var source = CreatePopulatedSave();
            string sourceBefore = JsonUtility.ToJson(source);
            var fresh = MetaSave.CreateNew();

            var candidate = MetaSaveService.CreateResetCandidate(source, categories);

            Assert.AreNotSame(source, candidate);
            foreach (var group in Groups)
            {
                var expected = (categories & group) == group ? fresh : source;
                Assert.AreEqual(ProbeJson(expected, group), ProbeJson(candidate, group), $"{categories} → {group}");
            }
            Assert.AreEqual(source.version, candidate.version, "부분 초기화는 버전을 올리지 않는다.");
            Assert.AreEqual(source.saveTimeUtc, candidate.saveTimeUtc);
            Assert.AreEqual(sourceBefore, JsonUtility.ToJson(source), "원본은 읽기만 한다.");
        }

        [Test]
        public void CodexResetKeepsViewingFlagsAndStoryResetClearsOnlyViewingFlags()
        {
            var source = CreatePopulatedSave();

            var codex = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.CodexAndRunRecords);
            Assert.True(codex.records.hasSeenPrologue);
            Assert.True(codex.records.hasSeenEnding);
            Assert.AreEqual(0, codex.records.totalRunCount);
            Assert.AreEqual(0, codex.records.bestGoldShards);

            var story = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.StoryAndTutorial);
            Assert.False(story.records.hasSeenPrologue);
            Assert.False(story.records.hasSeenEnding);
            Assert.AreEqual(9, story.records.totalRunCount);
            Assert.AreEqual(40, story.records.bestGoldShards);
        }

        [Test]
        public void RelicResetKeepsCurrencyAndCurrencyResetKeepsRelics()
        {
            var source = CreatePopulatedSave();

            var relics = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.Relics);
            Assert.IsEmpty(relics.relics);
            Assert.IsEmpty(relics.equippedRelicIds);
            Assert.AreEqual(321, relics.abyssShardsTotal);

            var currency = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.Currency);
            Assert.AreEqual(0, currency.abyssShardsTotal);
            Assert.AreEqual(1, currency.relics.Count);
            Assert.AreEqual("relic_eye", currency.equippedRelicIds[0]);
        }

        [Test]
        public void AllIsEntirelyFreshIncludingSettingsPrologueRelicsAndVersion()
        {
            var source = CreatePopulatedSave();
            source.version = MetaSave.MinimumVersion;

            var candidate = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.All);

            Assert.AreEqual(JsonUtility.ToJson(MetaSave.CreateNew()), JsonUtility.ToJson(candidate));
            Assert.AreEqual(MetaSave.CurrentVersion, candidate.version);
            Assert.AreEqual(0.7f, candidate.settings.masterVolume);
            Assert.AreEqual(0, candidate.settings.screenWidth);
            Assert.AreEqual(string.Empty, candidate.settings.language);
            Assert.False(candidate.records.hasSeenPrologue);
            Assert.IsEmpty(candidate.relics);
        }

        [Test]
        public void SelectingEverySevenGroupsIsTheSameAsAll()
        {
            var combined = MetaResetCategory.None;
            foreach (var group in Groups) combined |= group;
            Assert.AreEqual(MetaResetCategory.All, combined);
            Assert.AreEqual(JsonUtility.ToJson(MetaSave.CreateNew()),
                JsonUtility.ToJson(MetaSaveService.CreateResetCandidate(CreatePopulatedSave(), combined)));
        }

        [Test]
        public void PartialCandidateSharesNoReferencesWithSource()
        {
            var source = CreatePopulatedSave();
            var candidate = MetaSaveService.CreateResetCandidate(source, MetaResetCategory.Currency);

            Assert.AreNotSame(source.relics, candidate.relics);
            Assert.AreNotSame(source.equippedRelicIds, candidate.equippedRelicIds);
            Assert.AreNotSame(source.settings, candidate.settings);
            Assert.AreNotSame(source.records, candidate.records);
            Assert.AreNotSame(source.storyProgress[0], candidate.storyProgress[0]);
            Assert.AreNotSame(source.storyProgress[0].viewedChapterStages, candidate.storyProgress[0].viewedChapterStages);

            candidate.relics.Clear();
            candidate.settings.masterVolume = 1f;
            candidate.storyProgress[0].viewedChapterStages.Add(9);
            Assert.AreEqual(1, source.relics.Count);
            Assert.AreEqual(0.25f, source.settings.masterVolume);
            Assert.AreEqual(2, source.storyProgress[0].viewedChapterStages.Count);
        }

        [Test]
        public void FreshValuesAreNotSharedBetweenCandidates()
        {
            var first = MetaSaveService.CreateResetCandidate(CreatePopulatedSave(), MetaResetCategory.Relics);
            var second = MetaSaveService.CreateResetCandidate(CreatePopulatedSave(), MetaResetCategory.Relics);
            Assert.AreNotSame(first.relics, second.relics);
        }

        [TestCase(MetaResetCategory.None)]
        [TestCase((MetaResetCategory)(1 << 7))]
        [TestCase(MetaResetCategory.Relics | (MetaResetCategory)(1 << 9))]
        public void InvalidSelectionIsRejected(MetaResetCategory categories)
        {
            Assert.False(MetaSaveService.IsValidResetSelection(categories));
            Assert.Throws<ArgumentException>(() => MetaSaveService.CreateResetCandidate(CreatePopulatedSave(), categories));
        }

        [Test]
        public void NullSourceIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => MetaSaveService.CreateResetCandidate(null, MetaResetCategory.Relics));
        }
    }
}
