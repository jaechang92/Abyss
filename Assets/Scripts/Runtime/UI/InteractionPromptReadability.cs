using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>활성 Interactor가 숨겨진 안내도 갱신할 수 있는 글자 배치 보조 객체.</summary>
    public sealed class InteractionPromptReadability
    {
        private static readonly ConditionalWeakTable<Text, InteractionPromptReadability> helpers = new();
        private readonly Text label;
        private readonly RectTransform rect;
        private readonly RectTransform parent;
        private readonly Canvas canvas;
        private readonly Vector2 size;
        private readonly Vector2 position;
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 pivot;
        private readonly int fontSize;
        private string content;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;
        private Vector2 lastBounds;
        private float lastPreferredHeight = -1f;

        private InteractionPromptReadability(Text target)
        {
            label = target;
            rect = target.rectTransform;
            parent = rect.parent as RectTransform;
            canvas = target.GetComponentInParent<Canvas>();
            size = rect.rect.size;
            position = rect.anchoredPosition;
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            fontSize = target.fontSize;
        }

        public static InteractionPromptReadability Attach(Text target)
        {
            if (target == null) return null;
            var helper = helpers.GetValue(target, static text => new InteractionPromptReadability(text));
            helper.Refresh();
            return helper;
        }

        public void Refresh()
        {
            if (label == null || rect == null) return;
            float scale = MenuTextReadability.Scale;
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 bounds = parent != null ? parent.rect.size : new Vector2(1920f, 1080f);
            if (content == label.text && lastScale == scale && lastCanvasScale == canvasScale && lastBounds == bounds &&
                Mathf.Abs(lastPreferredHeight - label.preferredHeight) < 0.01f) return;
            label.fontSize = Mathf.RoundToInt(fontSize * scale);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            float margin = Mathf.Min(48f, bounds.x * 0.1f);
            float width = Mathf.Min(size.x * scale, Mathf.Max(1f, bounds.x - margin * 2f));
            var settings = label.GetGenerationSettings(new Vector2(width, 0f));
            float height = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            rect.sizeDelta = new Vector2(width, Mathf.Max(size.y, height + 4f));
            Vector2 next = position;
            if (parent != null && anchorMin == anchorMax)
            {
                Vector2 anchor = parent.rect.min + Vector2.Scale(parent.rect.size, anchorMin);
                float minX = parent.rect.xMin + 4f - anchor.x + width * pivot.x;
                float maxX = parent.rect.xMax - 4f - anchor.x - width * (1f - pivot.x);
                float minY = parent.rect.yMin + 4f - anchor.y + rect.rect.height * pivot.y;
                float maxY = parent.rect.yMax - 4f - anchor.y - rect.rect.height * (1f - pivot.y);
                next.x = Mathf.Clamp(next.x, minX, Mathf.Max(minX, maxX));
                next.y = Mathf.Clamp(next.y, minY, Mathf.Max(minY, maxY));
            }
            rect.anchoredPosition = next;
            content = label.text;
            lastScale = scale;
            lastCanvasScale = canvasScale;
            lastBounds = bounds;
            lastPreferredHeight = label.preferredHeight;
        }
    }
}
