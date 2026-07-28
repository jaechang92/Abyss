using Abyss.Runtime.Audio;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 볼륨·화면 설정 패널. 완주 루프 계획 Phase 0-3.
    ///
    /// 타이틀과 일시정지 양쪽에서 열려야 하는데 두 화면은 서로 다른 씬에 있다.
    /// 씬마다 패널을 만들면 같은 UI가 두 벌이 되므로, <see cref="Flow.ScreenFader"/>와 같이
    /// **런타임 동적 생성 + DontDestroyOnLoad**로 하나만 두고 공유한다(씬 배선 불필요).
    ///
    /// timeScale=0인 일시정지 중에도 조작되어야 하므로 시간에 의존하는 연출을 쓰지 않는다.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const int SORTING_ORDER = 500; // 일시정지 패널(200)보다 위
        private const string KEY_GUIDE =
            "← → 이동   C 점프   D 대시   Z 공격   X 강공격\nA 스킬1   S 스킬2   LCtrl 폼 교체   G 상호작용   ESC 일시정지";

        private static SettingsPanel instance;

        private GameObject body;
        private Slider masterSlider;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Toggle fullscreenToggle;
        private Text masterValue;
        private Text bgmValue;
        private Text sfxValue;

        // 슬라이더를 코드로 세팅할 때 onValueChanged가 발화해 저장이 도는 것을 막는다.
        private bool isSyncing;

        /// <summary>패널을 열어 현재 설정을 표시한다. 없으면 생성한다.</summary>
        public static void Open()
        {
            EnsureInstance();
            if (instance == null) return;
            instance.SyncFromCurrent();
            instance.body.SetActive(true);
        }

        /// <summary>열려 있으면 닫고 설정을 저장한다.</summary>
        public static void Close()
        {
            if (instance == null || instance.body == null) return;
            instance.body.SetActive(false);
            instance.SaveAll();
        }

        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = new GameObject("SettingsPanel");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            go.AddComponent<GraphicRaycaster>();

            // 다른 캔버스(HUD·로비·타이틀)와 동일한 기준 해상도로 스케일해 크기가 튀지 않게 한다.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            instance = go.AddComponent<SettingsPanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        // ───────────────────────── 값 동기화 ─────────────────────────

        /// <summary>현재 오디오·화면 상태를 위젯에 반영한다.</summary>
        private void SyncFromCurrent()
        {
            isSyncing = true;

            var audio = AudioManager.GetInstanceSafe();
            if (audio != null)
            {
                if (masterSlider != null) masterSlider.value = audio.GetMasterVolume();
                if (bgmSlider != null) bgmSlider.value = audio.GetBgmVolume();
                if (sfxSlider != null) sfxSlider.value = audio.GetSfxVolume();
            }
            if (fullscreenToggle != null) fullscreenToggle.isOn = Screen.fullScreen;

            isSyncing = false;
            RefreshValueLabels();
        }

        private void RefreshValueLabels()
        {
            if (masterValue != null && masterSlider != null) masterValue.text = ToPercent(masterSlider.value);
            if (bgmValue != null && bgmSlider != null) bgmValue.text = ToPercent(bgmSlider.value);
            if (sfxValue != null && sfxSlider != null) sfxValue.text = ToPercent(sfxSlider.value);
        }

        private static string ToPercent(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + "%";

        private void OnMasterChanged(float v)
        {
            if (isSyncing) return;
            AudioManager.GetInstanceSafe()?.SetMasterVolume(v);
            RefreshValueLabels();
        }

        private void OnBgmChanged(float v)
        {
            if (isSyncing) return;
            AudioManager.GetInstanceSafe()?.SetBgmVolume(v);
            RefreshValueLabels();
        }

        private void OnSfxChanged(float v)
        {
            if (isSyncing) return;
            AudioManager.GetInstanceSafe()?.SetSfxVolume(v);
            RefreshValueLabels();
        }

        private void OnFullscreenChanged(bool isOn)
        {
            if (isSyncing) return;
            // 해상도는 현재 값을 유지하고 창 모드만 바꾼다.
            Screen.fullScreen = isOn;
        }

        /// <summary>볼륨과 화면 설정을 세이브에 반영한다(패널을 닫을 때 1회).</summary>
        private void SaveAll()
        {
            AudioManager.GetInstanceSafe()?.SaveVolumes();

            var service = MetaSaveService.GetInstanceSafe();
            if (service != null)
            {
                service.UpdateScreenSettings(Screen.width, Screen.height, Screen.fullScreen);
            }
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateRect(root, "Body", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)body.transform);
            var dim = body.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);
            dim.raycastTarget = true;

            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 480));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            CreateLabel(panel.transform, "TitleText", new Vector2(0, -36), new Vector2(500, 40), "설정", 24, new Color(0.92f, 0.92f, 1f), TextAnchor.MiddleCenter);

            masterSlider = CreateVolumeRow(panel.transform, "Master", 60, "전체 음량", out masterValue);
            bgmSlider = CreateVolumeRow(panel.transform, "Bgm", 10, "배경 음악", out bgmValue);
            sfxSlider = CreateVolumeRow(panel.transform, "Sfx", -40, "효과음", out sfxValue);

            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);

            fullscreenToggle = CreateFullscreenRow(panel.transform, -100);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            CreateLabel(panel.transform, "KeyGuide", new Vector2(0, -160), new Vector2(500, 60), KEY_GUIDE, 14, new Color(0.62f, 0.62f, 0.72f), TextAnchor.MiddleCenter);

            var close = CreateButton(panel.transform, "CloseButton", new Vector2(0, -212), new Vector2(240, 48), "닫기");
            close.onClick.AddListener(Close);
        }

        private Slider CreateVolumeRow(Transform parent, string name, float y, string label, out Text valueText)
        {
            CreateLabel(parent, name + "Label", new Vector2(-180, y), new Vector2(160, 30), label, 17, Color.white, TextAnchor.MiddleLeft);
            valueText = CreateLabel(parent, name + "Value", new Vector2(210, y), new Vector2(70, 30), "0%", 15, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleRight);

            var go = CreateRect(parent, name + "Slider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, y), new Vector2(280, 20));
            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bg = CreateRect(go.transform, "Background", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 8));
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.22f, 0.22f, 0.28f);

            var fillArea = CreateRect(go.transform, "FillArea", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 8));
            var fill = CreateRect(fillArea.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)fill.transform);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.55f, 0.45f, 0.9f);

            var handleArea = CreateRect(go.transform, "HandleArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)handleArea.transform);
            var handle = CreateRect(handleArea.transform, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.92f, 0.92f, 1f);

            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private Toggle CreateFullscreenRow(Transform parent, float y)
        {
            CreateLabel(parent, "FullscreenLabel", new Vector2(-180, y), new Vector2(160, 30), "전체 화면", 17, Color.white, TextAnchor.MiddleLeft);

            var go = CreateRect(parent, "FullscreenToggle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60, y), new Vector2(28, 28));
            var toggle = go.AddComponent<Toggle>();

            var bg = CreateRect(go.transform, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)bg.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.22f, 0.22f, 0.28f);

            var check = CreateRect(bg.transform, "Checkmark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.55f, 0.45f, 0.9f);

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            return toggle;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        private static Text CreateLabel(Transform parent, string name, Vector2 pos, Vector2 size, string content, int fontSize, Color color, TextAnchor anchor)
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

        private static Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.30f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.22f, 0.30f);
            colors.highlightedColor = new Color(0.34f, 0.34f, 0.46f);
            colors.pressedColor = new Color(0.17f, 0.17f, 0.24f);
            button.colors = colors;

            var textGo = CreateRect(go.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            ApplyFont(text);
            text.text = label;
            text.fontSize = 17;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return button;
        }

        private static GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplyFont(Text text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
        }
    }
}
