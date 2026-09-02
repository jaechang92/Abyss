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
    /// </summary>
    public sealed class ShopRoomPanel : MonoBehaviour
    {
        private const int MAX_ITEMS = 4;

        private const float PANEL_WIDTH = 820f;
        private const float PANEL_HEIGHT = 640f;
        private const float ROW_WIDTH = 720f;
        private const float ROW_HEIGHT = 76f;
        private const float ROW_GAP = 8f;
        private const float ROW_TOP_Y = 112f;

        private static readonly Color GoldColor = new Color(0.95f, 0.82f, 0.45f);
        private static readonly Color ShortColor = new Color(0.92f, 0.45f, 0.42f);
        private static readonly Color DimColor = new Color(0.62f, 0.62f, 0.70f);

        private static ShopRoomPanel instance;

        private GameObject body;
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

        // 떠날 때 열어야 하는 드래프트 횟수. 여러 번 사면 그만큼 쌓인다.
        private int pendingDrafts;

        private bool isOpen;

        public static bool IsOpen => instance != null && instance.isOpen;

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

            EnsureInstance();
            if (instance == null) return;
            instance.Show(data);
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            // 씬 전환으로 파괴된 인스턴스는 Unity의 == 오버로드 덕에 여기서 null로 판정되어 다시 만들어진다.
            if (instance != null) return;

            var go = CreateOverlayCanvas("ShopRoomPanel", UiSortingOrder.Modal);
            // Run 씬 전용이므로 DontDestroyOnLoad 하지 않는다(EventRoomPanel과 같은 판단).
            instance = go.AddComponent<ShopRoomPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        // ───────────────────────── 흐름 ─────────────────────────

        private void Show(ShopData data)
        {
            current = data;
            pendingDrafts = 0;
            for (int i = 0; i < purchasedCounts.Length; i++) purchasedCounts[i] = 0;

            if (titleText != null) titleText.text = data.title;
            if (descriptionText != null) descriptionText.text = data.description;

            RefreshItems();
            body.SetActive(true);

            if (!isOpen)
            {
                isOpen = true;
                GameEvents.RaiseDraftOpened();   // 기존 DraftOpen 상태로 전역 정지
            }
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
                int remaining = Mathf.Max(0, item.stock - purchasedCounts[i]);
                bool soldOut = remaining <= 0;
                bool affordable = item.GoldCost <= gold;

                nameLabels[i].text = item.label;
                descLabels[i].text = item.description;

                costLabels[i].text = item.CostText;
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

            // 잔액 검사는 여기서 확정한다 — ApplyNonModal은 지불 실패를 조용히 넘기므로
            // (상태 불변) 걸러 두지 않으면 나머지 효과만 공짜로 적용된다.
            int gold = RunManager.HasInstance ? RunManager.Instance.GoldShards : 0;
            if (item.GoldCost > gold) return;

            purchasedCounts[index] += 1;
            pendingDrafts += EventEffectApplier.ApplyNonModal(item.effects);
            Debug.Log($"[ShopRoomPanel] 구매: {item.label} ({item.CostText}) — 대기 드래프트 {pendingDrafts}");

            RefreshItems();
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

            body.SetActive(false);
            current = null;

            if (isOpen)
            {
                isOpen = false;
                GameEvents.RaiseDraftClosed();
            }

            EventEffectApplier.GrantDrafts(drafts);
            GameEvents.RaiseEventResolved();
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.82f);

            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            titleText = CreateLabel(panel.transform, "TitleText", new Vector2(0, 268), new Vector2(760, 44),
                string.Empty, 26, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

            descriptionText = CreateLabel(panel.transform, "DescriptionText", new Vector2(0, 218), new Vector2(720, 34),
                string.Empty, 18, new Color(0.86f, 0.86f, 0.94f), TextAnchor.MiddleCenter);
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;

            goldText = CreateLabel(panel.transform, "GoldText", new Vector2(0, 174), new Vector2(720, 28),
                string.Empty, 19, GoldColor, TextAnchor.MiddleCenter);

            BuildItemRows(panel.transform);

            var leave = CreateButton(panel.transform, "LeaveButton", new Vector2(0, -252), new Vector2(300, 52),
                "떠난다", 20);
            leave.onClick.AddListener(OnLeaveClicked);
        }

        private void BuildItemRows(Transform parent)
        {
            // 최대 개수만큼 미리 만들어 두고 표시 여부만 토글한다 — 상점마다 계층을 다시 짓지 않는다.
            for (int i = 0; i < MAX_ITEMS; i++)
            {
                float y = ROW_TOP_Y - i * (ROW_HEIGHT + ROW_GAP);
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
