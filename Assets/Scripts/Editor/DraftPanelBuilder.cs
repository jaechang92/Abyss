#if UNITY_EDITOR
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// DraftPanelPresenter가 붙은 GameObject 자식 UI를 일괄 생성·연결.
    /// 메뉴 경로는 <see cref="AbyssMenu.BuildDraftPanel"/>.
    /// </summary>
    public static class DraftPanelBuilder
    {
        private const string UndoLabel = "Build Draft Panel Children";

        [MenuItem(AbyssMenu.BuildDraftPanel)]
        public static void Build()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("DraftPanelBuilder", "DraftPanel 루트 GameObject를 선택하세요.", "확인");
                return;
            }

            var presenter = go.GetComponent<DraftPanelPresenter>();
            if (presenter == null)
            {
                EditorUtility.DisplayDialog("DraftPanelBuilder", $"'{go.name}'에 DraftPanelPresenter가 없습니다.", "확인");
                return;
            }

            if (go.transform.childCount > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "DraftPanelBuilder",
                    $"'{go.name}'의 자식 {go.transform.childCount}개를 모두 제거하고 재생성할까요?",
                    "재생성", "취소");
                if (!confirm) return;

                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
                }
            }

            Undo.RegisterCompleteObjectUndo(presenter, UndoLabel);

            var root = CreateRoot(go.transform);
            var (title, cards, rerollButton, rerollLabel, skipButton, skipLabel) = CreateMainPanel(root.transform);
            var buildContext = CreateBuildContextPanel(root.transform);

            var so = new SerializedObject(presenter);
            SetObject(so, "root", root);
            SetObject(so, "titleText", title);
            var cardsProp = so.FindProperty("cards");
            if (cardsProp != null)
            {
                cardsProp.arraySize = 3;
                for (int i = 0; i < 3; i++) cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }
            SetObject(so, "rerollButton", rerollButton);
            SetObject(so, "rerollLabel", rerollLabel);
            SetObject(so, "skipButton", skipButton);
            SetObject(so, "skipLabel", skipLabel);
            SetObject(so, "buildContext", buildContext);
            so.ApplyModifiedProperties();

            go.SetActive(true);
            root.SetActive(false);

            EditorUtility.SetDirty(presenter);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            Debug.Log("[DraftPanelBuilder] 완료 — 초기 상태 비활성 (OnDraftOptionsReady 시 자동 활성).");
            Selection.activeGameObject = go;
        }

        private static GameObject CreateRoot(Transform parent)
        {
            var root = CreateRect(parent, "DraftRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.72f);
            bg.raycastTarget = true;
            return root;
        }

        private static (Text title, SkillCardView[] cards, Button reroll, Text rerollLabel, Button skip, Text skipLabel) CreateMainPanel(Transform parent)
        {
            var panel = CreateRect(parent, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110, 0), new Vector2(1000, 620));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            var titleGo = CreateRect(panel.transform, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 60));
            var title = titleGo.AddComponent<Text>();
            ApplyFont(title);
            title.text = "레벨 업! — 스킬 1개 선택";
            title.fontSize = 24;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.92f, 0.75f);

            var cards = new SkillCardView[3];
            float startX = -320f;
            for (int i = 0; i < 3; i++)
            {
                cards[i] = CreateCard(panel.transform, $"Card{i}", new Vector2(startX + i * 320f, 10));
            }

            var rerollGo = CreateButtonBlock(panel.transform, "RerollButton", new Vector2(-140, -260), new Vector2(240, 60), "리롤");
            var skipGo = CreateButtonBlock(panel.transform, "SkipButton", new Vector2(140, -260), new Vector2(240, 60), "스킵");

            return (title, cards, rerollGo.button, rerollGo.label, skipGo.button, skipGo.label);
        }

        private static SkillCardView CreateCard(Transform parent, string name, Vector2 anchoredPosition)
        {
            var card = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(280, 440));
            var bg = card.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.2f, 0.22f);

            var btn = card.AddComponent<Button>();
            btn.targetGraphic = bg;

            var iconGo = CreateRect(card.transform, "Icon", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(120, 120));
            var icon = iconGo.AddComponent<Image>();
            icon.color = new Color(0.35f, 0.35f, 0.4f);

            var nameGo = CreateRect(card.transform, "Name", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(240, 32));
            var nameText = nameGo.AddComponent<Text>();
            ApplyFont(nameText);
            nameText.text = "이름";
            nameText.fontSize = 18;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;

            var rcGo = CreateRect(card.transform, "RarityCategory", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -205), new Vector2(240, 24));
            var rcText = rcGo.AddComponent<Text>();
            ApplyFont(rcText);
            rcText.text = "희귀도 · 카테고리 · [태그]";
            rcText.fontSize = 12;
            rcText.alignment = TextAnchor.MiddleCenter;
            rcText.color = new Color(1f, 0.85f, 0.6f);

            var descGo = CreateRect(card.transform, "Description", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(240, 120));
            var descText = descGo.AddComponent<Text>();
            ApplyFont(descText);
            descText.text = "설명";
            descText.fontSize = 12;
            descText.alignment = TextAnchor.UpperCenter;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.color = new Color(0.9f, 0.9f, 0.9f);

            var formulaGo = CreateRect(card.transform, "Formula", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 24));
            var formulaText = formulaGo.AddComponent<Text>();
            ApplyFont(formulaText);
            formulaText.text = "";
            formulaText.fontSize = 10;
            formulaText.alignment = TextAnchor.MiddleCenter;
            formulaText.color = new Color(0.65f, 0.65f, 0.65f);

            var view = card.AddComponent<SkillCardView>();
            var so = new SerializedObject(view);
            SetObject(so, "background", bg);
            SetObject(so, "iconImage", icon);
            SetObject(so, "nameText", nameText);
            SetObject(so, "rarityCategoryText", rcText);
            SetObject(so, "descriptionText", descText);
            SetObject(so, "formulaText", formulaText);
            SetObject(so, "selectButton", btn);
            so.ApplyModifiedProperties();

            return view;
        }

        private struct ButtonHandle
        {
            public Button button;
            public Text label;
        }

        private static ButtonHandle CreateButtonBlock(Transform parent, string name, Vector2 position, Vector2 size, string text)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.32f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = CreateRect(go.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)textGo.transform);
            var label = textGo.AddComponent<Text>();
            ApplyFont(label);
            label.text = text;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            return new ButtonHandle { button = btn, label = label };
        }

        private static BuildContextPanel CreateBuildContextPanel(Transform parent)
        {
            var root = CreateRect(parent, "BuildContextPanel", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(240, 520));
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            var titleGo = CreateRect(root.transform, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(220, 32));
            var title = titleGo.AddComponent<Text>();
            ApplyFont(title);
            title.text = "현재 빌드";
            title.fontSize = 18;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;

            var synergyGo = CreateRect(root.transform, "SynergyCounts", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(220, 32));
            var synergyText = synergyGo.AddComponent<Text>();
            ApplyFont(synergyText);
            synergyText.text = "시너지: —";
            synergyText.fontSize = 12;
            synergyText.alignment = TextAnchor.MiddleCenter;
            synergyText.color = new Color(1f, 0.85f, 0.6f);

            var listGo = CreateRect(root.transform, "SkillList", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var listRect = (RectTransform)listGo.transform;
            listRect.offsetMin = new Vector2(16, 16);
            listRect.offsetMax = new Vector2(-16, -110);
            var listText = listGo.AddComponent<Text>();
            ApplyFont(listText);
            listText.text = "보유 스킬 없음";
            listText.fontSize = 12;
            listText.alignment = TextAnchor.UpperLeft;
            listText.horizontalOverflow = HorizontalWrapMode.Wrap;
            listText.verticalOverflow = VerticalWrapMode.Truncate;
            listText.color = new Color(0.9f, 0.9f, 0.9f);

            var panel = root.AddComponent<BuildContextPanel>();
            var so = new SerializedObject(panel);
            SetObject(so, "titleText", title);
            SetObject(so, "synergyCountsText", synergyText);
            SetObject(so, "skillListText", listText);
            so.ApplyModifiedProperties();

            return panel;
        }

        // ==================== Helpers ====================
        private static GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
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

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
#endif
