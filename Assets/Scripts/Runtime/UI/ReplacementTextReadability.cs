using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>폼/스킬 교체창의 제목과 버튼 글자를 확대하고 두 버튼 행의 간격을 보존한다.</summary>
    public sealed class ReplacementTextReadability : MonoBehaviour
    {
        private sealed class ButtonEntry
        {
            public Button Button;
            public Text Label;
            public int FontSize;
            public Vector2 Size;
            public Vector2 Position;
            public string Content;
            public float Height;
        }
        private readonly List<ButtonEntry> entries = new();
        private RectTransform panel;
        private Vector2 panelSize;
        private Text incoming;
        private int incomingFont;
        private float incomingWidth;
        private float incomingHeight;
        private float titleTop;
        private float titleGap;
        private float footerGap;
        private float bottomPadding;
        private float rowHeight;
        private ButtonEntry cancel;
        private string lastIncoming;
        private float lastIncomingHeight;
        private float lastScale = -1f;
        private Canvas canvas;
        private float lastCanvasScale;
        private readonly Vector3[] corners = new Vector3[4];

        public static ReplacementTextReadability Attach(Transform root, Text incoming, Button[] slots, Button cancel)
        {
            if (root == null || incoming == null || cancel == null) return null;
            var panel = incoming.transform.parent as RectTransform;
            if (panel == null) return null;
            var existing = panel.GetComponent<ReplacementTextReadability>();
            if (existing != null) { existing.Apply(); return existing; }
            var component = panel.gameObject.AddComponent<ReplacementTextReadability>();
            component.panel = panel;
            component.panelSize = panel.sizeDelta;
            component.incoming = incoming;
            component.incomingFont = incoming.fontSize;
            component.incomingWidth = incoming.rectTransform.rect.width;
            component.incomingHeight = incoming.rectTransform.rect.height;
            component.canvas = panel.GetComponentInParent<Canvas>();
            var title = component.Bounds(incoming.rectTransform);
            component.titleTop = panel.rect.yMax - title.yMax;
            float rowTop = float.MinValue;
            float rowBottom = float.MaxValue;
            foreach (var button in slots)
            {
                if (button == null) continue;
                var rect = (RectTransform)button.transform;
                var bounds = component.Bounds(rect);
                rowTop = Mathf.Max(rowTop, bounds.yMax);
                rowBottom = Mathf.Min(rowBottom, bounds.yMin);
                component.rowHeight = Mathf.Max(component.rowHeight, rect.rect.height);
                component.entries.Add(Capture(button));
            }
            var footer = component.Bounds((RectTransform)cancel.transform);
            component.titleGap = rowTop == float.MinValue ? 16f : title.yMin - rowTop;
            component.footerGap = rowBottom == float.MaxValue ? 20f : rowBottom - footer.yMax;
            component.bottomPadding = footer.yMin - panel.rect.yMin;
            component.cancel = Capture(cancel);
            component.Apply();
            return component;
        }

        private static ButtonEntry Capture(Button button)
        {
            var rect = (RectTransform)button.transform;
            var label = button.GetComponentInChildren<Text>(true);
            return new ButtonEntry { Button = button, Label = label, FontSize = label != null ? label.fontSize : 14,
                Size = rect.sizeDelta, Position = rect.anchoredPosition };
        }

        private Rect Bounds(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var bottom = panel.InverseTransformPoint(corners[0]);
            var top = panel.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(bottom.x, bottom.y, top.x, top.y);
        }

        public void Apply()
        {
            if (panel == null) return;
            float scale = MenuTextReadability.Scale;
            float extraWidth = 480f * (scale - 1f);
            incoming.fontSize = Mathf.RoundToInt(incomingFont * scale);
            incoming.resizeTextForBestFit = false;
            incoming.horizontalOverflow = HorizontalWrapMode.Wrap;
            incoming.rectTransform.sizeDelta = new Vector2(incomingWidth + extraWidth, incomingHeight);
            float titleHeight = Mathf.Max(incomingHeight, incoming.preferredHeight);
            float height = rowHeight;
            foreach (var entry in entries) height = Mathf.Max(height, Measure(entry, scale));
            float cancelHeight = Measure(cancel, scale);
            float panelHeight = titleTop + titleHeight + titleGap + height + footerGap + cancelHeight + bottomPadding;
            panel.sizeDelta = new Vector2(panelSize.x + extraWidth, Mathf.Max(panelSize.y, panelHeight));
            incoming.rectTransform.anchorMin = incoming.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            incoming.rectTransform.pivot = new Vector2(0.5f, 1f);
            incoming.rectTransform.sizeDelta = new Vector2(incomingWidth + extraWidth, titleHeight);
            incoming.rectTransform.anchoredPosition = new Vector2(0f, -titleTop);
            float rowTop = titleTop + titleHeight + titleGap;
            foreach (var entry in entries)
            {
                var rect = (RectTransform)entry.Button.transform;
                float added = rect.sizeDelta.x - entry.Size.x;
                Place(rect, new Vector2(entry.Position.x + Mathf.Sign(entry.Position.x) * added * 0.5f, -rowTop - height * 0.5f), height);
            }
            Place((RectTransform)cancel.Button.transform, new Vector2(cancel.Position.x, -rowTop - height - footerGap - cancelHeight * 0.5f), cancelHeight);
            lastScale = scale;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            lastIncoming = incoming.text;
            lastIncomingHeight = incoming.preferredHeight;
        }

        private static float Measure(ButtonEntry entry, float scale)
        {
            var rect = (RectTransform)entry.Button.transform;
            rect.sizeDelta = new Vector2(entry.Size.x + 160f * (scale - 1f), entry.Size.y);
            float height = entry.Size.y;
            if (entry.Label != null)
            {
                entry.Label.fontSize = Mathf.RoundToInt(entry.FontSize * scale);
                entry.Label.resizeTextForBestFit = false;
                entry.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                height = Mathf.Max(height, entry.Label.preferredHeight + 16f);
                entry.Content = entry.Label.text;
                entry.Height = entry.Label.preferredHeight;
            }
            return height;
        }

        private static void Place(RectTransform rect, Vector2 position, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = position;
        }

        private void LateUpdate()
        {
            if (panel == null) return;
            bool changed = lastScale != MenuTextReadability.Scale || lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f) ||
                lastIncoming != incoming.text || Mathf.Abs(lastIncomingHeight - incoming.preferredHeight) > 0.01f;
            foreach (var entry in entries)
                if (entry.Label != null && (entry.Content != entry.Label.text || Mathf.Abs(entry.Height - entry.Label.preferredHeight) > 0.01f)) changed = true;
            if (cancel.Label != null && (cancel.Content != cancel.Label.text || Mathf.Abs(cancel.Height - cancel.Label.preferredHeight) > 0.01f)) changed = true;
            if (changed) Apply();
        }
    }
}
