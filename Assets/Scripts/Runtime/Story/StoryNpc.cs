using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;

namespace Abyss.Runtime.Story
{
    /// <summary>
    /// 심연의 서사를 단계적으로 들려주는 NPC(기록자). 화자별 진행도 + records에 따라
    /// 아직 보지 않은 해금된 챕터 중 가장 이른 것을 재생하고, 시청 완료 시 그 챕터를 시청 처리한다.
    /// 정비/제단 NPC와 동일하게 대화 중 플레이어 입력을 잠근다.
    /// 초기화 순서 무관 일관성을 위해 MetaSaveService는 Instance(자동 생성)로 접근한다.
    ///
    /// 발견 반응(26-expedition-discovery E4)이 자격이 되면 챕터보다 먼저 한 편 재생한다 — StoryNpc.Discovery.cs.
    /// 매 대화는 세션 번호를 받아, 늦게 온 이전 대화의 콜백이 지금 대화를 끝내지 못하게 한다.
    /// </summary>
    public sealed partial class StoryNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private StoryData story;
        [SerializeField] private DialogueUI dialogueUI;
        [Tooltip("대화 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;
        [Tooltip("진행도를 기록할 화자 id(StorySpeakerIds). 비우면 기록자로 본다.")]
        [SerializeField] private string speakerId = StorySpeakerIds.Chronicler;

        private bool isBusy;
        private int pendingStage = -1;  // 재생 중인 챕터 단계(완료 시 시청 처리)
        private int sessionToken;       // 대화 세션 번호 — 콜백이 지금 세션의 것인지 가른다

        // 옛 씬 인스턴스에는 speakerId가 직렬화돼 있지 않다 — 빈 값을 기록자로 보아
        // 씬을 다시 만들지 않아도 기존 기록자가 그대로 동작하게 한다.
        private string SpeakerId => string.IsNullOrEmpty(speakerId) ? StorySpeakerIds.Chronicler : speakerId;

        public string InteractionPrompt => Loc.Get(string.IsNullOrEmpty(promptKey) ? StringKey.Npc_Recordkeeper_Prompt : promptKey);
        public bool CanInteract => !isBusy && (dialogueUI == null || !dialogueUI.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (isBusy || dialogueUI == null || story == null) return;
            isBusy = true;
            if (player != null) player.InputLocked = true;
            int token = ++sessionToken;

            // 발견 반응이 먼저다. 재생하지 못하면(자격 없음·키 누락) 기존 챕터 경로 그대로.
            if (TryPlayDiscoveryReaction(token)) return;

            if (StoryChapterSelector.TryPickNext(story, MetaSaveService.Instance, SpeakerId, out var chapter))
            {
                pendingStage = chapter.ChapterStage;
                dialogueUI.Play(chapter.Lines, () => OnComplete(token));
            }
            else
            {
                pendingStage = -1;
                dialogueUI.Play(story.idleLines, () => OnComplete(token));
            }
        }

        private void OnComplete(int token)
        {
            if (this == null || token != sessionToken) return;   // 지난 세션의 늦은 콜백
            if (pendingStage > 0)
            {
                var meta = MetaSaveService.Instance;
                if (meta != null)
                {
                    var r = meta.Current.records;
                    meta.MarkChapterViewed(SpeakerId, pendingStage, r.totalRunCount, r.totalBossKillCount);
                }
            }
            pendingStage = -1;
            EndSession();
        }

        /// <summary>세션을 닫는다 — 번호를 올려 남은 콜백을 무효로 만들고 잠금을 푼다.</summary>
        private void EndSession()
        {
            sessionToken += 1;
            isBusy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
