using Abyss.Runtime.Meta;
using Abyss.Runtime.Story;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 스토리 챕터 선택 규칙 테스트 — 한 자료 구조가 두 모델을 담는다.
    ///
    /// <b>기록자는 연재</b>(순차 개방, 진행 델타로 해금), <b>각인사는 사전</b>(폼 발견으로 항목별 개방).
    /// 이 테스트의 핵심은 각인사를 얹어도 <b>기록자 동작이 그대로인가</b>이고,
    /// 그다음이 두 화자가 서로의 진행도를 먹지 않는가다.
    ///
    /// 셀렉터가 MetaSaveService를 인자로 받기 때문에 씬 없이 검증할 수 있다.
    /// </summary>
    public sealed class StoryChapterSelectorTests
    {
        private MetaSaveService service;
        private StoryData chroniclerStory;
        private StoryData engraverStory;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("[Test] MetaSaveService");
            service = go.AddComponent<MetaSaveService>();
            service.ResetAll(autoSave: false);

            // 기록자: 폼 조건 없음, 델타로만 열린다(실제 Chronicler.asset과 같은 모양).
            chroniclerStory = ScriptableObject.CreateInstance<StoryData>();
            chroniclerStory.chapters = new[]
            {
                Chapter(1, minRunCount: 0, minBossKills: 0),
                Chapter(2, minRunCount: 1, minBossKills: 0),
                Chapter(3, minRunCount: 0, minBossKills: 1),
            };

            // 각인사: 델타 조건 없음, 폼 발견(런에서 실제 사용)으로만 열린다(실제 Engraver.asset과 같은 모양).
            engraverStory = ScriptableObject.CreateInstance<StoryData>();
            engraverStory.chapters = new[]
            {
                FormChapter(1, "dark_blade"),
                FormChapter(2, "void_archer"),
                FormChapter(3, "ancient_shield"),
                FormChapter(4, "void_thrower"),
                ClosingChapter(99, minViewedChapters: 4),
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null) Object.DestroyImmediate(service.gameObject);
            if (chroniclerStory != null) Object.DestroyImmediate(chroniclerStory);
            if (engraverStory != null) Object.DestroyImmediate(engraverStory);
        }

        private static StoryChapter Chapter(int stage, int minRunCount, int minBossKills) =>
            new() { chapterStage = stage, minRunCount = minRunCount, minBossKills = minBossKills };

        private static StoryChapter FormChapter(int stage, string formId) =>
            new() { chapterStage = stage, requiredFormId = formId };

        /// <summary>폼 조건 없이 "이 화자의 것을 이만큼 본 뒤"로만 열리는 챕터(각인사 닫는 말).</summary>
        private static StoryChapter ClosingChapter(int stage, int minViewedChapters) =>
            new() { chapterStage = stage, minViewedChapters = minViewedChapters };

        private void SetTotals(int runCount, int bossKills)
        {
            var records = service.Current.records;
            records.totalRunCount = runCount;
            records.totalBossKillCount = bossKills;
        }

        private bool TryPickChronicler(out StoryChapter picked) =>
            StoryChapterSelector.TryPickNext(chroniclerStory, service, StorySpeakerIds.Chronicler, out picked);

        private bool TryPickEngraver(out StoryChapter picked) =>
            StoryChapterSelector.TryPickNext(engraverStory, service, StorySpeakerIds.Engraver, out picked);

        // ───────────────────────── 기록자 — 기존 동작 보존 ─────────────────────────

        [Test]
        public void Chronicler_FreshSave_OffersFirstChapter()
        {
            Assert.IsTrue(TryPickChronicler(out var picked));
            Assert.AreEqual(1, picked.chapterStage);
        }

        [Test]
        public void Chronicler_WithoutProgress_StopsAtLockedChapter()
        {
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 0, 0, autoSave: false);

            Assert.IsFalse(TryPickChronicler(out _), "런 델타가 없으면 2단계는 안 열린다.");
        }

        [Test]
        public void Chronicler_LockedChapter_DoesNotLetLaterChapterJumpAhead()
        {
            // 🔴 이 테스트가 연재 모델의 핵심이다. 후보 전체에 델타를 물으면
            // 조건이 다른 3단계(보스 1)가 막힌 2단계(런 1)를 건너뛰고 먼저 열린다.
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 0, 0, autoSave: false);
            SetTotals(runCount: 0, bossKills: 1);

            Assert.IsFalse(TryPickChronicler(out _), "2단계가 막혀 있으면 3단계도 열리면 안 된다.");
        }

        [Test]
        public void Chronicler_AfterProgress_OffersNextChapter()
        {
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 0, 0, autoSave: false);
            SetTotals(runCount: 1, bossKills: 0);

            Assert.IsTrue(TryPickChronicler(out var picked));
            Assert.AreEqual(2, picked.chapterStage);
        }

        [Test]
        public void Chronicler_DeltaIsMeasuredFromLastViewing_NotFromZero()
        {
            // 스냅샷을 안 쓰면 런 1회로 남은 챕터가 한꺼번에 열린다.
            SetTotals(runCount: 1, bossKills: 0);
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 1, 0, autoSave: false);

            Assert.IsFalse(TryPickChronicler(out _), "시청 시점 이후의 추가 진전이 있어야 한다.");
        }

        // ───────────────────────── 각인사 — 사전 모델 ─────────────────────────

        [Test]
        public void Engraver_NoFormsDiscovered_OffersNothing()
        {
            Assert.IsFalse(TryPickEngraver(out _));
        }

        [Test]
        public void Engraver_DiscoveredForm_OffersItsLore()
        {
            service.DiscoverForm("ancient_shield", autoSave: false);

            Assert.IsTrue(TryPickEngraver(out var picked));
            Assert.AreEqual(3, picked.chapterStage);
        }

        [Test]
        public void Engraver_LaterFormFirst_DoesNotConsumeEarlierLore()
        {
            // 🔴 화자를 나눈 것만으로는 부족하다 — 한 화자 안에서 '최댓값 하나'로 진행도를 들고 있으면
            // 3번을 먼저 본 순간 1·2번이 시청 처리되어 같은 결함이 되풀이된다.
            service.DiscoverForm("ancient_shield", autoSave: false);
            service.MarkChapterViewed(StorySpeakerIds.Engraver, 3, 0, 0, autoSave: false);

            service.DiscoverForm("dark_blade", autoSave: false);

            Assert.IsTrue(TryPickEngraver(out var picked));
            Assert.AreEqual(1, picked.chapterStage);
        }

        [Test]
        public void Engraver_ViewedLore_IsNotRepeated()
        {
            service.DiscoverForm("dark_blade", autoSave: false);
            service.MarkChapterViewed(StorySpeakerIds.Engraver, 1, 0, 0, autoSave: false);

            Assert.IsFalse(TryPickEngraver(out _));
        }

        [Test]
        public void Engraver_UndiscoveredForm_DoesNotBlockOthers()
        {
            // 사전 모델의 요점 — 1번 폼을 안 써 봤어도 2번 폼의 내력은 열려야 한다.
            service.DiscoverForm("void_archer", autoSave: false);

            Assert.IsTrue(TryPickEngraver(out var picked));
            Assert.AreEqual(2, picked.chapterStage);
        }

        // ───────────────────────── 화자 격리 ─────────────────────────

        [Test]
        public void ViewingOneSpeaker_DoesNotAdvanceTheOther()
        {
            service.DiscoverForm("dark_blade", autoSave: false);
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 0, 0, autoSave: false);

            Assert.IsTrue(TryPickEngraver(out var picked), "기록자 시청이 각인사 내력을 소진하면 안 된다.");
            Assert.AreEqual(1, picked.chapterStage);
        }

        [Test]
        public void ViewingOneSpeaker_DoesNotResetTheOthersSnapshot()
        {
            // 옛 스키마에서는 각인사와 대화하는 것만으로 기록자의 델타 기준점이 초기화됐다.
            SetTotals(runCount: 3, bossKills: 0);
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 3, 0, autoSave: false);

            service.DiscoverForm("dark_blade", autoSave: false);
            service.MarkChapterViewed(StorySpeakerIds.Engraver, 1, 3, 0, autoSave: false);

            service.GetStorySnapshots(StorySpeakerIds.Chronicler, out int runSnapshot, out _);
            Assert.AreEqual(3, runSnapshot);
            Assert.IsFalse(TryPickChronicler(out _), "기록자는 여전히 추가 진전을 기다려야 한다.");
        }

        [Test]
        public void MarkChapterViewed_SameChapterTwice_KeepsFirstSnapshot()
        {
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 2, 0, autoSave: false);
            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 9, 0, autoSave: false);

            service.GetStorySnapshots(StorySpeakerIds.Chronicler, out int runSnapshot, out _);
            Assert.AreEqual(2, runSnapshot, "이미 본 챕터의 재기록은 델타 기준점을 밀면 안 된다.");
        }

        // ───────────────────── 각인사 닫는 말 — "다 본 뒤"에만 열린다 ─────────────────────

        /// <summary>
        /// 🔴 이 테스트가 조건을 추가한 이유다. 닫는 말은 폼 조건이 없어서, 그것만으로는
        /// <b>아무 폼도 안 써 본 새 세이브에서 제일 먼저 열린다</b> — 사전 모델에 순서가 없기 때문이다.
        /// </summary>
        [Test]
        public void Engraver_FreshSave_DoesNotOfferClosingChapter()
        {
            Assert.IsFalse(TryPickEngraver(out _), "폼을 하나도 안 써 봤는데 닫는 말이 열리면 안 된다.");
        }

        [Test]
        public void Engraver_ClosingChapter_StaysShutUntilAllFourViewed()
        {
            for (int stage = 1; stage <= 3; stage++)
            {
                service.MarkChapterViewed(StorySpeakerIds.Engraver, stage, 0, 0, autoSave: false);
            }
            // 넷째 폼을 발견했지만 아직 그 내력을 안 봤다 → 닫는 말이 아니라 넷째가 나와야 한다.
            service.DiscoverForm("void_thrower", autoSave: false);

            Assert.IsTrue(TryPickEngraver(out var picked));
            Assert.AreEqual(4, picked.chapterStage, "셋만 본 상태에서 닫는 말이 넷째를 가로채면 안 된다.");
        }

        [Test]
        public void Engraver_ClosingChapter_OpensAfterAllFourViewed()
        {
            for (int stage = 1; stage <= 4; stage++)
            {
                service.MarkChapterViewed(StorySpeakerIds.Engraver, stage, 0, 0, autoSave: false);
            }

            Assert.IsTrue(TryPickEngraver(out var picked), "넷을 다 본 뒤에는 닫는 말이 열려야 한다.");
            Assert.AreEqual(99, picked.chapterStage);
        }

        /// <summary>닫는 말은 <b>한 번뿐</b>이다 — 반복되면 아크 씨앗의 무게가 사라진다.</summary>
        [Test]
        public void Engraver_ClosingChapter_PlaysOnlyOnce()
        {
            for (int stage = 1; stage <= 4; stage++)
            {
                service.MarkChapterViewed(StorySpeakerIds.Engraver, stage, 0, 0, autoSave: false);
            }
            Assert.IsTrue(TryPickEngraver(out var picked));
            service.MarkChapterViewed(StorySpeakerIds.Engraver, picked.chapterStage, 0, 0, autoSave: false);

            Assert.IsFalse(TryPickEngraver(out _), "닫는 말이 두 번 나오면 안 된다.");
        }

        /// <summary>기록자 에셋은 minViewedChapters가 0이라 조건 추가 전과 똑같이 동작해야 한다.</summary>
        [Test]
        public void Chronicler_Unaffected_ByViewedCountCondition()
        {
            Assert.IsTrue(TryPickChronicler(out var first));
            Assert.AreEqual(1, first.chapterStage);

            service.MarkChapterViewed(StorySpeakerIds.Chronicler, 1, 0, 0, autoSave: false);
            SetTotals(runCount: 1, bossKills: 0);

            Assert.IsTrue(TryPickChronicler(out var second));
            Assert.AreEqual(2, second.chapterStage);
        }
    }
}
