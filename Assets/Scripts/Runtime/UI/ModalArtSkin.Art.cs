using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// A2(2026-10-09) — 선택 포커스 괄호 그림과 리롤 버튼 아이콘.
    ///
    /// 괄호는 기존 선택 표식 묶음(ArtFocus)의 일부라 <see cref="SelectableArtFeedback"/>이 <b>실제로 선택된 동안에만</b> 켠다 —
    /// 상시 표시가 없다. 괄호가 붙으면 같은 자리의 좌우 홈은 끈다(겹쳐 그리지 않는다).
    /// 드래프트 카드(화살 판)는 전용 선택 그림이 따로 있어 괄호를 넣지 않는다.
    /// </summary>
    public static partial class ModalArtSkin
    {
        private const string BRACKET_LEFT_NAME = "BracketLeft";
        private const string BRACKET_RIGHT_NAME = "BracketRight";
        private const float BRACKET_HEIGHT_RATIO = 0.6f;
        private const float BRACKET_MIN_HEIGHT = 48f;

        private const string REROLL_ICON_NAME = "ArtRerollIcon";
        private const float REROLL_ICON_SIZE = 40f;
        private const float REROLL_ICON_LEFT = 14f;
        private const float REROLL_TEXT_GAP = 6f;

        /// <summary>선택 표식 묶음 좌우 가장자리에 괄호(오른쪽 그림 · 왼쪽은 좌우 반전). 그림이 없으면 홈을 그대로 쓴다.</summary>
        private static void TryAddFocusBrackets(Transform group, Transform target, List<Image> marks, Image leftNotch, Image rightNotch)
        {
            var sprite = UiArtLibrary.Get(UiArtKeys.UI_FOCUS_BRACKET);
            if (sprite == null || !UiArtLibrary.TryGetLayout(UiArtKeys.UI_FOCUS_BRACKET, out var layout)) return;

            var targetSize = target is RectTransform rect ? UiArtDecor.SizeOf(rect) : Vector2.zero;
            float groupHeight = targetSize.y + FOCUS_OUTSET * 2f;
            if (groupHeight <= 0f) return;

            float height = Mathf.Min(groupHeight, Mathf.Max(BRACKET_MIN_HEIGHT, groupHeight * BRACKET_HEIGHT_RATIO));
            var size = new Vector2(height * layout.Aspect, height);

            marks.Add(Bracket(group, BRACKET_RIGHT_NAME, sprite, new Vector2(1f, 0.5f), size, mirrored: false));
            marks.Add(Bracket(group, BRACKET_LEFT_NAME, sprite, new Vector2(0f, 0.5f), size, mirrored: true));

            // 같은 자리의 홈은 괄호로 대신한다 — 표식 목록에서 빼고 투명하게 둔다.
            // 색까지 지우는 이유: DraftImageSkin 이 버튼 표식을 묶음의 자식 전부로 다시 연결해 홈을 켜더라도 보이지 않게.
            marks.Remove(leftNotch);
            marks.Remove(rightNotch);
            HideNotch(leftNotch);
            HideNotch(rightNotch);
        }

        private static void HideNotch(Image notch)
        {
            if (notch == null) return;
            notch.color = Color.clear;
            notch.enabled = false;
        }

        /// <summary>괄호 하나. 중심을 표식 묶음 가장자리에 둔다 — 절반만 바깥으로 나가 옆 카드·노드 간격 안에 머문다.</summary>
        private static Image Bracket(Transform group, string name, Sprite sprite, Vector2 anchor, Vector2 size, bool mirrored)
        {
            var image = GetOrCreateImage(group, name);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            SetRect(image.transform, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            image.transform.localRotation = Quaternion.identity;
            image.transform.localScale = mirrored ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            return image;
        }

        /// <summary>
        /// 드래프트 리롤 버튼 왼쪽에 리롤 아이콘. 글자 칸은 아이콘 칸만큼 오른쪽에서 시작한다(문구·비용·콜백은 Presenter 그대로).
        /// </summary>
        private static void ApplyRerollIcon(Button reroll, Text label)
        {
            if (reroll == null || reroll.transform is not RectTransform rect) return;

            var size = UiArtDecor.SizeOf(rect);
            var box = new Rect(-size.x * 0.5f + REROLL_ICON_LEFT, -REROLL_ICON_SIZE * 0.5f, REROLL_ICON_SIZE, REROLL_ICON_SIZE);
            var icon = UiArtDecor.ApplyIconInRect(rect, REROLL_ICON_NAME, UiArtKeys.COMMON_REROLL, box, Color.white);
            if (icon == null || label == null) return;

            var labelRect = label.rectTransform;
            if (labelRect.parent == rect && labelRect.anchorMin.x == 0f && labelRect.anchorMax.x == 1f)
            {
                labelRect.offsetMin = new Vector2(REROLL_ICON_LEFT + REROLL_ICON_SIZE + REROLL_TEXT_GAP, labelRect.offsetMin.y);
            }
        }
    }
}
