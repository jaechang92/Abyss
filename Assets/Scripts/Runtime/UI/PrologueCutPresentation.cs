using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>기존 네 문단의 움직이는 구도 시안. 서사 상태·입력·씬 전환은 소유하지 않는다.</summary>
    public sealed class PrologueCutPresentation
    {
        private static readonly Color Stone = new Color(0.13f, 0.17f, 0.23f);
        private static readonly Color BodyColor = new Color(0.34f, 0.40f, 0.48f);
        private static readonly Color Water = new Color(0.23f, 0.43f, 0.52f);
        private readonly RectTransform[] scenes = new RectTransform[4];
        private readonly CanvasGroup[] groups = new CanvasGroup[4];
        private readonly RectTransform[] dust = new RectTransform[18];
        private readonly RectTransform[] ripples = new RectTransform[5];
        private RectTransform fallingBody;
        private RectTransform hand;
        private RectTransform lamp;
        private RectTransform observer;
        private int activeIndex = -1;

        public PrologueCutPresentation(Transform parent)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                var go = CreateRect(parent, "PrologueCut" + (i + 1), Vector2.one * 0.5f,
                    Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0f, 110f), new Vector2(1200f, 560f));
                scenes[i] = (RectTransform)go.transform;
                groups[i] = go.AddComponent<CanvasGroup>();
                groups[i].interactable = false;
                groups[i].blocksRaycasts = false;
                go.SetActive(false);
            }
            BuildFall();
            BuildContact();
            BuildDirection();
            BuildObserver();
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
            float fadeIn = Mathf.Clamp01(seconds / SubtitleSequence.FADE_IN);
            float fadeOut = Mathf.Clamp01((SubtitleSequence.PARAGRAPH_DURATION - seconds) / SubtitleSequence.FADE_OUT);
            groups[index].alpha = Mathf.Min(fadeIn, fadeOut);
            scenes[index].localScale = Vector3.one * Mathf.Lerp(1f, 1.035f, p);
            switch (index)
            {
                case 0:
                    fallingBody.anchoredPosition = new Vector2(-35f + p * 70f, 150f - p * 320f);
                    fallingBody.localRotation = Quaternion.Euler(0f, 0f, -18f + p * 30f);
                    for (int i = 0; i < dust.Length; i++)
                    {
                        float travel = Mathf.Repeat(i * 0.137f + p * 0.8f, 1f);
                        dust[i].anchoredPosition = new Vector2((i % 2 == 0 ? -1f : 1f) * (110f + i * 17f), -270f + travel * 540f);
                    }
                    break;
                case 1:
                    hand.anchoredPosition = new Vector2(-90f + p * 38f, -20f - Mathf.SmoothStep(0f, 1f, p) * 45f);
                    for (int i = 0; i < ripples.Length; i++)
                    {
                        float pulse = Mathf.Repeat(p * 1.3f + i * 0.18f, 1f);
                        ripples[i].sizeDelta = new Vector2(90f + pulse * 560f, 2f);
                        var image = ripples[i].GetComponent<Image>();
                        image.color = new Color(Water.r, Water.g, Water.b, (1f - pulse) * 0.6f);
                    }
                    break;
                case 2:
                    scenes[index].anchoredPosition = new Vector2(-p * 28f, 110f + p * 20f);
                    lamp.localScale = Vector3.one * (1f + Mathf.Sin(seconds * 3f) * 0.035f);
                    break;
                case 3:
                    observer.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, Mathf.SmoothStep(0f, 1f, p));
                    observer.anchoredPosition = new Vector2(0f, 180f - p * 28f);
                    break;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                groups[i].alpha = 0f;
                scenes[i].gameObject.SetActive(false);
                scenes[i].anchoredPosition = new Vector2(0f, 110f);
                scenes[i].localScale = Vector3.one;
            }
            activeIndex = -1;
        }

        private void BuildFall()
        {
            for (int i = 0; i < 7; i++)
            {
                float y = -240f + i * 80f;
                Piece(scenes[0], "LeftCrack", new Vector2(-350f - i % 3 * 28f, y), new Vector2(18f, 98f), Stone, -18f + i * 5f);
                Piece(scenes[0], "RightCrack", new Vector2(350f + i % 3 * 28f, y), new Vector2(18f, 98f), Stone, 22f - i * 5f);
            }
            fallingBody = Container(scenes[0], "FallingSilhouette");
            Piece(fallingBody, "Head", new Vector2(0f, 35f), new Vector2(23f, 26f), BodyColor, 12f);
            Piece(fallingBody, "Torso", Vector2.zero, new Vector2(30f, 50f), BodyColor, 0f);
            Piece(fallingBody, "LeftArm", new Vector2(-27f, 9f), new Vector2(12f, 47f), BodyColor, -60f);
            Piece(fallingBody, "RightArm", new Vector2(26f, 0f), new Vector2(12f, 47f), BodyColor, 55f);
            Piece(fallingBody, "LeftLeg", new Vector2(-14f, -43f), new Vector2(13f, 48f), BodyColor, -17f);
            Piece(fallingBody, "RightLeg", new Vector2(18f, -42f), new Vector2(13f, 48f), BodyColor, 28f);
            for (int i = 0; i < dust.Length; i++)
                dust[i] = Piece(scenes[0], "FallDust", Vector2.zero, new Vector2(3f, 5f + i % 3 * 3f), Water * 0.7f, i * 13f);
        }

        private void BuildContact()
        {
            Piece(scenes[1], "Ground", new Vector2(0f, -155f), new Vector2(1080f, 120f), Stone, 0f);
            for (int i = 0; i < ripples.Length; i++)
                ripples[i] = Piece(scenes[1], "WaterTrace", new Vector2(30f, -98f - i * 13f), new Vector2(100f, 2f), Water, 0f);
            hand = Container(scenes[1], "HandAtGround");
            Piece(hand, "Forearm", new Vector2(-150f, 80f), new Vector2(205f, 48f), BodyColor, -26f);
            Piece(hand, "Palm", new Vector2(-34f, 27f), new Vector2(92f, 47f), BodyColor, -12f);
            for (int i = 0; i < 4; i++)
                Piece(hand, "Finger", new Vector2(19f + i * 6f, 7f + i * 10f), new Vector2(50f - i * 5f, 8f), BodyColor, -8f - i * 3f);
        }

        private void BuildDirection()
        {
            for (int i = 0; i < 8; i++)
            {
                var color = Color.Lerp(Stone, new Color(0.07f, 0.09f, 0.14f), i / 8f);
                Piece(scenes[2], "DescendingStep", new Vector2(-350f + i * 88f, 130f - i * 43f), new Vector2(120f, 16f), color, 0f);
            }
            lamp = Container(scenes[2], "DistantLamp");
            lamp.anchoredPosition = new Vector2(315f, -130f);
            Piece(lamp, "Glow", Vector2.zero, new Vector2(44f, 65f), new Color(0.6f, 0.36f, 0.12f, 0.13f), 45f);
            Piece(lamp, "Light", Vector2.zero, new Vector2(9f, 20f), new Color(0.88f, 0.65f, 0.32f), 0f);
            Piece(lamp, "Stand", new Vector2(0f, -31f), new Vector2(4f, 42f), Stone, 0f);
        }

        private void BuildObserver()
        {
            Piece(scenes[3], "SmallGround", new Vector2(0f, -190f), new Vector2(1000f, 18f), Stone, 0f);
            Piece(scenes[3], "RemainingBody", new Vector2(-50f, -163f), new Vector2(140f, 28f), BodyColor, 5f);
            observer = Container(scenes[3], "NamelessShadow");
            Piece(observer, "Cloak", Vector2.zero, new Vector2(230f, 190f), new Color(0.08f, 0.10f, 0.15f), 45f);
            Piece(observer, "Veil", new Vector2(0f, 100f), new Vector2(92f, 75f), new Color(0.10f, 0.12f, 0.17f), 0f);
            // 얼굴·눈·이름을 그리지 않아 기존 서사의 미확정 존재를 유지한다.
        }

        private static RectTransform Container(Transform parent, string name)
            => (RectTransform)CreateRect(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero).transform;

        private static RectTransform Piece(Transform parent, string name, Vector2 position, Vector2 size, Color color, float angle)
        {
            var go = CreateRect(parent, name, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return (RectTransform)go.transform;
        }
    }
}
