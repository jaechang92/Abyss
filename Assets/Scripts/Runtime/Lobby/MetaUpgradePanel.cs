using System;
using System.Collections.Generic;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 심연의 제단 영구 업그레이드 패널. AltarNpc 상호작용 시 열린다.
    /// 심연 조각 잔액과 MetaUpgrades.All 각 항목을 1행씩 표시하고 [강화] 구매를 처리한다.
    /// 행은 최초 Open 시 런타임 동적 생성한다(업그레이드 개수 유동적).
    /// 모든 텍스트는 Loc.Get(StringKey)로 조회(하드코딩 금지).
    /// FormSelectPanel/DialogueUI의 Open·IsOpen 패턴을 따르고, 키보드·패드 조작은 MetaUpgradePanel.Navigation.cs가 맡는다.
    /// </summary>
    public sealed partial class MetaUpgradePanel : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;

        [Header("라벨")]
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text shardsLabel;

        [Header("행")]
        [Tooltip("업그레이드 행이 생성될 컨테이너(top-center 정렬).")]
        [SerializeField] private RectTransform rowContainer;

        [Header("버튼")]
        [SerializeField] private Button closeButton;

        [Tooltip("행 1개의 세로 간격(px).")]
        [SerializeField] private float rowHeight = 88f;

        // 행/버튼 색(제단 느낌의 보라색 계열)
        private static readonly Color RowBg = new Color(0.14f, 0.12f, 0.2f, 0.95f);
        private static readonly Color PurchaseBase = new Color(0.45f, 0.3f, 0.6f);
        private static readonly Color PurchaseDisabled = new Color(0.25f, 0.24f, 0.3f);

        private readonly List<UpgradeRow> rows = new();
        private Action onClosed;

        public bool IsOpen { get; private set; }

        /// <summary>업그레이드 1행의 갱신 대상 참조 묶음.</summary>
        private sealed class UpgradeRow
        {
            public MetaUpgradeData Data;
            public Text LevelLabel;
            public Text CostLabel;
            public Button Button;
            public Image ButtonImage;
        }

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (root != null) root.SetActive(false);
            IsOpen = false;
        }

        /// <summary>
        /// 패널을 연다. closed는 닫힐 때(Esc·패드 B/닫기) 호출되는 콜백.
        /// 열린 채 다시 불리면 콜백만 바꾸고, 열기 전 뒤 화면 선택 기억은 처음 것을 유지한다.
        /// </summary>
        public void Open(Action closed)
        {
            bool isFirstOpen = !IsOpen;
            onClosed = closed;
            EnsureRows();
            if (titleLabel != null) titleLabel.text = Loc.Get(StringKey.Altar_Title);
            if (closeButton != null)
            {
                var closeText = closeButton.GetComponentInChildren<Text>();
                if (closeText != null) closeText.text = Loc.Get(StringKey.Common_Close);
            }
            Refresh();
            isRootHidePending = false;
            if (root != null) root.SetActive(true);
            IsOpen = true;
            BeginFocus(isFirstOpen);
        }

        /// <summary>
        /// 닫는다. 외부 정리용으로도 불리므로 연 프레임 가드를 두지 않는다(UI의 [닫기]는 <see cref="OnCloseClicked"/>).
        ///
        /// 🔴 상태와 콜백을 먼저 비운 뒤 root를 끈다 — root가 이 GameObject면 SetActive(false)가 같은 호출 안에서
        /// OnDisable을 부르는데, 그때 IsOpen이 남아 있으면 외부 비활성으로 오인해 콜백이 두 번 불린다.
        /// </summary>
        public void Close()
        {
            IsOpen = false;
            var cb = onClosed;
            onClosed = null;
            isRootHidePending = false;
            ReleaseFocus();
            if (root != null) root.SetActive(false);
            cb?.Invoke();
        }

        // ───────────────────────── 행 생성 ─────────────────────────

        /// <summary>최초 1회 카탈로그(MetaUpgrades.All)로 행을 만든다. 비어 있으면 다음 Open에서 재시도.</summary>
        private void EnsureRows()
        {
            if (rows.Count > 0 || rowContainer == null) return;

            var catalog = MetaUpgrades.All;
            if (catalog == null) return;

            for (int i = 0; i < catalog.Length; i++)
            {
                var data = catalog[i];
                if (data == null) continue;
                rows.Add(BuildRow(data, rows.Count));
            }
        }

        private UpgradeRow BuildRow(MetaUpgradeData data, int index)
        {
            var rowRt = CreateRect(rowContainer, "Row_" + data.upgradeId,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -index * rowHeight), new Vector2(760f, rowHeight - 8f));
            var bg = rowRt.gameObject.AddComponent<Image>();
            bg.color = RowBg;

            CreateText(rowRt, "Name", Loc.Get(data.nameKey), 22,
                new Vector2(-230f, 15f), new Vector2(300f, 30f), TextAnchor.LowerLeft, Color.white);
            CreateText(rowRt, "Desc", Loc.Get(data.descKey), 14,
                new Vector2(-224f, -16f), new Vector2(320f, 26f), TextAnchor.UpperLeft, new Color(0.72f, 0.72f, 0.8f));

            var levelLabel = CreateText(rowRt, "Level", "", 20,
                new Vector2(-10f, 0f), new Vector2(110f, 40f), TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f));
            var costLabel = CreateText(rowRt, "Cost", "", 18,
                new Vector2(110f, 0f), new Vector2(120f, 40f), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));

            var (btn, img) = CreateButton(rowRt, "Purchase",
                new Vector2(300f, 0f), new Vector2(130f, 56f), PurchaseBase, Loc.Get(StringKey.Altar_Purchase));

            var row = new UpgradeRow { Data = data, LevelLabel = levelLabel, CostLabel = costLabel, Button = btn, ButtonImage = img };
            btn.onClick.AddListener(() => OnPurchase(row));
            return row;
        }

        // ───────────────────────── 구매 / 갱신 ─────────────────────────

        private void OnPurchase(UpgradeRow row)
        {
            // 여는 입력이 Submit으로 흘러 든 것·상위 모달이 입력을 쥔 동안의 클릭 — 조각을 쓰지 않는다
            if (!IsOpen || IsOpenedThisFrame || IsUpperModalOwningInput) return;
            if (row == null || row.Data == null) return;
            if (MetaSaveService.Instance.TryPurchaseUpgrade(row.Data)) Refresh();
        }

        /// <summary>잔액·레벨·비용·버튼 상태를 전체 재계산한다.</summary>
        private void Refresh()
        {
            int shards = MetaSaveService.Instance.Current.abyssShardsTotal;
            if (shardsLabel != null) shardsLabel.text = Loc.GetFormat(StringKey.Altar_ShardsFormat, shards);

            foreach (var row in rows)
            {
                if (row == null || row.Data == null) continue;

                int level = MetaSaveService.Instance.GetUpgradeLevel(row.Data.upgradeId);
                if (row.LevelLabel != null) row.LevelLabel.text = Loc.GetFormat(StringKey.Altar_LevelFormat, level, row.Data.MaxLevel);

                int cost = row.Data.CostForNextLevel(level);
                bool isMaxed = cost < 0;
                if (row.CostLabel != null)
                {
                    row.CostLabel.text = isMaxed
                        ? Loc.Get(StringKey.Altar_Maxed)
                        : Loc.GetFormat(StringKey.Altar_CostFormat, cost);
                }

                bool canBuy = !isMaxed && shards >= cost;
                if (row.Button != null) row.Button.interactable = canBuy;
                if (row.ButtonImage != null) row.ButtonImage.color = canBuy ? PurchaseBase : PurchaseDisabled;
            }

            RefreshNavigation();
        }

        // ───────────────────────── UI 헬퍼(런타임 생성) ─────────────────────────

        private RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(Transform parent, string name, string content, int size, Vector2 pos, Vector2 rectSize, TextAnchor anchor, Color color)
        {
            var rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, rectSize);
            var t = rect.gameObject.AddComponent<Text>();
            UiFactory.ApplyFont(t);
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            return t;
        }

        private (Button button, Image image) CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, Color baseColor, string labelText)
        {
            var rect = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = baseColor;
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.35f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            colors.disabledColor = PurchaseDisabled;
            btn.colors = colors;

            var labelRect = CreateRect(rect, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelRect.gameObject.AddComponent<Text>();
            UiFactory.ApplyFont(label);
            label.text = labelText;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return (btn, img);
        }
    }
}
