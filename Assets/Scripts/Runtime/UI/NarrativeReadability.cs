using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public enum NarrativeTextAnchor { Top, Center, Bottom }

    /// <summary>서사 본문의 원본 배치를 보존하고 확대 시 줄바꿈 높이에 맞춰 공간을 늘린다.</summary>
    public sealed class NarrativeReadability : MonoBehaviour
    {
        private Text label;
        private RectTransform panel;
        private RectTransform speaker;
        private Image background;
        private int originalFont;
        private Color originalColor;
        private HorizontalWrapMode originalWrap;
        private bool originalBestFit;
        private Vector2 originalSize;
        private Vector2 originalPivot;
        private Vector2 originalPosition;
        private Vector2 panelSize;
        private Vector2 panelPosition;
        private Vector2 speakerPosition;
        private string lastText;
        private float lastScale = -1f;
        private bool lastContrast;
        private NarrativeTextAnchor anchor;
        private RectTransform heading;
        private Vector2 headingPosition;
        private bool followsTextAlpha;

        public static float Scale
        {
            get
            {
                float value = MetaSaveService.GetInstanceSafe()?.Current?.settings?.narrativeTextScale ?? 1f;
                return value == 1.25f || value == 1.5f ? value : 1f;
            }
        }
        public static bool HighContrast => MetaSaveService.GetInstanceSafe()?.Current?.settings?.highContrastNarrative ?? false;

        public static NarrativeReadability Attach(Text text, RectTransform panel = null, RectTransform speaker = null,
            NarrativeTextAnchor anchor = NarrativeTextAnchor.Top, RectTransform heading = null, bool followsTextAlpha = false)
        {
            if (text == null) return null;
            var existing = text.GetComponent<NarrativeReadability>();
            if (existing != null) return existing;
            var component = text.gameObject.AddComponent<NarrativeReadability>();
            component.label = text;
            component.panel = panel;
            component.speaker = speaker;
            component.anchor = anchor;
            component.heading = heading;
            component.followsTextAlpha = followsTextAlpha;
            if (heading != null) component.headingPosition = heading.anchoredPosition;
            component.originalFont = text.fontSize;
            component.originalColor = text.color;
            component.originalWrap = text.horizontalOverflow;
            component.originalBestFit = text.resizeTextForBestFit;
            component.originalSize = text.rectTransform.sizeDelta;
            component.originalPivot = text.rectTransform.pivot;
            component.originalPosition = text.rectTransform.anchoredPosition;
            if (panel != null) { component.panelSize = panel.sizeDelta; component.panelPosition = panel.anchoredPosition; }
            if (speaker != null) component.speakerPosition = speaker.anchoredPosition;
            var go = new GameObject("NarrativeContrast", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(text.transform.parent, false);
            go.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
            component.background = go.GetComponent<Image>();
            component.background.color = new Color(0f, 0f, 0f, 0.96f);
            component.background.raycastTarget = false;
            go.SetActive(false);
            component.Apply();
            return component;
        }

        public void Apply()
        {
            if (label == null) return;
            float scale = Scale;
            bool contrast = HighContrast;
            lastScale = scale;
            lastContrast = contrast;
            lastText = label.text;
            var rect = label.rectTransform;
            label.fontSize = Mathf.RoundToInt(originalFont * scale);
            label.resizeTextForBestFit = scale > 1f ? false : originalBestFit;
            label.horizontalOverflow = scale > 1f ? HorizontalWrapMode.Wrap : originalWrap;
            rect.sizeDelta = originalSize;
            rect.pivot = originalPivot;
            rect.anchoredPosition = originalPosition;
            float height = scale > 1f ? Mathf.Max(originalSize.y, label.preferredHeight + 8f) : originalSize.y;
            float extension = height - originalSize.y;
            rect.sizeDelta = new Vector2(originalSize.x, height);
            if (panel != null)
            {
                // NPC 창의 바닥과 힌트는 고정하고 본문·화자 위쪽에 필요한 공간만 추가한다.
                panel.sizeDelta = panelSize + Vector2.up * extension;
                panel.anchoredPosition = panelPosition + Vector2.up * extension * panel.pivot.y;
                rect.anchoredPosition = originalPosition + Vector2.up * extension * (rect.pivot.y - rect.anchorMin.y);
                if (speaker != null) speaker.anchoredPosition = speakerPosition + Vector2.up * extension * (1f - speaker.anchorMin.y);
            }
            else if (anchor != NarrativeTextAnchor.Center)
            {
                float verticalPivot = anchor == NarrativeTextAnchor.Bottom ? 0f : 1f;
                rect.pivot = new Vector2(originalPivot.x, verticalPivot);
                rect.anchoredPosition = originalPosition + Vector2.up * originalSize.y * (verticalPivot - originalPivot.y);
                if (scale == 1f) { rect.pivot = originalPivot; rect.anchoredPosition = originalPosition; }
            }
            if (heading != null) heading.anchoredPosition = headingPosition + Vector2.up * extension;
            // 페이드 알파는 연출 소유다. 색 대비 변경으로 알파를 1로 복구하지 않는다.
            var color = contrast ? Color.white : originalColor;
            color.a = label.color.a;
            label.color = color;
            var bg = background.rectTransform;
            bg.anchorMin = rect.anchorMin;
            bg.anchorMax = rect.anchorMax;
            bg.pivot = rect.pivot;
            bg.anchoredPosition = rect.anchoredPosition;
            bg.sizeDelta = rect.sizeDelta + new Vector2(16f, panel != null ? 0f : 12f);
            background.gameObject.SetActive(contrast && gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text));
            SyncBackgroundAlpha();
        }

        /// <summary>생성 직후 호출자가 정한 별도 배치를 기본값으로 확정한다. 현재 글자 배율은 다시 캡처하지 않는다.</summary>
        public void CaptureLayout()
        {
            if (label == null) return;
            originalSize = label.rectTransform.sizeDelta;
            originalPivot = label.rectTransform.pivot;
            originalPosition = label.rectTransform.anchoredPosition;
            lastScale = -1f;
        }

        private void SyncBackgroundAlpha()
        {
            if (!followsTextAlpha || background == null || label == null) return;
            var color = background.color;
            color.a = 0.96f * label.color.a;
            background.color = color;
        }

        private void OnEnable() { lastScale = -1f; }
        private void LateUpdate()
        {
            if (label != null && (lastScale != Scale || lastContrast != HighContrast || lastText != label.text)) Apply();
            SyncBackgroundAlpha();
        }
        private void OnDisable() { if (background != null) background.gameObject.SetActive(false); }
        private void OnDestroy() { if (background != null) Destroy(background.gameObject); }
    }
}
