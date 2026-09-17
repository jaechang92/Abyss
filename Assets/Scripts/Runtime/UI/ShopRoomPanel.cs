using System.Collections.Generic;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 상점 방 진열 모달. 완주 루프 계획 1-2.
    ///
    /// 틀은 <see cref="EventRoomPanel"/>과 같다 — 동적 생성(HudBuilder 수정·씬 재빌드 0),
    /// 정지는 <see cref="GameEvents.RaiseDraftOpened"/>로 기존 DraftOpen FSM 상태를 빌려 쓰고,
    /// 게이트 해제는 <see cref="GameEvents.RaiseEventResolved"/>로 알린다(비전투 방 공용 규약).
    ///
    /// 다른 점은 <b>패널이 구매로 닫히지 않는다</b>는 것이다. 골드가 닿는 만큼 사고 [떠난다]로 나간다.
    /// 그래서 구매마다 진열대를 다시 그린다(잔액·재고가 그때그때 바뀐다).
    ///
    /// <b>스킬 드래프트는 떠날 때 열린다.</b> 드래프트 모달은 정지를 다시 걸기 때문에 상점이 열린 채로
    /// 띄우면 두 모달이 겹치고 드래프트가 닫힐 때 정지가 풀려 버린다. 이벤트 방도 같은 이유로
    /// 효과를 닫은 뒤 적용하므로, 플레이어가 이미 겪은 순서와 다르지 않다.
    ///
    /// 생성·정지 규약은 <see cref="RunModalPanel{T}"/>에 있다.
    /// </summary>
    public sealed class ShopRoomPanel : RunModalPanel<ShopRoomPanel>
    {
        // 진열 상한. 넘치는 품목은 조용히 안 보인다 — 에러도 로그도 없다.
        // 4 → 5 (리롤권) → 6 (무기 좌판).
        //
        // 🔴 이제 상한을 올리면 패널이 <b>알아서</b> 커진다. 예전에는 PANEL_HEIGHT·ROW_TOP_Y·
        //    LEAVE_BUTTON_Y 를 손으로 같이 올리라고 주석이 일렀는데, 그건 잊으면 아무 표시 없이
        //    맨 아래 물건이 잘린다 — 이 저장소가 상점·도감에서 세 번 밟은 자리다.
        //    좌표를 「몇 개가 될 수 있는가」에서 계산하면 잊을 것이 없다.
        private const int MAX_ITEMS = 6;

        private const float ROW_HEIGHT = 76f;
        private const float ROW_GAP = 8f;
        private const float ROW_STRIDE = ROW_HEIGHT + ROW_GAP;

        private const float PANEL_WIDTH = 820f;
        private const float ROW_WIDTH = 720f;

        // 행이 차지하지 않는 부분(제목·설명·잔액·[떠난다] 여백)의 합. 행 수와 무관한 상수다.
        private const float PANEL_CHROME = 304f;

        private const float PANEL_HEIGHT = PANEL_CHROME + MAX_ITEMS * ROW_STRIDE;
        private const float ROW_TOP_Y = PANEL_HEIGHT * 0.5f - 208f;      // 헤더 아래 첫 행 중심
        private const float LEAVE_BUTTON_Y = -(PANEL_HEIGHT * 0.5f) + 68f; // 바닥에서 띄운 높이

        private static readonly Color GoldColor = new Color(0.95f, 0.82f, 0.45f);
        private static readonly Color ShortColor = new Color(0.92f, 0.45f, 0.42f);
        private static readonly Color DimColor = new Color(0.62f, 0.62f, 0.70f);

        private Text titleText;
        private Text descriptionText;
        private Text goldText;

        private readonly List<GameObject> itemRows = new();
        private readonly List<Text> nameLabels = new();
        private readonly List<Text> descLabels = new();
        private readonly List<Text> costLabels = new();
        private readonly List<Text> stockLabels = new();
        private readonly List<Button> buyButtons = new();
        private readonly List<Text> buyLabels = new();

        private ShopData current;

        // 이번 방문의 품목별 구매 횟수. 재고는 방문 단위로 회복된다(같은 상점을 두 번 만나지 않는다).
        private readonly int[] purchasedCounts = new int[MAX_ITEMS];

        // 무기 슬롯이 이번 방문에 파는 것. 🔴 <b>열 때 한 번 정하고 그 뒤로 안 바꾼다</b> —
        // RefreshItems 는 구매마다 도는데 거기서 뽑으면 물건을 살 때마다 진열이 갈린다.
        private readonly Weapon.WeaponShop.Offer[] weaponOffers = new Weapon.WeaponShop.Offer[MAX_ITEMS];

        // 떠날 때 열어야 하는 드래프트 횟수. 여러 번 사면 그만큼 쌓인다.
        private int pendingDrafts;

        /// <summary>상점을 연다. 떠나면 <see cref="GameEvents.OnEventResolved"/>가 발행된다.</summary>
        public static void Open(ShopData data)
        {
            if (data == null || data.items == null || data.items.Count == 0)
            {
                // 데이터가 비어 있으면 방이 영영 안 끝난다 — 즉시 해결로 흘려보낸다.
                Debug.LogWarning("[ShopRoomPanel] ShopData 비어 있음 — 상점을 건너뛴다.");
                GameEvents.RaiseEventResolved();
                return;
            }

            var panel = EnsureInstance();
            if (panel == null) return;
            panel.Show(data);
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례). 제네릭 베이스에선 안 불려 여기 둔다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

        // ───────────────────────── 흐름 ─────────────────────────

        private void Show(ShopData data)
        {
            current = data;
            pendingDrafts = 0;
            for (int i = 0; i < purchasedCounts.Length; i++) purchasedCounts[i] = 0;
            ResolveWeaponOffers(data);

            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;

            RefreshItems();
            ShowBody();
        }

        /// <summary>진열대를 현재 잔액·재고 기준으로 다시 그린다. 구매할 때마다 호출된다.</summary>
        private void RefreshItems()
        {
            int gold = RunManager.HasInstance ? RunManager.Instance.GoldShards : 0;
            if (goldText != null) goldText.text = $"보유 골드 {gold}";

            for (int i = 0; i < itemRows.Count; i++)
            {
                bool used = current != null && i < current.items.Count && i < MAX_ITEMS;
                itemRows[i].SetActive(used);
                if (!used) continue;

                var item = current.items[i];

                // 무기 좌판은 후보가 없으면 자리를 아예 감춘다 — "품절"로 두면 팔다가 떨어진 것처럼 보인다.
                if (item.isWeaponSlot && !weaponOffers[i].IsValid)
                {
                    itemRows[i].SetActive(false);
                    continue;
                }

                int remaining = Mathf.Max(0, item.stock - purchasedCounts[i]);
                bool soldOut = remaining <= 0;
                bool affordable = CostOf(i) <= gold;

                nameLabels[i].text = DisplayNameOf(i);
                descLabels[i].text = DisplayDescriptionOf(i);

                costLabels[i].text = CostTextOf(i);
                // 왜 못 사는지가 화면에 없으면 버그처럼 보인다 — 잔액 부족은 가격을 붉게 물들여 알린다.
                costLabels[i].color = (!soldOut && !affordable) ? ShortColor : GoldColor;

                stockLabels[i].text = soldOut ? "품절" : $"남은 재고 {remaining}";
                stockLabels[i].color = soldOut ? ShortColor : DimColor;

                buyLabels[i].text = soldOut ? "품절" : "구매";
                buyButtons[i].interactable = !soldOut && affordable;
            }
        }

        private void OnBuyClicked(int index)
        {
            if (current == null || index < 0 || index >= current.items.Count) return;

            var item = current.items[index];
            if (purchasedCounts[index] >= item.stock) return;
            if (item.isWeaponSlot && !weaponOffers[index].IsValid) return;

            // 잔액 검사는 여기서 확정한다 — ApplyNonModal은 지불 실패를 조용히 넘기므로
            // (상태 불변) 걸러 두지 않으면 나머지 효과만 공짜로 적용된다.
            int gold = RunManager.HasInstance ? RunManager.Instance.GoldShards : 0;
            if (CostOf(index) > gold) return;

            purchasedCounts[index] += 1;

            if (item.isWeaponSlot)
            {
                BuyWeapon(index);
            }
            else
            {
                pendingDrafts += EventEffectApplier.ApplyNonModal(item.effects);
                Debug.Log($"[ShopRoomPanel] 구매: {item.label} ({item.CostText}) — 대기 드래프트 {pendingDrafts}");
            }

            RefreshItems();
        }

        // ───────────────────────── 무기 좌판 ─────────────────────────

        /// <summary>
        /// 무기 슬롯이 이번 방문에 팔 것을 정한다. <b>상점을 여는 순간 한 번만</b> 돈다.
        ///
        /// 🔑 <b>여는 시점에야 정해지는 이유</b>: 무기는 폼 전용이라 지금 무슨 폼이냐에 따라
        /// 후보가 달라진다. 에셋에 미리 적으면 인스펙터에 보이는 것과 실제로 나오는 것이 갈린다
        /// (제단이 <c>Configure</c> 로 런타임에 꽂는 것과 같은 이유다).
        /// </summary>
        private void ResolveWeaponOffers(ShopData data)
        {
            for (int i = 0; i < weaponOffers.Length; i++) weaponOffers[i] = default;

            int slots = 0;
            int count = Mathf.Min(data.items.Count, MAX_ITEMS);
            for (int i = 0; i < count; i++)
            {
                if (data.items[i] != null && data.items[i].isWeaponSlot) slots += 1;
            }
            if (slots == 0) return;

            var run = RunManager.HasInstance ? RunManager.Instance : null;
            var config = run != null ? run.Config : null;
            if (config == null)
            {
                Debug.LogWarning("[ShopRoomPanel] RunConfig 미발견 — 무기 좌판을 비운다(자리는 감춰진다).");
                return;
            }

            var offers = Weapon.WeaponShop.Build(Weapon.WeaponCatalog.All, CurrentFormId(),
                                                 config.weaponRarityWeights, config.weaponPriceByRarity, slots);

            // 채운 만큼만 앞에서부터 꽂는다. 모자란 자리는 IsValid 가 false 로 남아 감춰진다.
            int next = 0;
            for (int i = 0; i < count && next < offers.Count; i++)
            {
                if (data.items[i] == null || !data.items[i].isWeaponSlot) continue;
                weaponOffers[i] = offers[next++];
            }

            if (offers.Count < slots)
            {
                Debug.LogWarning($"[ShopRoomPanel] 무기 좌판 {slots}자리 중 {offers.Count}개만 채웠다 — " +
                                 "현재 폼이 쓸 무기가 모자라거나 RunConfig.weaponPriceByRarity 가 0이다.");
            }
        }

        private void BuyWeapon(int index)
        {
            var offer = weaponOffers[index];
            var run = RunManager.HasInstance ? RunManager.Instance : null;
            if (run == null) return;

            // 🔴 값을 먼저 치르고 나서 담는다. 순서가 뒤집히면 잔액이 모자랄 때 공짜로 얻는다.
            if (!run.SpendGoldShards(offer.Price)) return;

            bool isNew = run.Weapons.Grant(offer.Weapon);
            Debug.Log($"[ShopRoomPanel] 무기 구매: {offer.Weapon.weaponId} (골드 {offer.Price}) — {(isNew ? "신규" : "강화")}");
        }

        private static string CurrentFormId()
        {
            var player = Object.FindAnyObjectByType<Player.PlayerCharacter>();
            var form = player != null ? player.Form : null;
            return form != null && form.CurrentForm != null ? form.CurrentForm.formId : null;
        }

        // ───────────────────────── 한 자리의 표시·값 ─────────────────────────
        //
        // 🔴 진열·잔액 판정·차감이 <b>같은 한 값</b>을 읽게 모아 둔다. 각자 계산하면 어긋나고,
        //    그건 「살 수 있다고 떴는데 안 사진다」로만 드러난다.

        /// <summary>이 자리의 값(골드). 무기 좌판은 등급 가격, 나머지는 효과에서 파생된 가격.</summary>
        private int CostOf(int index)
        {
            var item = current.items[index];
            return item.isWeaponSlot ? weaponOffers[index].Price : item.GoldCost;
        }

        private string CostTextOf(int index)
        {
            var item = current.items[index];
            return item.isWeaponSlot ? $"골드 {weaponOffers[index].Price}" : item.CostText;
        }

        private string DisplayNameOf(int index)
        {
            var item = current.items[index];
            if (!item.isWeaponSlot) return item.label;

            return Weapon.WeaponText.NameOf(weaponOffers[index].Weapon);
        }

        /// <summary>
        /// 무기 좌판의 설명. <b>이미 가진 무기면 「강화」로 읽히게 한다</b> —
        /// 같은 무기가 또 나왔을 때 꽝으로 보이면 살 이유가 사라진다(제단 프롬프트와 같은 규약).
        /// </summary>
        private string DisplayDescriptionOf(int index)
        {
            var item = current.items[index];
            if (!item.isWeaponSlot) return item.description;

            var weapon = weaponOffers[index].Weapon;
            var run = RunManager.HasInstance ? RunManager.Instance : null;
            return Weapon.WeaponText.ShopDescription(weapon, run != null && run.Weapons.Owns(weapon));
        }

        /// <summary>
        /// [떠난다] — 닫고 나서 미뤄둔 드래프트를 연다.
        ///
        /// 순서는 이벤트 방과 같다: 이 패널이 먼저 <c>DraftClosed</c>로 정지를 넘겨야 드래프트가
        /// 겹치지 않고 열린다. 게이트 해제는 마지막이다 — StageDirector의 다음 방 진행은
        /// 스케일 시간 <c>Invoke</c>라 드래프트가 다 닫힐 때까지 알아서 기다린다.
        /// </summary>
        private void OnLeaveClicked()
        {
            int drafts = pendingDrafts;
            pendingDrafts = 0;

            current = null;
            HideBody();

            EventEffectApplier.GrantDrafts(drafts);
            GameEvents.RaiseEventResolved();
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        protected override void BuildContent(Transform body)
        {
            var panel = CreateRect(body, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            titleText = CreateLabel(panel.transform, "TitleText", new Vector2(0, 310), new Vector2(760, 44),
                string.Empty, 26, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

            descriptionText = CreateLabel(panel.transform, "DescriptionText", new Vector2(0, 260), new Vector2(720, 34),
                string.Empty, 18, new Color(0.86f, 0.86f, 0.94f), TextAnchor.MiddleCenter);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;

            goldText = CreateLabel(panel.transform, "GoldText", new Vector2(0, 216), new Vector2(720, 28),
                string.Empty, 19, GoldColor, TextAnchor.MiddleCenter);

            BuildItemRows(panel.transform);

            var leave = CreateButton(panel.transform, "LeaveButton", new Vector2(0, LEAVE_BUTTON_Y), new Vector2(300, 52),
                "떠난다", 20);
            leave.onClick.AddListener(OnLeaveClicked);
        }

        private void BuildItemRows(Transform parent)
        {
            // 최대 개수만큼 미리 만들어 두고 표시 여부만 토글한다 — 상점마다 계층을 다시 짓지 않는다.
            for (int i = 0; i < MAX_ITEMS; i++)
            {
                float y = ROW_TOP_Y - i * ROW_STRIDE;
                var row = CreateRect(parent, $"Item{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(ROW_WIDTH, ROW_HEIGHT));

                var rowImg = row.AddComponent<Image>();
                rowImg.color = new Color(0.16f, 0.16f, 0.22f, 0.9f);
                rowImg.raycastTarget = false;

                var name = CreateLabel(row.transform, "Name", new Vector2(-165, 17), new Vector2(330, 26),
                    string.Empty, 19, Color.white, TextAnchor.MiddleLeft);

                var desc = CreateLabel(row.transform, "Desc", new Vector2(-165, -12), new Vector2(330, 22),
                    string.Empty, 15, DimColor, TextAnchor.MiddleLeft);

                var cost = CreateLabel(row.transform, "Cost", new Vector2(105, 12), new Vector2(170, 26),
                    string.Empty, 18, GoldColor, TextAnchor.MiddleRight);

                var stock = CreateLabel(row.transform, "Stock", new Vector2(105, -12), new Vector2(170, 20),
                    string.Empty, 14, DimColor, TextAnchor.MiddleRight);

                var buy = CreateButton(row.transform, "BuyButton", new Vector2(275, 0), new Vector2(130, 52),
                    "구매", 18);

                int index = i;
                buy.onClick.AddListener(() => OnBuyClicked(index));

                itemRows.Add(row);
                nameLabels.Add(name);
                descLabels.Add(desc);
                costLabels.Add(cost);
                stockLabels.Add(stock);
                buyButtons.Add(buy);
                buyLabels.Add(buy.GetComponentInChildren<Text>());
                row.SetActive(false);
            }
        }
    }
}
