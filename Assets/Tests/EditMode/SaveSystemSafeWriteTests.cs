using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// SaveSystem 안전 저장·복구 EditMode 테스트.
    ///
    /// 모든 테스트는 <b>임시 폴더</b>(Path.GetTempPath 아래 GUID 폴더)에서만 읽고 쓴다 —
    /// SetSaveDirectoryOverride로 persistentDataPath(사용자 세이브)를 우회한다.
    /// 실패는 두 방식으로 일으킨다: 실제 OS 실패(임시 파일 자리에 폴더, Windows 파일 잠금)와
    /// 단계별 주입(<see cref="FaultyFileOperations"/>). 어느 쪽이든 확인하는 것은 같다 —
    /// "실패한 저장 뒤에도 마지막으로 성공한 저장을 다시 읽을 수 있는가".
    ///
    /// EditMode에서 AddComponent는 Awake를 부르지 않으므로 SaveSystem.Instance(정적 싱글톤)는 건드리지 않는다.
    /// </summary>
    public sealed partial class SaveSystemSafeWriteTests
    {
        private const string FILE_NAME = "safe_write_test.json";

        [Serializable]
        public sealed class TestSave
        {
            public int value;
        }

        private enum FaultStep
        {
            None,
            WritePartial,       // 임시 파일을 절반만 쓰고 끊김(디스크 가득·프로세스 종료)
            Move,               // 최초 저장의 이름 바꾸기 실패
            Replace,            // 교체가 아무것도 바꾸지 못하고 실패
            ReplaceAfterBackup, // 본 파일을 직전 정상본으로 옮긴 뒤 끊김 — 본 파일이 사라진 상태
            ReplaceByCopy,      // File.Replace 미지원 플랫폼의 폴백 경로를 그대로 탄다
            ReplaceByCopyCut    // 폴백 ② 단계(본 파일 덮어쓰기) 도중 끊김
        }

        private sealed class FaultyFileOperations : SaveFileOperations
        {
            public FaultStep FailAt;

            // 읽기 실패 주입: ReadFailPath를 여는 시도 중 앞의 ReadFailuresLeft번을 실패시킨다(-1 = 계속).
            // 파일이 없는 경로에도 그대로 걸린다 — File.Exists 선검사로 우회되면 이 주입이 무시될 것이다.
            public string ReadFailPath;
            public int ReadFailuresLeft;
            public int ReadAttempts;
            public bool IsReadFailureAccessDenied;

            public override string ReadAllText(string path)
            {
                if (path == ReadFailPath)
                {
                    ReadAttempts++;
                    if (ReadFailuresLeft != 0)
                    {
                        if (ReadFailuresLeft > 0) ReadFailuresLeft--;
                        throw IsReadFailureAccessDenied
                            ? new UnauthorizedAccessException("테스트: 경로에 대한 액세스가 거부되었습니다")
                            : (Exception)new IOException("테스트: 다른 프로세스가 파일을 사용 중");
                    }
                }
                return base.ReadAllText(path);
            }

            public override void WriteAllText(string path, string contents)
            {
                if (FailAt == FaultStep.WritePartial)
                {
                    File.WriteAllText(path, contents.Substring(0, contents.Length / 2));
                    throw new IOException("테스트: 임시 파일 쓰기 도중 실패");
                }
                base.WriteAllText(path, contents);
            }

            public override void Move(string sourcePath, string destinationPath)
            {
                if (FailAt == FaultStep.Move) throw new IOException("테스트: 이름 바꾸기 실패");
                base.Move(sourcePath, destinationPath);
            }

            public override void Replace(string sourcePath, string destinationPath, string backupPath)
            {
                if (FailAt == FaultStep.Replace) throw new IOException("테스트: 교체 실패");
                if (FailAt == FaultStep.ReplaceAfterBackup)
                {
                    // Windows ReplaceFile의 ERROR_UNABLE_TO_MOVE_REPLACEMENT_2와 같은 상태를 만든다.
                    if (backupPath != null)
                    {
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                        File.Move(destinationPath, backupPath);
                    }
                    throw new IOException("테스트: 교체 도중 끊김");
                }
                if (FailAt == FaultStep.ReplaceByCopy)
                {
                    ReplaceByCopy(sourcePath, destinationPath, backupPath);
                    return;
                }
                if (FailAt == FaultStep.ReplaceByCopyCut)
                {
                    // ① 직전 정상본 복사는 끝났고 ② 본 파일을 절반만 덮은 채 끊긴 상태를 폴백과 같은 순서로 만든다.
                    if (backupPath != null) File.Copy(destinationPath, backupPath, overwrite: true);
                    string incoming = File.ReadAllText(sourcePath);
                    File.WriteAllText(destinationPath, incoming.Substring(0, incoming.Length / 2));
                    throw new IOException("테스트: 폴백 교체 도중 끊김");
                }
                base.Replace(sourcePath, destinationPath, backupPath);
            }
        }

        private string testDirectory;
        private SaveSystem saveSystem;
        private FaultyFileOperations faults;
        private int savedCount;
        private int saveFailedCount;

        private string PrimaryPath => saveSystem.GetFilePath(FILE_NAME);
        private string BackupPath => saveSystem.GetBackupFilePath(FILE_NAME);
        private string TempPath => saveSystem.GetTempFilePath(FILE_NAME);

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "AbyssSaveSafeWrite-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);

            saveSystem = new GameObject("[Test] SaveSystem").AddComponent<SaveSystem>();
            saveSystem.SetSaveDirectoryOverride(testDirectory);
            faults = new FaultyFileOperations();
            saveSystem.SetFileOperations(faults);

            savedCount = 0;
            saveFailedCount = 0;
            saveSystem.OnSaved += _ => savedCount++;
            saveSystem.OnSaveFailed += _ => saveFailedCount++;
        }

        [TearDown]
        public void TearDown()
        {
            if (saveSystem != null) Object.DestroyImmediate(saveSystem.gameObject);
            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }

        private bool SaveValue(int value) => saveSystem.Save(new TestSave { value = value }, FILE_NAME);

        private static int ReadValue(string path) => JsonUtility.FromJson<TestSave>(File.ReadAllText(path)).value;

        private int LoadValue(out SaveFileStatus primary, out SaveFileStatus backup)
        {
            var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out primary, out backup);
            Assert.IsNotNull(data, "마지막 정상 저장을 다시 읽을 수 있어야 한다.");
            return data.value;
        }

        /// <summary>1 → 2 순서로 성공 저장해 본 파일=2, 직전 정상본=1 상태를 만든다.</summary>
        private void SaveTwoGenerations()
        {
            Assert.IsTrue(SaveValue(1));
            Assert.IsTrue(SaveValue(2));
            savedCount = 0;
        }

        private static void ExpectSaveFailedLog() =>
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패"));

        // ───────────────────────── 정상 경로 ─────────────────────────

        [Test]
        public void 최초저장은_본파일만_만들고_직전정상본과_임시파일을_남기지_않는다()
        {
            Assert.IsTrue(SaveValue(7));

            Assert.AreEqual(7, ReadValue(PrimaryPath));
            Assert.IsFalse(File.Exists(BackupPath), "최초 저장에는 직전 정상본이 없다.");
            Assert.IsFalse(File.Exists(TempPath), "성공한 저장은 임시 파일을 남기지 않는다.");
            Assert.AreEqual(1, savedCount);
        }

        [Test]
        public void 갱신저장은_교체직전_본파일을_직전정상본으로_보존한다()
        {
            Assert.IsTrue(SaveValue(1));
            Assert.IsTrue(SaveValue(2));
            Assert.IsTrue(SaveValue(3));

            Assert.AreEqual(3, ReadValue(PrimaryPath));
            Assert.AreEqual(2, ReadValue(BackupPath), "직전 정상본은 바로 앞 세대여야 한다.");
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(3, savedCount);
        }

        [Test]
        public void 저장형식은_BOM없는_UTF8이다()
        {
            // 기존 File.WriteAllText와 같은 바이트 형식이어야 이전 세이브·외부 열람과 어긋나지 않는다.
            Assert.IsTrue(SaveValue(1));
            byte[] bytes = File.ReadAllBytes(PrimaryPath);
            Assert.AreEqual((byte)'{', bytes[0]);
        }

        // ───────────────────────── 쓰기 실패 ─────────────────────────

        [Test]
        public void 임시파일_쓰기가_OS에서_실패하면_본파일과_직전정상본이_그대로다()
        {
            SaveTwoGenerations();
            // 임시 파일 자리에 폴더를 만들어 두면 파일 생성이 실제로 실패한다.
            Directory.CreateDirectory(TempPath);

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3));

            Assert.AreEqual(2, ReadValue(PrimaryPath));
            Assert.AreEqual(1, ReadValue(BackupPath));
            Assert.AreEqual(0, savedCount, "실패한 저장에 OnSaved가 나가면 안 된다.");
            Assert.AreEqual(1, saveFailedCount);
            Assert.AreEqual(2, LoadValue(out _, out _));
        }

        [Test]
        public void 임시파일을_절반만_쓰고_끊겨도_본파일이_그대로고_불완전한_임시파일은_치운다()
        {
            SaveTwoGenerations();
            faults.FailAt = FaultStep.WritePartial;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3));

            Assert.AreEqual(2, ReadValue(PrimaryPath));
            Assert.AreEqual(1, ReadValue(BackupPath));
            Assert.IsFalse(File.Exists(TempPath), "불완전한 임시 파일은 정리돼야 한다.");
            Assert.AreEqual(0, savedCount);
        }

        [Test]
        public void 최초저장의_이름바꾸기가_실패하면_본파일을_만들지_않고_실패를_알린다()
        {
            faults.FailAt = FaultStep.Move;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(1));

            Assert.IsFalse(File.Exists(PrimaryPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, savedCount);
            Assert.AreEqual(1, saveFailedCount);
        }

        // ───────────────────────── 교체 실패 ─────────────────────────

        [Test]
        public void 교체가_실패하면_본파일과_직전정상본이_그대로고_OnSaved가_없다()
        {
            SaveTwoGenerations();
            faults.FailAt = FaultStep.Replace;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3));

            Assert.AreEqual(2, ReadValue(PrimaryPath));
            Assert.AreEqual(1, ReadValue(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, savedCount);
            Assert.AreEqual(2, LoadValue(out var primary, out _));
            Assert.AreEqual(SaveFileStatus.Loaded, primary);
        }

        [Test]
        public void 교체_도중_본파일이_사라져도_직전정상본에서_마지막_정상저장을_읽는다()
        {
            SaveTwoGenerations();
            faults.FailAt = FaultStep.ReplaceAfterBackup;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3));
            Assert.IsFalse(File.Exists(PrimaryPath), "전제: 본 파일이 직전 정상본 이름으로 옮겨진 상태.");

            LogAssert.Expect(LogType.Warning, new Regex("직전 정상본을 불러왔다"));
            Assert.AreEqual(2, LoadValue(out var primary, out var backup), "마지막 성공 저장(2)이 살아 있어야 한다.");
            Assert.AreEqual(SaveFileStatus.Missing, primary);
            Assert.AreEqual(SaveFileStatus.Loaded, backup);

            // 다음 저장은 최초 저장 경로로 본 파일을 다시 만들고, 직전 정상본을 건드리지 않는다.
            faults.FailAt = FaultStep.None;
            Assert.IsTrue(SaveValue(4));
            Assert.AreEqual(4, ReadValue(PrimaryPath));
            Assert.AreEqual(2, ReadValue(BackupPath));
        }

        [Test]
        public void Windows_본파일이_잠겨_교체가_실제로_실패해도_마지막_정상저장이_남는다()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("파일 잠금으로 교체를 막는 동작은 Windows 전용이다.");

            SaveTwoGenerations();

            ExpectSaveFailedLog();
            // 읽기는 허용하고 삭제·이름 바꾸기는 막는다 — 백신·동기화 도구가 파일을 잡고 있는 상황.
            using (new FileStream(PrimaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.IsFalse(SaveValue(3));
            }

            Assert.AreEqual(0, savedCount);
            Assert.AreEqual(2, LoadValue(out _, out _));
        }

        // ───────────────────────── 로드 우선순위 · 손상 ─────────────────────────

        [Test]
        public void 정상_본파일이_있으면_더_오래된_직전정상본을_읽지_않는다()
        {
            SaveTwoGenerations();

            Assert.AreEqual(2, LoadValue(out var primary, out var backup));
            Assert.AreEqual(SaveFileStatus.Loaded, primary);
            Assert.AreEqual(SaveFileStatus.NotChecked, backup);
        }

        [Test]
        public void 깨진_본파일은_직전정상본으로_복구하되_파일을_고치지_않는다()
        {
            SaveTwoGenerations();
            const string garbage = "{ \"value\": 3";   // 쓰다 끊긴 JSON
            File.WriteAllText(PrimaryPath, garbage);

            LogAssert.Expect(LogType.Warning, new Regex("직전 정상본을 불러왔다"));
            Assert.AreEqual(1, LoadValue(out var primary, out var backup));
            Assert.AreEqual(SaveFileStatus.Unreadable, primary);
            Assert.AreEqual(SaveFileStatus.Loaded, backup);
            Assert.AreEqual(garbage, File.ReadAllText(PrimaryPath), "로드는 손상 증거를 지우거나 고치지 않는다.");
        }

        [Test]
        public void 빈_본파일도_손상으로_보고_직전정상본을_읽는다()
        {
            SaveTwoGenerations();
            File.WriteAllText(PrimaryPath, string.Empty);

            LogAssert.Expect(LogType.Warning, new Regex("직전 정상본을 불러왔다"));
            Assert.AreEqual(1, LoadValue(out var primary, out _));
            Assert.AreEqual(SaveFileStatus.Unreadable, primary);
        }

        [Test]
        public void 깨진_본파일에_백업도_없으면_default를_돌려주고_파일은_그대로다()
        {
            const string garbage = "이건 JSON이 아니다";
            File.WriteAllText(PrimaryPath, garbage);

            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 불러오기 실패"));
            var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var primary, out var backup);

            Assert.IsNull(data);
            Assert.AreEqual(SaveFileStatus.Unreadable, primary);
            Assert.AreEqual(SaveFileStatus.Missing, backup);
            Assert.AreEqual(garbage, File.ReadAllText(PrimaryPath));
        }

        [Test]
        public void 아무_파일도_없으면_Missing_둘이고_오류가_아니다()
        {
            var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var primary, out var backup);

            Assert.IsNull(data);
            Assert.AreEqual(SaveFileStatus.Missing, primary);
            Assert.AreEqual(SaveFileStatus.Missing, backup);
        }

        [Test]
        public void 불완전한_임시파일은_로드하지_않고_다음_저장이_덮어쓴다()
        {
            SaveTwoGenerations();
            // 이전 실행이 임시 파일을 쓰다 죽은 흔적. 값은 더 새것처럼 보이지만 끝까지 썼는지 알 수 없다.
            File.WriteAllText(TempPath, "{ \"value\": 99");

            Assert.AreEqual(2, LoadValue(out _, out _), "임시 파일은 로드 후보가 아니다.");

            Assert.IsTrue(SaveValue(3));
            Assert.AreEqual(3, ReadValue(PrimaryPath));
            Assert.AreEqual(2, ReadValue(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
        }

        [Test]
        public void 깨진_본파일_위에_저장해도_멀쩡한_직전정상본을_밀어내지_않는다()
        {
            SaveTwoGenerations();                       // 본=2, 직전=1
            File.WriteAllText(PrimaryPath, "{ 깨짐");

            LogAssert.Expect(LogType.Warning, new Regex("직전 정상본으로 돌리지 않고"));
            Assert.IsTrue(SaveValue(5));

            Assert.AreEqual(5, ReadValue(PrimaryPath));
            Assert.AreEqual(1, ReadValue(BackupPath), "깨진 파일이 직전 정상본 자리를 차지하면 안 된다.");
        }

        [Test]
        public void DeleteFile은_직전정상본까지_지워_삭제한_세이브가_되살아나지_않는다()
        {
            SaveTwoGenerations();

            Assert.IsTrue(saveSystem.DeleteFile(FILE_NAME));

            Assert.IsFalse(File.Exists(PrimaryPath));
            Assert.IsFalse(File.Exists(BackupPath));
            Assert.IsNull(saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out _, out _));
        }

        // 열지 못함(Inaccessible)·File.Replace 폴백 테스트는 SaveSystemSafeWriteTests.ReadFailure.cs — 500줄 규약으로 분리.

        [Test]
        public void 테스트는_사용자_세이브_폴더를_쓰지_않는다()
        {
            // 이 파일 전체의 격리 전제. 깨지면 위 테스트들이 사용자 폴더에 파일을 만든다.
            StringAssert.StartsWith(testDirectory, PrimaryPath);
            Assert.AreNotEqual(Application.persistentDataPath, saveSystem.SaveDirectory);
        }
    }
}
