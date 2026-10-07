using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    public sealed partial class FormSlotPresenter
    {
        private sealed class WeaponBadge
        {
            public GameObject Root;
            public Image Back;
            public Image Icon;
            public Image FormIcon;
            public Text Level;
            public WeaponData ShownWeapon;
            public int ShownLevel;
            public string ShownForm;
            public float FlashRemaining;
        }

        private WeaponBadge currentWeaponBadge;
        private WeaponBadge otherWeaponBadge;
        private WeaponInventory shownInventory;
        private int shownWeaponVersion = -1;

        private void TickWeaponBadges()
        {
            var inventory = RunManager.HasInstance ? RunManager.Instance.Weapons : null;
            bool inventoryChanged = !ReferenceEquals(inventory, shownInventory);
            bool didGrant = !inventoryChanged && inventory != null && inventory.Version != shownWeaponVersion;
            shownInventory = inventory;
            shownWeaponVersion = inventory != null ? inventory.Version : -1;
            if (currentWeaponBadge == null && currentFormIcon != null) currentWeaponBadge = CreateWeaponBadge(currentFormIcon);
            if (otherWeaponBadge == null && otherFormIcon != null) otherWeaponBadge = CreateWeaponBadge(otherFormIcon);
            ApplyWeaponBadge(currentWeaponBadge, formController.CurrentForm, inventory, didGrant);
            ApplyWeaponBadge(otherWeaponBadge, formController.OtherForm, inventory, didGrant);
        }

        private static WeaponBadge CreateWeaponBadge(Image formIcon)
        {
            var go = new GameObject("EquippedWeaponBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(formIcon.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(32f, 32f);
            var back = go.GetComponent<Image>();
            back.raycastTarget = false;
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            ((RectTransform)iconGo.transform).sizeDelta = new Vector2(27f, 27f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var level = UiFactory.CreateLabel(go.transform, "Upgrade", new Vector2(0f, -9f),
                new Vector2(36f, 18f), string.Empty, 14, Color.white, TextAnchor.MiddleCenter);
            level.raycastTarget = false;
            level.gameObject.AddComponent<Outline>().effectColor = Color.black;
            return new WeaponBadge { Root = go, Back = back, Icon = icon, FormIcon = formIcon, Level = level };
        }

        private static void ApplyWeaponBadge(WeaponBadge badge, FormData form, WeaponInventory inventory, bool didGrant)
        {
            if (badge == null) return;
            var weapon = form != null ? inventory != null ? inventory.ResolveFor(form.formId, form.defaultWeapon) : form.defaultWeapon : null;
            int level = inventory != null ? inventory.UpgradeLevelOf(weapon) : 0;
            bool changed = badge.ShownForm == form?.formId &&
                (badge.ShownWeapon != weapon || badge.ShownLevel != level);
            badge.ShownForm = form != null ? form.formId : null;
            badge.ShownWeapon = weapon;
            badge.ShownLevel = level;
            badge.Root.SetActive(weapon != null && weapon.icon != null && badge.FormIcon.enabled);
            if (weapon == null || weapon.icon == null) { badge.FlashRemaining = 0f; return; }
            badge.Icon.sprite = weapon.icon;
            badge.Level.text = level > 0 ? $"+{level}" : string.Empty;
            if (didGrant && changed) badge.FlashRemaining = 0.4f;
            badge.FlashRemaining = Mathf.Max(0f, badge.FlashRemaining - Time.unscaledDeltaTime);
            badge.Back.color = Color.Lerp(new Color(0.08f, 0.08f, 0.12f, 0.9f),
                new Color(1f, 0.82f, 0.35f, 0.9f), badge.FlashRemaining / 0.4f);
        }

        private void ResetWeaponBadges()
        {
            ClearWeaponBadgeFlashes();
            shownInventory = null;
            shownWeaponVersion = -1;
        }

        private void ClearWeaponBadgeFlashes()
        {
            // 방/런 경계의 늦은 갱신을 새 획득으로 강조하지 않는다.
            shownInventory = RunManager.HasInstance ? RunManager.Instance.Weapons : null;
            shownWeaponVersion = shownInventory != null ? shownInventory.Version : -1;
            ClearWeaponBadgeFlash(currentWeaponBadge);
            ClearWeaponBadgeFlash(otherWeaponBadge);
        }

        private static void ClearWeaponBadgeFlash(WeaponBadge badge)
        {
            if (badge == null) return;
            badge.FlashRemaining = 0f;
            badge.Back.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
        }
    }
}
