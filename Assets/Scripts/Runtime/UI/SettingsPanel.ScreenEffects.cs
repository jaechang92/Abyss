using Abyss.Runtime.Feedback;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;
using static Abyss.Runtime.UI.SettingsPanelLayout;

namespace Abyss.Runtime.UI
{
    public sealed partial class SettingsPanel
    {
        private Text shakeValue;
        private Button shakePrev;
        private Button shakeNext;
        private Toggle bossFlashToggle;

        private void BuildScreenEffects(Transform parent)
        {
            shakeValue = CreateSelectorRow(parent, "CameraShake", RowY(7), StringKey.Settings_CameraShake,
                () => ShiftShake(-1), () => ShiftShake(1), out shakePrev, out shakeNext);
            CreateLocalizedLabel(parent, "BossFlashLabel", new Vector2(-80, RowY(8)), new Vector2(360, 30),
                StringKey.Settings_ReduceBossFlashes, 17, Color.white, TextAnchor.MiddleLeft);
            var go = CreateRect(parent, "BossFlashToggle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(190, RowY(8)), new Vector2(28, 28));
            bossFlashToggle = go.AddComponent<Toggle>();
            var background = go.AddComponent<Image>();
            background.color = new Color(0.22f, 0.22f, 0.28f);
            var check = CreateRect(go.transform, "Checkmark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16)).AddComponent<Image>();
            check.color = new Color(0.55f, 0.45f, 0.9f);
            bossFlashToggle.targetGraphic = background;
            bossFlashToggle.graphic = check;
            bossFlashToggle.onValueChanged.AddListener(value =>
            {
                ScreenEffectPreferences.TrySet(ScreenEffectPreferences.ShakeScale, value);
                SyncScreenEffects();
            });
            BuildReadability(parent);
        }

        private void ShiftShake(int direction)
        {
            float current = ScreenEffectPreferences.ShakeScale;
            float next = direction < 0 ? (current > 0.5f ? 0.5f : 0f) : (current < 0.5f ? 0.5f : 1f);
            ScreenEffectPreferences.TrySet(next, ScreenEffectPreferences.ReduceBossFlashes);
            SyncScreenEffects();
        }

        private void SyncScreenEffects()
        {
            float scale = ScreenEffectPreferences.ShakeScale;
            if (shakeValue != null) shakeValue.text = ToPercent(scale);
            if (shakePrev != null) shakePrev.interactable = scale > 0f;
            if (shakeNext != null) shakeNext.interactable = scale < 1f;
            if (bossFlashToggle != null) bossFlashToggle.SetIsOnWithoutNotify(ScreenEffectPreferences.ReduceBossFlashes);
            SyncReadability();
            RefreshNavigation();
        }
    }
}
