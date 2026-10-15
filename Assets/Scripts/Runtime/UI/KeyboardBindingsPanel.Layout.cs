using System.Collections.Generic;
using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    public sealed partial class KeyboardBindingsPanel
    {
        private const float ROW_HEIGHT = 56f;
        private readonly List<string> bindingIds = new();
        private readonly List<Button> keyButtons = new();
        private readonly List<Text> actionLabels = new();
        private readonly List<GameObject> rowObjects = new();
        private ScrollRect scroll;
        private RectTransform content;
        private Button resetButton;
        private Button backButton;
        private Text statusLabel;
        private string statusKey;
        private GameObject lastScrollSelection;

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root);
            var panel = CreateRect(body.transform, "Panel", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(760f, 790f));
            panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.15f, 1f);
            CreateLocalizedLabel(panel.transform, "Title", new Vector2(0f, 350f), new Vector2(690f, 42f),
                StringKey.Settings_Keyboard, 26, Color.white, TextAnchor.MiddleCenter);
            statusLabel = CreateLabel(panel.transform, "Status", new Vector2(0f, 284f), new Vector2(680f, 80f),
                "", 22, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleCenter);
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            var viewport = CreateRect(panel.transform, "Viewport", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, new Vector2(0f, -12f), new Vector2(700f, 440f));
            viewport.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 1f);
            viewport.AddComponent<RectMask2D>();
            var contentObject = CreateRect(viewport.transform, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(680f, 0f));
            content = (RectTransform)contentObject.transform;
            scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 35f;

            resetButton = CreateLocalizedButton(panel.transform, "Reset", new Vector2(-170f, -308f),
                new Vector2(300f, 54f), StringKey.Keys_Reset, 22);
            resetButton.onClick.AddListener(ResetBindings);
            backButton = CreateLocalizedButton(panel.transform, "Back", new Vector2(170f, -308f),
                new Vector2(300f, 54f), StringKey.Common_Back, 22);
            backButton.onClick.AddListener(RequestBack);
            CreateLocalizedLabel(panel.transform, "CancelGuide", new Vector2(0f, -366f), new Vector2(700f, 34f),
                StringKey.Keys_CancelGuide, 18, new Color(0.72f, 0.72f, 0.82f), TextAnchor.MiddleCenter);
        }

        private void RefreshRows()
        {
            if (content == null) return;
            var rows = GameplayKeyBindings.Rows;
            bool rebuild = rows.Count != bindingIds.Count;
            if (!rebuild)
                for (int i = 0; i < rows.Count; i++) rebuild |= rows[i].BindingId != bindingIds[i];
            if (rebuild)
            {
                foreach (var row in rowObjects)
                {
                    row.SetActive(false);
                    Destroy(row);
                }
                rowObjects.Clear();
                bindingIds.Clear();
                keyButtons.Clear();
                actionLabels.Clear();
                for (int i = 0; i < rows.Count; i++)
                {
                    string id = rows[i].BindingId;
                    var row = CreateRect(content, "BindingRow" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        Vector2.one * 0.5f, new Vector2(0f, -(i + 0.5f) * ROW_HEIGHT), new Vector2(660f, ROW_HEIGHT));
                    var label = CreateLabel(row.transform, "Action", new Vector2(-130f, 0f), new Vector2(370f, 44f),
                        "", 22, Color.white, TextAnchor.MiddleLeft);
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    var button = CreateButton(row.transform, "Key", new Vector2(205f, 0f), new Vector2(240f, 44f), "", 22);
                    button.onClick.AddListener(() => BeginCapture(id));
                    bindingIds.Add(id);
                    rowObjects.Add(row);
                    actionLabels.Add(label);
                    keyButtons.Add(button);
                }
                content.sizeDelta = new Vector2(680f, rows.Count * ROW_HEIGHT);
                lastScrollSelection = null;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                actionLabels[i].text = ActionLabel(rows[i].ActionName, rows[i].PartName);
                keyButtons[i].GetComponentInChildren<Text>().text = rows[i].DisplayKey;
            }
            SetRowsInteractable(!IsCapturing);
            ConfigureNavigation();
            if (rows.Count == 0) SetStatus(StringKey.Keys_Unavailable);
        }

        private void SetRowsInteractable(bool enabled)
        {
            foreach (var button in keyButtons) button.interactable = enabled;
            if (resetButton != null) resetButton.interactable = enabled && keyButtons.Count > 0;
        }

        private void ConfigureNavigation()
        {
            for (int i = 0; i < keyButtons.Count; i++)
            {
                keyButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? keyButtons[i - 1] : null,
                    selectOnDown = i + 1 < keyButtons.Count ? keyButtons[i + 1] : resetButton
                };
            }
            Selectable last = keyButtons.Count > 0 ? keyButtons[^1] : null;
            resetButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnRight = backButton };
            backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = last, selectOnLeft = resetButton };
        }

        private void SelectFirst()
        {
            if (EventSystem.current == null || SaveStatusOverlay.IsCapturingInput) return;
            var target = captureSelection != null && captureSelection.activeInHierarchy
                ? captureSelection : keyButtons.Count > 0 ? keyButtons[0].gameObject : backButton.gameObject;
            captureSelection = null;
            EventSystem.current.SetSelectedGameObject(target);
        }

        private void KeepFocusInside()
        {
            var events = EventSystem.current;
            if (events == null) return;
            if (!IsInside(events.currentSelectedGameObject)) SelectFirst();
            var selected = events.currentSelectedGameObject;
            if (selected == lastScrollSelection) return;
            lastScrollSelection = selected;
            for (int i = 0; i < keyButtons.Count; i++)
            {
                if (keyButtons[i].gameObject != selected) continue;
                var row = (RectTransform)rowObjects[i].transform;
                float top = -row.anchoredPosition.y - row.rect.height * 0.5f;
                float bottom = top + row.rect.height;
                float current = content.anchoredPosition.y;
                float height = scroll.viewport.rect.height;
                if (top < current) current = top;
                else if (bottom > current + height) current = bottom - height;
                content.anchoredPosition = new Vector2(0f, Mathf.Clamp(current, 0f, Mathf.Max(0f, content.rect.height - height)));
                break;
            }
        }

        private bool IsInside(GameObject selected) => selected != null && body != null && selected.transform.IsChildOf(body.transform);

        private void SetStatus(string key)
        {
            statusKey = key;
            if (statusLabel != null) statusLabel.text = Loc.Get(key);
        }

        private void ShowCapturePrompt()
        {
            if (statusLabel == null) return;
            foreach (var row in GameplayKeyBindings.Rows)
            {
                if (row.BindingId != captureBindingId) continue;
                statusLabel.text = Loc.GetFormat(StringKey.Keys_Capture, ActionLabel(row.ActionName, row.PartName));
                return;
            }
        }

        private static string ActionLabel(string action, string part)
        {
            string key = action switch
            {
                "Move" => (part ?? "").ToLowerInvariant() switch
                {
                    "up" => StringKey.Keys_MoveUp,
                    "down" => StringKey.Keys_MoveDown,
                    "left" => StringKey.Keys_MoveLeft,
                    "right" => StringKey.Keys_MoveRight,
                    _ => StringKey.Keys_Move
                },
                "Jump" => StringKey.Keys_Jump,
                "Dash" => StringKey.Keys_Dash,
                "Attack" => StringKey.Keys_Attack,
                "AttackHeavy" => StringKey.Keys_AttackHeavy,
                "Skill1" => StringKey.Keys_Skill1,
                "Skill2" => StringKey.Keys_Skill2,
                "FormSwap" => StringKey.Keys_FormSwap,
                "Interact" => StringKey.Keys_Interact,
                _ => null
            };
            return key != null ? Loc.Get(key) : action;
        }
    }
}
