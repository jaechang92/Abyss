using System.Collections;
using System.Collections.Generic;
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

        // 테스트 시작 시점에 이미 있던 .bak 목록. 정리 대상에서 제외한다 —
        // 손상 백업은 사람이 복구하려고 남겨 둔 것일 수 있어, 테스트가 지우면 안 된다.
        private HashSet<string> preExistingBackups;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            backupPath = savePath + ".testbackup";
            preExistingBackups = new HashSet<string>(FindBackupFiles());

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
            LogAssert.ignoreFailingMessages = false;

            if (!string.IsNullOrEmpty(savePath) && File.Exists(savePath)) File.Delete(savePath);

            // 마이그레이션 테스트가 만든 .bak 를 치운다. 실사용에서는 남기는 게 맞지만
            // 테스트가 사용자 폴더에 잔재를 쌓으면 안 된다.
            DeleteGeneratedBackups();

            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                if (hadOriginal) File.Move(backupPath, savePath);
                else File.Delete(backupPath);
            }

            if (MetaSaveService.HasInstance) MetaSaveService.Instance.ResetAll(autoSave: false);
            yield return null;
        }

        private void DeleteGeneratedBackups()
        {
            foreach (var f in EnumerateGeneratedBackups()) File.Delete(f);
        }

        /// <summary>이번 테스트가 만든 백업만. SetUp 시점에 이미 있던 것은 건드리지 않는다.</summary>
        private string[] EnumerateGeneratedBackups()
        {
            var all = FindBackupFiles();
            if (preExistingBackups == null || preExistingBackups.Count == 0) return all;

            var fresh = new List<string>(all.Length);
            foreach (var f in all)
            {
                if (!preExistingBackups.Contains(f)) fresh.Add(f);
            }
            return fresh.ToArray();
        }

        private string[] FindBackupFiles()
        {
            string dir = Path.GetDirectoryName(savePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return System.Array.Empty<string>();

            string stem = Path.GetFileNameWithoutExtension(savePath);
            return Directory.GetFiles(dir, $"{stem}.*.bak");
        }

        /// <summary>세이브 파일을 임의 JSON으로 갈아 끼우고 디스크에서 다시 읽는다.</summary>
        private MetaSave WriteRawAndReload(string json)
        {
            File.WriteAllText(savePath, json);
            return MetaSaveService.Instance.Reload();
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

        // ───────────────────── 스키마 버전 / 손상 세이브 ─────────────────────

        [UnityTest]
        public IEnumerator NewFile_StampsCurrentSchemaVersion()
        {
            MetaSaveService.Instance.AddAbyssShards(10, autoSave: true);
            yield return null;

            var reloaded = MetaSaveService.Instance.Reload();
            Assert.AreEqual(MetaSave.CurrentVersion, reloaded.version,
                "새로 만든 세이브는 현재 스키마 버전으로 기록되어야 한다.");
            Assert.AreEqual(MetaSaveLoadResult.UpToDate, MetaSaveService.Instance.LastLoadResult);
        }

        [UnityTest]
        public IEnumerator CorruptFile_BacksUpOriginal_AndStartsFresh()
        {
            // SaveSystem의 파싱 실패 로그 + MetaSaveService의 폴백 로그가 함께 뜬다.
            LogAssert.ignoreFailingMessages = true;

            const string garbage = "{ 이건 JSON이 아니다";
            var loaded = WriteRawAndReload(garbage);
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Corrupted, MetaSaveService.Instance.LastLoadResult);
            Assert.AreEqual(0, loaded.abyssShardsTotal, "파싱 실패 시 새 세이브로 시작한다.");

            // 핵심: 원본이 어딘가 남아 있어야 한다. 예전에는 곧이어 일어나는 아무 저장이
            // 원본을 덮어써 복구 가능성까지 지웠다.
            var backups = EnumerateGeneratedBackups();
            Assert.AreEqual(1, backups.Length, "손상 원본 백업이 정확히 1개 생겨야 한다.");
            StringAssert.Contains("corrupt-", backups[0]);
            Assert.AreEqual(garbage, File.ReadAllText(backups[0]), "백업은 손대지 않은 원본이어야 한다.");
        }

        [UnityTest]
        public IEnumerator FutureVersion_BacksUpOriginal_AndKeepsKnownFields()
        {
            LogAssert.ignoreFailingMessages = true;

            int future = MetaSave.CurrentVersion + 98;
            var loaded = WriteRawAndReload($"{{\"version\":{future},\"abyssShardsTotal\":4242}}");
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.FutureVersion, MetaSaveService.Instance.LastLoadResult);
            Assert.AreEqual(4242, loaded.abyssShardsTotal, "아는 필드는 그대로 살아야 한다.");
            Assert.AreEqual(future, loaded.version, "미래 버전을 현재 버전으로 끌어내리면 안 된다.");

            var backups = EnumerateGeneratedBackups();
            Assert.AreEqual(1, backups.Length);
            StringAssert.Contains($".v{future}.", backups[0]);
        }

        [UnityTest]
        public IEnumerator LegacySaveWithoutVersionField_LoadsAndPreservesData()
        {
            // version 키가 아예 없는 파일 — JsonUtility가 필드 초기값(MinimumVersion)을 남긴다.
            // MetaSave 기본 생성자가 CurrentVersion을 찍었다면 이 파일이 '이미 최신'으로 위장한다.
            var loaded = WriteRawAndReload("{\"abyssShardsTotal\":77}");
            yield return null;

            Assert.AreEqual(77, loaded.abyssShardsTotal);
            Assert.GreaterOrEqual(loaded.version, MetaSave.MinimumVersion);
            Assert.AreNotEqual(MetaSaveLoadResult.Corrupted, MetaSaveService.Instance.LastLoadResult);
            Assert.AreNotEqual(MetaSaveLoadResult.FutureVersion, MetaSaveService.Instance.LastLoadResult);
        }
    }
}
