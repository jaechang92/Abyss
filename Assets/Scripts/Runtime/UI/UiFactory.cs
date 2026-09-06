using Abyss.Runtime.Localization;
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

        /// <summary>
        /// StringKey로 글자를 채우고 언어가 바뀌면 스스로 갱신되는 라벨.
        /// <see cref="CreateLabel"/>과 인자가 같고 content 자리가 stringKey로 바뀐 것뿐이다.
        ///
        /// 초기 문자열을 비워 두고 <see cref="LocalizedText"/>가 채우게 한다 — 여기서 한 번 채워 두면
        /// 같은 조회가 두 곳(팩토리 · 컴포넌트)에 생기고, 한쪽만 고쳤을 때 <b>첫 프레임만 옛 글자</b>가
        /// 되는 어긋남이 열린다.
        /// </summary>
        public static Text CreateLocalizedLabel(Transform parent, string name, Vector2 pos, Vector2 size, string stringKey, int fontSize, Color color, TextAnchor anchor)
        {
            var text = CreateLabel(parent, name, pos, size, string.Empty, fontSize, color, anchor);
            LocalizedText.Attach(text, stringKey);
            return text;
        }

        /// <summary>라벨이 언어를 따라가는 버튼. 부착 대상은 <see cref="CreateButton"/>이 만드는 자식 "Text"다.</summary>
        public static Button CreateLocalizedButton(Transform parent, string name, Vector2 pos, Vector2 size, string stringKey, int fontSize = 17)
        {
            var button = CreateButton(parent, name, pos, size, string.Empty, fontSize);

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) LocalizedText.Attach(label, stringKey);
            return button;
        }

        /// <summary>
        /// 씬에 배치된 패널을 <b>제자리에서</b> 오버레이 층으로 올린다. 부모 Canvas 안의 하위 Canvas로
        /// 만들어 정렬만 오버라이드하는 방식이라, 계층 구조도 배선도 건드리지 않는다.
        ///
        /// <see cref="CreateOverlayCanvas"/>와 나눈 이유는 <b>출발점이 다르기</b> 때문이다 — 저쪽은
        /// 아무것도 없는 상태에서 캔버스를 만들고(그래서 CanvasScaler로 기준 해상도까지 정한다),
        /// 이쪽은 이미 씬 캔버스 밑에서 그 스케일을 물려받고 있는 오브젝트를 층만 올린다.
        /// 여기서 Scaler를 또 붙이면 부모의 스케일 위에 스케일이 겹쳐 크기가 튄다.
        ///
        /// 중첩 Canvas의 그래픽은 부모 GraphicRaycaster가 잡지 못하므로 전용 Raycaster를 함께 붙인다
        /// (붙이지 않으면 화면은 멀쩡한데 버튼만 눌리지 않는다).
        /// 이미 붙어 있으면 값만 갱신한다 — 두 번 호출해도 안전해야 한다.
        /// </summary>
        public static void ApplyOverlaySorting(GameObject target, int sortingOrder)
        {
            if (target == null) return;

            var canvas = target.GetComponent<Canvas>();
            if (canvas == null) canvas = target.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            if (target.GetComponent<GraphicRaycaster>() == null) target.AddComponent<GraphicRaycaster>();
        }

        public static void ApplyFont(Text text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
        }
    }
}
