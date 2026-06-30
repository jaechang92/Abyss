using Abyss.Runtime.Flow;
using Abyss.Runtime.Interaction;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 던전 입구. 상호작용 시 폼 선택 패널을 열고, 확정하면 Run 씬으로 전환한다.
    /// 패널이 없으면 즉시 진입(폴백). 패널 표시 중에는 플레이어 입력을 잠근다.
    /// </summary>
    public sealed class DungeonPortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private FormSelectPanel formSelectPanel;
        [Tooltip("패널 표시 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;
        [SerializeField] private string prompt = "던전 입장 (G)";

        private bool busy;

        public string InteractionPrompt => prompt;
        public bool CanInteract => !busy && (formSelectPanel == null || !formSelectPanel.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy) return;
            busy = true;
            if (player != null) player.InputLocked = true;

            if (formSelectPanel != null)
            {
                formSelectPanel.Open(EnterRun, CancelEnter);
            }
            else
            {
                EnterRun();
            }
        }

        private void EnterRun()
        {
            // 씬이 전환되므로 busy/InputLocked 해제는 불필요(로비 언로드).
            _ = SceneFlowController.Instance.LoadRunAsync();
        }

        private void CancelEnter()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
