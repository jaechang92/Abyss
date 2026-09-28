using UnityEngine;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 맵 방 클리어 표시(A2) — 화면 위쪽 가운데에 CLEAR를 약 0.9초 띄운다.
    ///
    /// 씬 배선 없이 처음 쓸 때 코드로 만든다(BossPresenter와 같은 동적 생성 규약). Run 씬 전용이라
    /// DontDestroyOnLoad 하지 않는다 — 씬이 바뀌면 파괴되고 다음 클리어 때 다시 만든다.
    /// 조작·레이캐스트를 막지 않는다(CanvasGroup·라벨 모두 레이캐스트 끔). 클리어 슬로 위에서도 제 시간에
    /// 사라지도록 실시간으로 잰다. 문구는 호출자가 현지화 경로(Loc)로 넘긴다.
    /// </summary>
    public sealed class RoomClearBanner : MonoBehaviour
    {
        private const float SHOW_SECONDS = 0.9f;
        private const float FADE_IN_SECONDS = 0.1f;
        private const float FADE_OUT_SECONDS = 0.3f;
        private const float PUNCH_SCALE = 1.25f;

        private static readonly Color LabelColor = new Color(1f, 0.9f, 0.62f);

        private static RoomClearBanner instance;

        private CanvasGroup group;
        private Text label;
        private float shownAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        /// <summary>문구를 띄운다. 이미 떠 있으면 처음부터 다시 보인다.</summary>
        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureInstance().Begin(text);
        }

        /// <summary>
        /// 떠 있으면 즉시 숨긴다 — 방 이동·런 종료·디렉터 비활성화에서 부른다. 없으면 만들지 않는다.
        /// </summary>
        public static void Hide()
        {
            if (instance != null) instance.HideNow();
        }

        private static RoomClearBanner EnsureInstance()
        {
            if (instance != null) return instance;

            // 보스 HUD와 같은 층 — CLEAR는 보스 방에서 뜨지 않아 겹치지 않고, 모달(100)보다 아래라 정지 화면을 가리지 않는다.
            var go = CreateOverlayCanvas("RoomClearBanner", UiSortingOrder.BossHud);
            instance = go.AddComponent<RoomClearBanner>();
            instance.BuildUI(go.transform);
            return instance;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // 꺼질 때 표시를 버린다 — 다시 켜졌을 때 남은 시간으로 오래된 문구가 되살아나지 않게.
        // 다음 표시는 반드시 Show(새 문구)로만 시작한다.
        private void OnDisable() => HideNow();

        /// <summary>
        /// 즉시 숨긴다. 투명도만 0으로 내리고 <c>SetActive</c>는 하지 않는다 — 이 메서드는 OnDisable·씬 정리 중
        /// 다른 오브젝트의 OnDisable에서도 불리는데, 비활성화 콜백 안에서 계층을 바꾸면 Unity가 거부한다.
        /// 레이캐스트는 원래 막지 않으므로 투명한 채 남아 있어도 조작에 영향이 없다.
        /// </summary>
        private void HideNow()
        {
            shownAt = -1f;
            if (group != null) group.alpha = 0f;
        }

        private void Begin(string text)
        {
            // 꺼진 상태에서 받은 표시는 버린다 — 나중에 켜질 때 그 문구가 뒤늦게 뜨지 않게.
            if (!isActiveAndEnabled) return;
            label.text = text;
            group.alpha = 0f;
            group.gameObject.SetActive(true);
            shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (shownAt < 0f) return;

            float elapsed = Time.unscaledTime - shownAt;
            if (elapsed >= SHOW_SECONDS)
            {
                shownAt = -1f;
                group.gameObject.SetActive(false);
                return;
            }

            float fadeOutStart = SHOW_SECONDS - FADE_OUT_SECONDS;
            group.alpha = elapsed < FADE_IN_SECONDS
                ? elapsed / FADE_IN_SECONDS
                : elapsed < fadeOutStart ? 1f : Mathf.Clamp01(1f - (elapsed - fadeOutStart) / FADE_OUT_SECONDS);

            // 크게 찍혔다가 제자리로 — 짧은 강조.
            float punch = Mathf.Lerp(PUNCH_SCALE, 1f, Mathf.Clamp01(elapsed / (FADE_IN_SECONDS * 2f)));
            label.rectTransform.localScale = new Vector3(punch, punch, 1f);
        }

        private void BuildUI(Transform root)
        {
            var body = CreateRect(root, "Body", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 120));
            group = body.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            label = CreateLabel(body.transform, "Text", Vector2.zero, new Vector2(900, 120), string.Empty, 72, LabelColor, TextAnchor.MiddleCenter);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            body.SetActive(false);
        }
    }
}
