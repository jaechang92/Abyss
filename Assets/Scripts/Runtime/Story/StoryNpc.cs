using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;

namespace Abyss.Runtime.Story
{
    /// <summary>
    /// 심연의 서사를 단계적으로 들려주는 NPC. 진행도(MetaSave.storyStage + records)에 따라
    /// 아직 보지 않은 해금된 챕터 중 가장 이른 것을 재생하고, 시청 완료 시 storyStage를 전진시킨다.
    /// 정비/제단 NPC와 동일하게 대화 중 플레이어 입력을 잠근다.
    /// 초기화 순서 무관 일관성을 위해 MetaSaveService는 Instance(자동 생성)로 접근한다.
    /// </summary>
    public sealed class StoryNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private StoryData story;
        [SerializeField] private DialogueUI dialogueUI;
        [Tooltip("대화 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;
        private int pendingStage = -1;  // 재생 중인 챕터 단계(완료 시 storyStage에 반영)

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "이야기 듣기 (G)" : Loc.Get(promptKey);
        public bool CanInteract => !busy && (dialogueUI == null || !dialogueUI.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || dialogueUI == null || story == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;

            if (TryPickNextChapter(out var chapter))
            {
                pendingStage = chapter.chapterStage;
                dialogueUI.Play(chapter.lines, OnComplete);
            }
            else
            {
                pendingStage = -1;
                dialogueUI.Play(story.idleLines, OnComplete);
            }
        }

        /// <summary>
        /// 바로 다음 미시청 챕터(chapterStage가 storyStage보다 큰 것 중 최소)를 하나만 후보로 잡고,
        /// 마지막 시청 이후의 추가 진전(런/보스 델타)이 그 챕터 조건을 넘어야 재생 대상으로 선택한다.
        /// 반복 대화만으로는 넘어가지 않고, 실제 던전 진행이 있어야 다음 이야기가 열린다.
        /// </summary>
        private bool TryPickNextChapter(out StoryChapter picked)
        {
            picked = default;
            if (story.chapters == null || story.chapters.Length == 0) return false;

            var meta = MetaSaveService.Instance;
            int stage = meta != null ? meta.StoryStage : 0;
            var save = meta != null ? meta.Current : null;
            int runDelta = save != null ? save.records.totalRunCount - save.storyRunSnapshot : 0;
            int bossDelta = save != null ? save.records.totalBossKillCount - save.storyBossSnapshot : 0;

            // 다음 단계 챕터(가장 낮은 미시청) 하나만 고려 → 챕터는 항상 순차적으로 열린다.
            bool hasNext = false;
            foreach (var ch in story.chapters)
            {
                if (ch.chapterStage <= stage) continue;
                if (!hasNext || ch.chapterStage < picked.chapterStage)
                {
                    picked = ch;
                    hasNext = true;
                }
            }
            if (!hasNext) return false;

            // 마지막 시청 이후 추가 진전이 조건을 충족해야 해금.
            if (runDelta < picked.minRunCount || bossDelta < picked.minBossKills)
            {
                picked = default;
                return false;
            }
            return true;
        }

        private void OnComplete()
        {
            if (pendingStage > 0)
            {
                var meta = MetaSaveService.Instance;
                if (meta != null)
                {
                    var r = meta.Current.records;
                    meta.AdvanceStory(pendingStage, r.totalRunCount, r.totalBossKillCount);
                }
            }
            pendingStage = -1;
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
