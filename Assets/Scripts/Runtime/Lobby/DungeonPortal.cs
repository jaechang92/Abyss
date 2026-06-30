using Abyss.Runtime.Flow;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 던전 입구. 상호작용 시 즉시 Run 씬으로 전환한다(입장 전용).
    /// 시작 폼 선택은 정비 NPC(ServiceNpc)가 담당하고, 그 결과는 RunStartContext로 전달된다.
    /// </summary>
    public sealed class DungeonPortal : MonoBehaviour, IInteractable
    {
        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        private bool busy;

        public string InteractionPrompt => string.IsNullOrEmpty(promptKey) ? "던전 입장 (G)" : Loc.Get(promptKey);
        public bool CanInteract => !busy;

        public void Interact(GameObject interactor)
        {
            if (busy) return;
            busy = true;
            // 씬이 전환되므로 busy 해제는 불필요(로비 언로드).
            _ = SceneFlowController.Instance.LoadRunAsync();
        }
    }
}
