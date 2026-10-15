using Abyss.Runtime.ArtIntegration;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// A2(2026-10-09) — 보스 체력바 프레임과 보스 초상. 체력 비율·흔적·페이즈 눈금 계산은 BossPresenter.cs 그대로다.
    ///
    /// 프레임은 게이지(Back) 둘레를 개구부로 감싼다. 그림 띠가 이름과 겹치지 않도록 게이지를 조금 내리고,
    /// 자막을 프레임 아래로 옮긴다 — 프레임이 없으면(그림 누락) 기존 배치를 그대로 둔다.
    /// 초상은 <c>enemyId</c>(EnemyData) 로 고른다 — 수호자는 v2 채택본. 초상이 없는 보스는 초상 칸을 끈다.
    /// </summary>
    public sealed partial class BossPresenter
    {
        private const string BAR_FRAME_NAME = "ArtImageFrame";
        private const string BAR_PORTRAIT_NAME = "ArtBossPortrait";

        // 프레임이 붙었을 때의 배치(바 루트 중심 기준). 기존: 게이지 -14, 이름 +14, 자막 위 가장자리 -104.
        private const float FRAMED_BACK_Y = -37f;
        private const float NAME_GAP = 4f;
        private const float FRAMED_SUBTITLE_TOP = -142f;
        private const float PORTRAIT_SIZE = 80f;
        private const float PORTRAIT_GAP = 12f;

        /// <summary>BuildUI 끝에서 한 번. 게이지 프레임과 이름·자막 자리.</summary>
        private void ApplyBarArt(Transform root)
        {
            var back = barRoot.transform.Find("Back") as RectTransform;
            if (back == null) return;

            var savedPosition = back.anchoredPosition;
            back.anchoredPosition = new Vector2(savedPosition.x, FRAMED_BACK_Y);
            var frame = UiArtDecor.ApplyFitted(back, BAR_FRAME_NAME, UiArtKeys.HUD_BOSS_HEALTH_FRAME, UiArtFit.AroundInner, out var placement);
            if (frame == null)
            {
                back.anchoredPosition = savedPosition;
                return;
            }

            // 이름은 프레임 위 가장자리 바로 위.
            float frameTop = FRAMED_BACK_Y + placement.Center.y + placement.Size.y * 0.5f;
            var nameRect = nameLabel.rectTransform;
            nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, frameTop + NAME_GAP + nameRect.sizeDelta.y * 0.5f);

            if (root.Find("Subtitle") is RectTransform subtitle)
            {
                subtitle.anchoredPosition = new Vector2(subtitle.anchoredPosition.x, FRAMED_SUBTITLE_TOP);
            }
        }

        /// <summary>보스가 바뀔 때마다(PrepareBar 뒤). 게이지 왼쪽 바깥에 초상.</summary>
        private void ApplyBarPortrait(string enemyId)
        {
            var barRect = (RectTransform)barRoot.transform;
            var back = barRoot.transform.Find("Back") as RectTransform;
            float y = back != null ? back.anchoredPosition.y : 0f;
            float x = -BAR_WIDTH * 0.5f - PORTRAIT_GAP - PORTRAIT_SIZE * 0.5f;
            var box = new Rect(x - PORTRAIT_SIZE * 0.5f, y - PORTRAIT_SIZE * 0.5f, PORTRAIT_SIZE, PORTRAIT_SIZE);
            UiArtDecor.ApplyIconInRect(barRect, BAR_PORTRAIT_NAME, UiArtKeys.BossPortrait(enemyId), box, Color.white);
        }
    }
}
