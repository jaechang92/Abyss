using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class KeyboardBindingsPanel
    {
        // Row heights are measured independently: a wrapped action must remain reachable by selection.
        private void LateUpdate()
        {
            if (!IsOpen || content == null) return;
            float scale = MenuTextReadability.Scale;
            var panel = content.parent.parent;
            ResizeText(panel.Find("Title").GetComponent<Text>(), 26, scale, 42f);
            ResizeText(statusLabel, 22, scale, 80f);
            statusLabel.rectTransform.anchoredPosition = new Vector2(0f, 324f - statusLabel.rectTransform.rect.height * 0.5f);
            var guide = panel.Find("CancelGuide").GetComponent<Text>();
            ResizeText(guide, 18, scale, 34f);
            guide.rectTransform.anchoredPosition = new Vector2(0f, -383f + guide.rectTransform.rect.height * 0.5f);
            float buttonHeight = 54f;
            for (int i = 0; i < 2; i++)
            {
                var button = i == 0 ? resetButton : backButton;
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = Mathf.RoundToInt(22f * scale);
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                buttonHeight = Mathf.Max(buttonHeight, label.preferredHeight + 8f);
            }
            float buttonBottom = -383f + guide.rectTransform.rect.height + 10f;
            for (int i = 0; i < 2; i++)
            {
                var button = i == 0 ? resetButton : backButton;
                var rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(300f, buttonHeight);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, buttonBottom + buttonHeight * 0.5f);
            }
            float viewportTop = Mathf.Min(208f, 324f - statusLabel.rectTransform.rect.height - 12f);
            float viewportBottom = Mathf.Max(-232f, buttonBottom + buttonHeight + 20f);
            var viewport = scroll.viewport;
            float height = viewportTop - viewportBottom;
            bool changed = Mathf.Abs(viewport.rect.height - height) > 0.01f;
            viewport.sizeDelta = new Vector2(700f, height);
            viewport.anchoredPosition = new Vector2(0f, (viewportTop + viewportBottom) * 0.5f);
            float offset = 0f;
            for (int i = 0; i < rowObjects.Count; i++)
            {
                var label = actionLabels[i];
                ResizeText(label, 22, scale, 44f);
                var key = keyButtons[i].GetComponentInChildren<Text>();
                key.fontSize = Mathf.RoundToInt(22f * scale);
                key.resizeTextForBestFit = false;
                key.horizontalOverflow = HorizontalWrapMode.Wrap;
                float textHeight = Mathf.Max(44f, Mathf.Max(label.preferredHeight, key.preferredHeight));
                var button = (RectTransform)keyButtons[i].transform;
                button.sizeDelta = new Vector2(240f, textHeight);
                var row = (RectTransform)rowObjects[i].transform;
                float rowHeight = Mathf.Max(ROW_HEIGHT, textHeight + 12f);
                changed |= Mathf.Abs(row.rect.height - rowHeight) > 0.01f;
                row.sizeDelta = new Vector2(660f, rowHeight);
                row.anchoredPosition = new Vector2(0f, -offset - rowHeight * 0.5f);
                offset += rowHeight;
            }
            content.sizeDelta = new Vector2(680f, offset);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(content.anchoredPosition.y, 0f, Mathf.Max(0f, offset - height)));
            if (changed) lastScrollSelection = null;
            if (!IsCapturing && !SaveStatusOverlay.IsCapturingInput) KeepFocusInside();
        }

        private static void ResizeText(Text text, int fontSize, float scale, float minimumHeight)
        {
            text.fontSize = Mathf.RoundToInt(fontSize * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, Mathf.Max(minimumHeight, text.preferredHeight));
        }
    }
}
