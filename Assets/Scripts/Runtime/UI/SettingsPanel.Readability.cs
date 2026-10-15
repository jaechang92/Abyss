using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;
using static Abyss.Runtime.UI.SettingsPanelLayout;

namespace Abyss.Runtime.UI
{
    public sealed partial class SettingsPanel
    {
        private Text textSizeValue;
        private Button textSizePrev;
        private Button textSizeNext;
        private Toggle narrativeContrastToggle;

        private void BuildReadability(Transform parent)
        {
            textSizeValue = CreateSelectorRow(parent, "NarrativeTextSize", RowY(9), StringKey.Settings_NarrativeTextSize,
                () => ShiftTextSize(-1), () => ShiftTextSize(1), out textSizePrev, out textSizeNext);
            CreateLocalizedLabel(parent, "NarrativeContrastLabel", new Vector2(-80, RowY(10)), new Vector2(360, 30),
                StringKey.Settings_NarrativeContrast, 17, Color.white, TextAnchor.MiddleLeft);
            var go = CreateRect(parent, "NarrativeContrastToggle", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(190, RowY(10)), new Vector2(28, 28));
            narrativeContrastToggle = go.AddComponent<Toggle>();
            var image = go.AddComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.28f);
            var check = CreateRect(go.transform, "Checkmark", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(16, 16)).AddComponent<Image>();
            check.color = new Color(0.55f, 0.45f, 0.9f);
            narrativeContrastToggle.targetGraphic = image;
            narrativeContrastToggle.graphic = check;
            narrativeContrastToggle.onValueChanged.AddListener(value =>
            {
                MetaSaveService.Instance.TrySaveReadability(NarrativeReadability.Scale, value);
                SyncReadability();
            });
            BuildMenuTextOptions(parent);
        }

        private void ShiftTextSize(int direction)
        {
            float next = Mathf.Clamp(NarrativeReadability.Scale + direction * 0.25f, 1f, 1.5f);
            MetaSaveService.Instance.TrySaveReadability(next, NarrativeReadability.HighContrast);
            SyncReadability();
        }

        private void SyncReadability()
        {
            float scale = NarrativeReadability.Scale;
            if (textSizeValue != null) textSizeValue.text = Mathf.RoundToInt(scale * 100f) + "%";
            if (textSizePrev != null) textSizePrev.interactable = scale > 1f;
            if (textSizeNext != null) textSizeNext.interactable = scale < 1.5f;
            if (narrativeContrastToggle != null) narrativeContrastToggle.SetIsOnWithoutNotify(NarrativeReadability.HighContrast);
            SyncMenuTextOptions();
            RefreshNavigation();
        }
    }
}
