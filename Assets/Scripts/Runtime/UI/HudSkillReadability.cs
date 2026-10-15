using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>스킬 이름은 두 줄 미리보기, 연소 안내는 전체 문구를 유지하며 확대한다.</summary>
    public sealed class HudSkillReadability : MonoBehaviour
    {
        private sealed class Entry
        {
            public RectTransform Root;
            public Text Label;
            public Vector2 Position;
            public Vector2 Size;
            public Vector2 LabelPosition;
            public Vector2 LabelSize;
            public int FontSize;
            public string Content;
            public float PreferredWidth;
            public float PreferredHeight;
        }

        private Entry first;
        private Entry second;
        private Entry burn;
        private Canvas canvas;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;

        public static void Attach(Transform hudRoot)
        {
            if (hudRoot == null) return;
            var existing = hudRoot.GetComponent<HudSkillReadability>();
            if (existing != null) { existing.Apply(); return; }
            var component = hudRoot.gameObject.AddComponent<HudSkillReadability>();
            component.canvas = hudRoot.GetComponentInParent<Canvas>();
            component.first = Capture(hudRoot, "SkillSlot0", "Label");
            component.second = Capture(hudRoot, "SkillSlot1", "Label");
            component.burn = Capture(hudRoot, "BurnStack", "Body/Label");
            component.Apply();
        }

        private static Entry Capture(Transform hudRoot, string name, string path)
        {
            if (hudRoot.Find(name) is not RectTransform root) return null;
            var labelObject = root.Find(path);
            if (labelObject == null || !labelObject.TryGetComponent(out Text label)) return null;
            return new Entry
            {
                Root = root, Label = label, Position = root.anchoredPosition,
                Size = root.rect.size, LabelPosition = label.rectTransform.anchoredPosition,
                LabelSize = label.rectTransform.rect.size, FontSize = label.fontSize
            };
        }

        private void Apply()
        {
            float scale = MenuTextReadability.Scale;
            ApplySlot(first, scale);
            ApplySlot(second, scale);
            if (first != null && second != null && scale > 1f)
            {
                float separation = (first.Label.rectTransform.rect.width + second.Label.rectTransform.rect.width) * 0.5f + 8f;
                float firstCenter = first.Root.anchoredPosition.x + first.Size.x * 0.5f;
                float secondX = Mathf.Max(second.Position.x, firstCenter + separation - second.Size.x * 0.5f);
                second.Root.anchoredPosition = new Vector2(secondX, second.Root.anchoredPosition.y);
            }
            ApplyBurn(scale);
            Cache(first); Cache(second); Cache(burn);
            lastScale = scale;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
        }

        private static void ApplySlot(Entry entry, float scale)
        {
            if (entry == null || entry.Root == null || entry.Label == null) return;
            var label = entry.Label;
            label.fontSize = Mathf.RoundToInt(entry.FontSize * scale);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var rect = label.rectTransform;
            rect.sizeDelta = entry.LabelSize * scale;
            // 이름 띠의 위쪽을 유지하고 아래쪽으로 두 줄 영역을 확장한다.
            float top = entry.LabelPosition.y + entry.LabelSize.y;
            rect.anchoredPosition = new Vector2(entry.LabelPosition.x, top - rect.rect.height);
            float bottom = rect.anchoredPosition.y;
            float rootY = Mathf.Max(entry.Position.y, 4f - bottom);
            entry.Root.anchoredPosition = new Vector2(entry.Position.x, rootY);
        }

        private void ApplyBurn(float scale)
        {
            if (burn == null || burn.Root == null || burn.Label == null) return;
            var label = burn.Label;
            label.fontSize = Mathf.RoundToInt(burn.FontSize * scale);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            burn.Root.sizeDelta = new Vector2(Mathf.Max(burn.Size.x, label.preferredWidth + 4f),
                Mathf.Max(burn.Size.y, label.preferredHeight + 4f));
            float right = Mathf.Max(SlotRight(first), SlotRight(second));
            float gap = scale > 1f ? 16f : 8f;
            float x = Mathf.Max(burn.Position.x, right + gap);
            float raised = Mathf.Max(SlotRaise(first), SlotRaise(second));
            burn.Root.anchoredPosition = new Vector2(x, burn.Position.y + raised);
        }

        private static float SlotRight(Entry entry) => entry != null && entry.Root != null && entry.Label != null
            ? entry.Root.anchoredPosition.x + entry.Size.x * 0.5f + entry.Label.rectTransform.rect.width * 0.5f : 0f;

        private static float SlotRaise(Entry entry) => entry != null && entry.Root != null
            ? entry.Root.anchoredPosition.y - entry.Position.y : 0f;

        private static void Cache(Entry entry)
        {
            if (entry == null || entry.Label == null) return;
            entry.Content = entry.Label.text;
            entry.PreferredWidth = entry.Label.preferredWidth;
            entry.PreferredHeight = entry.Label.preferredHeight;
        }

        private static bool Changed(Entry entry) => entry != null && entry.Label != null &&
            (entry.Content != entry.Label.text || Mathf.Abs(entry.PreferredWidth - entry.Label.preferredWidth) > 0.01f ||
             Mathf.Abs(entry.PreferredHeight - entry.Label.preferredHeight) > 0.01f);

        private void LateUpdate()
        {
            if (lastScale != MenuTextReadability.Scale || lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f) ||
                Changed(first) || Changed(second) || Changed(burn)) Apply();
        }
    }
}
