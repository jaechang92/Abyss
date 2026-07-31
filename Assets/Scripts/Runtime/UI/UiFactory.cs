using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 런타임에 동적 생성되는 오버레이 패널(<see cref="SettingsPanel"/>·<see cref="LobbyMenuPanel"/>)이
    /// 공유하는 uGUI 조립 헬퍼.
    ///
    /// 씬 배선 없이 코드로 만드는 패널이 둘 이상이 되면서 같은 CreateRect/CreateButton/폰트 적용이
    /// 파일마다 복제될 참이었다. 기준 해상도·폰트·버튼 색 같은 표시 규약이 한 곳에만 있도록 여기로 모은다.
    ///
    /// 씬 빌더(Editor 어셈블리)가 만드는 패널은 대상이 아니다 — 그쪽은 에디터 전용 경로라 공유하지 않는다.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>동적 패널 공통 기준 해상도. 씬 캔버스(HUD·로비·타이틀)와 같아야 크기가 튀지 않는다.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        private static readonly Color ButtonNormal = new Color(0.22f, 0.22f, 0.30f);
        private static readonly Color ButtonHighlighted = new Color(0.34f, 0.34f, 0.46f);
        private static readonly Color ButtonPressed = new Color(0.17f, 0.17f, 0.24f);
        private static readonly Color ButtonDisabled = new Color(0.14f, 0.14f, 0.18f, 0.6f);

        /// <summary>화면 전체를 덮는 오버레이 캔버스를 만든다. sortingOrder가 패널 간 겹침 순서를 정한다.</summary>
        public static GameObject CreateOverlayCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            go.AddComponent<GraphicRaycaster>();

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            return go;
        }

        /// <summary>
        /// 화면을 덮는 딤 배경. 뒤쪽 UI 클릭을 막는 역할도 하므로 raycastTarget을 켠 채 둔다.
        /// 패널의 표시/숨김 토글 대상이기도 하다.
        /// </summary>
        public static GameObject CreateDimBody(Transform parent, float alpha = 0.82f)
        {
            var body = CreateRect(parent, "Body", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)body.transform);

            var dim = body.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, alpha);
            dim.raycastTarget = true;
            return body;
        }

        public static GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        /// <summary>부모를 가득 채우도록 늘린다.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>부모 중심 기준으로 배치되는 라벨.</summary>
        public static Text CreateLabel(Transform parent, string name, Vector2 pos, Vector2 size, string content, int fontSize, Color color, TextAnchor anchor)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

            var text = go.AddComponent<Text>();
            ApplyFont(text);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>부모 중심 기준으로 배치되는 버튼. 라벨은 자식 "Text"로 붙는다.</summary>
        public static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label, int fontSize = 17)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

            var img = go.AddComponent<Image>();
            img.color = ButtonNormal;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlighted;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            button.colors = colors;

            var textGo = CreateRect(go.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)textGo.transform);

            var text = textGo.AddComponent<Text>();
            ApplyFont(text);
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            // 2단계 확인 문구처럼 라벨이 길어져도 줄바꿈되지 않게 한다.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = Color.white;

            return button;
        }

        public static void ApplyFont(Text text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
        }
    }
}
