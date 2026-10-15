using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    public sealed partial class ShopRoomPanel
    {
        private sealed class ShopReadingEntry
        {
            public int FontSize;
            public string Content;
            public float Height;
            public float Width;
        }
        private readonly Dictionary<Text, ShopReadingEntry> shopReadingFonts = new();
        private ScrollRect shopReadingScroll;
        private RectTransform shopReadingPanel;
        private Canvas shopReadingCanvas;
        private float shopReadingScale = -1f;
        private float shopCanvasScale;
        private GameObject shopReadingSelection;
        private Button shopReadingFocus;
        private int shopActiveRows;

        private void BuildShopReadability(Transform parent)
        {
            shopReadingPanel = (RectTransform)parent;
            shopReadingCanvas = GetComponent<Canvas>();
            foreach (var text in parent.GetComponentsInChildren<Text>(true))
                shopReadingFonts[text] = new ShopReadingEntry { FontSize = text.fontSize };
            var viewport = CreateRect(parent, "ItemsViewport", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(750f, 520f));
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            var content = CreateRect(viewport.transform, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(ROW_WIDTH, 520f));
            shopReadingScroll = viewport.AddComponent<ScrollRect>();
            shopReadingScroll.viewport = (RectTransform)viewport.transform;
            shopReadingScroll.content = (RectTransform)content.transform;
            shopReadingScroll.horizontal = false;
            shopReadingScroll.movementType = ScrollRect.MovementType.Clamped;
            shopReadingScroll.inertia = false;
            shopReadingScroll.scrollSensitivity = 45f;
            descriptionText.transform.SetParent(content.transform, false);
            foreach (var row in itemRows) row.transform.SetParent(content.transform, false);
            var barRoot = CreateRect(viewport.transform, "Scrollbar", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.one * 0.5f, new Vector2(-5f, 0f), new Vector2(8f, 520f));
            barRoot.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            var handle = CreateRect(barRoot.transform, "Handle", Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)handle.transform);
            var image = handle.AddComponent<Image>();
            image.color = new Color(0.7f, 0.66f, 0.85f);
            var bar = barRoot.AddComponent<Scrollbar>();
            bar.targetGraphic = image;
            bar.handleRect = (RectTransform)handle.transform;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            shopReadingScroll.verticalScrollbar = bar;
            shopReadingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void ApplyShopReadability()
        {
            bool rescaled = shopReadingScale != MenuTextReadability.Scale || shopCanvasScale != shopReadingCanvas.scaleFactor;
            shopReadingScale = MenuTextReadability.Scale;
            shopCanvasScale = shopReadingCanvas.scaleFactor;
            shopActiveRows = ShopRowMask();
            foreach (var entry in shopReadingFonts)
            {
                entry.Key.fontSize = Mathf.RoundToInt(entry.Value.FontSize * shopReadingScale);
                entry.Key.resizeTextForBestFit = false;
                entry.Key.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            float titleHeight = Mathf.Max(44f, titleText.preferredHeight);
            titleText.rectTransform.sizeDelta = new Vector2(760f, titleHeight);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, PANEL_HEIGHT * 0.5f - 22f - titleHeight * 0.5f);
            float goldHeight = Mathf.Max(28f, goldText.preferredHeight);
            goldText.rectTransform.sizeDelta = new Vector2(720f, goldHeight);
            float goldTop = PANEL_HEIGHT * 0.5f - 22f - titleHeight - 12f;
            goldText.rectTransform.anchoredPosition = new Vector2(0f, goldTop - goldHeight * 0.5f);
            UiArtDecor.PlaceLeadingIcon(goldText, UiArtKeys.CURRENCY_GOLD_SHARDS, GOLD_ICON_SIZE);
            var leaveRect = (RectTransform)leaveButton.transform;
            float leaveHeight = Mathf.Max(52f, leaveButton.GetComponentInChildren<Text>().preferredHeight + 8f);
            leaveRect.sizeDelta = new Vector2(300f, leaveHeight);
            leaveRect.anchoredPosition = new Vector2(0f, LEAVE_BUTTON_Y);
            float bottom = LEAVE_BUTTON_Y + leaveHeight * 0.5f + 20f;
            float top = goldTop - goldHeight - 16f;
            var viewport = shopReadingScroll.viewport;
            viewport.sizeDelta = new Vector2(750f, Mathf.Max(100f, top - bottom));
            viewport.anchoredPosition = new Vector2(0f, top - viewport.rect.height * 0.5f);
            ((RectTransform)shopReadingScroll.verticalScrollbar.transform).sizeDelta = new Vector2(8f, viewport.rect.height);
            float offset = 8f;
            float introHeight = string.IsNullOrEmpty(descriptionText.text) ? 0f : descriptionText.preferredHeight;
            PlaceShopText(descriptionText, introHeight, 0f, -offset, 720f);
            if (introHeight > 0f) offset += introHeight + 16f;
            for (int i = 0; i < itemRows.Count; i++)
            {
                if (!itemRows[i].activeSelf) continue;
                float nameHeight = Mathf.Max(26f, nameLabels[i].preferredHeight);
                float descriptionHeight = Mathf.Max(22f, descLabels[i].preferredHeight);
                float costHeight = Mathf.Max(26f, costLabels[i].preferredHeight);
                float stockHeight = Mathf.Max(20f, stockLabels[i].preferredHeight);
                float buyHeight = Mathf.Max(52f, buyLabels[i].preferredHeight + 8f);
                float height = Mathf.Max(ROW_HEIGHT, Mathf.Max(nameHeight + descriptionHeight + 22f, Mathf.Max(costHeight + stockHeight + 22f, buyHeight + 16f)));
                var row = (RectTransform)itemRows[i].transform;
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.pivot = Vector2.one * 0.5f;
                row.sizeDelta = new Vector2(ROW_WIDTH, height);
                row.anchoredPosition = new Vector2(0f, -offset - height * 0.5f);
                float rowTop = height * 0.5f - 8f;
                PlaceShopText(nameLabels[i], nameHeight, -165f, rowTop, 330f);
                PlaceShopText(descLabels[i], descriptionHeight, -165f, rowTop - nameHeight - 6f, 330f);
                PlaceShopText(costLabels[i], costHeight, 105f, rowTop, 170f);
                PlaceShopText(stockLabels[i], stockHeight, 105f, rowTop - costHeight - 6f, 170f);
                var buy = (RectTransform)buyButtons[i].transform;
                buy.sizeDelta = new Vector2(130f, buyHeight);
                buy.anchoredPosition = new Vector2(275f, 0f);
                offset += height + ROW_GAP;
            }
            shopReadingScroll.content.sizeDelta = new Vector2(ROW_WIDTH, Mathf.Max(viewport.rect.height, offset));
            ClampShopScroll(shopReadingScroll.content.anchoredPosition.y);
            // Metric refreshes during wheel scrolling must preserve the user's scroll position.
            if (rescaled) shopReadingSelection = null;
            foreach (var entry in shopReadingFonts)
            {
                entry.Value.Content = entry.Key.text;
                entry.Value.Height = entry.Key.preferredHeight;
                entry.Value.Width = entry.Key.preferredWidth;
            }
        }

        private static void PlaceShopText(Text text, float height, float x, float top, float width)
        {
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.one * 0.5f;
            text.rectTransform.pivot = Vector2.one * 0.5f;
            // Intro is a top-anchored child of the scrolling content; row labels are centred children.
            if (text.name == "DescriptionText") text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.rectTransform.anchoredPosition = new Vector2(x, top - height * 0.5f);
        }

        private void ClampShopScroll(float y) => shopReadingScroll.content.anchoredPosition = new Vector2(0f,
            Mathf.Clamp(y, 0f, Mathf.Max(0f, shopReadingScroll.content.rect.height - shopReadingScroll.viewport.rect.height)));

        private int ShopRowMask()
        {
            int mask = 0;
            for (int i = 0; i < itemRows.Count; i++) if (itemRows[i].activeSelf) mask |= 1 << i;
            return mask;
        }

        private Button ShopReadingFocusOrEntry()
        {
            if (shopReadingFocus == leaveButton && IsNavigable(leaveButton)) return leaveButton;
            int slot = BuySlotOf(shopReadingFocus != null ? shopReadingFocus.gameObject : null);
            return IsBuyNavigable(slot) ? shopReadingFocus : EntryButton();
        }

        private void LateUpdate()
        {
            if (!IsBodyOpen || current == null || shopReadingScroll == null) return;
            bool changed = shopReadingScale != MenuTextReadability.Scale || shopCanvasScale != shopReadingCanvas.scaleFactor || shopActiveRows != ShopRowMask();
            foreach (var entry in shopReadingFonts)
                if (entry.Value.Content != entry.Key.text || Mathf.Abs(entry.Value.Height - entry.Key.preferredHeight) > 0.01f ||
                    Mathf.Abs(entry.Value.Width - entry.Key.preferredWidth) > 0.01f) { changed = true; break; }
            if (changed) ApplyShopReadability();
            if (IsUpperModalOwningInput) return;
            var selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            // Pointer deselection can occur after Update. Keep the cached selection until it is restored.
            if (selection != null && selection != shopReadingSelection)
            {
                shopReadingSelection = selection;
                if (selection == leaveButton.gameObject) shopReadingFocus = leaveButton;
                for (int i = 0; i < buyButtons.Count; i++)
                {
                    if (buyButtons[i].gameObject != selection) continue;
                    if (IsBuyNavigable(i)) shopReadingFocus = buyButtons[i];
                    var row = (RectTransform)itemRows[i].transform;
                    float top = -row.anchoredPosition.y - row.rect.height * 0.5f;
                    float bottom = top + row.rect.height;
                    if (row.rect.height > shopReadingScroll.viewport.rect.height)
                    {
                        var buy = (RectTransform)buyButtons[i].transform;
                        top = -row.anchoredPosition.y - buy.rect.height * 0.5f;
                        bottom = top + buy.rect.height;
                    }
                    float y = shopReadingScroll.content.anchoredPosition.y;
                    if (top < y) y = top;
                    else if (bottom > y + shopReadingScroll.viewport.rect.height) y = bottom - shopReadingScroll.viewport.rect.height;
                    ClampShopScroll(y);
                    break;
                }
            }
            float delta = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.pageDownKey.wasPressedThisFrame) delta += shopReadingScroll.viewport.rect.height * 0.8f;
                if (Keyboard.current.pageUpKey.wasPressedThisFrame) delta -= shopReadingScroll.viewport.rect.height * 0.8f;
            }
            if (Gamepad.current != null) delta += -Gamepad.current.rightStick.ReadValue().y * 420f * Time.unscaledDeltaTime;
            if (delta != 0f) ClampShopScroll(shopReadingScroll.content.anchoredPosition.y + delta);
        }
    }
}
