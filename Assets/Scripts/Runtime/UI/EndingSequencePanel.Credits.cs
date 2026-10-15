using UnityEngine;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 엔딩 크레딧 단계. 배치·스크롤은 <see cref="EndingCreditsReadability"/>가 맡고,
    /// 보조 컴포넌트가 없으면 원래(고정 해상도 기준) 동작으로 물러선다.
    /// </summary>
    public sealed partial class EndingSequencePanel
    {
        private void BeginCredits()
        {
            phase = Phase.Credits;
            phaseTimer = 0f;
            subtitles?.Clear();

            if (creditsText == null || creditsRect == null)
            {
                BeginStats();
                return;
            }

            creditsText.gameObject.SetActive(true);

            // 마스크 창 아래에서 시작 — 글자 배율·실제 캔버스 크기 기준. 다시 재생해도 처음 자리로.
            if (creditsReadability != null)
            {
                creditsReadability.BeginCredits();
                creditsHeight = creditsReadability.CreditsHeight;
                return;
            }

            // 폭이 확정된 뒤에야 줄바꿈이 반영된 높이를 얻을 수 있다.
            creditsHeight = creditsText.preferredHeight;
            if (creditsHeight <= 0f) creditsHeight = CREDITS_FALLBACK_HEIGHT;
            creditsRect.sizeDelta = new Vector2(CREDITS_WIDTH, creditsHeight);

            // 화면 아래에서 시작해 위로 완전히 빠져나갈 때까지 올린다.
            creditsRect.anchoredPosition = new Vector2(0f, -(ReferenceResolution.y * 0.5f + creditsHeight * 0.5f));
        }

        private void TickCredits()
        {
            if (creditsRect == null)
            {
                BeginStats();
                return;
            }

            if (creditsReadability != null)
            {
                // 이동·완료 판정 전에 배율·크기 변화를 반영한다(진행 비율 유지).
                creditsReadability.RefreshIfNeeded();
                bool isDone = creditsReadability.Advance(Time.unscaledDeltaTime);
                creditsHeight = creditsReadability.CreditsHeight;
                if (isDone) BeginStats();
                return;
            }

            var pos = creditsRect.anchoredPosition;
            pos.y += CREDITS_SCROLL_SPEED * Time.unscaledDeltaTime;
            creditsRect.anchoredPosition = pos;

            if (pos.y >= ReferenceResolution.y * 0.5f + creditsHeight * 0.5f) BeginStats();
        }
    }
}
