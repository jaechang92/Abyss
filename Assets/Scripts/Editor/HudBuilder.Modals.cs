#if UNITY_EDITOR
using Abyss.Runtime.Draft;
using Abyss.Runtime.Player;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// <see cref="HudBuilder"/>의 모달 생성 파트(500줄 규칙 분할).
    /// 스킬 교체 모달·폼 보상 모달과 이들이 공유하는 오버레이·버튼 헬퍼를 담는다.
    /// </summary>
    public static partial class HudBuilder
    {
        // 모달용 하위 Canvas 정렬 순위. 루트 Canvas 소속인 DraftPanel·ResultPanel(정렬 0)보다 위.
        private const int MODAL_SORTING_ORDER = 100;

        // ==================== Replacement Modal ====================
        private static ReplacementModal CreateReplacementModal(Transform parent)
        {
            var root = CreateRectChild(parent, "ReplacementModal", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.72f);
            bg.raycastTarget = true;

            var panel = CreateRectChild(root.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 320));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.98f);

            var titleGo = CreateRectChild(panel.transform, "IncomingText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(480, 40));
            var title = titleGo.AddComponent<Text>();
            ApplyDefaultFont(title);
            title.text = "획득: ???";
            title.fontSize = 20;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.95f, 0.75f);

            var slot0 = CreateModalButton(panel.transform, "Slot0Button", new Vector2(-120, 20), "슬롯 1");
            var slot1 = CreateModalButton(panel.transform, "Slot1Button", new Vector2(120, 20), "슬롯 2");
            var cancel = CreateModalButton(panel.transform, "CancelButton", new Vector2(0, -110), "취소");

            MakeModalOverlay(root);

            var presenter = root.AddComponent<ReplacementModal>();
            SetPrivateField(presenter, "root", root);
            SetPrivateField(presenter, "incomingText", title);
            SetPrivateFieldArray(presenter, "currentSlotButtons", new[] { slot0.button, slot1.button });
            SetPrivateFieldArray(presenter, "currentSlotLabels", new[] { slot0.label, slot1.label });
            SetPrivateField(presenter, "cancelButton", cancel.button);

            root.SetActive(false);
            return presenter;
        }

        private static FormReplacementModal CreateFormRewardModal(Transform parent)
        {
            var root = CreateRectChild(parent, "FormRewardModal", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.72f);
            bg.raycastTarget = true;

            var panel = CreateRectChild(root.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 320));
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.14f, 0.2f, 0.98f);

            var titleGo = CreateRectChild(panel.transform, "IncomingText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(480, 40));
            var title = titleGo.AddComponent<Text>();
            ApplyDefaultFont(title);
            title.text = "폼 획득: ???";
            title.fontSize = 20;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.75f, 0.9f, 1f);

            var slot0 = CreateModalButton(panel.transform, "Slot0Button", new Vector2(-120, 20), "슬롯 1");
            var slot1 = CreateModalButton(panel.transform, "Slot1Button", new Vector2(120, 20), "슬롯 2");
            var cancel = CreateModalButton(panel.transform, "CancelButton", new Vector2(0, -110), "취소");

            MakeModalOverlay(root);

            var presenter = root.AddComponent<FormReplacementModal>();
            SetPrivateField(presenter, "root", root);
            SetPrivateField(presenter, "incomingText", title);
            SetPrivateFieldArray(presenter, "currentSlotButtons", new[] { slot0.button, slot1.button });
            SetPrivateFieldArray(presenter, "currentSlotLabels", new[] { slot0.label, slot1.label });
            SetPrivateField(presenter, "cancelButton", cancel.button);

            root.SetActive(false);
            return presenter;
        }

        /// <summary>
        /// 모달 root를 항상 패널 위에 그리게 만든다.
        /// 모달은 HUD 자식이고 HUD는 Canvas의 첫 자식이라, 형제 순서만으로는 DraftPanel(둘째 자식)을
        /// 넘을 수 없다(같은 Canvas는 계층 순서대로 그린다). HUD를 뒤로 옮기는 건 답이 아니다 —
        /// DraftPanel이 평소 HUD를 덮는 것은 의도된 동작이다. 그래서 모달만 하위 Canvas로 분리해
        /// 정렬을 오버라이드한다. 중첩 Canvas의 그래픽은 부모 GraphicRaycaster가 잡지 못하므로
        /// 전용 Raycaster를 함께 붙여야 버튼 클릭이 동작한다.
        /// </summary>
        private static void MakeModalOverlay(GameObject root)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = MODAL_SORTING_ORDER;
            root.AddComponent<GraphicRaycaster>();
        }

        private struct ButtonHandle
        {
            public Button button;
            public Text label;
        }

        private static ButtonHandle CreateModalButton(Transform parent, string name, Vector2 position, string labelText)
        {
            var root = CreateRectChild(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(200, 72));
            var img = root.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.32f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.32f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.45f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.28f);
            button.colors = colors;

            var textGo = CreateRectChild(root.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            ApplyDefaultFont(text);
            text.text = labelText;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return new ButtonHandle { button = button, label = text };
        }
    }
}
#endif
