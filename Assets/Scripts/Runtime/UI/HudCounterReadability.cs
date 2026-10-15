using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>HP·골드 글자와 기존 프레임만 확대한다. 값·Fill·강조색·입력은 Presenter 소유다.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class HudCounterReadability : MonoBehaviour
    {
        private sealed class Counter
        {
            public RectTransform Root;
            public Text Label;
            public int FontSize;
            public Vector2 Size;
            public Vector2 Position;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public RectTransform Icon;
            public Vector2 IconSize;
            public string Content;
            public float PreferredWidth;
            public float PreferredHeight;
            public bool Health;
        }

        private sealed class BandEntry
        {
            public RectTransform Rect;
            public Vector2 Position;
        }

        private Counter health;
        private Counter gold;
        private readonly List<BandEntry> lowerBand = new();
        private Canvas canvas;
        private float lastScale = -1f;
        private float lastCanvasScale = -1f;
        private float lastCanvasWidth = -1f;
        private int lastBossGeometry;
        private static readonly Vector3[] counterCorners = new Vector3[4];
        private const float MAX_COUNTER_WIDTH = 600f;
        private const float BOSS_BAR_WIDTH = 900f;
        private const float BOSS_RESERVED_BOTTOM = 104f;
        private const float BOSS_HORIZONTAL_GAP = 20f;
        private const float TEXT_PADDING = 2f;

        public static void Attach(Transform hudRoot)
        {
            if (hudRoot == null) return;
            var existing = hudRoot.GetComponent<HudCounterReadability>();
            if (existing != null) { existing.Apply(); return; }
            var component = hudRoot.gameObject.AddComponent<HudCounterReadability>();
            component.canvas = hudRoot.GetComponentInParent<Canvas>();
            component.health = Capture(hudRoot, "HealthBar", "HPText", true);
            component.gold = Capture(hudRoot, "GoldCounter", "Label", false);
            foreach (string name in new[] { "FormSlot", "FormBiasWarning", "SynergyCounter" })
            {
                if (hudRoot.Find(name) is RectTransform rect)
                    component.lowerBand.Add(new BandEntry { Rect = rect, Position = rect.anchoredPosition });
            }
            component.Apply();
        }

        private static Counter Capture(Transform root, string path, string textPath, bool isHealth)
        {
            if (root.Find(path) is not RectTransform rect) return null;
            var labelTransform = rect.Find(textPath);
            if (labelTransform == null || !labelTransform.TryGetComponent(out Text label)) return null;
            var icon = rect.Find("ArtCurrencyIcon") as RectTransform;
            return new Counter
            {
                Root = rect, Label = label, FontSize = label.fontSize,
                Size = rect.rect.size, Position = rect.anchoredPosition,
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot,
                Icon = icon, IconSize = icon != null ? icon.sizeDelta : Vector2.zero,
                Health = isHealth
            };
        }

        private void Apply()
        {
            float scale = MenuTextReadability.Scale;
            float canvasWidth = CanvasWidth();
            ResizeCounter(health, scale, canvasWidth, canvas);
            ResizeCounter(gold, scale, canvasWidth, canvas);
            lastBossGeometry = BossPresenter.HudGeometrySignature(canvas);
            float bandShift = 0f;
            if (health != null && health.Root != null)
            {
                float verticalMove = health.Position.y - health.Root.anchoredPosition.y;
                float addedHeight = health.Root.rect.height - health.Size.y;
                bandShift = Mathf.Max(0f, verticalMove + addedHeight);
            }
            foreach (var entry in lowerBand)
                if (entry.Rect != null) entry.Rect.anchoredPosition = entry.Position + Vector2.down * bandShift;
            lastScale = scale;
            lastCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            lastCanvasWidth = canvasWidth;
            GetComponent<HudStatusReadability>()?.RefreshLayout();
        }

        private static void ResizeCounter(Counter counter, float scale, float canvasWidth, Canvas targetCanvas)
        {
            if (counter == null || counter.Root == null || counter.Label == null) return;
            var root = counter.Root;
            var label = counter.Label;
            root.anchorMin = counter.AnchorMin;
            root.anchorMax = counter.AnchorMax;
            root.pivot = counter.Pivot;
            root.anchoredPosition = counter.Position;
            label.fontSize = Mathf.RoundToInt(counter.FontSize * scale);
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // Uniform growth keeps the authored frame aspect. Refit its Inner/Slot
            // after each pass, then measure the label's actual opening again.
            float growth = 1f;
            for (int pass = 0; pass < 4; pass++)
            {
                root.sizeDelta = counter.Size * growth;
                RefreshArt(counter);
                float neededWidth = label.preferredWidth + TEXT_PADDING * 2f;
                float neededHeight = label.preferredHeight + TEXT_PADDING;
                float widthRatio = neededWidth / Mathf.Max(1f, label.rectTransform.rect.width);
                float heightRatio = neededHeight / Mathf.Max(1f, label.rectTransform.rect.height);
                float ratio = Mathf.Max(1f, Mathf.Max(widthRatio, heightRatio));
                if (ratio <= 1.001f) break;
                float next = Mathf.Min(MAX_COUNTER_WIDTH / Mathf.Max(1f, counter.Size.x), growth * ratio);
                if (Mathf.Abs(next - growth) <= 0.0001f) break;
                growth = next;
            }
            root.sizeDelta = counter.Size * growth;
            RefreshArt(counter);

            // At normal values keep the original corner. Wide values move below
            // the shared 900-wide boss health band rather than covering it.
            float originalTop = -counter.Position.y;
            float originalSide = Mathf.Abs(counter.Position.x);
            float sideLimit = Mathf.Max(1f, (canvasWidth - BOSS_BAR_WIDTH) * 0.5f - originalSide - BOSS_HORIZONTAL_GAP);
            float top = root.rect.width > sideLimit ? Mathf.Max(originalTop, BOSS_RESERVED_BOTTOM) : originalTop;
            root.anchoredPosition = new Vector2(counter.Position.x, -top);
            AvoidVisibleBossHud(root, targetCanvas);
            counter.Content = label.text;
            counter.PreferredWidth = label.preferredWidth;
            counter.PreferredHeight = label.preferredHeight;
        }

        private static void AvoidVisibleBossHud(RectTransform root, Canvas canvas)
        {
            if (canvas == null) return;
            var target = canvas.rootCanvas.transform as RectTransform;
            if (target == null) return;
            root.GetWorldCorners(counterCorners);
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in counterCorners)
            {
                Vector2 local = target.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            float shift = BossPresenter.RequiredHudCounterShift(canvas, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            if (shift > 0f) root.position += target.TransformVector(Vector3.down * shift);
        }

        private static void RefreshArt(Counter counter)
        {
            HudArtSkin.RefreshCounterArt(counter.Root, counter.Health);
            if (counter.Icon != null)
            {
                // Refit recalculates the slot centre, but the currency glyph itself
                // keeps its original dimensions and aspect instead of stretching.
                counter.Icon.sizeDelta = counter.IconSize;
            }
        }

        private float CanvasWidth()
        {
            if (canvas != null && canvas.rootCanvas.transform is RectTransform rect) return rect.rect.width;
            return 1920f;
        }

        private static bool HasChanged(Counter counter) =>
            counter != null && counter.Label != null &&
            (counter.Content != counter.Label.text ||
             Mathf.Abs(counter.PreferredWidth - counter.Label.preferredWidth) > 0.01f ||
             Mathf.Abs(counter.PreferredHeight - counter.Label.preferredHeight) > 0.01f);

        private void LateUpdate()
        {
            if (lastScale != MenuTextReadability.Scale || lastCanvasScale != (canvas != null ? canvas.scaleFactor : 1f) ||
                lastCanvasWidth != CanvasWidth() || lastBossGeometry != BossPresenter.HudGeometrySignature(canvas) || HasChanged(health) || HasChanged(gold)) Apply();
        }
    }
}
