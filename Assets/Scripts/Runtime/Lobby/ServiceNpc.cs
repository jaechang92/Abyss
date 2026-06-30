using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 정비 NPC. 상호작용 시 폼 선택 패널을 열어 시작 폼을 고른다(선택만 — 던전 진입은 포털 담당).
    /// 확정/취소 모두 입력을 복귀시킨다. 선택 결과는 FormSelectPanel이 RunStartContext에 기록한다.
    /// </summary>
    public sealed class ServiceNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private FormSelectPanel formSelectPanel;
        [Tooltip("패널 표시 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "장비 정비 (G)" : Loc.Get(promptKey);
        public bool CanInteract => !busy && (formSelectPanel == null || !formSelectPanel.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || formSelectPanel == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;
            formSelectPanel.Open(OnClosed, OnClosed);
        }

        private void OnClosed()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
