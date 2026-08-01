using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 완주 엔딩 연출. 완주 루프 계획 2-2 — 자막 → 크레딧 → 결과 패널.
    ///
    /// 씬이 아니라 동적 오버레이 패널인 이유: 지금 엔딩은 텍스트뿐이라 씬이 가진 것(배치·조명·전용
    /// 카메라)을 하나도 쓰지 않는데, 씬으로 만들면 빌드 설정 등록·씬 빌더·Run 씬에서의 전환 처리가
    /// 따라온다. <see cref="SettingsPanel"/>·<see cref="LobbyMenuPanel"/>이 세운 관습을 그대로 쓴다.
    /// 나중에 일러스트가 붙으면 그때 씬으로 승격해도 호출부(<see cref="Open"/>)는 그대로다.
    ///
    /// <b>timeScale=0 위에서 동작한다</b> — 런 종료 시 ResultState가 전역 정지를 걸어두므로
    /// 모든 시간 계산은 <see cref="Time.unscaledDeltaTime"/>을 쓴다.
    /// </summary>
    public sealed class EndingSequencePanel : MonoBehaviour
    {
        private const int SORTING_ORDER = 400;   // 결과 패널(씬 캔버스)보다 위, 설정(500)보다 아래

        // 자막 한 문단의 페이드 인 → 유지 → 페이드 아웃 (초, unscaled).
        private const float FADE_IN = 0.9f;
        private const float HOLD = 2.8f;
        private const float FADE_OUT = 0.9f;
        private const float PARAGRAPH_DURATION = FADE_IN + HOLD + FADE_OUT;

        private const float CREDITS_SCROLL_SPEED = 110f;   // px/s (기준 해상도 1080 높이 기준)
        private const float CREDITS_FALLBACK_HEIGHT = 600f;
        private const float CREDITS_WIDTH = 760f;

        /// <summary>임시 엔딩 텍스트. 내용은 4-3에서 교체하고 경로는 그대로 둔다.</summary>
        private static readonly string[] Paragraphs =
        {
            "심연의 밑바닥에서, 마지막 불꽃이 꺼졌다.",
            "당신이 걸어 내려온 길은 이제 닫힌다.\n올라가는 길만이 남았다.",
            "무너진 왕좌 너머로, 오래 잊혔던 빛이 스며든다.",
            "당신은 심연을 벗어났다.",
        };

        private const string CREDITS_TEXT =
            "ABYSS\n\n\n" +
            "기획 · 프로그래밍\nJaeChang\n\n" +
            "엔진\nUnity 6\n\n" +
            "시스템\nGAS Core · FSM Core\n\n\n" +
            "플레이해 주셔서 감사합니다.\n\n\n" +
            "— 프로토타입 빌드 —";

        private enum Phase
        {
            Subtitles,
            Credits,
            Done,
        }

        private static EndingSequencePanel instance;

        private GameObject body;
        private Text subtitleText;
        private RectTransform creditsRect;
        private Text creditsText;
        private Text skipHint;

        private Phase phase;
        private int paragraphIndex;
        private float phaseTimer;
        private float creditsHeight;
        private Action onFinished;

        /// <summary>
        /// 엔딩을 재생한다. <paramref name="onFinished"/>는 크레딧이 끝나거나 건너뛰었을 때 한 번 호출된다.
        /// </summary>
        public static void Play(Action onFinished)
        {
            EnsureInstance();
            if (instance == null) return;

            instance.onFinished = onFinished;
            instance.Restart();
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = CreateOverlayCanvas("EndingSequencePanel", SORTING_ORDER);
            // 엔딩 도중 타이틀로 나가는 경로는 없지만, 크레딧 끝에서 씬이 바뀌어도 콜백이 살아 있도록 영속으로 둔다.
            DontDestroyOnLoad(go);

            instance = go.AddComponent<EndingSequencePanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        private void Restart()
        {
            phase = Phase.Subtitles;
            paragraphIndex = 0;
            phaseTimer = 0f;

            SetSubtitleAlpha(0f);
            if (subtitleText != null) subtitleText.text = Paragraphs.Length > 0 ? Paragraphs[0] : string.Empty;
            if (creditsText != null) creditsText.gameObject.SetActive(false);
            if (skipHint != null) skipHint.text = "ESC / Enter — 건너뛰기";

            body.SetActive(true);
        }

        // ───────────────────────── 진행 ─────────────────────────

        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            // 정지 중에도 흘러야 하므로 unscaled. 스킵 입력을 먼저 처리해 같은 프레임에 시간이 겹쳐 흐르지 않게 한다.
            if (ConsumeSkipInput()) return;

            phaseTimer += Time.unscaledDeltaTime;

            if (phase == Phase.Subtitles) TickSubtitles();
            else if (phase == Phase.Credits) TickCredits();
        }

        /// <summary>
        /// 건너뛰기. 한 번에 전부 끝내지 않고 <b>한 단계씩</b> 넘긴다 —
        /// 자막 중이면 다음 문단, 마지막 문단이면 크레딧, 크레딧 중이면 종료.
        /// 실수로 한 번 눌러 엔딩 전체가 사라지는 것을 막으면서도 반복 테스트는 빠르다.
        /// </summary>
        private bool ConsumeSkipInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (!kb.escapeKey.wasPressedThisFrame && !kb.enterKey.wasPressedThisFrame && !kb.spaceKey.wasPressedThisFrame) return false;

            if (phase == Phase.Subtitles) AdvanceParagraph();
            else if (phase == Phase.Credits) Finish();
            return true;
        }

        private void TickSubtitles()
        {
            SetSubtitleAlpha(CalcParagraphAlpha(phaseTimer));

            if (phaseTimer < PARAGRAPH_DURATION) return;
            AdvanceParagraph();
        }

        /// <summary>페이드 인 → 유지 → 페이드 아웃 구간별 알파.</summary>
        private static float CalcParagraphAlpha(float t)
        {
            if (t < FADE_IN) return Mathf.Clamp01(t / FADE_IN);
            if (t < FADE_IN + HOLD) return 1f;
            return Mathf.Clamp01(1f - (t - FADE_IN - HOLD) / FADE_OUT);
        }

        private void AdvanceParagraph()
        {
            paragraphIndex += 1;
            phaseTimer = 0f;

            if (paragraphIndex >= Paragraphs.Length)
            {
                BeginCredits();
                return;
            }

            if (subtitleText != null) subtitleText.text = Paragraphs[paragraphIndex];
            SetSubtitleAlpha(0f);
        }

        private void BeginCredits()
        {
            phase = Phase.Credits;
            phaseTimer = 0f;
            SetSubtitleAlpha(0f);

            if (creditsText == null || creditsRect == null)
            {
                Finish();
                return;
            }

            creditsText.gameObject.SetActive(true);

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
                Finish();
                return;
            }

            var pos = creditsRect.anchoredPosition;
            pos.y += CREDITS_SCROLL_SPEED * Time.unscaledDeltaTime;
            creditsRect.anchoredPosition = pos;

            if (pos.y >= ReferenceResolution.y * 0.5f + creditsHeight * 0.5f) Finish();
        }

        private void Finish()
        {
            if (phase == Phase.Done) return;

            phase = Phase.Done;
            body.SetActive(false);

            // 콜백을 먼저 비우고 호출한다 — 콜백 안에서 다시 재생해도 중첩되지 않게.
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        private void SetSubtitleAlpha(float a)
        {
            if (subtitleText == null) return;
            var c = subtitleText.color;
            c.a = Mathf.Clamp01(a);
            subtitleText.color = c;
        }

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            // 완전 불투명 검정 — 엔딩 뒤로 멈춰 있는 전투 화면이 비치면 몰입이 깨진다.
            body = CreateDimBody(root, 1f);

            subtitleText = CreateLabel(body.transform, "Subtitle", Vector2.zero, new Vector2(1100, 260),
                string.Empty, 30, new Color(0.94f, 0.94f, 1f, 0f), TextAnchor.MiddleCenter);
            // 문단이 두 줄 이상이라 가로 오버플로 대신 줄바꿈이 필요하다(CreateLabel 기본값은 Overflow).
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.lineSpacing = 1.4f;

            creditsText = CreateLabel(body.transform, "Credits", Vector2.zero, new Vector2(CREDITS_WIDTH, CREDITS_FALLBACK_HEIGHT),
                CREDITS_TEXT, 24, new Color(0.86f, 0.86f, 0.96f), TextAnchor.UpperCenter);
            creditsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            creditsText.verticalOverflow = VerticalWrapMode.Overflow;
            creditsText.lineSpacing = 1.3f;
            creditsRect = (RectTransform)creditsText.transform;
            creditsText.gameObject.SetActive(false);

            skipHint = CreateLabel(body.transform, "SkipHint", new Vector2(0, -460), new Vector2(600, 30),
                string.Empty, 15, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
        }
    }
}
