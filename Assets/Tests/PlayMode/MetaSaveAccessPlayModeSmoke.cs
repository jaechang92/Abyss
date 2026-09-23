using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 세이브를 <b>열지 못했을 때</b>(잠금·권한)의 MetaSaveService 계약 — 손상과 달리 옛 백업·새 세이브를
    /// 현재값으로 채택하지 않고 저장을 보류하며, 명시적 Reload로만 풀린다(MetaSaveService.LoadState.cs).
    ///
    /// 실패 주입: 대부분 <see cref="LockingFileOperations"/>(특정 경로 읽기를 I/O 예외로 실패 — 모든 플랫폼),
    /// 한 건은 Windows 실제 파일 잠금. 저장소는 공용 <see cref="PlayModeSaveGuard"/>의 임시 폴더다.
    /// </summary>
    public sealed class MetaSaveAccessPlayModeSmoke
    {
        private sealed class LockingFileOperations : SaveFileOperations
        {
            public string LockedPath;
            public int ReadAttempts;

            public override string ReadAllText(string path)
            {
                if (path != LockedPath) return base.ReadAllText(path);
                ReadAttempts++;
                throw new IOException("테스트: 다른 프로세스가 파일을 사용 중");
            }
        }

        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private string savePath;
        private string prevPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            prevPath = SaveSystem.Instance.GetBackupFilePath(MetaSave.FileName);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            saveGuard.Release();   // 주입한 파일 조작도 Acquire 전 값으로 돌아간다
            yield return null;
        }

        private static string CurrentVersionJson(int shards) =>
            $"{{\"version\":{MetaSave.CurrentVersion},\"abyssShardsTotal\":{shards}}}";

        private static int ShardsOnDisk(string path) =>
            JsonUtility.FromJson<MetaSave>(File.ReadAllText(path)).abyssShardsTotal;

        private LockingFileOperations Lock(string path)
        {
            var ops = new LockingFileOperations { LockedPath = path };
            SaveSystem.Instance.SetFileOperations(ops);
            return ops;
        }

        private static void Unlock() => SaveSystem.Instance.SetFileOperations(null);

        private static MetaSave ReloadBlocked(MetaSaveService service)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] (본 파일을 열지 못했다|불러오기 실패)"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[MetaSaveService\] 세이브를 열지 못했다"));
            return service.Reload();
        }

        private string[] CorruptBackups() =>
            Directory.GetFiles(saveGuard.TestDirectory, "abyss_meta.corrupt-*.bak");

        [UnityTest]
        public IEnumerator 본파일을_열지_못하면_옛_백업을_채택하지_않고_저장을_보류한다()
        {
            File.WriteAllText(prevPath, CurrentVersionJson(10));
            File.WriteAllText(savePath, CurrentVersionJson(20));
            Lock(savePath);

            var svc = MetaSaveService.Instance;
            var loaded = ReloadBlocked(svc);
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Inaccessible, svc.LastLoadResult);
            Assert.IsTrue(svc.IsSaveBlocked);
            Assert.AreNotEqual(10, loaded.abyssShardsTotal, "옛 직전 정상본(10)을 현재값으로 채택하면 안 된다.");
            Assert.AreEqual(0, CorruptBackups().Length, "열지 못한 것은 손상이 아니다 — 손상 백업을 만들지 않는다.");

            svc.AddAbyssShards(5, autoSave: true);
            svc.RecordRunResult(default, 1f, 1, 0, autoSave: true);
            Assert.IsFalse(svc.Save(), "보류 중 저장은 false여야 한다.");
            yield return null;

            Assert.AreEqual(20, ShardsOnDisk(savePath), "열지 못한 최신본을 임시 값으로 덮으면 안 된다.");
            Assert.AreEqual(10, ShardsOnDisk(prevPath));
        }

        [UnityTest]
        public IEnumerator 잠금이_풀린_뒤_Reload하면_최신본으로_돌아오고_보류가_풀린다()
        {
            File.WriteAllText(prevPath, CurrentVersionJson(10));
            File.WriteAllText(savePath, CurrentVersionJson(20));
            Lock(savePath);
            var svc = MetaSaveService.Instance;
            ReloadBlocked(svc);
            svc.AddAbyssShards(999, autoSave: true);   // 보류 중 임시 값 — 버려져야 한다
            yield return null;

            Unlock();
            var reloaded = svc.Reload();

            Assert.AreEqual(MetaSaveLoadResult.UpToDate, svc.LastLoadResult);
            Assert.IsFalse(svc.IsSaveBlocked);
            Assert.AreEqual(20, reloaded.abyssShardsTotal);

            svc.AddAbyssShards(1, autoSave: true);
            yield return null;
            Assert.AreEqual(21, ShardsOnDisk(savePath));
            Assert.AreEqual(20, ShardsOnDisk(prevPath));
        }

        [UnityTest]
        public IEnumerator 계속_열지_못하면_보류가_유지되고_숨은_재시도가_없다()
        {
            File.WriteAllText(savePath, CurrentVersionJson(20));
            var ops = Lock(savePath);
            var svc = MetaSaveService.Instance;

            ReloadBlocked(svc);
            ReloadBlocked(svc);
            for (int i = 0; i < 5; i++)
            {
                svc.AddAbyssShards(1, autoSave: true);
                _ = svc.Current;
            }
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Inaccessible, svc.LastLoadResult);
            Assert.IsTrue(svc.IsSaveBlocked);
            Assert.AreEqual(SaveSystem.READ_ATTEMPTS * 2, ops.ReadAttempts,
                "디스크 읽기는 명시적 Reload 두 번 × 정해진 시도 횟수뿐이어야 한다(저장·조회가 몰래 재시도하지 않는다).");
            Assert.AreEqual(20, ShardsOnDisk(savePath));
        }

        [UnityTest]
        public IEnumerator 본파일이_없고_직전정상본을_열지_못하면_새_본파일을_만들지_않는다()
        {
            // 교체 도중 끊겨 본 파일이 없는데 직전 정상본이 잠긴 상황 — 새 세이브로 시작해 저장하면
            // 다음 로드부터 새 본 파일이 1순위가 되어 직전 정상본의 진행도가 사실상 사라진다.
            File.WriteAllText(prevPath, CurrentVersionJson(33));
            Lock(prevPath);
            var svc = MetaSaveService.Instance;

            ReloadBlocked(svc);
            svc.AddAbyssShards(1, autoSave: true);
            yield return null;

            Assert.AreEqual(MetaSaveLoadResult.Inaccessible, svc.LastLoadResult);
            Assert.IsFalse(File.Exists(savePath), "보류 중에 새 본 파일이 생기면 안 된다.");

            Unlock();
            var reloaded = svc.Reload();
            Assert.AreEqual(MetaSaveLoadResult.RecoveredFromBackup, svc.LastLoadResult);
            Assert.AreEqual(33, reloaded.abyssShardsTotal);
        }

        [UnityTest]
        public IEnumerator 보류_중_ResetAll_저장도_열지_못한_본파일을_덮지_못한다()
        {
            File.WriteAllText(savePath, CurrentVersionJson(20));
            Lock(savePath);
            var svc = MetaSaveService.Instance;
            ReloadBlocked(svc);

            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패:"));
            svc.ResetAll(autoSave: true);   // 사용자의 명시적 초기화 — 보류는 풀리지만 디스크가 확인을 거부한다
            yield return null;

            Assert.AreEqual(20, ShardsOnDisk(savePath));
        }

        [UnityTest]
        public IEnumerator Windows_실제_잠금이_풀린_뒤_Reload하면_이어진다()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                Assert.Ignore("공유 금지 잠금(FileShare.None)으로 읽기를 막는 동작은 Windows 전용이다.");

            File.WriteAllText(savePath, CurrentVersionJson(20));
            var svc = MetaSaveService.Instance;

            using (new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                ReloadBlocked(svc);
                Assert.AreEqual(MetaSaveLoadResult.Inaccessible, svc.LastLoadResult);
                Assert.IsFalse(svc.Save());
            }
            yield return null;

            var reloaded = svc.Reload();
            Assert.AreEqual(MetaSaveLoadResult.UpToDate, svc.LastLoadResult);
            Assert.AreEqual(20, reloaded.abyssShardsTotal);
        }
    }
}
