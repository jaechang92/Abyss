using Abyss.Runtime.Audio;
using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;
using static Abyss.Runtime.UI.SettingsPanelLayout;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 설정 창의 데이터 초기화 진입 행과 초기화 성공 후 닫기.
    ///
    /// 진입 버튼은 타이틀·로비에서 런이 없을 때만 눌린다. 그 밖에서는 꺼진 채 「타이틀·로비 전용」을 보여 준다 —
    /// 꺼진 이유가 안 보이면 고장으로 읽힌다. 상태는 열려 있는 동안 매 프레임 확인한다(런 시작·씬 전환 반영).
    ///
    /// 🔑 <b>성공 후에는 <see cref="Close"/>를 쓰지 않는다.</b> Close는 SaveAll로 지금 위젯·오디오 값을 저장하는데,
    /// 그 값은 초기화 <b>전</b> 값이라 방금 지운 설정을 되살린다. 그래서 저장 없이 숨기고 포커스만 놓는
    /// <see cref="CloseAfterDataReset"/>를 따로 둔다. 설정 묶음을 지웠을 때만 디스크 저장이 끝난 새 값을 런타임에 적용한다.
    /// </summary>
    public sealed partial class SettingsPanel
    {
        private Text dataResetLabel;
        private Button dataResetButton;
        private Text dataResetButtonText;
        private bool isDataResetShownAllowed;
        private LocalizationLanguage? dataResetShownLanguage;

        /// <summary>MenuTextReadability.Attach 전에 불러야 확대 대상에 들어간다.</summary>
        private void BuildDataResetEntry(Transform parent)
        {
            dataResetLabel = CreateLabel(parent, "DataResetLabel", new Vector2(-180, RowY(12)), new Vector2(160, 30),
                string.Empty, 17, Color.white, TextAnchor.MiddleLeft);
            dataResetButton = CreateButton(parent, "DataResetButton", new Vector2(80, RowY(12)), new Vector2(300, 34), string.Empty, 16);
            dataResetButtonText = dataResetButton.GetComponentInChildren<Text>(true);
            dataResetButton.onClick.AddListener(OpenDataReset);
            SyncDataResetEntry();
        }

        private void OpenDataReset()
        {
            if (!IsOpen || KeyboardBindingsPanel.IsOpen || SaveStatusOverlay.IsCapturingInput) return;
            if (!DataResetPanel.IsResetContextAllowed)
            {
                SyncDataResetEntry();
                return;
            }
            DataResetPanel.Open();
        }

        /// <summary>허용 여부와 언어에 맞춰 진입 행 글자·활성을 맞춘다. 저장하지 않는다.</summary>
        private void SyncDataResetEntry()
        {
            if (dataResetButton == null) return;
            bool isAllowed = DataResetPanel.IsResetContextAllowed;
            isDataResetShownAllowed = isAllowed;
            dataResetShownLanguage = LocalizationManager.GetInstanceSafe() != null ? Loc.CurrentLanguage : (LocalizationLanguage?)null;
            if (dataResetLabel != null) dataResetLabel.text = DataResetPanel.Tr("Entry_Label");
            if (dataResetButtonText != null) dataResetButtonText.text = DataResetPanel.Tr(isAllowed ? "Entry_Open" : "Entry_Disabled");
            if (dataResetButton.interactable == isAllowed) return;
            dataResetButton.interactable = isAllowed;
            // 꺼진 버튼으로 가는 길을 남기지 않는다.
            RefreshNavigation();
        }

        private void LateUpdate()
        {
            if (body == null || !body.activeSelf || dataResetButton == null) return;
            var language = LocalizationManager.GetInstanceSafe() != null ? Loc.CurrentLanguage : (LocalizationLanguage?)null;
            if (DataResetPanel.IsResetContextAllowed != isDataResetShownAllowed || language != dataResetShownLanguage) SyncDataResetEntry();
        }

        /// <summary>
        /// 데이터 초기화 성공 직후 전용 닫기. <b>SaveAll 없이</b> 숨기고 포커스를 놓는다.
        /// <paramref name="applySettings"/>면 디스크에 저장된 새 설정을 런타임(오디오·언어·키·화면)에 적용하고
        /// 위젯을 저장 없이 다시 맞춘다. 설정을 지우지 않았으면 런타임 값은 그대로 둔다.
        /// </summary>
        internal static void CloseAfterDataReset(bool applySettings)
        {
            KeyboardBindingsPanel.Close();
            if (applySettings)
            {
                ApplySettingsAfterReset();
                if (instance != null && instance.body != null) instance.SyncFromCurrent();
            }
            if (!IsOpen) return;
            instance.body.SetActive(false);
            closedFrame = Time.frameCount;
            instance.ReleaseFocus();
        }

        /// <summary>저장이 끝난 설정을 적용한다. Set 계열만 쓰고 SaveVolumes·Update* 같은 저장 경로는 부르지 않는다.</summary>
        private static void ApplySettingsAfterReset()
        {
            var service = MetaSaveService.GetInstanceSafe();
            if (service == null || !service.IsLoaded) return;
            var settings = service.Current?.settings;
            if (settings == null) return;

            var audio = AudioManager.GetInstanceSafe();
            if (audio != null)
            {
                audio.SetMasterVolume(settings.masterVolume);
                audio.SetBgmVolume(settings.bgmVolume);
                audio.SetSfxVolume(settings.sfxVolume);
            }
            // 초기화된 언어는 「미설정」이다. 옛 PlayerPrefs 언어가 남아 있으면 ReloadLanguageFromSave가 그것을 다시
            // 이관(= 두 번째 저장)해 방금 지운 선택이 되살아난다. 사용자가 설정 초기화를 확정·저장 성공한 뒤에만,
            // 그 키 하나만 지운다(PlayerPrefs 전체는 건드리지 않는다). 키가 없으면 PlayerPrefs에 쓰지 않는다.
            if (PlayerPrefs.HasKey(LocalizationManager.LegacyLanguagePrefKey))
            {
                PlayerPrefs.DeleteKey(LocalizationManager.LegacyLanguagePrefKey);
                PlayerPrefs.Save();
            }
            LocalizationManager.GetInstanceSafe()?.ReloadLanguageFromSave();
            GameplayKeyBindings.ReloadFromSettings();
            ApplyScreenAfterReset(settings);
        }

        /// <summary>새 기본값(0×0 = 미설정)이면 현재 디스플레이의 기본 해상도로, 그것도 없으면 지금 창 크기로 맞춘다.</summary>
        private static void ApplyScreenAfterReset(MetaSettings settings)
        {
            int width = settings.screenWidth;
            int height = settings.screenHeight;
            if (width <= 0 || height <= 0)
            {
                var native = Screen.currentResolution;
                width = native.width > 0 ? native.width : Screen.width;
                height = native.height > 0 ? native.height : Screen.height;
            }
            if (width <= 0 || height <= 0) return;
            var mode = settings.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(width, height, mode);
        }
    }
}
