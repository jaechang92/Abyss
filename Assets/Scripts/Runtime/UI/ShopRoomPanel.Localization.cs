using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>상점 공통 문구. 언어 변경은 방문 상태와 선택을 건드리지 않고 글자만 갱신한다.</summary>
    public sealed partial class ShopRoomPanel
    {
        private Text leaveText;

        private void OnEnable()
        {
            Loc.AddLanguageChangedListener(OnShopLanguageChanged);
            // 최초 OnEnable은 BuildContent보다 빠르다. 재활성화 때는 놓친 언어를 반영한다.
            RefreshLocalizedShopText();
        }

        private void DetachShopLocalization() => Loc.RemoveLanguageChangedListener(OnShopLanguageChanged);

        private void OnShopLanguageChanged(LocalizationLanguage language) => RefreshLocalizedShopText();

        private void RefreshLocalizedShopText()
        {
            if (!IsBodyOpen || current == null) return;

            RefreshShopHeaderText(RunManager.HasInstance ? RunManager.Instance.GoldShards : 0);
            for (int i = 0; i < itemRows.Count; i++)
            {
                if (!itemRows[i].activeSelf || i >= current.items.Count) continue;

                int remaining = Mathf.Max(0, current.items[i].stock - purchasedCounts[i]);
                costLabels[i].text = CostTextOf(i);
                stockLabels[i].text = StockText(remaining);
                buyLabels[i].text = BuyText(remaining <= 0);
            }
            // RefreshItems/RefreshNavigation을 호출하지 않는다. 상위 모달의 포커스도 유지한다.
        }

        // 열기 직전에도 호출한다. 닫힌 사이 언어가 바뀌어도 첫 화면부터 현재 언어를 쓴다.
        private void RefreshShopHeaderText(int gold)
        {
            if (goldText != null) goldText.text = Loc.GetFormat(StringKey.Shop_GoldBalanceFormat, gold);
            if (leaveText != null) leaveText.text = Loc.Get(StringKey.Shop_Leave);
        }

        private static string StockText(int remaining) => remaining <= 0
            ? Loc.Get(StringKey.Shop_SoldOut)
            : Loc.GetFormat(StringKey.Shop_StockFormat, remaining);

        private static string BuyText(bool soldOut) => Loc.Get(soldOut ? StringKey.Shop_SoldOut : StringKey.Shop_Buy);

        private string CostTextOf(int index)
        {
            var item = current.items[index];
            if (item.isWeaponSlot) return Loc.GetFormat(StringKey.Shop_CostGoldFormat, weaponOffers[index].Price);

            // ShopItem.CostText의 가격 분기를 그대로 표시한다. 상품 데이터/지불 로직은 바꾸지 않는다.
            int gold = item.GoldCost;
            int hp = item.HpCostPercent;
            if (gold > 0 && hp > 0) return Loc.GetFormat(StringKey.Shop_CostGoldHpFormat, gold, hp);
            if (gold > 0) return Loc.GetFormat(StringKey.Shop_CostGoldFormat, gold);
            if (hp > 0) return Loc.GetFormat(StringKey.Shop_CostHpFormat, hp);
            return Loc.Get(StringKey.Shop_CostFree);
        }
    }
}
