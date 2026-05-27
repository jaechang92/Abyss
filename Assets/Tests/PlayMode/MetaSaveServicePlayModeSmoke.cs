using System.Collections;
using System.IO;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// MetaSaveService PlayMode 통합 스모크.
    /// SaveSystem과 협력해 디스크 라운드트립까지 검증한다.
    /// 사용자 데이터 보호: SetUp에서 abyss_meta.json을 백업하고 TearDown에서 원상 복구한다.
    /// </summary>
    public sealed class MetaSaveServicePlayModeSmoke
    {
        private string savePath;
        private string backupPath;
        private bool hadOriginal;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            backupPath = savePath + ".testbackup";

            hadOriginal = File.Exists(savePath);
            if (hadOriginal && !File.Exists(backupPath)) File.Copy(savePath, backupPath, overwrite: true);
            if (File.Exists(savePath)) File.Delete(savePath);

            // 메모리 상 인스턴스도 깨끗하게 시작. ResetAll(false)는 디스크를 건드리지 않는다.
            MetaSaveService.Instance.ResetAll(autoSave: false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath)) File.Delete(savePath);

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                if (hadOriginal) File.Move(backupPath, savePath);
                else File.Delete(backupPath);
            }

            if (MetaSaveService.HasInstance) MetaSaveService.Instance.ResetAll(autoSave: false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_SingletonsAvailable()
        {
            Assert.IsNotNull(SaveSystem.Instance, "SaveSystem 싱글톤이 생성되어야 한다.");
            Assert.IsNotNull(MetaSaveService.Instance, "MetaSaveService 싱글톤이 생성되어야 한다.");
            yield return null;
            Assert.IsTrue(MetaSaveService.Instance.IsLoaded, "ResetAll 후 IsLoaded는 true.");
        }

        [UnityTest]
        public IEnumerator AddAbyssShards_AutoSave_PersistsToDisk()
        {
            MetaSaveService.Instance.AddAbyssShards(75, autoSave: true);
            yield return null;

            Assert.IsTrue(File.Exists(savePath), "abyss_meta.json이 디스크에 생성되어야 한다.");

            // 강제 재로드 — 디스크 → MetaSave 라운드트립 검증.
            var reloaded = MetaSaveService.Instance.Reload();
            Assert.AreEqual(75, reloaded.abyssShardsTotal);
        }

        [UnityTest]
        public IEnumerator UnlockFormAndSkill_RoundTrip()
        {
            var svc = MetaSaveService.Instance;
            svc.UnlockForm("dark_blade", autoSave: true);
            svc.UnlockSkill("ember_strike", autoSave: true);
            yield return null;

            var reloaded = svc.Reload();
            CollectionAssert.Contains(reloaded.unlockedFormIds, "dark_blade");
            CollectionAssert.Contains(reloaded.unlockedSkillIds, "ember_strike");
        }

        [UnityTest]
        public IEnumerator RecordRunResult_AggregatesAndPersists()
        {
            var svc = MetaSaveService.Instance;
            svc.RecordRunResult("stage_2", 120f, 200, 1, autoSave: true);
            svc.RecordRunResult("stage_3", 90f, 150, 0, autoSave: true);
            yield return null;

            var reloaded = svc.Reload();
            Assert.AreEqual(2, reloaded.records.totalRunCount);
            Assert.AreEqual(1, reloaded.records.totalBossKillCount);
            Assert.AreEqual(120f, reloaded.records.bestRunDurationSeconds);
            Assert.AreEqual(200, reloaded.records.bestGoldShards);
        }
    }
}
