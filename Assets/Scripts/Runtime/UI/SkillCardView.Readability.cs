using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class SkillCardView
    {
        private Text[] readingTexts;
        private int[] readingFonts;
        private Vector2[] readingSizes;
        private Vector2[] readingPositions;
        private float readingScale = -1f;
        private float readingCanvasScale = -1f;
        private float readingCardHeight = -1f;
        private string readingName;
        private string readingHeadline;
        private string readingDescription;

        /// <summary>스킨 적용 뒤 원본을 한 번만 보존한다. 카드는 미리보기, 전체 문구는 상세에서 읽는다.</summary>
        public void RefreshReadability()
        {
            if (readingTexts == null)
            {
                readingTexts = new[] { nameText, rarityCategoryText, descriptionText, formulaText };
                readingFonts = new int[readingTexts.Length];
                readingSizes = new Vector2[readingTexts.Length];
                readingPositions = new Vector2[readingTexts.Length];
                for (int i = 0; i < readingTexts.Length; i++)
                {
                    if (readingTexts[i] == null) continue;
                    readingFonts[i] = readingTexts[i].fontSize;
                    readingSizes[i] = readingTexts[i].rectTransform.sizeDelta;
                    readingPositions[i] = readingTexts[i].rectTransform.anchoredPosition;
                }
            }
            readingScale = MenuTextReadability.Scale;
            var canvas = GetComponentInParent<Canvas>();
            readingCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            readingCardHeight = transform is RectTransform readingRect ? readingRect.rect.height : 0f;
            readingName = nameText != null ? nameText.text : null;
            readingHeadline = rarityCategoryText != null ? rarityCategoryText.text : null;
            readingDescription = descriptionText != null ? descriptionText.text : null;
            for (int i = 0; i < readingTexts.Length; i++)
            {
                var text = readingTexts[i];
                if (text == null) continue;
                text.fontSize = Mathf.RoundToInt(readingFonts[i] * readingScale);
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.rectTransform.sizeDelta = readingSizes[i];
                text.rectTransform.anchoredPosition = readingPositions[i];
            }
            // Adopted cards use top-anchored text blocks. Preserve their full
            // text-band extent and icon area; redistribute only the three labels.
            if (nameText == null || rarityCategoryText == null || descriptionText == null ||
                nameText.rectTransform.anchorMin != new Vector2(0.5f, 1f) ||
                nameText.rectTransform.pivot.y != 1f) return;
            float top = -readingPositions[0].y;
            float bottom = -readingPositions[2].y + readingSizes[2].y;
            var cardRect = transform as RectTransform;
            if (cardRect != null) bottom = Mathf.Min(bottom, Mathf.Max(1f, cardRect.rect.height - 24f));
            top = Mathf.Min(top, Mathf.Max(0f, bottom - 1f));
            float gap1 = Mathf.Max(0f, -readingPositions[1].y - top - readingSizes[0].y);
            float gap2 = Mathf.Max(0f, -readingPositions[2].y + readingPositions[1].y - readingSizes[1].y);
            float gapScale = Mathf.Min(1f, Mathf.Max(0f, bottom - top - 1f) / Mathf.Max(1f, gap1 + gap2));
            gap1 *= gapScale;
            gap2 *= gapScale;
            float available = Mathf.Max(1f, bottom - top - gap1 - gap2);
            float descriptionMin = Mathf.Min(readingSizes[2].y, descriptionText.fontSize * 1.3f);
            float headlineHeight = Mathf.Max(readingSizes[1].y, rarityCategoryText.preferredHeight);
            float nameHeight = Mathf.Max(readingSizes[0].y, nameText.preferredHeight);
            float needed = nameHeight + headlineHeight + descriptionMin;
            if (needed > available)
            {
                float ratio = available / needed;
                nameHeight *= ratio;
                headlineHeight *= ratio;
            }
            float descriptionHeight = Mathf.Max(0.01f, available - nameHeight - headlineHeight);
            PlaceReadingText(nameText, readingPositions[0].x, top, readingSizes[0].x, nameHeight);
            PlaceReadingText(rarityCategoryText, readingPositions[1].x, top + nameHeight + gap1, readingSizes[1].x, headlineHeight);
            PlaceReadingText(descriptionText, readingPositions[2].x, bottom - descriptionHeight, readingSizes[2].x, descriptionHeight);
        }

        private static void PlaceReadingText(Text text, float x, float top, float width, float height)
        {
            text.rectTransform.anchoredPosition = new Vector2(x, -top);
            text.rectTransform.sizeDelta = new Vector2(width, height);
        }

        private void LateUpdate()
        {
            var canvas = GetComponentInParent<Canvas>();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            float cardHeight = transform is RectTransform cardRect ? cardRect.rect.height : 0f;
            if (readingTexts != null && (readingScale != MenuTextReadability.Scale ||
                readingName != (nameText != null ? nameText.text : null) ||
                readingHeadline != (rarityCategoryText != null ? rarityCategoryText.text : null) ||
                readingDescription != (descriptionText != null ? descriptionText.text : null) ||
                readingCanvasScale != canvasScale || readingCardHeight != cardHeight))
                RefreshReadability();
        }
    }
}
