using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// A2(2026-10-09) — HUD 요소별 전용 프레임 그림(체력·폼 칸·스킬 칸·통화). <see cref="Apply"/>의 각 요소 스킨이 부른다.
    ///
    /// 전용 그림이 있으면 그 요소의 석판 9-slice 프레임(ArtFrame)을 끄고 그림을 종횡비대로 붙인다.
    /// 그림이 없으면 false — 호출한 쪽이 기존 석판 프레임을 그대로 쓴다(대체 경로).
    /// 게이지·쿨다운은 Image 설정(Filled 방향·원점·fillAmount)을 건드리지 않고 범위만 프레임 개구부에 맞춘다.
    /// </summary>
    public static partial class HudArtSkin
    {
        private const string ART_FRAME_NAME = "ArtImageFrame";
        private const string CURRENCY_ICON_NAME = "ArtCurrencyIcon";
        private const float LABEL_PADDING = 4f;

        /// <summary>체력: 프레임은 막대 안에 맞추고 배경·Fill 은 프레임 개구부로. 글자는 프레임 위.</summary>
        private static bool TryApplyHealthArt(Transform root, Transform background, Transform fill, Text hpText)
        {
            if (root is not RectTransform rootRect) return false;
            if (!UiArtLibrary.TryGetLayout(UiArtKeys.HUD_PLAYER_HEALTH_FRAME, out var layout) || !layout.HasInner) return false;

            var frame = UiArtDecor.ApplyFitted(rootRect, ART_FRAME_NAME, UiArtKeys.HUD_PLAYER_HEALTH_FRAME, UiArtFit.Inside, out var placement);
            if (frame == null) return false;

            var inner = placement.ToTarget(layout.Inner);
            if (background is RectTransform backgroundRect) UiArtDecor.StretchTo(backgroundRect, rootRect, inner);
            if (fill is RectTransform fillRect) UiArtDecor.StretchTo(fillRect, rootRect, inner);

            PlaceBefore(frame.transform, hpText != null ? hpText.transform : null);
            UiArtDecor.Hide(root, FRAME_NAME);
            return true;
        }

        /// <summary>폼 칸: 아이콘 사각형 안에 맞춘 프레임을 맨 앞(쿨다운 위)에. 백플레이트는 그대로.</summary>
        private static bool TryApplyFormArt(Transform root, string prefix, Transform icon)
        {
            if (root is not RectTransform rootRect || icon is not RectTransform iconRect) return false;

            var frame = UiArtDecor.ApplyFittedOver(rootRect, prefix + ART_FRAME_NAME, iconRect,
                UiArtKeys.HUD_FORM_SLOT_FRAME, UiArtFit.Inside, out _);
            if (frame == null) return false;

            frame.transform.SetAsLastSibling();
            UiArtDecor.Hide(root, prefix + FRAME_NAME);
            return true;
        }

        /// <summary>스킬 칸: 프레임 개구부가 아이콘(쿨다운 칸과 같은 자리)을 감싼다. 이름 글자보다 뒤.</summary>
        private static bool TryApplySkillArt(Transform root, Transform icon, Text label)
        {
            if (root is not RectTransform rootRect || icon is not RectTransform iconRect) return false;

            var frame = UiArtDecor.ApplyFittedOver(rootRect, ART_FRAME_NAME, iconRect,
                UiArtKeys.HUD_SKILL_SLOT_FRAME, UiArtFit.AroundInner, out _);
            if (frame == null) return false;

            PlaceBefore(frame.transform, label != null ? label.transform : null);
            UiArtDecor.Hide(root, FRAME_NAME);
            return true;
        }

        /// <summary>
        /// 통화(골드): 프레임 왼쪽 칸에 골드 파편 아이콘, 오른쪽 개구부에 기존 글자. 배경은 두 칸을 합친 범위.
        /// 글자 색·문구는 GoldCounterPresenter 소유라 범위만 바꾼다.
        /// </summary>
        private static bool TryApplyCurrencyArt(Transform root, Transform background, Text label)
        {
            if (root is not RectTransform rootRect) return false;
            if (!UiArtLibrary.TryGetLayout(UiArtKeys.HUD_CURRENCY_FRAME, out var layout) || !layout.HasInner || !layout.HasSlot) return false;

            var frame = UiArtDecor.ApplyFitted(rootRect, ART_FRAME_NAME, UiArtKeys.HUD_CURRENCY_FRAME, UiArtFit.Inside, out var placement);
            if (frame == null) return false;

            var inner = placement.ToTarget(layout.Inner);
            var slot = placement.ToTarget(layout.Slot);

            if (background is RectTransform backgroundRect)
            {
                UiArtDecor.StretchTo(backgroundRect, rootRect, Rect.MinMaxRect(slot.xMin, inner.yMin, inner.xMax, inner.yMax));
            }

            if (label != null)
            {
                var textBox = Rect.MinMaxRect(inner.xMin + LABEL_PADDING, inner.yMin, inner.xMax - LABEL_PADDING, inner.yMax);
                UiArtDecor.StretchTo(label.rectTransform, rootRect, textBox);
            }

            var icon = UiArtDecor.ApplyIconInRect(rootRect, CURRENCY_ICON_NAME, UiArtKeys.CURRENCY_GOLD_SHARDS, slot, Color.white);
            PlaceBefore(frame.transform, label != null ? label.transform : null);
            if (icon != null) icon.transform.SetAsLastSibling(); // 아이콘은 프레임 칸 안 — 프레임 띠보다 앞

            UiArtDecor.Hide(root, FRAME_NAME);
            return true;
        }
    }
}
