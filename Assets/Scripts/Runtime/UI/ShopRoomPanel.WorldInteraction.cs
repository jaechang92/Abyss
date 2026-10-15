using System;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="ShopRoomPanel"/>의 정비 공간(R2) 경로 — 상인 오브젝트에서 열고, 닫으면 공간으로 돌아간다.
    ///
    /// legacy <see cref="Open"/>과 다른 점:
    /// <list type="bullet">
    /// <item>재고·구매수·무기 후보·대기 드래프트를 패널이 아니라 <see cref="MaintenanceSession"/>이 들고 있다 — 첫 개방에 한 번 정하고,
    /// 다시 열면(폼이 바뀌었어도) 그대로 보여 준다. 패널의 배열 참조를 세션 배열로 돌려 구매·표시·차감이 같은 길을 지난다.</item>
    /// <item>[떠난다]는 전역 <c>GameEvents.OnEventResolved</c>를 내지 않는다 — 연 쪽의 닫힘 콜백으로만 알린다.</item>
    /// <item>연 쪽의 세션이 낡으면(방 이동·런 종료) 구매·닫기·갱신에서 확인해 아무것도 바꾸지 않고 닫는다.</item>
    /// </list>
    /// </summary>
    public sealed partial class ShopRoomPanel
    {
        // legacy 방문의 배열. 월드 방문 중에는 purchasedCounts/weaponOffers 가 세션 배열을 가리킨다.
        private readonly int[] ownPurchasedCounts = new int[MAX_ITEMS];
        private readonly Weapon.WeaponShop.Offer[] ownWeaponOffers = new Weapon.WeaponShop.Offer[MAX_ITEMS];

        private MaintenanceSession worldShop;
        private Func<bool> isWorldOwnerValid;
        private Action worldClosed;

        private bool IsWorldShop => worldShop != null;

        /// <summary>
        /// 정비 공간의 상인에서 연다. 진열은 세션의 첫 개방에 한 번 정해진다. [떠난다]가 UI를 닫고 대기 드래프트를 연 뒤
        /// <paramref name="onClosed"/>를 부른다. 열지 못했으면 false(전역 해결 신호를 내지 않는다).
        /// </summary>
        public static bool OpenForWorld(MaintenanceSession session, Func<bool> isOwnerValid, Action onClosed)
        {
            var data = session?.Data != null ? session.Data.Shop : null;
            if (data == null || data.items == null || data.items.Count == 0 || isOwnerValid == null || onClosed == null)
            {
                Debug.LogWarning("[ShopRoomPanel] 정비 상점 열기 실패 — 데이터·콜백 없음");
                return false;
            }
            if (IsOpen)
            {
                Debug.LogWarning("[ShopRoomPanel] 이미 열린 상점이 있다 — 정비 상점을 열지 않는다");
                return false;
            }

            var panel = EnsureInstance();
            if (panel == null) return false;
            panel.ShowForWorld(session, data, isOwnerValid, onClosed);
            return true;
        }

        private void ShowForWorld(MaintenanceSession session, ShopData data, Func<bool> isOwnerValid, Action onClosed)
        {
            worldShop = session;
            isWorldOwnerValid = isOwnerValid;
            worldClosed = onClosed;
            current = data;

            // 첫 개방에만 진열을 정한다. 다시 열면 세션 배열을 그대로 가리킨다(재추첨·재고 회복 없음).
            bool isFirstOpen = session.TryStockShop(MAX_ITEMS);
            purchasedCounts = session.ShopPurchasedCounts;
            weaponOffers = session.ShopWeaponOffers;
            if (isFirstOpen) ResolveWeaponOffers(data);

            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;
            BindShopPortrait(data.shopId);

            RefreshItems();
            ShowBody();
            BeginFocus();
            Debug.Log($"[ShopRoomPanel] 정비 상점 열기: {session.InteractionId} ({(isFirstOpen ? "첫 개방 — 진열 확정" : "재개방 — 진열 유지")})");
        }

        /// <summary>legacy 방문으로 돌아간다 — 패널 배열을 자기 것으로 되돌리고 월드 소유를 잊는다(세션 배열은 건드리지 않는다).</summary>
        private void DetachWorldShop()
        {
            worldShop = null;
            isWorldOwnerValid = null;
            worldClosed = null;
            purchasedCounts = ownPurchasedCounts;
            weaponOffers = ownWeaponOffers;
        }

        /// <summary>월드 방문이면 연 쪽의 세션이 아직 현재인가. legacy 방문은 항상 true.</summary>
        private bool IsWorldOwnerValid() => !IsWorldShop || (isWorldOwnerValid != null && isWorldOwnerValid());

        /// <summary>대기 드래프트 적립 — 월드 방문은 세션에, legacy 방문은 패널에.</summary>
        private void AddPendingDrafts(int count)
        {
            if (IsWorldShop) worldShop.AddShopPendingDrafts(count);
            else pendingDrafts += count;
        }

        private int PendingDraftCount => IsWorldShop ? worldShop.ShopPendingDrafts : pendingDrafts;

        /// <summary>
        /// 월드 방문의 [떠난다]. 닫고 → 세션의 대기 드래프트를 한 번 인출해 열고 → 닫힘을 알린다(legacy와 같은 순서).
        /// 세션이 낡았으면 지급·알림 없이 닫기만 한다.
        /// </summary>
        private void CloseWorldShop()
        {
            var session = worldShop;
            var closed = worldClosed;
            bool isValid = IsWorldOwnerValid();

            ClearOwnedSelection();
            int drafts = isValid ? session.TakeShopPendingDrafts() : 0;

            DetachWorldShop();
            current = null;
            HideBodyWithPauseRelease(releasePause: isValid);

            if (!isValid)
            {
                Debug.Log($"[ShopRoomPanel] 낡은 정비 상점 닫힘 — 지급·알림 없음({session.InteractionId})");
                return;
            }
            EventEffectApplier.GrantDrafts(drafts);
            closed?.Invoke();
        }

        /// <summary>열린 채 세션이 낡았으면(방 이동·런 종료) 닫는다. 닫았으면 true.</summary>
        private bool CloseIfWorldOwnerStale()
        {
            if (!IsWorldShop || IsWorldOwnerValid()) return false;
            CloseWorldShop();
            return true;
        }
    }
}
