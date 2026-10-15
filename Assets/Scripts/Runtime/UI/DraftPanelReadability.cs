using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>드래프트의 고정 제목과 하단 버튼을 메뉴 글자 배율에 맞춘다.</summary>
    public sealed class DraftPanelReadability : MonoBehaviour
    {
        private Text title;
        private Text reroll;
        private Text skip;
        private RectTransform rerollRect;
        private RectTransform skipRect;
        private int titleFont;
        private int rerollFont;
        private int skipFont;
        private Vector2 titleSize;
        private Vector2 titlePosition;
        private Vector2 buttonSize;
        private Vector2 rerollPosition;
        private Vector2 skipPosition;
        private RectTransform rerollIcon;
        private Vector2 iconPosition;
        private readonly System.Collections.Generic.List<Image> titleRules = new();
        private readonly System.Collections.Generic.List<bool> ruleVisibility = new();
        private float lastScale = -1f;
        private string lastTitle;
        private string lastReroll;
        private string lastSkip;
        private Canvas canvas;
        private float lastCanvasScale;
        private RectTransform[] cardRects;
        private Vector2[] cardSizes;
        private Vector2[] cardPositions;
        private RectTransform detailSlot;
        private Vector2 detailSize;
        private Vector2 detailPosition;

        public static void Attach(Transform root, Text title, Button rerollButton, Text reroll, Button skipButton, Text skip)
        {
            if (root == null || title == null || rerollButton == null || skipButton == null) return;
            if (root.GetComponent<DraftPanelReadability>() != null) return;
            var component = root.gameObject.AddComponent<DraftPanelReadability>();
            component.title = title;
            component.reroll = reroll;
            component.skip = skip;
            component.canvas = root.GetComponentInParent<Canvas>();
            component.titleFont = title.fontSize;
            component.rerollFont = reroll != null ? reroll.fontSize : 26;
            component.skipFont = skip != null ? skip.fontSize : 26;
            component.titleSize = title.rectTransform.sizeDelta;
            component.titlePosition = title.rectTransform.anchoredPosition;
            component.rerollRect = (RectTransform)rerollButton.transform;
            component.skipRect = (RectTransform)skipButton.transform;
            component.buttonSize = component.rerollRect.sizeDelta;
            component.rerollPosition = component.rerollRect.anchoredPosition;
            component.skipPosition = component.skipRect.anchoredPosition;
            var cards = root.GetComponentsInChildren<SkillCardView>(true);
            component.cardRects = new RectTransform[cards.Length];
            component.cardSizes = new Vector2[cards.Length];
            component.cardPositions = new Vector2[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                var rect = (RectTransform)cards[i].transform;
                component.cardRects[i] = rect;
                component.cardSizes[i] = rect.sizeDelta;
                component.cardPositions[i] = rect.anchoredPosition;
            }
            component.detailSlot = title.transform.parent.Find("ArtDetailSlot") as RectTransform;
            if (component.detailSlot != null)
            {
                component.detailSize = component.detailSlot.sizeDelta;
                component.detailPosition = component.detailSlot.anchoredPosition;
            }
            component.rerollIcon = component.rerollRect.Find("ArtRerollIcon") as RectTransform;
            if (component.rerollIcon != null) component.iconPosition = component.rerollIcon.anchoredPosition;
            foreach (string name in new[] { "ArtTitleRuleLeft", "ArtTitleRuleRight", "ArtTitleDiamondLeft", "ArtTitleDiamondRight" })
            {
                var child = title.transform.parent.Find(name);
                if (child == null || !child.TryGetComponent(out Image rule)) continue;
                component.titleRules.Add(rule);
                component.ruleVisibility.Add(rule.enabled);
            }
            component.Apply();
        }

        private void LateUpdate()
        {
            if (title != null && (lastScale != MenuTextReadability.Scale ||
                lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f) || lastTitle != title.text ||
                lastReroll != reroll?.text || lastSkip != skip?.text)) Apply();
        }

        private void Apply()
        {
            float scale = MenuTextReadability.Scale;
            lastScale = scale;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            SetFont(title, titleFont, scale);
            SetFont(reroll, rerollFont, scale);
            SetFont(skip, skipFont, scale);
            title.rectTransform.sizeDelta = new Vector2(titleSize.x + 400f * (scale - 1f), titleSize.y);
            float titleHeight = Mathf.Max(titleSize.y, title.preferredHeight);
            title.rectTransform.sizeDelta = new Vector2(title.rectTransform.sizeDelta.x, titleHeight);
            title.rectTransform.anchoredPosition = titlePosition;
            float cardInset = Mathf.Max(0f, titleHeight - titleSize.y);
            for (int i = 0; i < cardRects.Length; i++)
            {
                cardRects[i].sizeDelta = cardSizes[i] - Vector2.up * cardInset;
                cardRects[i].anchoredPosition = cardPositions[i] + Vector2.down * cardInset * 0.5f;
            }
            for (int i = 0; i < titleRules.Count; i++) titleRules[i].enabled = scale == 1f && ruleVisibility[i];

            float addedWidth = 560f * (scale - 1f);
            rerollRect.sizeDelta = skipRect.sizeDelta = buttonSize + Vector2.right * addedWidth;
            float height = Mathf.Max(buttonSize.y, Mathf.Max(reroll != null ? reroll.preferredHeight : 0f,
                skip != null ? skip.preferredHeight : 0f) + 12f);
            rerollRect.sizeDelta = skipRect.sizeDelta = new Vector2(buttonSize.x + addedWidth, height);
            float footerInset = Mathf.Max(0f, height - buttonSize.y);
            rerollRect.anchoredPosition = rerollPosition + new Vector2(-addedWidth * 0.5f, footerInset * 0.5f);
            skipRect.anchoredPosition = skipPosition + new Vector2(addedWidth * 0.5f, footerInset * 0.5f);
            if (detailSlot != null)
            {
                detailSlot.sizeDelta = detailSize - Vector2.up * footerInset;
                detailSlot.anchoredPosition = detailPosition;
            }
            if (rerollIcon != null) rerollIcon.anchoredPosition = iconPosition + Vector2.left * addedWidth * 0.5f;
            lastTitle = title.text;
            lastReroll = reroll?.text;
            lastSkip = skip?.text;
        }

        private static void SetFont(Text text, int font, float scale)
        {
            if (text == null) return;
            text.fontSize = Mathf.RoundToInt(font * scale);
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}
