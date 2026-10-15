using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>실제 폼 영역 아래에 경고와 전체 시너지 문구를 배치한다.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class HudStatusReadability : MonoBehaviour
    {
        private RectTransform form;
        private RectTransform bias;
        private Text biasLabel;
        private RectTransform synergy;
        private RectTransform body;
        private readonly List<RectTransform> activeChips = new();
        private readonly List<float> chipHeights = new();
        private const int BASE_FONT_SIZE = 24;
        private const float MAX_WIDTH = 600f;

        public static void Attach(Transform hudRoot)
        {
            if (hudRoot == null) return;
            var component = hudRoot.GetComponent<HudStatusReadability>();
            if (component == null)
            {
                component = hudRoot.gameObject.AddComponent<HudStatusReadability>();
                component.form = hudRoot.Find("FormSlot") as RectTransform;
                component.bias = hudRoot.Find("FormBiasWarning") as RectTransform;
                var label = hudRoot.Find("FormBiasWarning/Body/Label");
                component.biasLabel = label != null ? label.GetComponent<Text>() : null;
                component.synergy = hudRoot.Find("SynergyCounter") as RectTransform;
                component.body = hudRoot.Find("SynergyCounter/Body") as RectTransform;
            }
            component.Apply();
        }

        public void RefreshLayout() => Apply();

        private void LateUpdate() => Apply();

        private void Apply()
        {
            float scale = MenuTextReadability.Scale;
            float formBottom = form != null ? -form.anchoredPosition.y + form.rect.height : 184f;
            float biasTop = formBottom + 16f;
            float biasHeight = 48f;
            if (bias != null)
            {
                float width = Mathf.Min(MAX_WIDTH, 360f * scale);
                if (biasLabel != null)
                {
                    Style(biasLabel, scale);
                    float padding = biasLabel.rectTransform.offsetMin.x - biasLabel.rectTransform.offsetMax.x;
                    biasHeight = Mathf.Max(biasHeight, MeasureHeight(biasLabel, width - padding) + 4f);
                }
                SetSize(bias, new Vector2(width, biasHeight));
                SetPosition(bias, new Vector2(48f, -biasTop));
            }
            if (synergy == null || body == null) return;
            float synergyWidth = Mathf.Min(MAX_WIDTH, 432f * scale);
            float cellWidth = (synergyWidth - 8f) * 0.5f;
            // 균등 격자는 긴 축 하나의 높이를 모든 행에 반복하므로 가변 행으로 배치한다.
            if (body.TryGetComponent(out GridLayoutGroup grid)) grid.enabled = false;
            if (body.TryGetComponent(out HorizontalLayoutGroup horizontal)) horizontal.enabled = false;
            activeChips.Clear();
            chipHeights.Clear();
            for (int i = 0; i < body.childCount; i++)
            {
                var chip = body.GetChild(i);
                var label = chip.GetComponentInChildren<Text>(true);
                if (label == null) continue;
                Style(label, scale);
                // 아이콘 전용 왼쪽 여백과 기존 글자 여백을 모두 보존한다.
                var rect = label.rectTransform;
                float padding = rect.offsetMin.x - rect.offsetMax.x;
                float preferred = MeasureHeight(label, cellWidth - padding);
                if (chip.name != "ChipTemplate" && chip.gameObject.activeSelf)
                {
                    if (chip is RectTransform chipRect)
                    {
                        activeChips.Add(chipRect);
                        chipHeights.Add(Mathf.Max(40f, preferred + 4f));
                    }
                }

            }
            float height = 0f;
            for (int i = 0; i < activeChips.Count; i += 2)
            {
                float rowHeight = chipHeights[i];
                if (i + 1 < activeChips.Count) rowHeight = Mathf.Max(rowHeight, chipHeights[i + 1]);
                PlaceChip(activeChips[i], 0f, height, cellWidth, rowHeight);
                if (i + 1 < activeChips.Count)
                    PlaceChip(activeChips[i + 1], cellWidth + 8f, height, cellWidth, rowHeight);
                height += rowHeight + 8f;
            }
            height = Mathf.Max(136f, height - 8f);
            SetSize(synergy, new Vector2(synergyWidth, height));
            SetPosition(synergy, new Vector2(48f, -(biasTop + biasHeight + 8f)));
        }

        private static void PlaceChip(RectTransform chip, float x, float top, float width, float height)
        {
            var topLeft = new Vector2(0f, 1f);
            chip.anchorMin = chip.anchorMax = topLeft;
            chip.pivot = topLeft;
            SetSize(chip, new Vector2(width, height));
            SetPosition(chip, new Vector2(x, -top));
        }

        private static float MeasureHeight(Text label, float width)
        {
            var settings = label.GetGenerationSettings(new Vector2(Mathf.Max(1f, width), 0f));
            return label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
        }

        private static void Style(Text label, float scale)
        {
            int fontSize = Mathf.RoundToInt(BASE_FONT_SIZE * scale);
            if (label.fontSize != fontSize) label.fontSize = fontSize;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void SetSize(RectTransform rect, Vector2 size)
        {
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }

        private static void SetPosition(RectTransform rect, Vector2 position)
        {
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }
    }
}
