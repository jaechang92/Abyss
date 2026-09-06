using System;
using System.Collections.Generic;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
// CreateRect·CreateLabel·CreateButton 등 uGUI 조립 헬퍼는 UiFactory가 소유한다(LobbyMenuPanel과 공유).
using static Abyss.Runtime.UI.UiFactory;
// 행 y·패널 높이는 SettingsPanelLayout이 계산한다 — 이 파일에 좌표 숫자를 직접 적지 않는다.
using static Abyss.Runtime.UI.SettingsPanelLayout;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 볼륨·화면·언어 설정 패널. 완주 루프 계획 Phase 0-3.
    ///
    /// 타이틀과 일시정지 양쪽에서 열려야 하는데 두 화면은 서로 다른 씬에 있다.
    /// 씬마다 패널을 만들면 같은 UI가 두 벌이 되므로, <see cref="Flow.ScreenFader"/>와 같이
    /// **런타임 동적 생성 + DontDestroyOnLoad**로 하나만 두고 공유한다(씬 배선 불필요).
    ///
    /// timeScale=0인 일시정지 중에도 조작되어야 하므로 시간에 의존하는 연출을 쓰지 않는다.
    ///
    /// ESC로 닫는 책임은 이 패널이 갖는다(<see cref="Update"/>) — 씬마다 ESC가 오는 경로가 달라
    /// 씬별 처리기에만 맡기면 입력 배선이 없는 타이틀 씬에서 닫을 방법이 사라진다.
    ///
    /// 🔑 <b>자기 글자를 스스로 다시 그린다.</b> 언어 선택이 이 패널 안에 있으므로, 라벨이 만들어질
    /// 때의 언어로 고정되면 <b>언어를 바꾼 그 창만 안 바뀌는</b> 자리가 된다 — 사용자가 방금 누른 것이
    /// 통했는지 확인할 수단이 사라진다. 그래서 고정 문구는 전부
    /// <see cref="Localization.LocalizedText"/>로 붙여 <c>OnLanguageChanged</c>를 듣게 한다.
    /// 값 칸(음량 %·해상도 크기·언어 이름)만은 번역 대상이 아니라 그대로 둔다.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static SettingsPanel instance;

        // ESC를 이번 프레임에 소비했음을 알리는 표식. WasClosedThisFrame 참고.
        private static int closedFrame = -1;

        private GameObject body;
        private Slider masterSlider;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Toggle fullscreenToggle;
        private Text masterValue;
        private Text bgmValue;
        private Text sfxValue;

        private Text resolutionValue;
        private Button resolutionPrev;
        private Button resolutionNext;

        // 이 기기가 지원하는 해상도 목록(주사율 중복 제거, 내림차순). 인덱스가 아니라 값으로만 저장한다.
        private readonly List<Vector2Int> resolutionOptions = new();
        private int resolutionIndex = -1;

        private Text languageValue;
        private Button languagePrev;
        private Button languageNext;

        // 지원 언어 목록. enum 선언 순서 그대로다 — 순서를 따로 정하면 enum에 언어를 끼울 때
        // 두 곳을 맞춰야 하고, 한쪽만 고치면 셀렉터에서 한 언어가 조용히 사라진다.
        private readonly List<LocalizationLanguage> languageOptions = new();
        private int languageIndex = -1;

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
            closedFrame = Time.frameCount;
        }

        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;

        /// <summary>
        /// 이번 프레임에 이 패널이 ESC를 소비했는지.
        /// 같은 프레임에 도착한 다른 ESC 처리(정지 해제·로비 메뉴)가 한 번의 입력을 두 번 쓰지 않게 막는 가드다.
        /// </summary>
        public static bool WasClosedThisFrame => closedFrame == Time.frameCount;

        /// <summary>
        /// 열려 있는 동안 ESC를 직접 받는다.
        ///
        /// 씬마다 ESC가 도착하는 경로가 다르다 — Run은 UI 맵 <c>Cancel</c>, 로비는 Player 맵 <c>Pause</c>,
        /// 타이틀은 <b>수신자가 아예 없다</b>. 패널이 자기 닫기를 직접 소유하면 타이틀처럼 입력 배선이
        /// 없는 씬에서도 ESC가 통한다(씬마다 입력을 배선하는 것보다 싸다).
        ///
        /// 씬의 ESC 처리기가 먼저 닫는 경우도 있으므로(입력 처리는 Update보다 먼저 돈다) 여기서는 아직
        /// 열려 있을 때만 동작하고, 반대 순서는 <see cref="WasClosedThisFrame"/>가 막는다.
        /// timeScale=0에서도 Update는 돌기 때문에 일시정지 중에도 유효하다.
        /// </summary>
        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            Close();
        }

        /// <summary>
        /// 도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).
        /// instance는 이전 플레이 세션에서 파괴된 오브젝트를 가리킬 수 있고, closedFrame은
        /// 리셋된 Time.frameCount와 우연히 맞아떨어질 수 있다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            closedFrame = -1;
        }

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = CreateOverlayCanvas("SettingsPanel", UiSortingOrder.Settings);
            DontDestroyOnLoad(go);

            instance = go.AddComponent<SettingsPanel>();
            // UI를 만들기 전에 목록이 있어야 버튼 활성 상태를 정할 수 있다
            instance.BuildResolutionOptions();
            instance.BuildLanguageOptions();
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
            SyncResolutionFromScreen();
            SyncLanguageFromCurrent();
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

        // ───────────────────────── 해상도 ─────────────────────────

        /// <summary>
        /// 이 기기가 지원하는 해상도 목록을 만든다.
        /// <see cref="Screen.resolutions"/>는 같은 크기를 주사율별로 여러 번 담으므로 크기 기준으로 중복을 제거하고,
        /// 큰 것부터 보이도록 내림차순 정렬한다(대개 원하는 값이 목록 위쪽에 있다).
        /// </summary>
        private void BuildResolutionOptions()
        {
            resolutionOptions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutionOptions.Contains(size)) resolutionOptions.Add(size);
            }
            resolutionOptions.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
        }

        /// <summary>현재 화면 크기를 목록에서 찾아 선택 위치를 맞춘다.</summary>
        private void SyncResolutionFromScreen()
        {
            if (resolutionOptions.Count == 0)
            {
                // 목록을 못 얻는 환경(일부 에디터·플랫폼)에서는 현재 크기만 표시하고 조작을 막는다.
                resolutionIndex = -1;
                if (resolutionValue != null) resolutionValue.text = $"{Screen.width} x {Screen.height}";
                SetResolutionInteractable(false, false);
                return;
            }

            var current = new Vector2Int(Screen.width, Screen.height);
            int found = resolutionOptions.IndexOf(current);
            // 창을 드래그로 늘린 경우처럼 목록에 없는 크기일 수 있어 가장 가까운 항목으로 맞춘다.
            resolutionIndex = found >= 0 ? found : FindNearestResolution(current);
            RefreshResolutionLabel();
        }

        private int FindNearestResolution(Vector2Int current)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                var o = resolutionOptions[i];
                int distance = Mathf.Abs(o.x - current.x) + Mathf.Abs(o.y - current.y);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>목록에서 delta칸 이동한 해상도를 즉시 적용한다. 양 끝에서는 더 가지 않는다.</summary>
        private void ShiftResolution(int delta)
        {
            if (resolutionOptions.Count == 0) return;

            int next = Mathf.Clamp(resolutionIndex + delta, 0, resolutionOptions.Count - 1);
            if (next == resolutionIndex) return;

            resolutionIndex = next;
            var size = resolutionOptions[resolutionIndex];
            // 창 모드는 전체화면 토글이 소유하므로 여기서는 현재 모드를 그대로 유지한다.
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
            RefreshResolutionLabel();
        }

        private void RefreshResolutionLabel()
        {
            if (resolutionIndex < 0 || resolutionIndex >= resolutionOptions.Count) return;

            var size = resolutionOptions[resolutionIndex];
            if (resolutionValue != null) resolutionValue.text = $"{size.x} x {size.y}";
            // 목록은 내림차순이라 '이전'이 더 큰 해상도다.
            SetResolutionInteractable(resolutionIndex > 0, resolutionIndex < resolutionOptions.Count - 1);
        }

        private void SetResolutionInteractable(bool canPrev, bool canNext)
        {
            if (resolutionPrev != null) resolutionPrev.interactable = canPrev;
            if (resolutionNext != null) resolutionNext.interactable = canNext;
        }

        // ───────────────────────── 언어 ─────────────────────────

        private void BuildLanguageOptions()
        {
            languageOptions.Clear();
            foreach (LocalizationLanguage language in Enum.GetValues(typeof(LocalizationLanguage)))
            {
                languageOptions.Add(language);
            }
        }

        /// <summary>현재 언어를 목록에서 찾아 선택 위치를 맞춘다.</summary>
        private void SyncLanguageFromCurrent()
        {
            if (languageOptions.Count == 0)
            {
                languageIndex = -1;
                SetLanguageInteractable(false, false);
                return;
            }

            int found = languageOptions.IndexOf(Loc.CurrentLanguage);
            languageIndex = found >= 0 ? found : 0;
            RefreshLanguageLabel();
        }

        /// <summary>
        /// 목록에서 delta칸 이동한 언어로 즉시 전환한다. 양 끝에서는 더 가지 않는다(해상도와 같은 규칙).
        ///
        /// 저장은 <see cref="Loc.SetLanguage"/>가 곧바로 한다 — 볼륨·해상도처럼 닫을 때 모아 쓰지
        /// 않는다. 언어는 <b>바뀐 화면 자체가 확인 수단</b>이라, 저장을 닫는 시점으로 미루면
        /// 글자만 바뀐 채 창을 강제 종료했을 때 다음 실행에서 되돌아간다.
        /// </summary>
        private void ShiftLanguage(int delta)
        {
            if (languageOptions.Count == 0) return;

            int next = Mathf.Clamp(languageIndex + delta, 0, languageOptions.Count - 1);
            if (next == languageIndex) return;

            languageIndex = next;
            // 이 호출이 OnLanguageChanged를 발행하고, 이 패널의 LocalizedText들이 그것을 듣고 다시 그린다.
            Loc.SetLanguage(languageOptions[languageIndex]);
            RefreshLanguageLabel();
        }

        private void RefreshLanguageLabel()
        {
            if (languageIndex < 0 || languageIndex >= languageOptions.Count) return;

            // 값 칸만은 번역하지 않는다 — 각 언어를 그 언어 자신의 표기로 보여 준다.
            if (languageValue != null)
            {
                languageValue.text = LocalizationLanguageNames.GetNativeName(languageOptions[languageIndex]);
            }
            SetLanguageInteractable(languageIndex > 0, languageIndex < languageOptions.Count - 1);
        }

        private void SetLanguageInteractable(bool canPrev, bool canNext)
        {
            if (languagePrev != null) languagePrev.interactable = canPrev;
            if (languageNext != null) languageNext.interactable = canNext;
        }

        /// <summary>볼륨과 화면 설정을 세이브에 반영한다(패널을 닫을 때 1회).</summary>
        private void SaveAll()
        {
            AudioManager.GetInstanceSafe()?.SaveVolumes();

            var service = MetaSaveService.GetInstanceSafe();
            if (service == null) return;

            // Screen.width는 SetResolution 직후 한 프레임 늦게 갱신되므로 화면이 아니라 '선택값'을 저장한다.
            bool hasSelection = resolutionIndex >= 0 && resolutionIndex < resolutionOptions.Count;
            int width = hasSelection ? resolutionOptions[resolutionIndex].x : Screen.width;
            int height = hasSelection ? resolutionOptions[resolutionIndex].y : Screen.height;
            service.UpdateScreenSettings(width, height, Screen.fullScreen);
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root);

            // 좌표는 전부 위쪽 배치 상수에서 파생된다. 여기에 숫자를 직접 적지 않는다.
            var panel = CreateRect(body.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.15f, 0.98f);

            CreateLocalizedLabel(panel.transform, "TitleText", new Vector2(0, TitleY), new Vector2(500, TitleHeight), StringKey.Menu_Settings, 24, new Color(0.92f, 0.92f, 1f), TextAnchor.MiddleCenter);

            masterSlider = CreateVolumeRow(panel.transform, "Master", RowY(0), StringKey.Settings_MasterVolume, out masterValue);
            bgmSlider = CreateVolumeRow(panel.transform, "Bgm", RowY(1), StringKey.Settings_BgmVolume, out bgmValue);
            sfxSlider = CreateVolumeRow(panel.transform, "Sfx", RowY(2), StringKey.Settings_SfxVolume, out sfxValue);

            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);

            fullscreenToggle = CreateFullscreenRow(panel.transform, RowY(3));
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            resolutionValue = CreateSelectorRow(panel.transform, "Resolution", RowY(4), StringKey.Settings_Resolution,
                () => ShiftResolution(-1), () => ShiftResolution(1), out resolutionPrev, out resolutionNext);

            languageValue = CreateSelectorRow(panel.transform, "Language", RowY(5), StringKey.Settings_Language,
                () => ShiftLanguage(-1), () => ShiftLanguage(1), out languagePrev, out languageNext);

            CreateLocalizedLabel(panel.transform, "KeyGuide", new Vector2(0, KeyGuideY), new Vector2(500, KeyGuideHeight), StringKey.Settings_KeyGuide, 14, new Color(0.62f, 0.62f, 0.72f), TextAnchor.MiddleCenter);

            var close = CreateLocalizedButton(panel.transform, "CloseButton", new Vector2(0, CloseY), new Vector2(240, CloseHeight), StringKey.Common_Close);
            close.onClick.AddListener(Close);
        }

        /// <summary>
        /// 좌우 셀렉터 행(◀ 값 ▶). 해상도와 언어가 모양이 같아 한 곳에서 만든다 —
        /// 둘로 복제하면 한쪽 x만 고쳐 두 행의 값 칸이 어긋나는 자리가 열린다.
        ///
        /// 드롭다운 대신 좌우 셀렉터인 이유: uGUI Dropdown을 코드로 만들려면 Template·Viewport·Content·Item
        /// 계층과 스크롤바까지 손으로 배선해야 해 이 패널의 다른 위젯(수제 슬라이더·토글)보다 훨씬 깨지기 쉽다.
        /// 선택지가 십수 개뿐이라 순차 이동으로 충분하고, 패드 조작과도 잘 맞는다.
        /// </summary>
        /// <returns>값이 표시되는 라벨. 갱신은 호출부가 맡는다(해상도는 크기, 언어는 자기 표기).</returns>
        private Text CreateSelectorRow(Transform parent, string name, float y, string labelKey,
            UnityAction onPrev, UnityAction onNext, out Button prev, out Button next)
        {
            CreateLocalizedLabel(parent, name + "Label", new Vector2(-180, y), new Vector2(160, 30), labelKey, 17, Color.white, TextAnchor.MiddleLeft);

            prev = CreateButton(parent, name + "Prev", new Vector2(-70, y), new Vector2(34, 34), "<");
            prev.onClick.AddListener(onPrev);

            var value = CreateLabel(parent, name + "Value", new Vector2(60, y), new Vector2(200, 30), "-", 17, new Color(0.86f, 0.86f, 0.96f), TextAnchor.MiddleCenter);

            next = CreateButton(parent, name + "Next", new Vector2(190, y), new Vector2(34, 34), ">");
            next.onClick.AddListener(onNext);

            return value;
        }

        private Slider CreateVolumeRow(Transform parent, string name, float y, string labelKey, out Text valueText)
        {
            CreateLocalizedLabel(parent, name + "Label", new Vector2(-180, y), new Vector2(160, 30), labelKey, 17, Color.white, TextAnchor.MiddleLeft);
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
            CreateLocalizedLabel(parent, "FullscreenLabel", new Vector2(-180, y), new Vector2(160, 30), StringKey.Settings_Fullscreen, 17, Color.white, TextAnchor.MiddleLeft);

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

    }
}
