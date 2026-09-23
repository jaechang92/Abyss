using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// MetaSaveService PlayMode 통합 스모크.
    /// SaveSystem과 협력해 디스크 라운드트립까지 검증한다.
    ///
    /// 사용자 데이터 보호: 사용자 세이브 폴더를 <b>아예 쓰지 않는다</b> — 공용 <see cref="PlayModeSaveGuard"/>가
    /// 저장 폴더를 임시 폴더로 돌리고, 해제 때 저장 폴더·파일 조작·메타 메모리를 테스트 전으로 되돌린 뒤 폴더째 지운다.
    /// </summary>
    public sealed class MetaSaveServicePlayModeSmoke
    {
        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private string testDirectory;
        private string savePath;
        private string prevPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            testDirectory = saveGuard.TestDirectory;

            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            prevPath = SaveSystem.Instance.GetBackupFilePath(MetaSave.FileName);
            StringAssert.StartsWith(testDirectory, savePath, "격리 전제: 테스트는 사용자 세이브 폴더를 쓰지 않는다.");

            // 메모리 상 인스턴스도 깨끗하게 시작. ResetAll(false)는 디스크를 건드리지 않는다.
            MetaSaveService.Instance.ResetAll(autoSave: false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            saveGuard.Release();
            yield return null;
        }

        /// <summary>격리 폴더라 있는 백업은 전부 이번 테스트가 만든 것이다.</summary>
        private string[] EnumerateGeneratedBackups() => FindBackupFiles();

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
            svc.RecordRunResult(StageReach.At(2, 5, "불꽃 회랑"), 120f, 200, 1, autoSave: true);
            svc.RecordRunResult(StageReach.At(3, 2, "왕좌의 잔해"), 90f, 150, 0, autoSave: true);
            yield return null;

            var reloaded = svc.Reload();
            Assert.AreEqual(2, reloaded.records.totalRunCount);
            Assert.AreEqual(1, reloaded.records.totalBossKillCount);
            Assert.AreEqual(120f, reloaded.records.bestRunDurationSeconds);
            Assert.AreEqual(200, reloaded.records.bestGoldShards);

            // 구조체가 디스크를 왕복해도 살아남는지 — 표시명까지 굳혀 두는 것이 이 설계의 전제다.
            Assert.AreEqual(3, reloaded.records.bestReach.stageNumber);
            Assert.AreEqual(2, reloaded.records.bestReach.stepNumber);
            Assert.AreEqual("왕좌의 잔해", reloaded.records.bestReach.stageName);
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

        // ───────────────────── 안전 저장 · 직전 정상본 복구 ─────────────────────

        /// <summary>교체 단계만 실패시킨다. 실패가 디스크에 아무것도 남기지 않는 경우.</summary>
        private sealed class FailingReplaceOperations : SaveFileOperations
        {
            public override void Replace(string sourcePath, string destinationPath, string backupPath) =>
                throw new IOException("테스트: 교체 실패");
        }

        private static string CurrentVersionJson(int shards) =>
            $"{{\"version\":{MetaSave.CurrentVersion},\"abyssShardsTotal\":{shards}}}";

        private static int ShardsOnDisk(string path) =>
            JsonUtility.FromJson<MetaSave>(File.ReadAllText(path)).abyssShardsTotal;

        [UnityTest]
        public IEnumerator FirstSave_CreatesNoPrevious_UpdateKeepsPrevious()
        {
            var svc = MetaSaveService.Instance;
            svc.AddAbyssShards(10, autoSave: true);
            yield return null;

            Assert.IsTrue(File.Exists(savePath));
            Assert.IsFalse(File.Exists(prevPath), "최초 저장에는 직전 정상본이 없다.");

            svc.AddAbyssShards(5, autoSave: true);
            yield return null;

            Assert.AreEqual(15, ShardsOnDisk(savePath));
            Assert.AreEqual(10, ShardsOnDisk(prevPath), "갱신 저장은 교체 직전 본 파일을 남긴다.");
        }

        [UnityTest]
        public IEnumerator SaveFailure_KeepsLastGoodProgressOnDisk_AndNoSavedEvent()
        {
            var svc = MetaSaveService.Instance;
            svc.AddAbyssShards(10, autoSave: true);
            svc.AddAbyssShards(5, autoSave: true);   // 본=15, 직전=10
            yield return null;

            int savedCount = 0;
            void CountSaved(string _) => savedCount++;
            SaveSystem.Instance.OnSaved += CountSaved;
            SaveSystem.Instance.SetFileOperations(new FailingReplaceOperations());
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패"));
                svc.AddAbyssShards(100, autoSave: false);
                Assert.IsFalse(svc.Save(), "교체 실패는 저장 실패로 보고돼야 한다.");
                Assert.AreEqual(0, savedCount, "교체가 끝나지 않았으면 OnSaved가 나가면 안 된다.");
            }
            finally
            {
                SaveSystem.Instance.OnSaved -= CountSaved;
                SaveSystem.Instance.SetFileOperations(null);
            }
            yield return null;

            Assert.AreEqual(15, ShardsOnDisk(savePath));
            Assert.AreEqual(10, ShardsOnDisk(prevPath));
            Assert.AreEqual(15, svc.Reload().abyssShardsTotal, "재시작 후에는 마지막 정상 저장(15)으로 이어진다.");
        }

        [UnityTest]
        public IEnumerator CorruptPrimary_WithGoodPrevious_RecoversPrevious_AndKeepsEvidence()
        {
            const string garbage = "{ \"abyssShardsTotal\": 60";   // 쓰다 끊긴 본 파일
            File.WriteAllText(prevPath, CurrentVersionJson(55));
            var loaded = WriteRawAndReload(garbage);
            yield return null;

            var svc = MetaSaveService.Instance;
            Assert.AreEqual(MetaSaveLoadResult.RecoveredFromBackup, svc.LastLoadResult);
            Assert.IsTrue(svc.LastLoadUsedBackup);
            Assert.AreEqual(55, loaded.abyssShardsTotal, "새 세이브가 아니라 직전 정상본으로 이어져야 한다.");

            var backups = EnumerateGeneratedBackups();
            Assert.AreEqual(1, backups.Length, "깨진 본 파일 증거가 정확히 1개 남아야 한다.");
            StringAssert.Contains("corrupt-", backups[0]);
            Assert.AreEqual(garbage, File.ReadAllText(backups[0]));

            // 복구 후 첫 저장: 깨진 본 파일이 직전 정상본 자리를 차지하면 안 된다.
            svc.AddAbyssShards(5, autoSave: true);
            yield return null;
            Assert.AreEqual(60, ShardsOnDisk(savePath));
            Assert.AreEqual(55, ShardsOnDisk(prevPath));

            svc.Reload();
            Assert.AreEqual(MetaSaveLoadResult.UpToDate, svc.LastLoadResult, "다시 쓴 본 파일이 1순위로 돌아와야 한다.");
        }

        [UnityTest]
        public IEnumerator MissingPrimary_WithPrevious_Recovers_WithoutCorruptBackup()
        {
            // 교체 도중 끊겨 본 파일만 사라진 상태.
            File.WriteAllText(prevPath, CurrentVersionJson(33));
            var loaded = MetaSaveService.Instance.Reload();
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.RecoveredFromBackup, MetaSaveService.Instance.LastLoadResult);
            Assert.AreEqual(33, loaded.abyssShardsTotal);
            Assert.AreEqual(0, EnumerateGeneratedBackups().Length, "깨진 파일이 없으면 손상 백업도 없다.");
        }

        [UnityTest]
        public IEnumerator ValidPrimary_IsPreferredOverOlderPrevious()
        {
            File.WriteAllText(prevPath, CurrentVersionJson(10));
            var loaded = WriteRawAndReload(CurrentVersionJson(20));
            yield return null;

            Assert.AreEqual(20, loaded.abyssShardsTotal, "더 오래된 직전 정상본이 정상 최신본을 이기면 안 된다.");
            Assert.AreEqual(MetaSaveLoadResult.UpToDate, MetaSaveService.Instance.LastLoadResult);
            Assert.IsFalse(MetaSaveService.Instance.LastLoadUsedBackup);
        }

        [UnityTest]
        public IEnumerator PrimaryAndPreviousBothCorrupt_StartsFresh_KeepsBothEvidence()
        {
            LogAssert.ignoreFailingMessages = true;

            File.WriteAllText(prevPath, "{ 직전도 깨짐");
            var loaded = WriteRawAndReload("{ 본도 깨짐");
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Corrupted, MetaSaveService.Instance.LastLoadResult);
            Assert.AreEqual(0, loaded.abyssShardsTotal);

            var backups = EnumerateGeneratedBackups();
            Assert.AreEqual(2, backups.Length, "본 파일·직전 정상본 증거가 각각 남아야 한다.");
            Assert.IsTrue(Array.Exists(backups, b => b.Contains("corrupt-prev-")));
        }

        [UnityTest]
        public IEnumerator FuturePrevious_Recovered_KeepsVersionAndBacksUpOriginal()
        {
            LogAssert.ignoreFailingMessages = true;

            int future = MetaSave.CurrentVersion + 98;
            string futureJson = $"{{\"version\":{future},\"abyssShardsTotal\":4242}}";
            File.WriteAllText(prevPath, futureJson);
            var loaded = WriteRawAndReload("{ 깨짐");
            yield return null;

            var svc = MetaSaveService.Instance;
            Assert.AreEqual(MetaSaveLoadResult.FutureVersion, svc.LastLoadResult, "미래 버전 신호가 복구에 가려지면 안 된다.");
            Assert.IsTrue(svc.LastLoadUsedBackup);
            Assert.AreEqual(future, loaded.version);
            Assert.AreEqual(4242, loaded.abyssShardsTotal);

            // 버전 백업은 본 파일과 같은 이름 규약(abyss_meta.v{N}.bak)이고, 내용은 직전 정상본 원문이다.
            string versionBackup = Path.Combine(testDirectory, $"abyss_meta.v{future}.bak");
            Assert.IsTrue(File.Exists(versionBackup));
            Assert.AreEqual(futureJson, File.ReadAllText(versionBackup));
        }

        [UnityTest]
        public IEnumerator MigrationSave_KeepsLegacyOriginalAsPrevious()
        {
            const string legacy = "{\"abyssShardsTotal\":77}";   // version 키 없음 = v1
            var loaded = WriteRawAndReload(legacy);
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Migrated, MetaSaveService.Instance.LastLoadResult);
            Assert.AreEqual(77, loaded.abyssShardsTotal);

            // 변환 직후 저장이 본 파일을 현재 버전으로 바꾸고, 변환 전 원문은 직전 정상본에 남는다.
            Assert.AreEqual(MetaSave.CurrentVersion, JsonUtility.FromJson<MetaSave>(File.ReadAllText(savePath)).version);
            Assert.AreEqual(legacy, File.ReadAllText(prevPath));
            Assert.IsTrue(File.Exists(Path.Combine(testDirectory, "abyss_meta.v1.bak")));
        }
    }
}
