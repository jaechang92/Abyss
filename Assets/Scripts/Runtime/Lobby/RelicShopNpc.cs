using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 유물 상점 NPC. 상호작용 시 <see cref="RelicShopPanel"/>을 연다.
    ///
    /// <see cref="AltarNpc"/>와 같은 패턴이다 — 열리는 동안 플레이어 입력을 잠그고 닫힐 때 복귀시킨다.
    /// 둘을 하나로 합치지 않은 이유는 <b>파는 것의 성격이 다르기 때문</b>이다.
    /// 제단은 같은 심연 조각으로 <i>확정</i>을 팔고 여기서는 <i>확률</i>을 판다.
    /// 한 NPC 안에 탭으로 넣으면 "무엇을 사는 곳인가"가 흐려지고,
    /// 제단이 이미 갖고 있는 서사(각인사·심연의 제단)까지 이 좌판이 끌어안게 된다.
    /// </summary>
    public sealed class RelicShopNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private RelicShopPanel shopPanel;

        [Tooltip("패널 표시 중 이동을 잠글 플레이어 컨트롤러.")]
        [SerializeField] private LobbyPlayerController player;

        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "유물 감정 (G)" : Loc.Get(promptKey);

        public bool CanInteract => !busy && (shopPanel == null || !shopPanel.IsOpen);

        public void Interact(GameObject interactor)
        {
            if (busy || shopPanel == null) return;
            busy = true;
            if (player != null) player.InputLocked = true;
            shopPanel.Open(OnClosed);
        }

        private void OnClosed()
        {
            busy = false;
            if (player != null) player.InputLocked = false;
        }
    }
}
