using System.Collections.Generic;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>명시적으로 연결된 설정/메뉴 글자만 확대한다. 원본 좌표와 버튼/탐색은 보존한다.</summary>
    public sealed class MenuTextReadability : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public int FontSize;
            public bool BestFit;
            public HorizontalWrapMode Wrap;
            public Vector2 Size;
            public Vector2 Position;
            public string Content;
            public float Height;
            public float Width;
        }
        private readonly List<Entry> entries = new();
        private sealed class ButtonEntry
        {
            public RectTransform Rect;
            public Vector2 Size;
            public Vector2 Position;
        }
        private readonly List<ButtonEntry> buttons = new();
        private bool isSettings;
        private bool isMenuPanel;
        private RectTransform panel;
        private Vector2 panelSize;
        private float lastScale = -1f;
        private Canvas canvas;
        private float lastCanvasScale = -1f;

        public static float NormalizeScale(float value) => value == 1.25f || value == 1.5f ? value : 1f;
        public static float Scale => NormalizeScale(MetaSaveService.GetInstanceSafe()?.Current?.settings?.menuTextScale ?? 1f);

        public static MenuTextReadability Attach(Transform root, bool settings = false, bool menuPanel = false)
        {
            if (root == null) return null;
            var existing = root.GetComponent<MenuTextReadability>();
            if (existing != null) return existing;
            var component = root.gameObject.AddComponent<MenuTextReadability>();
            component.isSettings = settings;
            component.isMenuPanel = menuPanel;
            component.canvas = root.GetComponentInParent<Canvas>();
            component.panel = root as RectTransform;
            if (component.panel != null) component.panelSize = component.panel.sizeDelta;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                // 작은 좌우 화살표는 고정 크기 컨트롤이다. 의미 글자/값만 확대한다.
                if (settings && (text.transform.parent.name.EndsWith("Prev") || text.transform.parent.name.EndsWith("Next"))) continue;
                component.entries.Add(new Entry {Text=text,FontSize=text.fontSize,BestFit=text.resizeTextForBestFit,Wrap=text.horizontalOverflow,
                    Size=text.rectTransform.sizeDelta,Position=text.rectTransform.anchoredPosition});
            }
            if (!settings)
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var rect = (RectTransform)button.transform;
                    component.buttons.Add(new ButtonEntry {Rect=rect,Size=rect.sizeDelta,Position=rect.anchoredPosition});
                }
            component.Apply();
            return component;
        }

        public void Apply()
        {
            float scale = Scale;
            lastScale = scale;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            float extraWidth = isSettings ? 200f * (scale - 1f) : 0f;
            if (isSettings && panel != null) panel.sizeDelta = panelSize + Vector2.right * extraWidth * 2f;
            foreach (var entry in entries)
            {
                var text = entry.Text;
                if (text == null) continue;
                entry.Content = text.text;
                text.fontSize = Mathf.RoundToInt(entry.FontSize * scale);
                text.resizeTextForBestFit = scale > 1f ? false : entry.BestFit;
                text.horizontalOverflow = entry.Wrap;
                var rect = text.rectTransform;
                rect.sizeDelta = entry.Size;
                rect.anchoredPosition = entry.Position;
                bool fixedRect = rect.anchorMin == rect.anchorMax;
                if (isSettings && fixedRect && (text.name == "KeyGuide" || text.name == "TitleText"))
                    rect.sizeDelta = entry.Size+Vector2.right*extraWidth*2f;
                if (isSettings && fixedRect && text.name.EndsWith("Label") && entry.Position.x < 0f)
                {
                    rect.sizeDelta = entry.Size + Vector2.right * extraWidth;
                    rect.anchoredPosition = entry.Position - Vector2.right * extraWidth * 0.5f;
                }
                if (!isSettings && text.preferredWidth > rect.rect.width) text.horizontalOverflow = HorizontalWrapMode.Wrap;
                if (fixedRect && (scale > 1f || text.preferredHeight > entry.Size.y))
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x,Mathf.Max(entry.Size.y,text.preferredHeight));
                entry.Height = text.preferredHeight;
                entry.Width = text.preferredWidth;
            }
            float shift = 0f;
            foreach (var button in buttons)
            {
                float height = button.Size.y;
                foreach (var text in button.Rect.GetComponentsInChildren<Text>(true)) height = Mathf.Max(height,text.preferredHeight+8f);
                float extension = height-button.Size.y;
                button.Rect.sizeDelta = new Vector2(button.Size.x,height);
                button.Rect.anchoredPosition = button.Position-Vector2.up*(shift+extension*0.5f);
                shift += extension;
            }
            if (isMenuPanel && panel != null)
            {
                panel.sizeDelta = panelSize+Vector2.up*shift*2f;
                foreach (var entry in entries)
                    if (entry.Text != null && entry.Text.name == "HintText") entry.Text.rectTransform.anchoredPosition -= Vector2.up*shift;
            }
        }

        private void OnEnable() => lastScale = -1f;
        private void LateUpdate()
        {
            bool changed = lastScale != Scale || (canvas != null && lastCanvasScale != canvas.scaleFactor);
            foreach (var entry in entries)
                if (entry.Text != null && (entry.Content != entry.Text.text ||
                    Mathf.Abs(entry.Height-entry.Text.preferredHeight)>0.01f ||
                    Mathf.Abs(entry.Width-entry.Text.preferredWidth)>0.01f)) { changed = true; break; }
            if (changed) Apply();
        }
    }
}
