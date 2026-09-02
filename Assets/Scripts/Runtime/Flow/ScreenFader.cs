using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 씬 전환 시 화면을 검게 덮었다가 걷어내는 영속 페이드 오버레이.
    /// 씬에 배치하지 않고 런타임에 동적 생성한다(scene-flow 설계 원칙 — 영속 싱글톤은 동적 생성).
    /// Result 등 timeScale=0 상황에서도 동작하도록 unscaledDeltaTime으로 트윈한다.
    /// Coroutine 금지(ADR-002)라 Awaitable로 페이드한다.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        private const float DEFAULT_DURATION = 0.35f;  // 기본 페이드 시간(초)

        private CanvasGroup group;

        /// <summary>페이드 오버레이를 생성하고 DontDestroyOnLoad로 영속화한다. 초기 상태는 투명(alpha 0).</summary>
        public static ScreenFader Create()
        {
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UiSortingOrder.ScreenFade;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(go.transform, false);
            var image = overlay.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var fader = go.AddComponent<ScreenFader>();
            fader.group = group;
            return fader;
        }

        /// <summary>화면을 검게(alpha 1) 덮는다.</summary>
        public Awaitable FadeOutAsync(float duration = DEFAULT_DURATION) => FadeToAsync(1f, duration);

        /// <summary>덮인 화면을 투명(alpha 0)하게 걷어낸다.</summary>
        public Awaitable FadeInAsync(float duration = DEFAULT_DURATION) => FadeToAsync(0f, duration);

        private async Awaitable FadeToAsync(float target, float duration)
        {
            if (group == null) return;

            group.blocksRaycasts = true;  // 페이드 진행 중 입력 차단
            float start = group.alpha;

            if (duration <= 0f)
            {
                group.alpha = target;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                group.alpha = target;
            }

            // 완전히 투명해졌으면 입력을 통과시킨다
            group.blocksRaycasts = target > 0f;
        }
    }
}
