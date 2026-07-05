using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 심연의 제단 NPC. 상호작용 시 메타 영구 업그레이드 패널(MetaUpgradePanel)을 연다.
    /// 정비 NPC(ServiceNpc)와 동일한 패턴 — 열리는 동안 플레이어 입력을 잠그고 닫힐 때 복귀시킨다.
    /// </summary>
    public sealed class AltarNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private MetaUpgradePanel upgradePanel;
        [Tooltip("패널 표시 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "심연의 제단 (G)" : Loc.Get(promptKey);
        public bool CanInteract => !busy && (upgradePanel == null || !upgradePanel.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || upgradePanel == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;
            upgradePanel.Open(OnClosed);
        }

        private void OnClosed()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
