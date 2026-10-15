using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>보스 이름과 부제만 확대하고 게이지·초상은 기존 기하를 함께 이동한다.</summary>
    public sealed partial class BossPresenter
    {
        private RectTransform readableBar;
        private RectTransform readableParent;
        private RectTransform readableSubtitle;
        private Vector2 originalBarPosition;
        private Vector2 originalNamePosition;
        private Vector2 originalNameSize;
        private Vector2 originalSubtitlePosition;
        private string originalNameContent;
        private string renderedNameContent;
        private float nameScale = -1f;
        private float nameCanvasScale = -1f;
        private Vector2 nameParentSize;
        private readonly List<Rect> visibleHudRects = new();
        private readonly Vector3[] nameCorners = new Vector3[4];

        /// <summary>대상 HUD 캔버스 좌표에서 실제 표시되는 보스 영역의 합집합.</summary>
        public static Rect ReservedHudBounds(Canvas targetCanvas)
        {
            if (!CollectVisibleHudRects(targetCanvas)) return Rect.zero;
            Rect bounds = instance.visibleHudRects[0];
            foreach (var rect in instance.visibleHudRects)
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                    Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
            return bounds;
        }

        public static float ReservedHudBottom(Canvas targetCanvas)
        {
            Rect bounds = ReservedHudBounds(targetCanvas);
            if (bounds.width <= 0f || targetCanvas == null) return 0f;
            var target = targetCanvas.rootCanvas.transform as RectTransform;
            return Mathf.Max(0f, target.rect.yMax - bounds.yMin);
        }

        public static int HudGeometrySignature(Canvas targetCanvas)
        {
            if (!CollectVisibleHudRects(targetCanvas)) return 0;
            int hash = instance.visibleHudRects.Count;
            foreach (var rect in instance.visibleHudRects) hash = unchecked(hash * 31 + rect.GetHashCode());
            return hash;
        }

        /// <summary>각 표시 영역과 실제로 겹칠 때만 아래로 이동할 거리를 반환한다.</summary>
        public static float RequiredHudCounterShift(Canvas targetCanvas, Rect candidate)
        {
            if (!CollectVisibleHudRects(targetCanvas)) return 0f;
            float shift = 0f;
            // 아래로만 이동하므로 각 장애물의 하단을 한 번 넘으면 다시 만날 수 없다.
            for (int pass = 0; pass <= instance.visibleHudRects.Count; pass++)
            {
                bool moved = false;
                foreach (var rect in instance.visibleHudRects)
                {
                    if (!candidate.Overlaps(rect)) continue;
                    float delta = candidate.yMax - rect.yMin + 8f;
                    candidate.y -= delta;
                    shift += delta;
                    moved = true;
                }
                if (!moved) break;
            }
            return shift;
        }

        private static bool CollectVisibleHudRects(Canvas targetCanvas)
        {
            if (targetCanvas == null || instance == null || targetCanvas.rootCanvas.transform is not RectTransform) return false;
            instance.visibleHudRects.Clear();
            if (instance.barRoot != null && instance.barRoot.activeInHierarchy)
            {
                instance.AddVisibleHudRect(instance.nameLabel.rectTransform, targetCanvas);
                var back = instance.barRoot.transform.Find("Back") as RectTransform;
                instance.AddVisibleHudRect(back, targetCanvas);
                instance.AddVisibleHudRect(back != null ? back.Find(BAR_FRAME_NAME) as RectTransform : null, targetCanvas);
                instance.AddVisibleHudRect(instance.barRoot.transform.Find(BAR_PORTRAIT_NAME) as RectTransform, targetCanvas);
            }
            if (instance.subtitleGroup != null && instance.subtitleGroup.gameObject.activeInHierarchy &&
                instance.subtitleGroup.alpha > 0f && instance.subtitleLabel != null && !string.IsNullOrEmpty(instance.subtitleLabel.text))
            {
                instance.AddVisibleHudRect(instance.subtitleLabel.rectTransform, targetCanvas);
                instance.AddVisibleHudRect(instance.subtitleGroup.transform.Find("NarrativeContrast") as RectTransform, targetCanvas);
            }
            return instance.visibleHudRects.Count > 0;
        }

        private void AddVisibleHudRect(RectTransform rect, Canvas targetCanvas)
        {
            Rect bounds = Rect.zero;
            bool hasBounds = false;
            IncludeReservedBounds(rect, targetCanvas, ref bounds, ref hasBounds);
            if (hasBounds && bounds.width > 0f && bounds.height > 0f) visibleHudRects.Add(bounds);
        }

        private void IncludeReservedBounds(RectTransform rect, Canvas targetCanvas, ref Rect bounds, ref bool hasBounds)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return;
            if (rect.TryGetComponent(out Graphic graphic) && !graphic.enabled) return;
            var target = targetCanvas.rootCanvas.transform as RectTransform;
            var sourceCanvas = rect.GetComponentInParent<Canvas>();
            var sourceCamera = sourceCanvas != null && sourceCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.rootCanvas.worldCamera : null;
            var targetCamera = targetCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.rootCanvas.worldCamera : null;
            rect.GetWorldCorners(nameCorners);
            foreach (var corner in nameCorners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screen, targetCamera, out var local)) continue;
                bounds = hasBounds
                    ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, local.x), Mathf.Min(bounds.yMin, local.y),
                        Mathf.Max(bounds.xMax, local.x), Mathf.Max(bounds.yMax, local.y))
                    : Rect.MinMaxRect(local.x, local.y, local.x, local.y);
                hasBounds = true;
            }
        }

        private void CaptureNameReadability()
        {
            if (readableBar != null || barRoot == null || nameLabel == null) return;
            readableBar = barRoot.transform as RectTransform;
            readableParent = readableBar.parent as RectTransform;
            readableSubtitle = subtitleGroup != null ? subtitleGroup.transform as RectTransform : null;
            originalBarPosition = readableBar.anchoredPosition;
            originalNamePosition = nameLabel.rectTransform.anchoredPosition;
            originalNameSize = nameLabel.rectTransform.sizeDelta;
            if (readableSubtitle != null) originalSubtitlePosition = readableSubtitle.anchoredPosition;
            originalNameContent = nameLabel.text;
            RefreshNameReadability();
        }

        private void RefreshNameReadability()
        {
            if (readableBar == null || nameLabel == null) return;
            if (nameLabel.text != renderedNameContent) originalNameContent = nameLabel.text;
            float scale = MenuTextReadability.Scale;
            // 원본 문자열에서만 부제 크기를 바꿔 반복 확대에 따른 누적을 피한다.
            renderedNameContent = (originalNameContent ?? string.Empty).Replace("<size=18>", $"<size={Mathf.RoundToInt(18f * scale)}>");
            nameLabel.text = renderedNameContent;
            nameLabel.fontSize = Mathf.RoundToInt(24f * scale);
            nameLabel.resizeTextForBestFit = false;
            nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = nameLabel.rectTransform;
            float width = readableParent != null ? Mathf.Min(originalNameSize.x, Mathf.Max(1f, readableParent.rect.width - 8f)) : originalNameSize.x;
            rect.sizeDelta = new Vector2(width, originalNameSize.y);
            float height = Mathf.Max(originalNameSize.y, nameLabel.preferredHeight + 4f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = originalNamePosition + Vector2.up * (height - originalNameSize.y) * 0.5f;
            readableBar.anchoredPosition = originalBarPosition;
            float shift = 0f;
            if (readableParent != null)
            {
                rect.GetWorldCorners(nameCorners);
                float top = readableParent.InverseTransformPoint(nameCorners[1]).y;
                shift = Mathf.Max(0f, top - readableParent.rect.yMax + 4f);
            }
            readableBar.anchoredPosition = originalBarPosition + Vector2.down * shift;
            if (readableSubtitle != null) readableSubtitle.anchoredPosition = originalSubtitlePosition + Vector2.down * shift;
            nameScale = scale;
            var canvas = GetComponent<Canvas>();
            nameCanvasScale = canvas != null ? canvas.scaleFactor : 1f;
            nameParentSize = readableParent != null ? readableParent.rect.size : Vector2.zero;
        }

        private void LateUpdate()
        {
            if (readableBar == null || nameLabel == null) return;
            var canvas = GetComponent<Canvas>();
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 parentSize = readableParent != null ? readableParent.rect.size : Vector2.zero;
            if (nameScale != MenuTextReadability.Scale || nameCanvasScale != canvasScale || nameParentSize != parentSize ||
                nameLabel.text != renderedNameContent) RefreshNameReadability();
        }
    }
}
