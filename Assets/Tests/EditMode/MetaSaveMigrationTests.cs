using System.Collections.Generic;
using System.Text.RegularExpressions;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// MetaSave 스키마 버전 변환기 테스트.
    ///
    /// 단계표가 아직 비어 있어(v1이 현재 스키마) 기본 경로만으로는 Migrated 분기가 한 번도
    /// 실행되지 않는다. 그래서 단계표를 주입하는 오버로드로 <b>변환 순서·all-or-nothing·버전 스탬프</b>를
    /// 지금 검증한다 — 실제 단계가 생겼을 때 처음 확인하는 것은 순서가 거꾸로다.
    ///
    /// 디스크와의 협력(백업 파일 생성·손상 폴백)은 MetaSaveServicePlayModeSmoke가 담당.
    /// </summary>
    public sealed class MetaSaveMigrationTests
    {
        private static MetaSave SaveAt(int version, int abyss = 0) =>
            new() { version = version, abyssShardsTotal = abyss };

        // ───────────────────────── 버전 스탬프 소유권 ─────────────────────────

        [Test]
        public void CreateNew_StampsCurrentVersion()
        {
            Assert.AreEqual(MetaSave.CurrentVersion, MetaSave.CreateNew().version);
        }

        [Test]
        public void DefaultConstructor_StaysAtMinimumVersion()
        {
            // 기본 생성자가 CurrentVersion을 찍으면, version 키가 없는 낡은 JSON이
            // 역직렬화될 때 "이미 최신"으로 위장해 마이그레이션을 건너뛴다.
            Assert.AreEqual(MetaSave.MinimumVersion, new MetaSave().version);
        }

        [Test]
        public void Touch_ClampsBelowMinimum_ButNeverRewritesValidVersion()
        {
            var low = SaveAt(0);
            low.Touch();
            Assert.AreEqual(MetaSave.MinimumVersion, low.version);

            // 저장이 버전을 임의로 바꾸면, 변환에 실패한 세이브가 '변환된 척' 기록되어
            // 다음 로드에서 마이그레이션을 건너뛴다.
            var high = SaveAt(MetaSave.CurrentVersion + 5);
            high.Touch();
            Assert.AreEqual(MetaSave.CurrentVersion + 5, high.version);
        }

        // ───────────────────────── 기본 단계표 경로 ─────────────────────────

        [Test]
        public void Run_CurrentVersion_ReturnsUpToDate()
        {
            var save = SaveAt(MetaSave.CurrentVersion, abyss: 120);

            var outcome = MetaSaveMigration.Run(save, out int fileVersion);

            Assert.AreEqual(MetaSaveMigrationOutcome.UpToDate, outcome);
            Assert.AreEqual(MetaSave.CurrentVersion, fileVersion);
            Assert.AreEqual(120, save.abyssShardsTotal);
        }

        [Test]
        public void Run_VersionBelowMinimum_ClampsAndPreservesData()
        {
            var save = SaveAt(0, abyss: 42);

            var outcome = MetaSaveMigration.Run(save, out int fileVersion);

            // 백업 파일명·로그는 파일에 적혀 있던 원래 값을 써야 한다(보정 후 값이 아니라).
            Assert.AreEqual(0, fileVersion);
            Assert.AreEqual(MetaSave.MinimumVersion, save.version);
            Assert.AreEqual(42, save.abyssShardsTotal);
            // MinimumVersion == CurrentVersion 인 동안은 UpToDate, 버전이 올라가면 Migrated가 된다.
            Assert.That(outcome, Is.EqualTo(MetaSaveMigrationOutcome.UpToDate)
                                   .Or.EqualTo(MetaSaveMigrationOutcome.Migrated));
        }

        [Test]
        public void Run_FutureVersion_LeavesSaveUntouched()
        {
            int future = MetaSave.CurrentVersion + 1;
            var save = SaveAt(future, abyss: 99);

            var outcome = MetaSaveMigration.Run(save, out int fileVersion);

            Assert.AreEqual(MetaSaveMigrationOutcome.FutureVersion, outcome);
            Assert.AreEqual(future, fileVersion);
            // 미래 버전을 현재 버전으로 끌어내리면, 다음 로드에서 손실이 굳어 복구 근거가 사라진다.
            Assert.AreEqual(future, save.version);
            Assert.AreEqual(99, save.abyssShardsTotal);
        }

        [Test]
        public void Run_NullSave_ReturnsIncomplete()
        {
            Assert.AreEqual(MetaSaveMigrationOutcome.Incomplete,
                MetaSaveMigration.Run(null, out int fileVersion));
            Assert.AreEqual(0, fileVersion);
        }

        // ───────────────────────── 단계 주입 경로 ─────────────────────────

        [Test]
        public void Run_WithSteps_AppliesEachStepInOrder_AndStampsTargetVersion()
        {
            var order = new List<int>();
            var steps = new Dictionary<int, MetaSaveMigration.UpgradeStep>
            {
                { 1, s => { order.Add(1); s.abyssShardsTotal += 10; } },
                { 2, s => { order.Add(2); s.abyssShardsTotal *= 2; } }
            };

            var save = SaveAt(1, abyss: 5);
            var outcome = MetaSaveMigration.Run(save, targetVersion: 3, steps, out int fileVersion);

            Assert.AreEqual(MetaSaveMigrationOutcome.Migrated, outcome);
            Assert.AreEqual(1, fileVersion);
            Assert.AreEqual(3, save.version);
            CollectionAssert.AreEqual(new[] { 1, 2 }, order, "단계는 낮은 버전부터 하나씩 이어져야 한다.");
            Assert.AreEqual(30, save.abyssShardsTotal, "(5 + 10) * 2 — 순서가 뒤바뀌면 값이 달라진다.");
        }

        [Test]
        public void Run_WithSteps_EachStepSeesVersionOfItsOwnStage()
        {
            var seen = new List<int>();
            var steps = new Dictionary<int, MetaSaveMigration.UpgradeStep>
            {
                { 1, s => seen.Add(s.version) },
                { 2, s => seen.Add(s.version) }
            };

            MetaSaveMigration.Run(SaveAt(1), targetVersion: 3, steps, out _);

            // 단계가 실행되는 시점의 version은 '아직 변환 전' 값이어야 한다 —
            // 중간 단계가 자기 앞 버전을 다시 읽어도 일관되게 보이도록.
            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void Run_MissingIntermediateStep_AppliesNothing()
        {
            bool firstStepRan = false;
            var steps = new Dictionary<int, MetaSaveMigration.UpgradeStep>
            {
                { 1, s => { firstStepRan = true; s.abyssShardsTotal = 999; } }
                // 2 → 3 단계 없음.
            };

            var save = SaveAt(1, abyss: 7);
            LogAssert.Expect(LogType.Error, new Regex("변환 단계가 없어"));

            var outcome = MetaSaveMigration.Run(save, targetVersion: 3, steps, out _);

            Assert.AreEqual(MetaSaveMigrationOutcome.Incomplete, outcome);
            // '절반만 변환된' 상태는 어느 버전으로도 설명되지 않아, 디스크에 남으면 복구할 길이 없다.
            Assert.IsFalse(firstStepRan, "단계 하나라도 적용됐다면 all-or-nothing이 깨진 것이다.");
            Assert.AreEqual(1, save.version);
            Assert.AreEqual(7, save.abyssShardsTotal);
        }

        [Test]
        public void Run_NullSteps_ReturnsIncomplete_WithoutThrowing()
        {
            var save = SaveAt(1, abyss: 3);
            LogAssert.Expect(LogType.Error, new Regex("변환 단계가 없어"));

            var outcome = MetaSaveMigration.Run(save, targetVersion: 2, null, out _);

            Assert.AreEqual(MetaSaveMigrationOutcome.Incomplete, outcome);
            Assert.AreEqual(1, save.version);
            Assert.AreEqual(3, save.abyssShardsTotal);
        }
    }
}
