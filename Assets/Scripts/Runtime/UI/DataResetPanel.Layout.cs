using System.Collections.Generic;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 데이터 초기화 창의 조립과 세로 배치. 글자 크기(메뉴 글자 100·125·150%)와 언어에 따라 높이가 바뀌므로
    /// 좌표를 고정하지 않고 매 프레임 실제 글자 높이로 쌓는다. 묶음 목록·경고는 스크롤 영역 안에 있어
    /// 큰 글자에서도 창이 화면 밖으로 자라지 않는다(키보드 설정 창과 같은 방식).
    /// </summary>
    public sealed partial class DataResetPanel
    {
        private const float PANEL_WIDTH = 820f;
        private const float PANEL_HEIGHT = 800f;
        private const float VIEW_WIDTH = 760f;
        private const float ROW_WIDTH = 720f;
        private const float ROW_TEXT_WIDTH = 630f;
        private const float BUTTON_WIDTH = 360f;
        private const float MIN_BUTTON_HEIGHT = 52f;
        private const float PANEL_TOP = 388f;
        private const float PANEL_BOTTOM = -388f;

        private Text titleLabel;
        private Text introLabel;
        private Text summaryLabel;
        private Text warningLabel;
        private Text statusLabel;
        private Text guideLabel;
        private ScrollRect scroll;
        private RectTransform content;
        private readonly List<Toggle> groupToggles = new();
        private readonly List<RectTransform> groupRows = new();
        private readonly List<Text> groupNameLabels = new();
        private readonly List<Text> groupDescLabels = new();
        private Button selectAllButton;
        private Button fullButton;
        private Button nextButton;
        private Button cancelButton;
        private Button confirmButton;
        private Button backButton;
        private LocalizationLanguage? shownLanguage;

        private void BuildUI(Transform root)
        {
            body = CreateDimBody(root, 0.7f);
            var panel = CreateRect(body.transform, "Panel", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(PANEL_WIDTH, PANEL_HEIGHT));
            panel.AddComponent<Image>().color = new Color(0.11f, 0.09f, 0.13f, 1f);

            titleLabel = CreateWrapLabel(panel.transform, "Title", VIEW_WIDTH, 28, new Color(1f, 0.84f, 0.78f), TextAnchor.MiddleCenter);

            var viewport = CreateRect(panel.transform, "Viewport", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(VIEW_WIDTH, 400f));
            viewport.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 1f);
            viewport.AddComponent<RectMask2D>();
            content = (RectTransform)CreateRect(viewport.transform, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(VIEW_WIDTH - 20f, 0f)).transform;
            scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 35f;

            introLabel = CreateWrapLabel(content, "Intro", ROW_WIDTH, 20, new Color(0.86f, 0.86f, 0.94f), TextAnchor.UpperLeft);
            for (int i = 0; i < Groups.Length; i++) BuildGroupRow(i);
            summaryLabel = CreateWrapLabel(content, "Summary", ROW_WIDTH, 19, new Color(0.95f, 0.88f, 0.62f), TextAnchor.UpperLeft);
            warningLabel = CreateWrapLabel(content, "Warning", ROW_WIDTH, 21, new Color(1f, 0.72f, 0.66f), TextAnchor.UpperLeft);
            statusLabel = CreateWrapLabel(panel.transform, "Status", VIEW_WIDTH, 19, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter);
            guideLabel = CreateWrapLabel(panel.transform, "Guide", VIEW_WIDTH, 18, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleCenter);

            selectAllButton = CreatePanelButton(panel.transform, "SelectAll", ToggleSelectAll);
            fullButton = CreatePanelButton(panel.transform, "FullReset", RequestFullReset);
            nextButton = CreatePanelButton(panel.transform, "Next", RequestNext);
            cancelButton = CreatePanelButton(panel.transform, "Cancel", RequestBack);
            confirmButton = CreatePanelButton(panel.transform, "Confirm", ConfirmReset);
            backButton = CreatePanelButton(panel.transform, "Back", RequestBack);
        }

        private void BuildGroupRow(int index)
        {
            var row = CreateRect(content, "Group" + index, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(ROW_WIDTH, 60f));
            var background = row.AddComponent<Image>();
            background.color = Color.white;
            var toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            var colors = toggle.colors;
            colors.normalColor = new Color(0.15f, 0.14f, 0.20f);
            colors.highlightedColor = new Color(0.27f, 0.25f, 0.36f);
            colors.selectedColor = new Color(0.30f, 0.27f, 0.42f);
            colors.pressedColor = new Color(0.12f, 0.11f, 0.16f);
            toggle.colors = colors;

            var box = CreateRect(row.transform, "Box", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-ROW_WIDTH * 0.5f + 34f, -14f), new Vector2(30f, 30f));
            box.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f);
            var check = CreateRect(box.transform, "Checkmark", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(18f, 18f)).AddComponent<Image>();
            check.color = new Color(0.95f, 0.45f, 0.40f);
            toggle.graphic = check;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(value => SetGroup(index, value));

            var nameLabel = CreateWrapLabel(row.transform, "Name", ROW_TEXT_WIDTH, 21, Color.white, TextAnchor.UpperLeft);
            var descLabel = CreateWrapLabel(row.transform, "Desc", ROW_TEXT_WIDTH, 18, new Color(0.74f, 0.74f, 0.84f), TextAnchor.UpperLeft);
            groupRows.Add((RectTransform)row.transform);
            groupToggles.Add(toggle);
            groupNameLabels.Add(nameLabel);
            groupDescLabels.Add(descLabel);
        }

        /// <summary>위쪽 가운데 기준, 줄바꿈 라벨. 높이는 배치가 실제 글자 높이로 정한다.</summary>
        private static Text CreateWrapLabel(Transform parent, string name, float width, int fontSize, Color color, TextAnchor anchor)
        {
            var text = CreateLabel(parent, name, Vector2.zero, new Vector2(width, 30f), string.Empty, fontSize, color, anchor);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            if (parent.name == "Panel")
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            }
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            return text;
        }

        private Button CreatePanelButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateButton(parent, name, Vector2.zero, new Vector2(BUTTON_WIDTH, MIN_BUTTON_HEIGHT), string.Empty, 22);
            var label = button.GetComponentInChildren<Text>(true);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = false;
            button.onClick.AddListener(onClick);
            return button;
        }

        private void SyncToggles()
        {
            for (int i = 0; i < groupToggles.Count; i++)
            {
                groupToggles[i].SetIsOnWithoutNotify((selection & Groups[i]) == Groups[i]);
            }
        }

        /// <summary>단계·선택·상태에 맞춰 글자·표시·탐색·배치를 다시 맞춘다. 데이터는 건드리지 않는다.</summary>
        private void Refresh()
        {
            if (body == null) return;
            shownLanguage = LocalizationManager.GetInstanceSafe() != null ? Loc.CurrentLanguage : (LocalizationLanguage?)null;
            bool isSelect = stage == Stage.Select;

            titleLabel.text = Tr("Title");
            introLabel.text = Tr("Intro");
            introLabel.gameObject.SetActive(isSelect);
            for (int i = 0; i < groupRows.Count; i++)
            {
                groupRows[i].gameObject.SetActive(isSelect);
                groupNameLabels[i].text = Tr(GroupKey(Groups[i]));
                groupDescLabels[i].text = Tr(GroupKey(Groups[i]) + "_Desc");
            }
            summaryLabel.gameObject.SetActive(isSelect);
            summaryLabel.text = BuildSummaryText();
            warningLabel.gameObject.SetActive(!isSelect);
            warningLabel.text = isSelect ? string.Empty : BuildWarningText();
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(statusKey));
            statusLabel.text = string.IsNullOrEmpty(statusKey) ? string.Empty : Tr(statusKey);
            guideLabel.text = Tr("Guide");

            SetButton(selectAllButton, isSelect, selection == MetaResetCategory.All ? "SelectNone" : "SelectAll");
            SetButton(fullButton, isSelect, "FullReset");
            SetButton(nextButton, isSelect, "Next");
            nextButton.interactable = MetaSaveService.IsValidResetSelection(selection);
            SetButton(cancelButton, isSelect, "Cancel");
            SetButton(confirmButton, !isSelect, "Confirm");
            SetButton(backButton, !isSelect, "Back");

            ConfigureNavigation();
            LayoutNow();
        }

        private static void SetButton(Button button, bool isVisible, string key)
        {
            button.gameObject.SetActive(isVisible);
            button.GetComponentInChildren<Text>(true).text = Tr(key);
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            var language = LocalizationManager.GetInstanceSafe() != null ? Loc.CurrentLanguage : (LocalizationLanguage?)null;
            if (language != shownLanguage) Refresh();
            else LayoutNow();
        }

        /// <summary>실제 글자 높이로 위에서·아래에서 쌓고, 남는 가운데를 스크롤 영역으로 쓴다.</summary>
        private void LayoutNow()
        {
            if (content == null) return;
            float scale = MenuTextReadability.Scale;

            float titleHeight = Fit(titleLabel, 28, scale, 44f);
            Place(titleLabel, PANEL_TOP - titleHeight * 0.5f);
            float titleBottom = PANEL_TOP - titleHeight;

            float guideHeight = Fit(guideLabel, 18, scale, 30f);
            Place(guideLabel, PANEL_BOTTOM + guideHeight * 0.5f);
            float cursor = PANEL_BOTTOM + guideHeight + 12f;

            float buttonHeight = MIN_BUTTON_HEIGHT;
            foreach (var button in new[] { selectAllButton, fullButton, nextButton, cancelButton, confirmButton, backButton })
            {
                var label = button.GetComponentInChildren<Text>(true);
                label.fontSize = Mathf.RoundToInt(22f * scale);
                buttonHeight = Mathf.Max(buttonHeight, label.preferredHeight + 10f);
            }
            if (stage == Stage.Select)
            {
                PlaceButtonRow(nextButton, cancelButton, cursor, buttonHeight);
                cursor += buttonHeight + 10f;
                PlaceButtonRow(selectAllButton, fullButton, cursor, buttonHeight);
            }
            else
            {
                PlaceButtonRow(confirmButton, backButton, cursor, buttonHeight);
            }
            cursor += buttonHeight + 10f;

            if (statusLabel.gameObject.activeSelf)
            {
                float statusHeight = Fit(statusLabel, 19, scale, 28f);
                Place(statusLabel, cursor + statusHeight * 0.5f);
                cursor += statusHeight + 10f;
            }

            float viewportTop = titleBottom - 10f;
            float viewportBottom = cursor + 2f;
            float viewHeight = Mathf.Max(120f, viewportTop - viewportBottom);
            var viewport = scroll.viewport;
            viewport.sizeDelta = new Vector2(VIEW_WIDTH, viewHeight);
            viewport.anchoredPosition = new Vector2(0f, viewportTop - viewHeight * 0.5f);

            float offset = 12f;
            offset = Stack(introLabel, 20, scale, offset);
            for (int i = 0; i < groupRows.Count; i++) offset = StackRow(i, scale, offset);
            offset = Stack(summaryLabel, 19, scale, offset);
            offset = Stack(warningLabel, 21, scale, offset);
            content.sizeDelta = new Vector2(VIEW_WIDTH - 20f, offset + 2f);
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(content.anchoredPosition.y, 0f, Mathf.Max(0f, offset - viewHeight)));
        }

        private static float Fit(Text text, int baseSize, float scale, float minimumHeight)
        {
            text.fontSize = Mathf.RoundToInt(baseSize * scale);
            float height = Mathf.Max(minimumHeight, Mathf.Ceil(text.preferredHeight) + 2f);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height);
            return height;
        }

        private static void Place(Text text, float centerY) =>
            text.rectTransform.anchoredPosition = new Vector2(0f, centerY);

        private static void PlaceButtonRow(Button left, Button right, float bottom, float height)
        {
            float y = bottom + height * 0.5f;
            var leftRect = (RectTransform)left.transform;
            leftRect.sizeDelta = new Vector2(BUTTON_WIDTH, height);
            leftRect.anchoredPosition = new Vector2(-BUTTON_WIDTH * 0.5f - 10f, y);
            var rightRect = (RectTransform)right.transform;
            rightRect.sizeDelta = new Vector2(BUTTON_WIDTH, height);
            rightRect.anchoredPosition = new Vector2(BUTTON_WIDTH * 0.5f + 10f, y);
        }

        private static float Stack(Text text, int baseSize, float scale, float offset)
        {
            if (!text.gameObject.activeSelf) return offset;
            float height = Fit(text, baseSize, scale, 0f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -offset);
            return offset + height + 10f;
        }

        private float StackRow(int index, float scale, float offset)
        {
            var row = groupRows[index];
            if (!row.gameObject.activeSelf) return offset;
            var nameLabel = groupNameLabels[index];
            var descLabel = groupDescLabels[index];
            float nameHeight = Fit(nameLabel, 21, scale, 0f);
            float descHeight = Fit(descLabel, 18, scale, 0f);
            float textX = -ROW_WIDTH * 0.5f + 64f + ROW_TEXT_WIDTH * 0.5f;
            nameLabel.rectTransform.anchoredPosition = new Vector2(textX, -10f);
            descLabel.rectTransform.anchoredPosition = new Vector2(textX, -14f - nameHeight);
            float rowHeight = Mathf.Max(60f, 24f + nameHeight + descHeight);
            row.sizeDelta = new Vector2(ROW_WIDTH, rowHeight);
            row.anchoredPosition = new Vector2(0f, -offset);
            return offset + rowHeight + 8f;
        }
    }
}
