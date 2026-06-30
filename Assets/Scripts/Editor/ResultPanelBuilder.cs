#if UNITY_EDITOR
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// ResultPanelPresenter가 붙은 GameObject 자식 UI를 일괄 생성·연결.
    /// 메뉴 경로는 <see cref="AbyssMenu.BuildResultPanel"/>.
    /// </summary>
    public static class ResultPanelBuilder
    {
        private const string UndoLabel = "Build Result Panel Children";

        [MenuItem(AbyssMenu.BuildResultPanel)]
        public static void Build()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("ResultPanelBuilder", "ResultPanel 루트 GameObject를 선택하세요.", "확인");
                return;
            }

            var presenter = go.GetComponent<ResultPanelPresenter>();
            if (presenter == null)
            {
                EditorUtility.DisplayDialog("ResultPanelBuilder", $"'{go.name}'에 ResultPanelPresenter가 없습니다.", "확인");
                return;
            }

            if (go.transform.childCount > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "ResultPanelBuilder",
                    $"'{go.name}'의 자식 {go.transform.childCount}개를 제거하고 재생성할까요?",
                    "재생성", "취소");
                if (!confirm) return;
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
                }
            }

            Undo.RegisterCompleteObjectUndo(presenter, UndoLabel);

            var root = CreateRoot(go.transform);
            var panel = CreatePanel(root.transform);
            var title = CreateText(panel.transform, "Title", "런 종료", 28, 0f, new Vector2(0, -40), new Vector2(700, 48), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));

            float y = -110;
            float spacing = 40f;
            var kills = CreateStatLine(panel.transform, "Kills", ref y, spacing);
            var combo = CreateStatLine(panel.transform, "Combo", ref y, spacing);
            var dominant = CreateStatLine(panel.transform, "DominantForm", ref y, spacing);
            var forms = CreateStatLine(panel.transform, "FormsUsed", ref y, spacing);
            var skills = CreateStatLine(panel.transform, "Skills", ref y, spacing);
            var stage = CreateStatLine(panel.transform, "Stage", ref y, spacing);
            var elapsed = CreateStatLine(panel.transform, "Elapsed", ref y, spacing);
            var abyss = CreateStatLine(panel.transform, "AbyssEarned", ref y, spacing);

            var (restartButton, restartLabel) = CreateButton(
                panel.transform, "RestartButton", new Vector2(-160, 50), new Vector2(300, 72),
                new Color(0.25f, 0.4f, 0.25f), "즉시 재시작 (Enter)");
            var (lobbyButton, lobbyLabel) = CreateButton(
                panel.transform, "LobbyButton", new Vector2(160, 50), new Vector2(300, 72),
                new Color(0.25f, 0.3f, 0.45f), "로비로");

            var so = new SerializedObject(presenter);
            SetObject(so, "root", root);
            SetObject(so, "titleText", title);
            SetObject(so, "killsText", kills);
            SetObject(so, "comboText", combo);
            SetObject(so, "dominantFormText", dominant);
            SetObject(so, "formsUsedText", forms);
            SetObject(so, "skillsText", skills);
            SetObject(so, "stageText", stage);
            SetObject(so, "elapsedText", elapsed);
            SetObject(so, "abyssEarnedText", abyss);
            SetObject(so, "restartButton", restartButton);
            SetObject(so, "restartLabel", restartLabel);
            SetObject(so, "lobbyButton", lobbyButton);
            SetObject(so, "lobbyLabel", lobbyLabel);
            so.ApplyModifiedProperties();

            root.SetActive(false);
            EditorUtility.SetDirty(presenter);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            Debug.Log("[ResultPanelBuilder] 완료 — 7항목 + Abyss 획득 라인 + 재시작 버튼 구성, 초기 상태 비활성.");
            Selection.activeGameObject = go;
        }

        private static GameObject CreateRoot(Transform parent)
        {
            var go = CreateRect(parent, "ResultRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)go.transform);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);
            bg.raycastTarget = true;
            return go;
        }

        private static GameObject CreatePanel(Transform parent)
        {
            var go = CreateRect(parent, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 560));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.14f, 0.98f);
            return go;
        }

        private static Text CreateStatLine(Transform parent, string name, ref float y, float spacing)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(640, 34));
            var t = go.AddComponent<Text>();
            ApplyFont(t);
            t.text = name + ": —";
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.white;
            y -= spacing;
            return t;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, float _, Vector2 pos, Vector2 rectSize, TextAnchor anchor, Color color)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, rectSize);
            var t = go.AddComponent<Text>();
            ApplyFont(t);
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            return t;
        }

        private static (Button button, Text label) CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, Color baseColor, string labelText)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), pos, size);
            var img = go.AddComponent<Image>();
            img.color = baseColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.35f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            btn.colors = colors;

            var labelGo = CreateRect(go.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)labelGo.transform);
            var label = labelGo.AddComponent<Text>();
            ApplyFont(label);
            label.text = labelText;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            return (btn, label);
        }

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
