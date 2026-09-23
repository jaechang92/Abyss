using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// <see cref="SaveSystemSafeWriteTests"/>의 두 번째 부분 — 열지 못함(Inaccessible)과 손상(Unreadable)의 구분,
    /// 유한 재시도, 잠금 해제 후 재시도, File.Replace 미지원 폴백. 픽스처(SetUp·임시 폴더·실패 주입)는 본체와 공유한다.
    /// </summary>
    public sealed partial class SaveSystemSafeWriteTests
    {
        // ───────────────────────── 열지 못함(Inaccessible) ≠ 손상(Unreadable) ─────────────────────────

        private static void ExpectLoadErrorLog() =>
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 본 파일을 열지 못했다"));

        private void LockPrimaryForReads(int failures)
        {
            faults.ReadFailPath = PrimaryPath;
            faults.ReadFailuresLeft = failures;
            faults.ReadAttempts = 0;
        }

        [Test]
        public void 열지_못한_본파일은_Inaccessible이고_직전정상본으로_대신하지_않는다()
        {
            SaveTwoGenerations();                       // 본=2, 직전=1
            LockPrimaryForReads(-1);

            ExpectLoadErrorLog();
            var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var primary, out var backup);

            Assert.IsNull(data, "최신본을 확인하지 못했는데 옛 직전 정상본(1)을 현재값으로 내주면 안 된다.");
            Assert.AreEqual(SaveFileStatus.Inaccessible, primary);
            Assert.AreEqual(SaveFileStatus.NotChecked, backup);
            Assert.AreEqual(2, ReadValue(PrimaryPath), "로드는 파일을 바꾸지 않는다.");
        }

        [Test]
        public void 열지_못하면_READ_ATTEMPTS번에서_멈추고_부를_때마다_다시_센다()
        {
            SaveTwoGenerations();
            LockPrimaryForReads(-1);

            ExpectLoadErrorLog();
            saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out _, out _);
            Assert.AreEqual(SaveSystem.READ_ATTEMPTS, faults.ReadAttempts, "재시도는 정해진 횟수까지만 — 무제한 재시도 금지.");

            ExpectLoadErrorLog();
            saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out _, out _);
            Assert.AreEqual(SaveSystem.READ_ATTEMPTS * 2, faults.ReadAttempts, "호출 사이에 숨은 반복이 없어야 한다.");
        }

        [Test]
        public void 짧은_잠금이_재시도_안에서_풀리면_그대로_최신본을_읽는다()
        {
            SaveTwoGenerations();
            LockPrimaryForReads(SaveSystem.READ_ATTEMPTS - 1);

            Assert.AreEqual(2, LoadValue(out var primary, out var backup));
            Assert.AreEqual(SaveFileStatus.Loaded, primary);
            Assert.AreEqual(SaveFileStatus.NotChecked, backup);
            Assert.AreEqual(SaveSystem.READ_ATTEMPTS, faults.ReadAttempts);
        }

        [Test]
        public void 손상은_재시도하지_않는다()
        {
            SaveTwoGenerations();
            File.WriteAllText(PrimaryPath, "{ 깨짐");
            LockPrimaryForReads(0);                     // 실패는 주입하지 않고 시도 횟수만 센다

            LoadValue(out var primary, out _);
            Assert.AreEqual(SaveFileStatus.Unreadable, primary);
            Assert.AreEqual(1, faults.ReadAttempts, "다시 읽어도 같은 내용이다 — 손상에 재시도는 의미가 없다.");
        }

        [Test]
        public void 열지_못한_본파일_위에는_저장하지_않고_풀린_뒤에는_정상_저장한다()
        {
            SaveTwoGenerations();
            LockPrimaryForReads(-1);

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3), "확인하지 못한 최신본을 덮으면 안 된다.");
            Assert.AreEqual(2, ReadValue(PrimaryPath));
            Assert.AreEqual(1, ReadValue(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, savedCount);

            faults.ReadFailPath = null;                 // 잠금 해제
            Assert.IsTrue(SaveValue(3));
            Assert.AreEqual(3, ReadValue(PrimaryPath));
            Assert.AreEqual(2, ReadValue(BackupPath));
        }

        [Test]
        public void Windows_실제_잠금이_풀린_뒤_다시_읽으면_최신본을_읽는다()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("공유 금지 잠금(FileShare.None)으로 읽기를 막는 동작은 Windows 전용이다.");

            SaveTwoGenerations();

            using (new FileStream(PrimaryPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                ExpectLoadErrorLog();
                var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var locked, out _);
                Assert.IsNull(data);
                Assert.AreEqual(SaveFileStatus.Inaccessible, locked);

                ExpectSaveFailedLog();
                Assert.IsFalse(SaveValue(3));
            }

            Assert.AreEqual(2, LoadValue(out var primary, out _), "잠금이 풀리면 최신본(2)으로 돌아와야 한다.");
            Assert.AreEqual(SaveFileStatus.Loaded, primary);
            Assert.AreEqual(1, ReadValue(BackupPath), "잠긴 동안의 실패가 직전 정상본을 바꾸지 않았다.");
        }

        // ───── 접근 거부는 "파일 없음"이 아니다(File.Exists 선검사를 두면 둘이 섞인다) ─────
        //
        // File.Exists는 존재하지 않을 때도, 확인할 권한이 없을 때도 false다. 선검사로 Missing을 확정하면
        // 권한 실패가 "세이브가 없다"로 둔갑해 새 세이브가 시작되고, 그 저장이 멀쩡한 진행도를 밀어낸다.

        [Test]
        public void 없는_파일도_직접_읽어_보고_판정한다()
        {
            // 주입이 걸린다는 것은 곧 File.Exists 선검사로 건너뛰지 않았다는 뜻이다.
            Assert.IsFalse(File.Exists(PrimaryPath), "전제: 본 파일이 없다.");
            LockPrimaryForReads(0);                     // 실패는 주입하지 않고 시도 횟수만 센다

            saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var primary, out _);

            Assert.AreEqual(SaveFileStatus.Missing, primary, "정말 없으면 Missing이 맞다.");
            Assert.AreEqual(1, faults.ReadAttempts, "읽기를 한 번은 실제로 시도해야 한다.");
        }

        [Test]
        public void 접근_거부는_파일이_없어_보여도_Missing이_아니라_Inaccessible이다()
        {
            Assert.IsFalse(File.Exists(PrimaryPath), "전제: 권한 때문에 존재 확인조차 안 되는 상황을 흉내 낸다.");
            LockPrimaryForReads(-1);
            faults.IsReadFailureAccessDenied = true;

            ExpectLoadErrorLog();
            var data = saveSystem.LoadWithBackup<TestSave>(FILE_NAME, out var primary, out var backup);

            Assert.IsNull(data);
            Assert.AreEqual(SaveFileStatus.Inaccessible, primary, "권한 실패가 '세이브 없음'으로 둔갑하면 안 된다.");
            Assert.AreEqual(SaveFileStatus.NotChecked, backup);
            Assert.AreEqual(SaveSystem.READ_ATTEMPTS, faults.ReadAttempts);
        }

        [Test]
        public void 접근_거부면_본파일이_없어_보여도_새로_만들지_않는다()
        {
            Assert.IsFalse(File.Exists(PrimaryPath));
            LockPrimaryForReads(-1);
            faults.IsReadFailureAccessDenied = true;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(1), "확인하지 못한 자리에 최초 저장을 밀어 넣으면 안 된다.");

            Assert.IsFalse(File.Exists(PrimaryPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(0, savedCount);
        }

        // ───────────────────────── File.Replace 미지원 폴백 ─────────────────────────
        // 실제 미지원 플랫폼에서 돌리는 것이 아니라, 폴백 구현(SaveFileOperations.ReplaceByCopy)을 Windows 에디터에서
        // 직접 태워 문서화한 보장(한 번의 중단 = 이번 저장 한 세대 손실)을 확인한다.

        [Test]
        public void 폴백_교체는_본파일을_새것으로_직전정상본을_이전_세대로_만든다()
        {
            SaveTwoGenerations();
            faults.FailAt = FaultStep.ReplaceByCopy;

            Assert.IsTrue(SaveValue(3));

            Assert.AreEqual(3, ReadValue(PrimaryPath));
            Assert.AreEqual(2, ReadValue(BackupPath));
            Assert.IsFalse(File.Exists(TempPath));
            Assert.AreEqual(1, savedCount);
        }

        [Test]
        public void 폴백_교체가_본파일을_덮다_끊겨도_이전_세대로_복구된다()
        {
            SaveTwoGenerations();
            faults.FailAt = FaultStep.ReplaceByCopyCut;

            ExpectSaveFailedLog();
            Assert.IsFalse(SaveValue(3));

            Assert.AreEqual(2, LoadValue(out var primary, out var backup), "잃는 것은 이번 저장 한 세대뿐이어야 한다.");
            Assert.AreEqual(SaveFileStatus.Unreadable, primary);
            Assert.AreEqual(SaveFileStatus.Loaded, backup);
        }
    }
}
