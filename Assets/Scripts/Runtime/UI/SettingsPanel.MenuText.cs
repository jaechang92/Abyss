using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.SettingsPanelLayout;

namespace Abyss.Runtime.UI
{
    public sealed partial class SettingsPanel
    {
        private Text menuTextValue;
        private Button menuTextPrev;
        private Button menuTextNext;

        private void BuildMenuTextOptions(Transform parent)
        {
            menuTextValue = CreateSelectorRow(parent,"MenuTextSize",RowY(11),StringKey.Settings_MenuTextSize,
                () => ShiftMenuText(-1),() => ShiftMenuText(1),out menuTextPrev,out menuTextNext);
        }

        private void ShiftMenuText(int direction)
        {
            MetaSaveService.Instance.TrySaveMenuTextScale(Mathf.Clamp(MenuTextReadability.Scale+direction*0.25f,1f,1.5f));
            SyncMenuTextOptions();
            var readable = body.GetComponentInChildren<MenuTextReadability>(true);
            if (readable != null) readable.Apply();
        }

        private void SyncMenuTextOptions()
        {
            float scale = MenuTextReadability.Scale;
            if (menuTextValue != null) menuTextValue.text = Mathf.RoundToInt(scale*100f)+"%";
            if (menuTextPrev != null) menuTextPrev.interactable = scale > 1f;
            if (menuTextNext != null) menuTextNext.interactable = scale < 1.5f;
            RefreshNavigation();
        }
    }
}
