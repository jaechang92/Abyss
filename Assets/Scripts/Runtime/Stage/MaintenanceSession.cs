using Abyss.Runtime.Weapon;

namespace Abyss.Runtime.Stage
{
    /// <summary>정비 공간에서 지금 열려 있는 서비스. 한 번에 하나만 연다.</summary>
    public enum MaintenanceService
    {
        None,
        Shop,
        Rest
    }

    /// <summary>
    /// 정비 공간(R2) 한 방문의 상태. <see cref="StageDirector"/>가 방 진입 세대(<c>roomEntryVersion</c>)와 함께 만들고,
    /// 콜백은 <b>이 세션 객체와 세대가 둘 다 현재일 때만</b> 받아들인다(R1 <see cref="RoomInteractionSession"/>과 같은 원칙).
    ///
    /// 상점: 재고·무기 후보를 <b>첫 개방 때 한 번</b> 정하고 방문 내내 보존한다 — 다시 열거나 폼이 바뀌어도 재추첨하지 않는다.
    /// 휴식: Ready → PanelOpen → AwaitingModals → Resolved. 앞으로만 간다(UI가 열리지 못했을 때의 되돌림 하나).
    /// 🔴 전역 <c>OnEventResolved</c>를 쓰지 않는다 — 인자 없는 신호가 이 방의 출구를 열 수 없게.
    /// </summary>
    public sealed class MaintenanceSession
    {
        public MaintenanceSession(int entryVersion, RoomData room, MaintenanceData data)
        {
            EntryVersion = entryVersion;
            Room = room;
            Data = data;
            InteractionId = $"{(room != null ? room.roomId : "null")}/{(data != null ? data.MaintenanceId : "null")}";
            RestState = RoomInteractionState.Ready;
        }

        public int EntryVersion { get; }
        public RoomData Room { get; }
        public MaintenanceData Data { get; }

        /// <summary>로그·추적용 ID(방 ID/정비 ID).</summary>
        public string InteractionId { get; }

        /// <summary>지금 열린 서비스. 상점은 UI가 닫히고 이어진 드래프트까지 끝나야 None으로 돌아온다.</summary>
        public MaintenanceService ActiveService { get; private set; }

        public RoomInteractionState RestState { get; private set; }
        public bool IsRestResolved => RestState == RoomInteractionState.Resolved;

        /// <summary>출구를 세웠는가. 한 방문에 한 번만 세운다.</summary>
        public bool IsExitOpened { get; private set; }

        // ───────────────────────── 상점 상태 ─────────────────────────

        /// <summary>진열을 정했는가(첫 개방 때 한 번).</summary>
        public bool IsShopStocked { get; private set; }

        /// <summary>슬롯별 구매 횟수. 진열을 정할 때 만들고 방문 내내 같은 배열이다.</summary>
        public int[] ShopPurchasedCounts { get; private set; }

        /// <summary>무기 슬롯 후보. 진열을 정할 때 한 번 채운다.</summary>
        public WeaponShop.Offer[] ShopWeaponOffers { get; private set; }

        /// <summary>상점이 닫힐 때 열 드래프트 수. 닫을 때 <see cref="TakeShopPendingDrafts"/>로 한 번만 인출한다.</summary>
        public int ShopPendingDrafts { get; private set; }

        /// <summary>
        /// 진열 상태를 처음 만든다. 처음이면 true — 호출자가 무기 후보를 채운다. 이미 만들었으면 false(기존 배열 유지).
        /// </summary>
        public bool TryStockShop(int slotCount)
        {
            if (IsShopStocked) return false;
            IsShopStocked = true;
            ShopPurchasedCounts = new int[slotCount];
            ShopWeaponOffers = new WeaponShop.Offer[slotCount];
            return true;
        }

        public void AddShopPendingDrafts(int count)
        {
            if (count > 0) ShopPendingDrafts += count;
        }

        public int TakeShopPendingDrafts()
        {
            int drafts = ShopPendingDrafts;
            ShopPendingDrafts = 0;
            return drafts;
        }

        // ───────────────────────── 서비스 ─────────────────────────

        /// <summary>상점을 연다. 다른 서비스가 열려 있으면 실패.</summary>
        public bool TryBeginShop()
        {
            if (ActiveService != MaintenanceService.None) return false;
            ActiveService = MaintenanceService.Shop;
            return true;
        }

        /// <summary>상점 서비스를 끝낸다 — UI를 열지 못했거나, 닫힌 뒤 모달이 모두 끝났을 때.</summary>
        public void ReleaseShop()
        {
            if (ActiveService == MaintenanceService.Shop) ActiveService = MaintenanceService.None;
        }

        /// <summary>휴식 UI를 연다. 준비 상태이고 다른 서비스가 없을 때 한 번만 성공한다.</summary>
        public bool TryBeginRest()
        {
            if (ActiveService != MaintenanceService.None || RestState != RoomInteractionState.Ready) return false;
            ActiveService = MaintenanceService.Rest;
            RestState = RoomInteractionState.PanelOpen;
            return true;
        }

        /// <summary>휴식 UI가 열리지 못했을 때만 준비 상태로 되돌린다.</summary>
        public void CancelRest()
        {
            if (RestState != RoomInteractionState.PanelOpen) return;
            RestState = RoomInteractionState.Ready;
            ActiveService = MaintenanceService.None;
        }

        /// <summary>선택을 확인하고 UI가 닫혔다. 서비스 잠금은 이어진 모달이 끝날 때까지 유지한다.</summary>
        public bool TryConfirmRest()
        {
            if (RestState != RoomInteractionState.PanelOpen) return false;
            RestState = RoomInteractionState.AwaitingModals;
            return true;
        }

        /// <summary>이어진 모달까지 끝났다. 확인 뒤 한 번만 성공한다 — 이때 사용 완료가 되고 서비스가 풀린다.</summary>
        public bool TryResolveRest()
        {
            if (RestState != RoomInteractionState.AwaitingModals) return false;
            RestState = RoomInteractionState.Resolved;
            ActiveService = MaintenanceService.None;
            return true;
        }

        /// <summary>출구를 세울 차례인가. 휴식 해결 뒤 한 번만 true.</summary>
        public bool TryMarkExitOpened()
        {
            if (IsExitOpened || !IsRestResolved) return false;
            IsExitOpened = true;
            return true;
        }
    }
}
