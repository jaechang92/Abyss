using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// 대화형 NPC. 상호작용 시 공용 DialogueUI로 대사를 재생하고, 재생 중 플레이어 입력을 잠근다.
    /// IInteractable 구현이라 PlayerInteractor의 근접 감지 파이프라인에 그대로 연결된다.
    /// </summary>
    public sealed class DialogueNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private DialogueData dialogue;
        [SerializeField] private DialogueUI dialogueUI;
        [Tooltip("대화 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "대화 (G)" : Loc.Get(promptKey);
        public bool CanInteract => !busy && (dialogueUI == null || !dialogueUI.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || dialogueUI == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;
            dialogueUI.Play(dialogue, OnDialogueComplete);
        }

        private void OnDialogueComplete()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
