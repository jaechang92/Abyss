using UnityEngine;
using UnityEngine.EventSystems;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 도감 타일 버튼의 선택(포커스) 이동을 <see cref="CodexPanel"/>에 알린다.
    /// 패드·키보드로 타일을 옮겨 다니면 결정 없이도 상세가 그 항목으로 바로 바뀌게 하려는 것이다.
    /// 클릭(onClick) 경로는 그대로 두고, 선택 이벤트만 따로 받는다.
    /// </summary>
    public sealed class CodexTileFocusRelay : MonoBehaviour, ISelectHandler
    {
        private CodexPanel owner;
        private int slot;

        public void Init(CodexPanel panel, int tileSlot)
        {
            owner = panel;
            slot = tileSlot;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (owner != null) owner.OnTileSelected(slot);
        }
    }
}
