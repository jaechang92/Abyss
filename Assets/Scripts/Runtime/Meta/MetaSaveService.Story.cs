using System;
using System.Collections.Generic;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// <see cref="MetaSaveService"/>의 스토리 진행도 부분 — 화자별 시청 기록 조회·갱신.
    /// (본체 MetaSaveService.cs와 같은 partial 클래스라 EnsureLoaded·current·Save를 공유한다.)
    ///
    /// 모든 API가 speakerId를 받는다(<see cref="StorySpeakerIds"/>). 화자를 뺀 오버로드를 두지 않는 것은
    /// 의도적이다 — 옛 전역 API가 남아 있으면 화자를 지정하지 않은 호출이 조용히 통과하고,
    /// 그 호출이 어느 화자의 진행도를 건드리는지는 아무도 답할 수 없다.
    /// </summary>
    public sealed partial class MetaSaveService
    {
        /// <summary>
        /// 해당 화자가 시청을 마친 챕터 단계 목록. 항목이 없으면 빈 목록(널 아님).
        /// 반환값은 읽기 전용 뷰다 — 진행 기록은 <see cref="MarkChapterViewed"/>로만 바꾼다.
        /// </summary>
        public IReadOnlyList<int> GetViewedChapterStages(string speakerId)
        {
            EnsureLoaded();
            var entry = FindStoryProgress(speakerId);
            return entry != null ? entry.viewedChapterStages : Array.Empty<int>();
        }

        /// <summary>해당 화자의 그 챕터를 이미 봤는가.</summary>
        public bool HasViewedChapter(string speakerId, int chapterStage)
        {
            EnsureLoaded();
            var entry = FindStoryProgress(speakerId);
            return entry != null && entry.viewedChapterStages.Contains(chapterStage);
        }

        /// <summary>
        /// 해당 화자의 마지막 시청 시점 스냅샷. 항목이 없으면 둘 다 0 —
        /// 한 번도 안 들은 화자에게는 "지금까지의 진행 전부"가 델타가 된다.
        /// </summary>
        public void GetStorySnapshots(string speakerId, out int runSnapshot, out int bossSnapshot)
        {
            EnsureLoaded();
            var entry = FindStoryProgress(speakerId);
            runSnapshot = entry != null ? entry.runSnapshot : 0;
            bossSnapshot = entry != null ? entry.bossSnapshot : 0;
        }

        /// <summary>
        /// 챕터 1개를 시청 완료로 기록하고, 그 시점의 런/보스 누적 스냅샷을 갱신한다.
        /// 다음 챕터는 이 스냅샷 이후의 추가 진전으로 해금된다. 이미 본 챕터면 아무것도 하지 않는다.
        ///
        /// 옛 <c>AdvanceStory</c>의 "현재 단계 이하는 무시"(후퇴 방지)를 집합 모델에서는 쓰지 않는다 —
        /// 각인사는 3번을 먼저 보고 1번을 나중에 볼 수 있어, 단계 비교로 막으면 정상적인 시청이 버려진다.
        /// 중복 방지는 집합 자체가 한다.
        /// </summary>
        public void MarkChapterViewed(
            string speakerId, int chapterStage, int runSnapshot, int bossSnapshot, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(speakerId) || chapterStage <= 0) return;
            EnsureLoaded();

            var entry = GetOrCreateStoryProgress(speakerId);
            if (entry.viewedChapterStages.Contains(chapterStage)) return;

            entry.viewedChapterStages.Add(chapterStage);
            entry.runSnapshot = runSnapshot;
            entry.bossSnapshot = bossSnapshot;
            if (autoSave) Save();
        }

        /// <summary>
        /// 디버그/치트: 화자의 시청 목록을 임의 설정한다. 스냅샷은 0으로 리셋해
        /// 델타 조건을 누적 기준으로 만든다(치트로 다음 챕터를 곧바로 열람하기 위함).
        /// </summary>
        public void DebugSetViewedChapters(string speakerId, IEnumerable<int> chapterStages, bool autoSave = true)
        {
            if (string.IsNullOrEmpty(speakerId)) return;
            EnsureLoaded();

            var entry = GetOrCreateStoryProgress(speakerId);
            entry.viewedChapterStages.Clear();
            if (chapterStages != null)
            {
                foreach (int stage in chapterStages)
                {
                    if (stage > 0 && !entry.viewedChapterStages.Contains(stage)) entry.viewedChapterStages.Add(stage);
                }
            }
            entry.runSnapshot = 0;
            entry.bossSnapshot = 0;
            if (autoSave) Save();
        }

        private StoryProgressEntry GetOrCreateStoryProgress(string speakerId)
        {
            var entry = FindStoryProgress(speakerId);
            if (entry != null) return entry;

            entry = new StoryProgressEntry { speakerId = speakerId };
            current.storyProgress.Add(entry);
            return entry;
        }

        private StoryProgressEntry FindStoryProgress(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            foreach (var entry in current.storyProgress)
            {
                if (entry != null && entry.speakerId == speakerId) return entry;
            }
            return null;
        }
    }
}
