using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class SkillCardDetailsPanel
    {
        private ScrollRect readingScroll;
        private Scrollbar readingBar;
        private Text readingHint;
        private float readingScale = -1f;
        private float readingCanvasScale = -1f;
        private bool resetReadingScroll;
        private bool hoveringReadingArea;

        private void BuildReadingView()
        {
            var topLeft = new Vector2(0f, 1f);
            var viewport = UiFactory.CreateRect(body.transform, "ReadingViewport", topLeft, topLeft, topLeft,
                Vector2.zero, Vector2.one);
            viewport.AddComponent<RectMask2D>();
            readingScroll = viewport.AddComponent<ScrollRect>();
            readingScroll.horizontal = false;
            readingScroll.vertical = true;
            readingScroll.inertia = false;
            readingScroll.movementType = ScrollRect.MovementType.Clamped;
            readingScroll.scrollSensitivity = 0f;
            readingScroll.viewport = (RectTransform)viewport.transform;
            var content = UiFactory.CreateRect(viewport.transform, "Content", topLeft, topLeft, topLeft,
                Vector2.zero, Vector2.one);
            readingScroll.content = (RectTransform)content.transform;
            headerText.transform.SetParent(content.transform, false);
            bodyText.transform.SetParent(content.transform, false);
            readingHint = CreateText(body.transform, "ReadingHint", 14, BodyColor);
            readingHint.alignment = TextAnchor.MiddleRight;

            var barObject = UiFactory.CreateRect(viewport.transform, "ReadingScrollbar", new Vector2(1f, 0f), Vector2.one,
                new Vector2(1f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
            var track = barObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.12f);
            track.raycastTarget = false;
            var handleObject = UiFactory.CreateRect(barObject.transform, "Handle", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var handle = handleObject.AddComponent<Image>();
            handle.color = new Color(0.85f, 0.82f, 1f, 0.65f);
            handle.raycastTarget = false;
            readingBar = barObject.AddComponent<Scrollbar>();
            readingBar.direction = Scrollbar.Direction.BottomToTop;
            readingBar.handleRect = (RectTransform)handleObject.transform;
            readingBar.targetGraphic = handle;
            readingBar.interactable = false;
            readingBar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = readingBar;
            RefreshReadability();
        }

        public void RefreshReadability()
        {
            if (headerText == null || bodyText == null) return;
            var canvas = GetComponentInParent<Canvas>();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            float scale = MenuTextReadability.Scale;
            if (scale == readingScale && canvasScale == readingCanvasScale) return;
            readingScale = scale;
            readingCanvasScale = canvasScale;
            headerText.fontSize = Mathf.RoundToInt((dockSlot != null ? DOCK_HEADER_FONT_SIZE : HEADER_FONT_SIZE) * scale);
            bodyText.fontSize = Mathf.RoundToInt((dockSlot != null ? DOCK_BODY_FONT_SIZE : BODY_FONT_SIZE) * scale);
            headerText.resizeTextForBestFit = bodyText.resizeTextForBestFit = false;
            headerText.verticalOverflow = bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            if (readingHint != null) readingHint.fontSize = Mathf.RoundToInt(14 * scale);
            isLayoutDirty = true;
        }

        private void ConfigureReadingArea(float width, float height, float left, float top)
        {
            if (readingScroll == null) return;
            float previous = readingScroll.verticalNormalizedPosition;
            float textWidth = Mathf.Max(1f, width - 14f);
            headerText.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            bodyText.rectTransform.sizeDelta = new Vector2(textWidth, 0f);
            float headerHeight = Mathf.Ceil(headerText.preferredHeight);
            float textHeight = Mathf.Ceil(bodyText.preferredHeight);
            float contentHeight = headerHeight + HEADER_GAP + textHeight;
            bool overflows = contentHeight > height;
            readingHint.text = Loc.CurrentLanguage switch
            {
                LocalizationLanguage.Korean => "휠 · Page Up/Down · 패드 오른쪽 스틱",
                LocalizationLanguage.Japanese => "ホイール · Page Up/Down · 右スティック",
                _ => "Wheel · Page Up/Down · Right stick"
            };
            readingHint.rectTransform.sizeDelta = new Vector2(width, 0f);
            float hintHeight = overflows ? Mathf.Ceil(readingHint.preferredHeight) : 0f;
            float viewportHeight = Mathf.Max(1f, height - (overflows ? hintHeight + 6f : 0f));
            readingHint.gameObject.SetActive(overflows);
            readingHint.rectTransform.anchoredPosition = new Vector2(left, -top - height + hintHeight);
            readingHint.rectTransform.sizeDelta = new Vector2(width, hintHeight);
            readingScroll.viewport.anchoredPosition = new Vector2(left, -top);
            readingScroll.viewport.sizeDelta = new Vector2(width, viewportHeight);
            readingScroll.content.sizeDelta = new Vector2(textWidth, Mathf.Max(viewportHeight, contentHeight));
            headerText.rectTransform.anchoredPosition = Vector2.zero;
            headerText.rectTransform.sizeDelta = new Vector2(textWidth, headerHeight);
            bodyText.rectTransform.anchoredPosition = new Vector2(0f, -headerHeight - HEADER_GAP);
            bodyText.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);
            readingBar.gameObject.SetActive(overflows);
            readingScroll.StopMovement();
            readingScroll.verticalNormalizedPosition = resetReadingScroll ? 1f : previous;
            resetReadingScroll = false;
        }

        private bool IsPointerOverReadingArea()
        {
            if (readingScroll == null || body == null || !body.activeInHierarchy || Mouse.current == null) return false;
            var canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)body.transform,
                Mouse.current.position.ReadValue(), camera);
        }

        private bool IsPointerInReadingCorridor(SkillCardView card)
        {
            if (card == null || body == null || !body.activeInHierarchy || Mouse.current == null) return false;
            var holder = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            UnityEngine.Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(holder, Mouse.current.position.ReadValue(), camera, out var point)) return false;
            Rect cardBounds = RectIn((RectTransform)card.transform, holder);
            Rect detailBounds = RectIn((RectTransform)body.transform, holder);
            // Let the pointer cross the visual gap into the reading slab without
            // clearing hover. Moving beyond this corridor restores focus priority.
            var corridor = Rect.MinMaxRect(Mathf.Min(cardBounds.xMin, detailBounds.xMin) - 4f,
                Mathf.Min(cardBounds.yMin, detailBounds.yMin) - 4f,
                Mathf.Max(cardBounds.xMax, detailBounds.xMax) + 4f,
                Mathf.Max(cardBounds.yMax, detailBounds.yMax) + 4f);
            return corridor.Contains(point);
        }

        private void HandleReadingInput()
        {
            if (readingScroll == null || SaveStatusOverlay.IsCapturingInput || SettingsPanel.IsOpen ||
                SettingsPanel.WasClosedThisFrame || CodexPanel.IsOpen || CodexPanel.WasClosedThisFrame) return;
            float range = readingScroll.content.rect.height - readingScroll.viewport.rect.height;
            if (range <= 0f) return;
            bool pointerInside = IsPointerOverReadingArea();
            bool hasFocus = EventSystem.current != null && shownCard != null &&
                EventSystem.current.currentSelectedGameObject == shownCard.FocusTarget;
            float pixels = 0f;
            if (pointerInside && Mouse.current != null) pixels += Mouse.current.scroll.ReadValue().y * 0.5f;
            if ((hasFocus || pointerInside) && !BuildContextPanel.IsReadingInputOwned)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.pageUpKey.isPressed) pixels += 420f * Time.unscaledDeltaTime;
                    if (keyboard.pageDownKey.isPressed) pixels -= 420f * Time.unscaledDeltaTime;
                    if (keyboard.homeKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 1f;
                    if (keyboard.endKey.wasPressedThisFrame) readingScroll.verticalNormalizedPosition = 0f;
                }
                if (Gamepad.current != null) pixels += Gamepad.current.rightStick.ReadValue().y * 420f * Time.unscaledDeltaTime;
            }
            if (pixels != 0f)
                readingScroll.verticalNormalizedPosition = Mathf.Clamp01(readingScroll.verticalNormalizedPosition + pixels / range);
        }
    }
}
