using System;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 정비 공간(R2 — 21-room-reward-flow §정비 공간). <see cref="RoomData.maintenanceData"/>가 있는 맵 방만 이 길을 탄다.
    ///
    /// 흐름: 진입(비전투라 즉시 클리어) → 상인(왼쪽)·불씨(오른쪽) → 각각 접근·G → 기존 <see cref="ShopRoomPanel"/>·<see cref="EventRoomPanel"/>.
    /// 구매→휴식, 휴식→구매 모두 된다. UI 닫기는 공간 복귀다. 휴식을 해결하면(구매 없이도) 출구가 한 번 선다.
    /// 출구가 선 뒤에도 상인은 다시 열린다 — 그동안은 문 이동을 막는다(<see cref="IsMaintenanceExitBlocked"/>).
    ///
    /// 🔴 legacy 게이트(<c>isRoomGateHeld</c>)와 전역 <c>OnEventResolved</c>를 쓰지 않는다. 완료는 <see cref="MaintenanceSession"/>과
    /// 방 진입 세대로만 받는다 — 런 종료·치트 이동·디렉터 비활성화(<c>CloseRoomSession</c>)가 세대를 올리면 늦은 콜백은 버려진다.
    /// </summary>
    public sealed partial class StageDirector
    {
        private static readonly Color MerchantFallbackColor = new Color(0.78f, 0.62f, 0.38f);
        private static readonly Color EmberFallbackColor = new Color(0.95f, 0.45f, 0.18f);

        private MaintenanceSession maintenanceSession;
        private MaintenanceInteractable merchantObject;
        private MaintenanceInteractable emberObject;

        /// <summary>새 방 진입(<see cref="ResetWorldInteraction"/>)에서 부른다. 오브젝트는 맵 루트 정리가 치운다.</summary>
        private void ResetMaintenance()
        {
            maintenanceSession = null;
            merchantObject = null;
            emberObject = null;
        }

        /// <summary>
        /// 정비 공간이면 상인·불씨를 세우고 true — 호출자는 월드 이벤트·legacy 자동 모달·자동 진행을 하지 않는다.
        /// 데이터가 모자라면(맵 방 아님·상점/휴식 없음) false — 이 방은 shop/eventData가 비어 있어 그대로 출구로 흘러간다.
        /// </summary>
        private bool TryBeginMaintenance(RoomData room)
        {
            if (room == null || room.maintenanceData == null) return false;

            var data = room.maintenanceData;
            if (!room.IsMapRoom || !data.IsValid)
            {
                Debug.LogWarning($"[StageDirector] {room.roomId} 정비 공간 데이터가 모자라다(맵 방·상점·휴식) — 서비스 없이 진행");
                return false;
            }

            var session = new MaintenanceSession(roomEntryVersion, room, data);
            maintenanceSession = session;

            // 높이는 레이아웃이 켜진 뒤(방 진입 이벤트 이후 — 비전투 방은 같은 호출 안)의 실제 바닥이다.
            float merchantX = data.MerchantX;
            float emberX = data.EmberX;
            string shopTitle = string.IsNullOrEmpty(data.Shop.title) ? "상인" : data.Shop.title;
            string restTitle = string.IsNullOrEmpty(data.RestEvent.title) ? "불씨" : data.RestEvent.title;

            merchantObject = MaintenanceInteractable.Create(ResolveMapRoot(), "Maintenance_Merchant",
                new Vector3(merchantX, ProbeFloorY(merchantX), 0f), shopTitle,
                UiArtLibrary.Get(UiArtKeys.ShopPortrait(data.Shop.shopId)), MerchantFallbackColor,
                () => $"{shopTitle} — 거래한다 (G)",
                () => CanUseMaintenance(session), () => HandleMerchantInteract(session));

            emberObject = MaintenanceInteractable.Create(ResolveMapRoot(), "Maintenance_Ember",
                new Vector3(emberX, ProbeFloorY(emberX), 0f), restTitle,
                ResolveEmberArt(data.RestEvent), EmberFallbackColor,
                () => session.IsRestResolved ? $"{restTitle} — 이미 쉬었다 (G)" : $"{restTitle} — 쉬어 간다 (G)",
                () => CanUseMaintenance(session), () => HandleEmberInteract(session));

            Debug.Log($"[StageDirector] 정비 공간 준비: {session.InteractionId} — 상인 x {merchantX} · 불씨 x {emberX}(자동 개방 없음)");
            return true;
        }

        /// <summary>
        /// 불씨 그림 — 휴식 이벤트 그림이 있으면 그것, 없으면 기존 휴식 경로 아이콘(route_rest)을 빌린다. 둘 다 없으면 null(색 사각형).
        /// 빌린 표현일 뿐 정비 공간 전용 불씨 아트가 아니다.
        /// </summary>
        private static Sprite ResolveEmberArt(EventData restEvent)
        {
            var art = WorldArtLibrary.Get(WorldArtLibrary.EventKey(restEvent.eventId, false));
            return art != null ? art : UiArtKeys.TryGetRouteIcon(RoomType.Rest);
        }

        private bool IsCurrentMaintenance(MaintenanceSession session) =>
            this != null && session != null && session == maintenanceSession &&
            session.EntryVersion == roomEntryVersion && !isRoomSessionClosed;

        /// <summary>모달·정지·드래프트·문 전환·저장 모달 중이거나 다른 서비스가 열려 있으면 받지 않는다.</summary>
        private bool CanUseMaintenance(MaintenanceSession session) =>
            IsCurrentMaintenance(session) && session.ActiveService == MaintenanceService.None &&
            !IsWorldInputBlocked && !SaveStatusOverlay.IsCapturingInput;

        /// <summary>
        /// 정비 공간의 출구 이동을 막아야 하는가 — 서비스·모달·드래프트·저장 모달·정지 중.
        /// 문 전환 자체(<c>isDoorTransitioning</c>)는 보지 않는다 — 페이드 뒤 확정 직전 재검사에서도 쓰기 때문이다.
        /// 정비 공간이 아니면 항상 false(일반 방의 문은 그대로).
        /// </summary>
        private bool IsMaintenanceExitBlocked()
        {
            var session = maintenanceSession;
            if (!IsCurrentMaintenance(session)) return false;
            return session.ActiveService != MaintenanceService.None || isRunModalOpen || isRunPaused ||
                   IsDraftSessionActive() || SaveStatusOverlay.IsCapturingInput ||
                   ShopRoomPanel.IsOpen || EventRoomPanel.IsOpen;
        }

        // ───────────────────────── 상인 ─────────────────────────

        private void HandleMerchantInteract(MaintenanceSession session)
        {
            if (!CanUseMaintenance(session) || !session.TryBeginShop()) return;

            Debug.Log($"[StageDirector] 정비 상인 상호작용: {session.InteractionId} — 진열 열기");
            if (ShopRoomPanel.OpenForWorld(session, () => IsCurrentMaintenance(session), () => HandleMaintenanceShopClosed(session))) return;

            session.ReleaseShop();
            Debug.LogWarning($"[StageDirector] {session.InteractionId} 진열 UI를 열지 못했다 — 다시 상호작용 가능");
        }

        /// <summary>상점 UI가 닫히고 대기 드래프트를 연 직후. 서비스 잠금은 이어진 모달이 모두 닫힌 뒤 푼다.</summary>
        private void HandleMaintenanceShopClosed(MaintenanceSession session)
        {
            if (!IsCurrentMaintenance(session))
            {
                Debug.Log($"[StageDirector] 무효화된 정비 상점 닫힘 — 무시({session?.InteractionId})");
                return;
            }
            _ = ReleaseShopAfterModalsAsync(session);
        }

        private async Awaitable ReleaseShopAfterModalsAsync(MaintenanceSession session)
        {
            if (!await WaitMaintenanceModalsAsync(session)) return;
            session.ReleaseShop();
            Debug.Log($"[StageDirector] 정비 상점 닫힘: {session.InteractionId} — 공간 복귀");
        }

        // ───────────────────────── 불씨 ─────────────────────────

        private void HandleEmberInteract(MaintenanceSession session)
        {
            if (!CanUseMaintenance(session)) return;

            // 사용 완료 뒤 재접근은 안내만 한다 — 효과를 다시 적용하지 않는다.
            if (session.RestState != RoomInteractionState.Ready)
            {
                Debug.Log($"[StageDirector] 정비 불씨: {session.InteractionId} — 이미 사용했다(효과 없음)");
                return;
            }
            if (!session.TryBeginRest()) return;

            Debug.Log($"[StageDirector] 정비 불씨 상호작용: {session.InteractionId} — 휴식 선택 열기");
            // 소유 판정을 함께 넘긴다 — 방 이동·런 종료 뒤의 낡은 패널은 선택 효과·드래프트·확인을 내지 않고 닫힌다.
            if (EventRoomPanel.OpenForWorld(session.Data.RestEvent, () => HandleMaintenanceRestConfirmed(session),
                    isOwnerValid: () => IsCurrentMaintenance(session))) return;

            session.CancelRest();
            Debug.LogWarning($"[StageDirector] {session.InteractionId} 휴식 UI를 열지 못했다 — 다시 상호작용 가능");
        }

        /// <summary>휴식 패널의 [확인] — UI가 닫히고 드래프트를 연 직후. 사용 완료는 모달이 모두 닫힌 뒤다.</summary>
        private void HandleMaintenanceRestConfirmed(MaintenanceSession session)
        {
            if (!IsCurrentMaintenance(session) || !session.TryConfirmRest())
            {
                Debug.Log($"[StageDirector] 무효화된 정비 휴식 확인 — 무시({session?.InteractionId})");
                return;
            }
            _ = ResolveRestAfterModalsAsync(session);
        }

        private async Awaitable ResolveRestAfterModalsAsync(MaintenanceSession session)
        {
            if (!await WaitMaintenanceModalsAsync(session)) return;
            if (!session.TryResolveRest()) return;

            if (emberObject != null) emberObject.ShowUsed();
            Debug.Log($"[StageDirector] 정비 휴식 해결: {session.InteractionId}");

            // 출구는 한 번만 세운다. 상점은 그 뒤에도 다시 열린다.
            if (!session.TryMarkExitOpened()) return;
            Debug.Log($"[StageDirector] 정비 공간 출구 대기: {session.InteractionId}");
            ContinueAfterRoom();
        }

        /// <summary>
        /// 이어진 드래프트(여러 번이면 연달아)가 모두 닫힐 때까지 프레임 단위로 기다린다(R1과 같은 다음 프레임 판정).
        /// 세션이 낡거나 디렉터가 파괴되면 false — 호출자는 아무 상태도 바꾸지 않는다.
        /// </summary>
        private async Awaitable<bool> WaitMaintenanceModalsAsync(MaintenanceSession session)
        {
            try
            {
                do
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    if (!IsCurrentMaintenance(session)) return false;
                }
                while (isRunModalOpen || IsDraftSessionActive() || ShopRoomPanel.IsOpen || EventRoomPanel.IsOpen);
            }
            catch (OperationCanceledException)
            {
                return false;   // 디렉터 파괴(씬 전환) — 할 일 없다.
            }
            return true;
        }
    }
}
