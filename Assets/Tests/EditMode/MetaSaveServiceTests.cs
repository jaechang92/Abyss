using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// MetaSaveService EditMode 테스트.
    /// SetUp에서 GameObject + AddComponent로 인스턴스를 직접 만들고 ResetAll(autoSave: false)로
    /// 메모리만 초기화 → 디스크 I/O 없이 게이트웨이 API를 검증한다.
    /// SaveSystem과의 실제 협력 (디스크 라운드트립)은 MetaSaveServicePlayModeSmoke가 담당.
    /// </summary>
    public sealed class MetaSaveServiceTests
    {
        private MetaSaveService service;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("[Test] MetaSaveService");
            service = go.AddComponent<MetaSaveService>();
            // 이전 테스트 잔여물 및 디스크 상태 무시 — 메모리 인스턴스만 깨끗하게 시작.
            service.ResetAll(autoSave: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null) Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void AddAbyssShards_Positive_AccumulatesTotal()
        {
            service.AddAbyssShards(50, autoSave: false);
            service.AddAbyssShards(30, autoSave: false);
            Assert.AreEqual(80, service.Current.abyssShardsTotal);
        }

        [Test]
        public void AddAbyssShards_NonPositive_NoChange()
        {
            service.AddAbyssShards(20, autoSave: false);
            service.AddAbyssShards(0, autoSave: false);
            service.AddAbyssShards(-5, autoSave: false);
            Assert.AreEqual(20, service.Current.abyssShardsTotal);
        }

        [Test]
        public void UnlockForm_NewId_AddsAndReturnsTrue()
        {
            bool added = service.UnlockForm("dark_blade", autoSave: false);
            Assert.IsTrue(added);
            CollectionAssert.Contains(service.Current.unlockedFormIds, "dark_blade");
        }

        [Test]
        public void UnlockForm_Duplicate_IgnoredAndReturnsFalse()
        {
            service.UnlockForm("dark_blade", autoSave: false);
            bool added = service.UnlockForm("dark_blade", autoSave: false);
            Assert.IsFalse(added);
            Assert.AreEqual(1, service.Current.unlockedFormIds.Count);
        }

        [Test]
        public void UnlockForm_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(service.UnlockForm(null, autoSave: false));
            Assert.IsFalse(service.UnlockForm(string.Empty, autoSave: false));
            Assert.IsEmpty(service.Current.unlockedFormIds);
        }

        [Test]
        public void UnlockSkill_NewId_AddsAndReturnsTrue()
        {
            bool added = service.UnlockSkill("ember_strike", autoSave: false);
            Assert.IsTrue(added);
            CollectionAssert.Contains(service.Current.unlockedSkillIds, "ember_strike");
        }

        [Test]
        public void UnlockSkill_Duplicate_IgnoredAndReturnsFalse()
        {
            service.UnlockSkill("ember_strike", autoSave: false);
            bool added = service.UnlockSkill("ember_strike", autoSave: false);
            Assert.IsFalse(added);
            Assert.AreEqual(1, service.Current.unlockedSkillIds.Count);
        }

        [Test]
        public void UnlockSkill_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(service.UnlockSkill(null, autoSave: false));
            Assert.IsFalse(service.UnlockSkill(string.Empty, autoSave: false));
            Assert.IsEmpty(service.Current.unlockedSkillIds);
        }

        [Test]
        public void RecordRunResult_IncrementsRunCount()
        {
            service.RecordRunResult("stage_2", 120f, 150, 1, autoSave: false);
            service.RecordRunResult("stage_3", 90f, 200, 0, autoSave: false);
            Assert.AreEqual(2, service.Current.records.totalRunCount);
        }

        [Test]
        public void RecordRunResult_KeepsMaxDurationAndGold()
        {
            service.RecordRunResult("stage_1", 60f, 100, 0, autoSave: false);
            service.RecordRunResult("stage_2", 30f, 200, 0, autoSave: false);
            service.RecordRunResult("stage_3", 90f, 50, 0, autoSave: false);

            var r = service.Current.records;
            Assert.AreEqual(90f, r.bestRunDurationSeconds);
            Assert.AreEqual(200, r.bestGoldShards);
        }

        [Test]
        public void RecordRunResult_AggregatesBossKills_ClampsNegative()
        {
            service.RecordRunResult("stage_1", 60f, 100, 1, autoSave: false);
            service.RecordRunResult("stage_2", 60f, 100, 2, autoSave: false);
            // 음수 bossKillDelta는 0으로 클램프 — 누적 감소 방지.
            service.RecordRunResult("stage_3", 60f, 100, -3, autoSave: false);
            Assert.AreEqual(3, service.Current.records.totalBossKillCount);
        }

        [Test]
        public void RecordRunResult_OverwritesStageId_OnlyWhenNonEmpty()
        {
            service.RecordRunResult("stage_1", 30f, 100, 0, autoSave: false);
            service.RecordRunResult(string.Empty, 30f, 100, 0, autoSave: false);
            Assert.AreEqual("stage_1", service.Current.records.bestStageId);
        }

        [Test]
        public void UpdateSettings_ClampsToUnitRange()
        {
            service.UpdateSettings(1.5f, -0.2f, 0.5f, autoSave: false);
            var s = service.Current.settings;
            Assert.AreEqual(1f, s.masterVolume);
            Assert.AreEqual(0f, s.bgmVolume);
            Assert.AreEqual(0.5f, s.sfxVolume);
        }

        [Test]
        public void ResetAll_ClearsAllState()
        {
            service.AddAbyssShards(100, autoSave: false);
            service.UnlockForm("dark_blade", autoSave: false);
            service.UnlockSkill("ember_strike", autoSave: false);
            service.RecordRunResult("stage_1", 60f, 200, 1, autoSave: false);

            service.ResetAll(autoSave: false);

            Assert.AreEqual(0, service.Current.abyssShardsTotal);
            Assert.IsEmpty(service.Current.unlockedFormIds);
            Assert.IsEmpty(service.Current.unlockedSkillIds);
            Assert.AreEqual(0, service.Current.records.totalRunCount);
            Assert.AreEqual(0, service.Current.records.totalBossKillCount);
        }

        [Test]
        public void IsLoaded_AfterResetAll_IsTrue()
        {
            // ResetAll은 EnsureLoaded를 거치지 않고 직접 isLoaded=true로 설정한다 — 디스크 우회의 핵심.
            Assert.IsTrue(service.IsLoaded);
        }
    }
}
