using System;
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
    /// 공용 <see cref="PlayModeSaveGuard"/> 자체의 계약 테스트 — 격리, 되돌리기, 중복·중첩, 정리 실패.
    ///
    /// 가드를 검사하는 동안 가드 밖도 사용자 폴더면 안 되므로, 이 픽스처는 가드와 별개로
    /// "바깥" 임시 폴더를 직접 걸어 둔다(사용자 세이브를 읽거나 쓰지 않는다). 가드가 되돌려야 할
    /// 이전 상태가 null이 아닌 값이어야 "되돌렸다"와 "기본값으로 지웠다"를 구분할 수 있다는 이유도 있다.
    /// </summary>
    public sealed class PlayModeSaveGuardTests
    {
        /// <summary>가드가 원래 구현을 되돌렸는지 참조로 확인하기 위한 표식.</summary>
        private sealed class MarkerFileOperations : SaveFileOperations { }

        private string outerDirectory;
        private MarkerFileOperations outerOperations;
        private MetaSaveService.MemorySnapshot fixtureMemory;
        private MetaSave outerMeta;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixtureMemory = MetaSaveService.Instance.CaptureMemoryState();

            outerDirectory = Path.Combine(Path.GetTempPath(), "AbyssGuardOuter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outerDirectory);
            outerOperations = new MarkerFileOperations();
            SaveSystem.Instance.SetSaveDirectoryOverride(outerDirectory);
            SaveSystem.Instance.SetFileOperations(outerOperations);

            MetaSaveService.Instance.ResetAll(autoSave: false);   // 디스크 없이 알려진 바깥 메모리
            MetaSaveService.Instance.AddAbyssShards(7, autoSave: false);
            outerMeta = MetaSaveService.Instance.Current;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 이 픽스처는 일부러 가드를 남기는 경우를 다룬다 — 실패로 중단됐을 때만 명시적 회복 경로를 탄다.
            PlayModeSaveGuard.ForceReleaseActive();

            SaveSystem.Instance.SetFileOperations(null);
            SaveSystem.Instance.SetSaveDirectoryOverride(null);
            MetaSaveService.Instance.RestoreMemoryState(fixtureMemory);
            if (Directory.Exists(outerDirectory)) Directory.Delete(outerDirectory, recursive: true);
            yield return null;
        }

        private static string MetaPathIn(string directory) => Path.Combine(directory, MetaSave.FileName);

        [UnityTest]
        public IEnumerator Acquire_모든_저장을_새_임시폴더로_돌리고_메모리를_비운다()
        {
            var guard = new PlayModeSaveGuard();
            guard.Acquire();
            try
            {
                string dir = guard.TestDirectory;
                Assert.AreEqual(dir, SaveSystem.Instance.SaveDirectory);
                StringAssert.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), dir);
                Assert.AreNotEqual(Application.persistentDataPath, SaveSystem.Instance.SaveDirectory);
                Assert.AreNotEqual(outerDirectory, dir);
                Assert.AreSame(SaveFileOperations.Default, SaveSystem.Instance.FileOperations, "가드 안에서는 기본 파일 조작을 쓴다.");
                Assert.IsFalse(MetaSaveService.Instance.IsLoaded, "바깥 메모리 값을 그대로 들고 들어오면 안 된다.");

                MetaSaveService.Instance.AddAbyssShards(3, autoSave: true);
                MetaSaveService.Instance.AddAbyssShards(4, autoSave: true);   // .prev까지 생기게
                yield return null;

                Assert.AreEqual(MetaSaveLoadResult.NewFile, MetaSaveService.Instance.LastLoadResult, "빈 임시 폴더에서 새로 시작해야 한다.");
                Assert.AreEqual(7, MetaSaveService.Instance.Current.abyssShardsTotal);
                Assert.IsTrue(File.Exists(MetaPathIn(dir)));
                Assert.IsTrue(File.Exists(MetaPathIn(dir) + SaveSystem.BACKUP_SUFFIX));
                Assert.AreEqual(0, Directory.GetFiles(outerDirectory).Length, "가드 밖 폴더에 아무것도 쓰면 안 된다.");
            }
            finally
            {
                guard.Release();
            }
        }

        [UnityTest]
        public IEnumerator Release_저장폴더_파일조작_메모리를_그대로_되돌리고_임시폴더를_지운다()
        {
            var guard = new PlayModeSaveGuard();
            guard.Acquire();
            string dir = guard.TestDirectory;
            MetaSaveService.Instance.AddAbyssShards(100, autoSave: true);
            yield return null;

            guard.Release();

            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
            Assert.AreSame(outerOperations, SaveSystem.Instance.FileOperations);
            Assert.IsTrue(MetaSaveService.Instance.IsLoaded);
            Assert.AreSame(outerMeta, MetaSaveService.Instance.Current, "Acquire 전 메모리 객체로 돌아와야 한다.");
            Assert.AreEqual(7, MetaSaveService.Instance.Current.abyssShardsTotal, "테스트 값(100)이 새면 안 된다.");
            Assert.IsFalse(Directory.Exists(dir), "임시 폴더는 지워져야 한다.");
            Assert.AreEqual(0, Directory.GetFiles(outerDirectory).Length);
            Assert.IsFalse(guard.IsAcquired);
            Assert.IsNull(guard.TestDirectory);
        }

        [UnityTest]
        public IEnumerator 같은_가드의_이중_Acquire는_예외이고_격리는_유지된다()
        {
            var guard = new PlayModeSaveGuard();
            guard.Acquire();
            string dir = guard.TestDirectory;
            try
            {
                Assert.Throws<InvalidOperationException>(guard.Acquire);
                Assert.AreEqual(dir, SaveSystem.Instance.SaveDirectory, "실패한 두 번째 Acquire가 격리를 바꾸면 안 된다.");
            }
            finally
            {
                guard.Release();
            }
            yield return null;
            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
        }

        [UnityTest]
        public IEnumerator Acquire_없는_Release와_이중_Release는_아무것도_바꾸지_않는다()
        {
            var guard = new PlayModeSaveGuard();
            guard.Release();                           // SetUp이 Acquire 전에 실패한 TearDown
            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);

            guard.Acquire();
            guard.Release();
            guard.Release();
            yield return null;

            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
            Assert.AreSame(outerOperations, SaveSystem.Instance.FileOperations);
            Assert.AreSame(outerMeta, MetaSaveService.Instance.Current);
        }

        [UnityTest]
        public IEnumerator 다른_가드의_중첩_Acquire는_거절되고_첫_가드_격리가_살아_있다()
        {
            var first = new PlayModeSaveGuard();
            var second = new PlayModeSaveGuard();
            first.Acquire();
            string firstDir = first.TestDirectory;

            try
            {
                Assert.Throws<InvalidOperationException>(second.Acquire, "중첩은 예외로 거절한다 — 자동으로 풀지 않는다.");

                // 첫 가드의 격리가 그대로여야 한다. 여기서 쓰는 저장은 전부 첫 가드 폴더로 간다.
                Assert.IsTrue(first.IsAcquired);
                Assert.IsFalse(second.IsAcquired);
                Assert.IsNull(second.TestDirectory, "거절된 Acquire는 임시 폴더도 만들지 않는다.");
                Assert.AreEqual(firstDir, SaveSystem.Instance.SaveDirectory);
                Assert.IsTrue(Directory.Exists(firstDir));

                MetaSaveService.Instance.AddAbyssShards(5, autoSave: true);
                yield return null;
                Assert.IsTrue(File.Exists(MetaPathIn(firstDir)), "첫 가드의 격리로 저장돼야 한다.");
                Assert.AreEqual(0, Directory.GetFiles(outerDirectory).Length);
            }
            finally
            {
                first.Release();
            }

            // 순서대로 풀린 뒤에야 두 번째 가드가 잡을 수 있고, 그 이전 상태는 바깥 상태다.
            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
            Assert.IsFalse(Directory.Exists(firstDir));

            second.Acquire();
            string secondDir = second.TestDirectory;
            Assert.AreNotEqual(firstDir, secondDir);
            Assert.AreEqual(secondDir, SaveSystem.Instance.SaveDirectory);
            second.Release();
            yield return null;

            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
            Assert.AreSame(outerOperations, SaveSystem.Instance.FileOperations);
            Assert.AreSame(outerMeta, MetaSaveService.Instance.Current);
        }

        [UnityTest]
        public IEnumerator 누수된_가드는_명시적_ForceReleaseActive로만_풀린다()
        {
            var leaked = new PlayModeSaveGuard();
            leaked.Acquire();                           // 앞 테스트의 TearDown이 Release 전에 터진 상황
            string leakedDir = leaked.TestDirectory;

            var next = new PlayModeSaveGuard();
            Assert.Throws<InvalidOperationException>(next.Acquire, "누수도 자동 회복하지 않는다 — 다음 Acquire는 멈춘다.");
            Assert.IsTrue(leaked.IsAcquired, "거절이 남은 가드를 풀어 버리면 안 된다.");
            Assert.AreEqual(leakedDir, SaveSystem.Instance.SaveDirectory);

            LogAssert.Expect(LogType.Warning, new Regex("강제로 푼다"));
            Assert.IsTrue(PlayModeSaveGuard.ForceReleaseActive());
            yield return null;

            Assert.IsFalse(leaked.IsAcquired);
            Assert.IsFalse(PlayModeSaveGuard.HasActiveGuard);
            Assert.IsFalse(Directory.Exists(leakedDir), "강제 해제도 임시 폴더를 정리한다.");
            Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
            Assert.AreSame(outerOperations, SaveSystem.Instance.FileOperations);
            Assert.AreSame(outerMeta, MetaSaveService.Instance.Current);

            Assert.IsFalse(PlayModeSaveGuard.ForceReleaseActive(), "풀 가드가 없으면 아무 일도 없다.");

            next.Acquire();                             // 회복 뒤에는 정상적으로 잡힌다
            next.Release();
        }

        [UnityTest]
        public IEnumerator 임시폴더_정리에_실패해도_되돌리기는_끝나고_경고만_남는다()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("열린 파일 때문에 폴더 삭제가 실패하는 동작은 Windows 전용이다.");

            var guard = new PlayModeSaveGuard();
            guard.Acquire();
            string dir = guard.TestDirectory;
            string pinned = Path.Combine(dir, "pinned.txt");
            File.WriteAllText(pinned, "x");

            using (new FileStream(pinned, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                LogAssert.Expect(LogType.Warning, new Regex("임시 저장 폴더 정리 실패"));
                Assert.DoesNotThrow(guard.Release);

                Assert.AreEqual(outerDirectory, SaveSystem.Instance.SaveDirectoryOverride);
                Assert.AreSame(outerOperations, SaveSystem.Instance.FileOperations);
                Assert.AreSame(outerMeta, MetaSaveService.Instance.Current);
                Assert.IsFalse(guard.IsAcquired);
            }
            yield return null;

            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
