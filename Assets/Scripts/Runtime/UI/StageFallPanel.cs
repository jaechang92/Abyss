using System.Collections.Generic;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 스테이지 사이 「아래로 떨어진다」 연출 — 어둠 속을 떨어지는 플레이어 + 위로 스치는 빛줄기 + 다음 스테이지 이름.
    /// 어비스는 층마다 다른 세계의 밑바닥이다(소설 §1-1) — 문을 지나 「더 깊이」 내려가는 순간을 한 번 보여 준다.
    ///
    /// 정지는 <see cref="RunModalPanel{T}"/> 규약(DraftOpen 차용). 정지 위에서 돌므로 전부 실시간으로 잰다.
    /// 월드 전환은 이 패널이 하지 않는다 — 호출자(StageDirector)가 검은 막 뒤에서 한다.
    /// 넘기기: Space·Enter·Z·패드 A(시작 0.3초 뒤부터).
    /// </summary>
    public sealed class StageFallPanel : RunModalPanel<StageFallPanel>
    {
        private const float DURATION_SECONDS = 2.6f;
        private const float INPUT_GRACE_SECONDS = 0.3f;
        private const float TITLE_DELAY_SECONDS = 0.5f;
        private const float TITLE_FADE_SECONDS = 0.6f;
        private const int STREAK_COUNT = 22;
        private const float SCREEN_HALF_HEIGHT = 540f;
        private const float SCREEN_HALF_WIDTH = 960f;
        private const float PLAYER_HEIGHT = 240f;

        private static readonly Color BackgroundColor = new Color(0.02f, 0.01f, 0.04f, 1f);
        private static readonly Color StreakColor = new Color(0.62f, 0.50f, 0.95f);
        private static readonly Color StageLabelColor = new Color(0.60f, 0.58f, 0.72f);
        private static readonly Color TitleColor = new Color(1f, 0.82f, 0.52f);

        private readonly List<RectTransform> streaks = new();
        private readonly List<float> streakSpeeds = new();
        private RectTransform playerRect;
        private Image playerImage;
        private CanvasGroup titleGroup;
        private Text stageLabel;
        private Text titleLabel;
        private bool isPlaying;
        private bool isFinishReported;
        private float startedAt;
        private AwaitableCompletionSource finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetInstance();

        /// <summary>
        /// 연출을 연다(정지). 정해진 길이가 지나거나 넘기기를 누르면 돌아온다 — <b>닫지는 않는다.</b>
        /// 호출자가 검은 막을 내린 뒤 <see cref="Close"/>한다: 여기서 닫으면 막이 내려오기 전 옛 화면이 한 프레임 보인다.
        /// 돌아온 뒤에도 닫힐 때까지 계속 떨어진다. <paramref name="playerSprite"/>가 없으면 빛줄기와 이름만 보인다.
        /// </summary>
        public static Awaitable PlayAsync(int stageNumber, string stageTitle, Sprite playerSprite, bool isFlipped)
        {
            var panel = EnsureInstance();
            if (!panel.isPlaying) panel.Begin(stageNumber, stageTitle, playerSprite, isFlipped);
            return panel.finished.Awaitable;
        }

        /// <summary>닫고 정지를 푼다.</summary>
        public static void Close()
        {
            var panel = EnsureInstance();
            if (!panel.isPlaying) return;
            panel.isPlaying = false;
            panel.ReportFinished();   // 넘기기 전에 닫혀도 기다리는 쪽이 멈추지 않게
            panel.HideBody();
        }

        protected override void BuildContent(Transform body)
        {
            var dim = body.GetComponent<Image>();
            if (dim != null) dim.color = BackgroundColor;

            for (int i = 0; i < STREAK_COUNT; i++)
            {
                var go = CreateRect(body, "Streak", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                streaks.Add((RectTransform)go.transform);
                streakSpeeds.Add(0f);
            }

            var player = CreateRect(body, "Player", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(PLAYER_HEIGHT, PLAYER_HEIGHT));
            playerRect = (RectTransform)player.transform;
            playerImage = player.AddComponent<Image>();
            playerImage.preserveAspect = true;
            playerImage.raycastTarget = false;

            var title = CreateRect(body, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(1400, 140));
            titleGroup = title.AddComponent<CanvasGroup>();
            titleGroup.blocksRaycasts = false;
            stageLabel = CreateLabel(title.transform, "Stage", new Vector2(0, 34), new Vector2(1400, 34), string.Empty, 22, StageLabelColor, TextAnchor.MiddleCenter);
            titleLabel = CreateLabel(title.transform, "Name", new Vector2(0, -20), new Vector2(1400, 70), string.Empty, 52, TitleColor, TextAnchor.MiddleCenter);
            titleLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        }

        private void Begin(int stageNumber, string stageTitle, Sprite playerSprite, bool isFlipped)
        {
            stageLabel.text = Loc.GetFormat(StringKey.Run_StageNumberFormat, stageNumber);
            titleLabel.text = stageTitle;
            titleGroup.alpha = 0f;

            playerImage.sprite = playerSprite;
            playerImage.enabled = playerSprite != null;
            playerRect.localScale = new Vector3(isFlipped ? -1f : 1f, 1f, 1f);
            ScatterStreaks();

            finished = new AwaitableCompletionSource();
            isFinishReported = false;
            isPlaying = true;
            startedAt = Time.unscaledTime;
            ShowBody();
        }

        /// <summary>정지(timeScale 0) 위에서도 Update는 돈다 — 실시간으로 움직인다.</summary>
        private void Update()
        {
            if (!isPlaying) return;

            float elapsed = Time.unscaledTime - startedAt;
            Animate(elapsed);

            if (isFinishReported) return;
            if (elapsed >= DURATION_SECONDS || elapsed >= INPUT_GRACE_SECONDS && WasSkipPressed()) ReportFinished();
        }

        private void ReportFinished()
        {
            if (isFinishReported || finished == null) return;
            isFinishReported = true;
            finished.SetResult();
        }

        private void Animate(float elapsed)
        {
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < streaks.Count; i++)
            {
                var rect = streaks[i];
                var position = rect.anchoredPosition;
                position.y += streakSpeeds[i] * delta;
                // 위로 빠져나가면 아래에서 다시 — 끝없이 떨어지는 느낌.
                if (position.y - rect.sizeDelta.y * 0.5f > SCREEN_HALF_HEIGHT) ResetStreak(i, false);
                else rect.anchoredPosition = position;
            }

            // 흔들리며 떨어진다 — 좌우 흔들림 + 기울기. 형체는 화면에 머물고 세계가 스친다.
            playerRect.anchoredPosition = new Vector2(Mathf.Sin(elapsed * 1.7f) * 26f, 40f + Mathf.Sin(elapsed * 3.1f) * 8f);
            playerRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 2.3f) * 9f);

            titleGroup.alpha = Mathf.Clamp01((elapsed - TITLE_DELAY_SECONDS) / TITLE_FADE_SECONDS);
        }

        private void ScatterStreaks()
        {
            for (int i = 0; i < streaks.Count; i++) ResetStreak(i, true);
        }

        /// <summary>빛줄기 하나를 새로 놓는다. 처음에는 화면 전체에, 다시 놓을 때는 화면 아래에서 시작한다.</summary>
        private void ResetStreak(int index, bool isAnywhere)
        {
            var rect = streaks[index];
            float height = UnityEngine.Random.Range(90f, 320f);
            float width = UnityEngine.Random.Range(2f, 5f);
            rect.sizeDelta = new Vector2(width, height);
            float y = isAnywhere
                ? UnityEngine.Random.Range(-SCREEN_HALF_HEIGHT, SCREEN_HALF_HEIGHT)
                : -SCREEN_HALF_HEIGHT - height * 0.5f;
            rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-SCREEN_HALF_WIDTH, SCREEN_HALF_WIDTH), y);
            streakSpeeds[index] = UnityEngine.Random.Range(1500f, 2800f);

            var image = rect.GetComponent<Image>();
            image.color = new Color(StreakColor.r, StreakColor.g, StreakColor.b, UnityEngine.Random.Range(0.12f, 0.45f));
        }

        private static bool WasSkipPressed()
        {
            if (SaveStatusOverlay.IsCapturingInput) return false;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.zKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }
    }
}
