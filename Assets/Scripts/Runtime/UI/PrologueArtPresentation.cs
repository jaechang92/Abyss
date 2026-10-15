using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>자막이 넘긴 문단/시간만 그린다. 2배 캔버스·등불 UV 셀로 원점을 보존한다.</summary>
    internal sealed class PrologueArtPresentation
    {
        private readonly RectTransform[] scenes = new RectTransform[4];
        private readonly CanvasGroup[] groups = new CanvasGroup[4];
        private readonly RectTransform[] dust = new RectTransform[12];
        private readonly Image[] ripples = new Image[4];
        private readonly RectTransform fallingBody;
        private readonly RectTransform hand;
        private readonly RawImage lamp;
        private readonly RawImage shadow;
        private readonly Canvas canvas;
        private int activeIndex = -1;

        public static PrologueArtPresentation TryCreate(Transform parent)
        {
            var data = PrologueArtData.TryLoad();
            return data == null ? null : new PrologueArtPresentation(parent, data);
        }

        private PrologueArtPresentation(Transform parent, PrologueArtData data)
        {
            canvas = parent.GetComponentInParent<Canvas>();
            for (int i = 0; i < scenes.Length; i++)
            {
                var root = Rect(parent, "PrologueArtCut" + (i + 1), new Vector2(1200f, 560f));
                scenes[i] = root;
                root.anchoredPosition = new Vector2(0f, 110f);
                groups[i] = root.gameObject.AddComponent<CanvasGroup>();
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
                root.gameObject.AddComponent<RectMask2D>();
                Layer(root, "Background", data.Background(i));
                root.gameObject.SetActive(false);
            }

            fallingBody = Layer(scenes[0], "FallingBody", data.FallingBody).rectTransform;
            for (int i = 0; i < dust.Length; i++)
                dust[i] = Mark(scenes[0], "Dust", new Vector2(2f, 4f), new Color(0.23f, 0.43f, 0.52f, 0.35f)).rectTransform;

            // 접촉 기준 (252,193): 상단 원점 PNG → 중심 원점 UI, 표시 배율 2배.
            for (int i = 0; i < ripples.Length; i++)
                ripples[i] = Mark(scenes[1], "WaterTrace", Vector2.zero, Color.clear);
            hand = Layer(scenes[1], "Hand", data.Hand).rectTransform;
            lamp = Layer(scenes[2], "Lamp", data.LampStrip);
            Layer(scenes[3], "BodyTrace", data.BodyTrace);
            shadow = Layer(scenes[3], "Shadow", data.Shadow);
            Clear();
        }

        public void Show(int index, float seconds)
        {
            if (index < 0 || index >= scenes.Length) { Clear(); return; }
            if (activeIndex != index)
            {
                Clear();
                activeIndex = index;
                scenes[index].gameObject.SetActive(true);
            }

            float p = Mathf.Clamp01(seconds / SubtitleSequence.PARAGRAPH_DURATION);
            groups[index].alpha = Mathf.Min(Mathf.Clamp01(seconds / SubtitleSequence.FADE_IN),
                Mathf.Clamp01((SubtitleSequence.PARAGRAPH_DURATION - seconds) / SubtitleSequence.FADE_OUT));
            SnapRoot(scenes[index]);
            switch (index)
            {
                case 0:
                    // 전체 캔버스를 확대/회전하지 않고 원본 픽셀 단위로만 이동한다.
                    fallingBody.anchoredPosition = Offset(0f, Mathf.Lerp(24f, -16f, p));
                    for (int i = 0; i < dust.Length; i++)
                        dust[i].anchoredPosition = Offset(-230f + i * 41f,
                            -130f + Mathf.Repeat(i * 19f + p * 65f, 260f));
                    break;
                case 1:
                    float contact = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seconds - 0.2f) / 0.65f));
                    hand.anchoredPosition = Offset(0f, (1f - contact) * 8f);
                    for (int i = 0; i < ripples.Length; i++)
                    {
                        float wave = Mathf.Clamp01((seconds - 0.2f - i * 0.16f) / 1.4f);
                        ripples[i].rectTransform.anchoredPosition = Offset(-48f, -53f - i * 3f);
                        ripples[i].rectTransform.sizeDelta = new Vector2(Snap(20f + wave * 220f), Snap(2f));
                        ripples[i].color = new Color(0.23f, 0.43f, 0.52f,
                            wave > 0f && wave < 1f ? (1f - wave) * 0.55f : 0f);
                    }
                    break;
                case 2:
                    int frame = Mathf.FloorToInt(Mathf.Max(0f, seconds) * 4f) % 3;
                    lamp.uvRect = new Rect(frame / 3f, 0f, 1f / 3f, 1f);
                    break;
                case 3:
                    float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / 1.4f));
                    shadow.rectTransform.anchoredPosition = Offset(0f, (1f - reveal) * 12f);
                    shadow.color = new Color(1f, 1f, 1f, reveal * 0.35f);
                    break;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                groups[i].alpha = 0f;
                scenes[i].gameObject.SetActive(false);
            }
            activeIndex = -1;
        }

        private float Snap(float value)
        {
            float scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
            return Mathf.Round(value * scale) / scale;
        }

        private Vector2 Offset(float x, float y)
            => new Vector2(Snap(Mathf.Round(x) * 2f), Snap(Mathf.Round(y) * 2f));

        private void SnapRoot(RectTransform root)
        {
            root.anchoredPosition = new Vector2(0f, 110f);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, root.position);
            float scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
            root.anchoredPosition += new Vector2(Mathf.Round(screen.x) - screen.x,
                Mathf.Round(screen.y) - screen.y) / scale;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 size)
            => (RectTransform)CreateRect(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, size).transform;

        private static RawImage Layer(Transform parent, string name, Texture2D texture)
        {
            var image = Rect(parent, name, new Vector2(1200f, 560f)).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static Image Mark(Transform parent, string name, Vector2 size, Color color)
        {
            var image = Rect(parent, name, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
