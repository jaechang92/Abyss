using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>글자 확대와 줄바꿈 높이를 표시 전용으로 반영한다. 입력/학습 상태는 변경하지 않는다.</summary>
    public sealed partial class FirstPlayTutorialPanel
    {
        private bool isReadingCompact;
        private float lastReadingScale = -1f;
        private Vector2 lastReadingBounds;
        private string lastReadingHeader;
        private string lastReadingHint;
        private string lastReadingPad;
        private string lastReadingSkip;
        private string lastReadingKey;
        private string lastReadingInstruction;
        private Text readingSkip;

        private void LateUpdate() => RefreshReadability();

        private void RefreshReadability(bool force = false)
        {
            if (body == null) return;
            readingSkip ??= skipButton.GetComponentInChildren<Text>(true);
            Vector2 bounds = ((RectTransform)transform).rect.size;
            if (bounds.x <= 0f || bounds.y <= 0f) return;
            float scale = MenuTextReadability.Scale;
            if (!force && lastReadingScale == scale && lastReadingBounds == bounds &&
                lastReadingHeader == header.text && lastReadingHint == hint.text &&
                lastReadingPad == padHint.text && lastReadingSkip == readingSkip.text &&
                lastReadingKey == (spatialKey != null ? spatialKey.text : null) &&
                lastReadingInstruction == (spatialHint != null ? spatialHint.text : null)) return;

            float width = Mathf.Min((isReadingCompact ? 460f : PanelSize.x) * scale, Mathf.Max(80f, bounds.x - 48f));
            float inner = width - 32f;
            ConfigureReadingText(header, 18, scale);
            ConfigureReadingText(hint, 24, scale);
            ConfigureReadingText(padHint, 14, scale);
            ConfigureReadingText(readingSkip, 14, scale);
            float buttonWidth = Mathf.Min(Mathf.Max(110f * scale, readingSkip.preferredWidth + 24f), inner * 0.45f);
            float padWidth = inner - buttonWidth - 16f;
            float buttonHeight = Mathf.Max(30f * scale, ReadingHeight(readingSkip, buttonWidth - 16f) + 12f);
            float padHeight = ReadingHeight(padHint, padWidth);
            float footerHeight = Mathf.Max(buttonHeight, padHeight);
            float headerHeight = ReadingHeight(header, inner);
            float hintHeight = ReadingHeight(hint, inner);
            float height = isReadingCompact ? footerHeight + 20f : headerHeight + hintHeight + footerHeight + 40f;
            var rect = (RectTransform)body.transform;
            rect.sizeDelta = new Vector2(width, height);
            float footerY = -height * 0.5f + 10f + footerHeight * 0.5f;
            SetReadingRect(padHint.rectTransform, new Vector2(-width * 0.5f + 16f + padWidth * 0.5f, footerY), new Vector2(padWidth, footerHeight));
            SetReadingRect((RectTransform)skipButton.transform, new Vector2(width * 0.5f - 16f - buttonWidth * 0.5f, footerY), new Vector2(buttonWidth, buttonHeight));
            readingSkip.rectTransform.offsetMin = new Vector2(8f, 4f);
            readingSkip.rectTransform.offsetMax = new Vector2(-8f, -4f);
            SetReadingRect(header.rectTransform, new Vector2(0f, height * 0.5f - 10f - headerHeight * 0.5f), new Vector2(inner, headerHeight));
            SetReadingRect(hint.rectTransform, new Vector2(0f, height * 0.5f - 20f - headerHeight - hintHeight * 0.5f), new Vector2(inner, hintHeight));
            if (isReadingCompact)
            {
                rect.anchoredPosition = new Vector2(-24f, 24f);
                SetFrameArtVisible(false);
            }
            else
            {
                ApplyFrameArt();
                // 좁은 화면에서 장식이 화면 밖으로 넘치면 본문 여백을 우선한다.
                bool fits = frameArt == null ||
                    Mathf.Abs(frameArt.rectTransform.anchoredPosition.x) + frameArt.rectTransform.rect.width * 0.5f <= bounds.x * 0.5f - 12f;
                SetFrameArtVisible(fits);
                if (!fits) rect.anchoredPosition = new Vector2(0f, -FRAME_SAFE_TOP);
            }

            LayoutSpatialReading(bounds, scale);
            lastReadingScale = scale;
            lastReadingBounds = bounds;
            lastReadingHeader = header.text;
            lastReadingHint = hint.text;
            lastReadingPad = padHint.text;
            lastReadingSkip = readingSkip.text;
            lastReadingKey = spatialKey != null ? spatialKey.text : null;
            lastReadingInstruction = spatialHint != null ? spatialHint.text : null;
        }

        private void LayoutSpatialReading(Vector2 bounds, float scale)
        {
            if (spatialBody == null) return;
            float width = Mathf.Min(460f * scale, Mathf.Max(80f, bounds.x - 48f));
            ConfigureReadingText(spatialKey, 18, scale);
            ConfigureReadingText(spatialHint, 20, scale);
            float keyWidth = Mathf.Max(1f, width - 124f);
            float keyHeight = Mathf.Max(34f * scale, ReadingHeight(spatialKey, keyWidth - 8f) + 8f);
            float hintHeight = ReadingHeight(spatialHint, width - 24f);
            float rowHeight = Mathf.Max(48f, keyHeight + 20f);
            float height = rowHeight + hintHeight + 32f;
            ((RectTransform)spatialBody.transform).sizeDelta = new Vector2(width, height);
            float rowY = height * 0.5f - 12f - rowHeight * 0.5f;
            SetReadingRect((RectTransform)spatialKey.transform.parent, new Vector2(34f, rowY), new Vector2(keyWidth, keyHeight));
            SetReadingRect(spatialKey.rectTransform, Vector2.zero, new Vector2(keyWidth - 8f, keyHeight));
            SetReadingRect(spatialHint.rectTransform, new Vector2(0f, -height * 0.5f + 10f + hintHeight * 0.5f), new Vector2(width - 24f, hintHeight));
            Vector2 iconPosition = new(-width * 0.5f + 36f, rowY);
            foreach (var shape in actionShapes) ((RectTransform)shape.transform).anchoredPosition = iconPosition;
            if (completeMark != null) completeMark.rectTransform.anchoredPosition = iconPosition;
        }

        private static void ConfigureReadingText(Text text, int baseSize, float scale)
        {
            text.fontSize = Mathf.RoundToInt(baseSize * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static float ReadingHeight(Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            return Mathf.Max(text.fontSize + 4f, height + 4f);
        }

        private static void SetReadingRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
