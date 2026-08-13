using Abyss.Runtime.Meta;

namespace Abyss.Runtime.Story
{
    /// <summary>
    /// "지금 이 화자가 들려줄 챕터가 있는가"를 판정하는 단일 지점.
    ///
    /// 기록자(<c>StoryNpc</c>)와 각인사(<c>ServiceNpc</c>)가 같은 규약을 쓰므로 여기에 모았다.
    /// NPC마다 따로 두면 한쪽만 고친 판정이 남아, 같은 세이브를 두고 두 NPC가 다른 답을 낸다.
    /// </summary>
    public static class StoryChapterSelector
    {
        /// <summary>
        /// 재생할 챕터 1개를 고른다. 없으면 false(호출자는 idle 대사로 넘어간다).
        ///
        /// 순서가 중요하다 — <b>폼 조건으로 먼저 거른 뒤</b> 그중 가장 이른 챕터 하나를 잡고,
        /// 진행 델타는 <b>그 하나에만</b> 묻는다:
        /// <list type="bullet">
        /// <item>폼 조건을 먼저 걸러야 각인사가 <b>아직 안 써 본 폼의 내력에 막히지 않는다</b>(사전 모델).</item>
        /// <item>델타를 후보 전체에 물으면 조건이 헐거운 뒷 챕터가 앞 챕터를 건너뛰고 먼저 열린다.
        /// 기록자는 연재라 그게 곧 순서 붕괴다 — 그래서 고른 하나에서 멈춘다(기존 동작 보존).</item>
        /// </list>
        /// </summary>
        /// <param name="meta">
        /// 진행도 저장소. <b>싱글톤을 안에서 잡지 않고 받는다</b> — 안에서 잡으면 이 판정을
        /// 씬 없이 검증할 수 없고, 그러면 두 화자가 서로를 침범하는지를 실행해 봐야만 알 수 있다.
        /// 호출자(NPC)는 <c>MetaSaveService.Instance</c>를 넘긴다.
        /// </param>
        public static bool TryPickNext(
            StoryData story, MetaSaveService meta, string speakerId, out StoryChapter picked)
        {
            picked = default;
            if (story == null || story.chapters == null || story.chapters.Length == 0) return false;
            if (string.IsNullOrEmpty(speakerId)) return false;

            var save = meta != null ? meta.Current : null;
            if (save == null) return false;

            bool hasNext = false;
            foreach (var chapter in story.chapters)
            {
                if (chapter.chapterStage <= 0) continue;
                if (meta.HasViewedChapter(speakerId, chapter.chapterStage)) continue;
                if (!string.IsNullOrEmpty(chapter.requiredFormId) && !meta.IsFormDiscovered(chapter.requiredFormId)) continue;
                if (hasNext && chapter.chapterStage >= picked.chapterStage) continue;

                picked = chapter;
                hasNext = true;
            }
            if (!hasNext) return false;

            meta.GetStorySnapshots(speakerId, out int runSnapshot, out int bossSnapshot);
            int runDelta = save.records.totalRunCount - runSnapshot;
            int bossDelta = save.records.totalBossKillCount - bossSnapshot;
            if (runDelta < picked.minRunCount || bossDelta < picked.minBossKills)
            {
                picked = default;
                return false;
            }
            return true;
        }
    }
}
