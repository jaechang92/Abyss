using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 저장 알림 계약 — MetaSaveService의 저장 상태 이벤트·명시적 재시도(<see cref="MetaSaveService.RetrySaveAccess"/>)와
    /// 그것을 화면에 띄우는 <see cref="SaveStatusOverlay"/>.
    ///
    /// 보류 자체의 디스크 계약(최신본을 덮지 않음 · 숨은 재시도 없음)은 <see cref="MetaSaveAccessPlayModeSmoke"/>가 고정한다.
    /// 여기는 「사용자가 그 상태를 알 수 있고, 누를 때만 재시도된다」를 고정한다.
    /// </summary>
    public sealed class SaveStatusPlayModeSmoke
    {
        private sealed class FailingFileOperations : SaveFileOperations
        {
            public string LockedPath;
            public bool IsWriteFailing;

            public override string ReadAllText(string path)
            {
                if (path == LockedPath) throw new IOException("테스트: 다른 프로세스가 파일을 사용 중");
                return base.ReadAllText(path);
            }

            public override void WriteAllText(string path, string contents)
            {
                if (IsWriteFailing) throw new IOException("테스트: 디스크 쓰기 실패");
                base.WriteAllText(path, contents);
            }
        }

        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private FailingFileOperations ops;
        private string savePath;
        private int statusChangedCount;
        private int writeFailedCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            ops = new FailingFileOperations();
            SaveSystem.Instance.SetFileOperations(ops);

            statusChangedCount = 0;
            writeFailedCount = 0;
            MetaSaveService.Instance.OnSaveStatusChanged += CountStatusChanged;
            MetaSaveService.Instance.OnSaveWriteFailed += CountWriteFailed;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var service = MetaSaveService.GetInstanceSafe();
            if (service != null)
            {
                service.OnSaveStatusChanged -= CountStatusChanged;
                service.OnSaveWriteFailed -= CountWriteFailed;
            }

            // 오버레이는 DontDestroyOnLoad라 다음 테스트로 새어 나간다 — 여기서 만든 것은 여기서 지운다.
            var overlay = Object.FindAnyObjectByType<SaveStatusOverlay>();
            if (overlay != null) Object.Destroy(overlay.gameObject);

            saveGuard.Release();   // 파일 조작 구현·메모리 상태도 Acquire 전 값으로 돌아간다
            yield return null;
        }

        private void CountStatusChanged() => statusChangedCount++;
        private void CountWriteFailed() => writeFailedCount++;

        private static string CurrentVersionJson(int shards) =>
            $"{{\"version\":{MetaSave.CurrentVersion},\"abyssShardsTotal\":{shards}}}";

        private static void ExpectBlockedLoadErrors()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] (본 파일을 열지 못했다|불러오기 실패)"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[MetaSaveService\] 세이브를 열지 못했다"));
        }

        private MetaSaveService LoadBlocked(int shardsOnDisk)
        {
            File.WriteAllText(savePath, CurrentVersionJson(shardsOnDisk));
            ops.LockedPath = savePath;
            var svc = MetaSaveService.Instance;
            ExpectBlockedLoadErrors();
            svc.Reload();
            return svc;
        }

        [UnityTest]
        public IEnumerator 보류_시작과_재시도_성공에만_상태_이벤트가_발생한다()
        {
            var svc = LoadBlocked(20);
            yield return null;
            Assert.IsTrue(svc.IsSaveBlocked);
            Assert.AreEqual(1, statusChangedCount, "보류 시작에서 한 번.");

            ExpectBlockedLoadErrors();
            Assert.IsFalse(svc.RetrySaveAccess(), "잠금이 그대로면 재시도는 실패한다.");
            Assert.AreEqual(1, statusChangedCount, "보류 → 보류는 상태 변화가 아니다.");

            ops.LockedPath = null;
            Assert.IsTrue(svc.RetrySaveAccess());
            yield return null;

            Assert.IsFalse(svc.IsSaveBlocked);
            Assert.AreEqual(2, statusChangedCount, "보류 해제에서 한 번 더.");
            Assert.AreEqual(20, svc.Current.abyssShardsTotal, "재시도 성공 뒤 화면이 읽는 값은 디스크 값이다.");
        }

        [UnityTest]
        public IEnumerator 보류가_아니면_재시도는_메모리_진행을_버리지_않는다()
        {
            File.WriteAllText(savePath, CurrentVersionJson(20));
            var svc = MetaSaveService.Instance;
            svc.Reload();
            svc.AddAbyssShards(5, autoSave: false);   // 아직 저장하지 않은 진행
            yield return null;

            Assert.IsTrue(svc.RetrySaveAccess());
            Assert.AreEqual(25, svc.Current.abyssShardsTotal, "보류가 아닌데 Reload하면 저장 전 진행을 디스크 값으로 덮는다.");
            Assert.AreEqual(0, statusChangedCount);
        }

        [UnityTest]
        public IEnumerator 쓰기_실패는_알리고_보류_중_저장_거부는_알리지_않는다()
        {
            File.WriteAllText(savePath, CurrentVersionJson(20));
            var svc = MetaSaveService.Instance;
            svc.Reload();

            ops.IsWriteFailing = true;
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패:"));
            Assert.IsFalse(svc.Save());
            Assert.AreEqual(1, writeFailedCount);
            ops.IsWriteFailing = false;
            yield return null;

            LoadBlocked(20);
            svc.AddAbyssShards(1, autoSave: true);
            svc.AddAbyssShards(1, autoSave: true);
            yield return null;

            Assert.AreEqual(1, writeFailedCount, "보류 중 autoSave마다 알림이 쏟아지면 안 된다 — 보류는 상시 표시가 알린다.");
        }

        [UnityTest]
        public IEnumerator 오버레이_보류면_모달_계속하면_표시_재시도_성공하면_모두_사라진다()
        {
            var svc = LoadBlocked(20);
            SaveStatusOverlay.Ensure();
            yield return null;

            Assert.IsTrue(SaveStatusOverlay.IsModalOpen, "부팅 중에 시작된 보류도 생성 시점에 모달로 보여야 한다.");
            Assert.IsFalse(SaveStatusOverlay.IsBadgeVisible);

            var overlay = Object.FindAnyObjectByType<SaveStatusOverlay>();
            var continueButton = overlay.transform.Find("Body/Panel/ContinueButton").GetComponent<UnityEngine.UI.Button>();
            continueButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(SaveStatusOverlay.IsModalOpen);
            Assert.IsTrue(SaveStatusOverlay.IsBadgeVisible, "저장 없이 계속해도 「저장 안 됨」은 남아야 한다.");

            ops.LockedPath = null;
            var badgeButton = overlay.transform.Find("Badge").GetComponent<UnityEngine.UI.Button>();
            badgeButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(svc.IsSaveBlocked);
            Assert.IsFalse(SaveStatusOverlay.IsModalOpen);
            Assert.IsFalse(SaveStatusOverlay.IsBadgeVisible);
        }
    }
}
