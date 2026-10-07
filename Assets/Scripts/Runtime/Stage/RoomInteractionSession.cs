namespace Abyss.Runtime.Stage
{
    /// <summary>월드 상호작용 한 건의 진행 단계(21-room-reward-flow §5 상태 예시).</summary>
    public enum RoomInteractionState
    {
        /// <summary>오브젝트가 서 있고 상호작용을 받을 수 있다.</summary>
        Ready,
        /// <summary>선택 UI가 열려 있다. 다른 상호작용·출구를 받지 않는다.</summary>
        PanelOpen,
        /// <summary>선택을 확인하고 UI를 닫았다. 이어진 드래프트 등 모달이 끝나기를 기다린다.</summary>
        AwaitingModals,
        /// <summary>해결됐다. 출구가 선다.</summary>
        Resolved
    }

    /// <summary>
    /// 월드 상호작용 방(R1: 이벤트 오브젝트) 한 방문의 상태. <see cref="StageDirector"/>가 방 진입 세대
    /// (<c>roomEntryVersion</c>)와 함께 만들고, 완료 콜백은 <b>이 세션 객체와 세대가 둘 다 현재일 때만</b> 받아들인다.
    ///
    /// 🔴 legacy 신호(<c>GameEvents.OnEventResolved</c>)는 인자가 없어 어느 방의 무엇이 끝났는지 모른다.
    /// 월드 모드는 그 신호를 쓰지 않고 이 세션으로 완료를 가린다 — 늦게 온 콜백·다른 방의 해결이 출구를 열지 않게.
    /// 상태는 앞으로만 간다(되돌림은 UI가 열리지 못했을 때의 <see cref="CancelPanel"/> 하나).
    /// </summary>
    public sealed class RoomInteractionSession
    {
        public RoomInteractionSession(int entryVersion, string interactionId, RoomData room)
        {
            EntryVersion = entryVersion;
            InteractionId = interactionId;
            Room = room;
            State = RoomInteractionState.Ready;
        }

        /// <summary>세션을 만든 방 진입 세대. 방 이동·세션 닫기가 세대를 올리면 이 세션은 낡는다.</summary>
        public int EntryVersion { get; }

        /// <summary>로그·추적용 상호작용 ID(방 ID/이벤트 ID).</summary>
        public string InteractionId { get; }

        public RoomData Room { get; }

        public RoomInteractionState State { get; private set; }

        /// <summary>UI를 연다. 준비 상태에서 한 번만 성공한다(연타·두 번째 상호작용 차단).</summary>
        public bool TryBeginPanel()
        {
            if (State != RoomInteractionState.Ready) return false;
            State = RoomInteractionState.PanelOpen;
            return true;
        }

        /// <summary>UI가 열리지 못했을 때만 준비 상태로 되돌린다.</summary>
        public void CancelPanel()
        {
            if (State == RoomInteractionState.PanelOpen) State = RoomInteractionState.Ready;
        }

        /// <summary>선택을 확인하고 UI가 닫혔다. 열린 UI에서 한 번만 성공한다.</summary>
        public bool TryConfirm()
        {
            if (State != RoomInteractionState.PanelOpen) return false;
            State = RoomInteractionState.AwaitingModals;
            return true;
        }

        /// <summary>이어진 모달까지 끝났다. 확인 뒤 한 번만 성공한다 — 출구는 이때만 선다.</summary>
        public bool TryResolve()
        {
            if (State != RoomInteractionState.AwaitingModals) return false;
            State = RoomInteractionState.Resolved;
            return true;
        }
    }
}
