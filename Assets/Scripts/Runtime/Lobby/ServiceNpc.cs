using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Story;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 정비 NPC(각인사). 상호작용 시 폼 선택 패널을 열어 시작 폼을 고른다(선택만 — 던전 진입은 포털 담당).
    /// 확정/취소 모두 입력을 복귀시킨다. 선택 결과는 FormSelectPanel이 RunStartContext에 기록한다.
    ///
    /// 새로 해금된 폼의 내력(N-2 각인사 대사)이 남아 있으면 <b>패널보다 먼저</b> 그 대사를 재생하고
    /// 완료 콜백에서 패널을 연다. 들려줄 것이 없으면 지금까지처럼 곧바로 패널이다 —
    /// 매번 대사를 끼우면 폼을 바꾸려 들를 때마다 같은 말을 듣게 된다.
    ///
    /// <b>이 패널은 <see cref="StoryData.idleLines"/>를 쓰지 않는다.</b> 각인사의 닫는 말
    /// (아크 씨앗 "자기 것은 못 새겼다")은 idle이 아니라 <b>챕터</b>다 —
    /// <see cref="StoryChapter.minViewedChapters"/>로 "넷을 다 본 뒤"에 한 번만 열린다.
    /// 그래서 이 클래스에는 그 처리를 위한 코드가 없다(챕터 재생 경로가 그대로 쓰인다).
    /// </summary>
    public sealed class ServiceNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private FormSelectPanel formSelectPanel;
        [Tooltip("패널 표시 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;
        [Tooltip("각인사 내력 데이터(비우면 대사 없이 곧바로 패널).")]
        [SerializeField] private StoryData story;
        [Tooltip("내력 재생에 쓸 대화 UI(기록자와 공용).")]
        [SerializeField] private DialogueUI dialogueUI;
        [Tooltip("진행도를 기록할 화자 id(StorySpeakerIds). 비우면 각인사로 본다.")]
        [SerializeField] private string speakerId = StorySpeakerIds.Engraver;

        private bool busy;
        private int pendingStage = -1;  // 재생 중인 내력 단계(완료 시 시청 처리)

        // 옛 씬 인스턴스에는 speakerId가 직렬화돼 있지 않다 — 빈 값을 각인사로 본다.
        private string SpeakerId => string.IsNullOrEmpty(speakerId) ? StorySpeakerIds.Engraver : speakerId;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "장비 정비 (G)" : Loc.Get(promptKey);

        public bool CanInteract =>
            !busy
            && (formSelectPanel == null || !formSelectPanel.IsOpen)
            && (dialogueUI == null || !dialogueUI.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || formSelectPanel == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;

            if (dialogueUI != null
                && StoryChapterSelector.TryPickNext(story, MetaSaveService.Instance, SpeakerId, out var chapter))
            {
                pendingStage = chapter.chapterStage;
                dialogueUI.Play(chapter.lines, OnLoreComplete);
                return;
            }

            OpenPanel();
        }

        /// <summary>
        /// 내력 재생 완료 → 시청 처리 후 패널로 이어 붙인다.
        ///
        /// 시청 처리를 <b>여기서</b> 하는 이유: 재생 시작 시점에 찍으면 대사를 끝까지 보지 않고
        /// 나간 플레이어도 본 것이 되어, 그 폼의 내력은 두 번 다시 열리지 않는다.
        /// </summary>
        private void OnLoreComplete()
        {
            if (pendingStage > 0)
            {
                var meta = MetaSaveService.Instance;
                if (meta != null)
                {
                    var records = meta.Current.records;
                    meta.MarkChapterViewed(SpeakerId, pendingStage, records.totalRunCount, records.totalBossKillCount);
                }
            }
            pendingStage = -1;
            OpenPanel();
        }

        private void OpenPanel() => formSelectPanel.Open(OnClosed, OnClosed);

        private void OnClosed()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
