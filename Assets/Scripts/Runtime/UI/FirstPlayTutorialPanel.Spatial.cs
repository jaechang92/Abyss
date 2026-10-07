using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    public sealed partial class FirstPlayTutorialPanel
    {
        private GameObject spatialBody;
        private Text spatialKey;
        private Text spatialHint;
        private GameObject[] actionShapes;

        public void SetCompact(bool isCompact)
        {
            var rect = (RectTransform)body.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = isCompact ? new Vector2(1f, 0f) : new Vector2(0.5f, 1f);
            rect.anchoredPosition = isCompact ? new Vector2(-24f, 24f) : new Vector2(0f, -24f);
            rect.sizeDelta = isCompact ? new Vector2(460f, 42f) : PanelSize;
            header.gameObject.SetActive(!isCompact);
            hint.gameObject.SetActive(!isCompact);
            ((RectTransform)padHint.transform).anchoredPosition = isCompact ? new Vector2(-60f, 0f) : new Vector2(-70f, -36f);
            ((RectTransform)padHint.transform).sizeDelta = isCompact ? new Vector2(320f, 30f) : new Vector2(440f, 24f);
            ((RectTransform)skipButton.transform).anchoredPosition = isCompact ? new Vector2(166f, 0f) : new Vector2(250f, -36f);
        }

        public void SetSpatialVisible(bool isVisible)
        {
            if (spatialBody != null) spatialBody.SetActive(isVisible);
        }

        public void SetSpatialContent(int action, string binding, string instruction)
        {
            if (spatialBody == null) BuildSpatialUI();
            Assign(spatialKey, binding);
            Assign(spatialHint, instruction);
            for (int i = 0; i < actionShapes.Length; i++) actionShapes[i].SetActive(i == action);
        }

        public void PlaceSpatialHint(UnityEngine.Camera camera, Vector3 target)
        {
            if (spatialBody == null || !spatialBody.activeSelf) return;
            var root = (RectTransform)transform;
            Vector3 screen = camera.WorldToScreenPoint(target);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 point)) return;
            point.x = Mathf.Clamp(point.x, root.rect.xMin + 240f, root.rect.xMax - 240f);
            point.y = Mathf.Clamp(point.y, root.rect.yMin + 130f, root.rect.yMax - 180f);
            ((RectTransform)spatialBody.transform).anchoredPosition = point;
        }

        private void BuildSpatialUI()
        {
            spatialBody = CreateRect(transform, "WorldHint", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(460f, 100f));
            var background = spatialBody.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;
            var keycap = CreateRect(spatialBody.transform, "Keycap", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(34f, 23f), new Vector2(320f, 34f));
            var keyBackground = keycap.AddComponent<Image>();
            keyBackground.color = new Color(0.2f, 0.24f, 0.28f);
            keyBackground.raycastTarget = false;
            spatialKey = CreateLabel(keycap.transform, "Binding", Vector2.zero, new Vector2(310f, 32f),
                string.Empty, 18, HeaderColor, TextAnchor.MiddleCenter);
            spatialHint = CreateLabel(spatialBody.transform, "Instruction", new Vector2(0f, -24f),
                new Vector2(440f, 42f), string.Empty, 20, HintColor, TextAnchor.MiddleCenter);
            spatialKey.raycastTarget = spatialHint.raycastTarget = false;
            actionShapes = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                actionShapes[i] = CreateRect(spatialBody.transform, "Action" + i, Vector2.one * 0.5f,
                    Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-190f, 23f), new Vector2(48f, 40f));
            }
            // 작은 UI 도형 시안: 발걸음, 점프 궤적, 대시 잔상, 검. 이미지 교체 전 표현이다.
            Bar(0, -12f, -8f, 12f, 7f, 0f); Bar(0, 8f, 4f, 12f, 7f, 0f);
            for (int i = 0; i < 5; i++) Bar(1, -20f + i * 10f, 12f - Mathf.Abs(i - 2) * 8f, 6f, 6f, 0f);
            Bar(2, 0f, 0f, 38f, 5f, 0f); Bar(2, -8f, -10f, 24f, 4f, 0f); Bar(2, -8f, 10f, 24f, 4f, 0f);
            Bar(3, 0f, 2f, 6f, 34f, -40f); Bar(3, -9f, -9f, 20f, 5f, -40f);
        }

        private void Bar(int action, float x, float y, float width, float height, float angle)
        {
            var go = CreateRect(actionShapes[action].transform, "Stroke", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(x, y), new Vector2(width, height));
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = go.AddComponent<Image>();
            image.color = HeaderColor;
            image.raycastTarget = false;
        }
    }
}
