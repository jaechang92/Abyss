using System;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 완주 엔딩 연출. 완주 루프 계획 2-2 — 자막 → 크레딧 → 통계 → 타이틀.
    ///
    /// 통계까지 이 패널이 검은 화면 위에 직접 보여준다. 크레딧 뒤에 <see cref="ResultPanelPresenter"/>의
    /// 런 종료 패널(창·버튼·[즉시 재시작])을 띄웠더니 엔딩의 톤이 끊겼다 — 사망 결과 화면과 같은
    /// 물건이라 "한 판 끝났으니 다시"가 되어버린다. 완주는 게임을 한 바퀴 닫는 지점이므로
    /// 같은 검은 화면에서 기록만 조용히 보여주고 타이틀로 빠진다.
    ///
    /// 씬이 아니라 동적 오버레이 패널인 이유: 지금 엔딩은 텍스트뿐이라 씬이 가진 것(배치·조명·전용
    /// 카메라)을 하나도 쓰지 않는데, 씬으로 만들면 빌드 설정 등록·씬 빌더·Run 씬에서의 전환 처리가
    /// 따라온다. <see cref="SettingsPanel"/>·<see cref="LobbyMenuPanel"/>이 세운 관습을 그대로 쓴다.
    /// 나중에 일러스트가 붙으면 그때 씬으로 승격해도 호출부(<see cref="Play"/>)는 그대로다.
    ///
    /// <b>timeScale=0 위에서 동작한다</b> — 런 종료 시 ResultState가 전역 정지를 걸어두므로
    /// 모든 시간 계산은 <see cref="Time.unscaledDeltaTime"/>을 쓴다.
    /// </summary>
    public sealed class EndingSequencePanel : MonoBehaviour
    {
        private const int SORTING_ORDER = 400;   // 결과 패널(씬 캔버스)보다 위, 설정(500)보다 아래

        // 자막 한 문단의 호흡은 SubtitleSequence가 소유한다 — 프롤로그와 같은 값을 써야 하므로.

        private const float CREDITS_SCROLL_SPEED = 110f;   // px/s (기준 해상도 1080 높이 기준)
        private const float CREDITS_FALLBACK_HEIGHT = 600f;
        private const float CREDITS_WIDTH = 760f;

        private const float STATS_FADE_IN = 0.8f;
        // 8줄을 읽고도 남을 시간. 다 읽었으면 Enter로 앞당길 수 있어 넉넉히 잡아도 손해가 없다.
        private const float STATS_AUTO_ADVANCE = 12f;

        private const string HINT_SKIP = "ESC / Enter — 건너뛰기";
        private const string HINT_TO_TITLE = "Enter — 타이틀로";

        // 씬 전환이 끝내 오지 않을 때(SceneFlowController 미가동 등) 검은 화면이 영원히 남지 않도록 하는 상한.
        private const float COVER_TIMEOUT = 5f;

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
            Stats,
            Done,
        }

        private static EndingSequencePanel instance;

        private GameObject body;
        private SubtitleSequence subtitles;
        private RectTransform creditsRect;
        private Text creditsText;
        private CanvasGroup statsGroup;
        private Text statsBody;
        private Text skipHint;

        private Phase phase;
        private float phaseTimer;
        private float creditsHeight;
        private Action onFinished;

        /// <summary>
        /// 엔딩을 재생한다. <paramref name="onFinished"/>는 <b>통계 화면까지 끝난 뒤</b> 한 번 호출된다 —
        /// 호출자가 타이틀로 넘기는 지점이다.
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
            // 직전 재생이 씬 전환을 기다리다 만 상태일 수 있다.
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            phase = Phase.Subtitles;
            phaseTimer = 0f;

            subtitles?.Restart(Paragraphs);
            if (creditsText != null) creditsText.gameObject.SetActive(false);
            if (statsGroup != null) statsGroup.gameObject.SetActive(false);
            if (skipHint != null) skipHint.text = HINT_SKIP;

            body.SetActive(true);
        }

        // ───────────────────────── 진행 ─────────────────────────

        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            // 정지 중에도 흘러야 하므로 unscaled. 스킵 입력을 먼저 처리해 같은 프레임에 시간이 겹쳐 흐르지 않게 한다.
            if (ConsumeSkipInput()) return;

            // 자막 페이즈의 시간은 SubtitleSequence가 자기 것으로 센다. 여기서 함께 더하면 두 배로 흐른다.
            if (phase == Phase.Subtitles)
            {
                if (subtitles == null || subtitles.Tick(Time.unscaledDeltaTime)) BeginCredits();
                return;
            }

            phaseTimer += Time.unscaledDeltaTime;

            if (phase == Phase.Credits) TickCredits();
            else if (phase == Phase.Stats) TickStats();
            else if (phase == Phase.Done && phaseTimer >= COVER_TIMEOUT) HideCover();
        }

        /// <summary>
        /// 건너뛰기. 한 번에 전부 끝내지 않고 <b>한 단계씩</b> 넘긴다 —
        /// 자막 중이면 다음 문단, 마지막 문단이면 크레딧, 크레딧 중이면 통계, 통계에서 종료.
        /// 실수로 한 번 눌러 엔딩 전체가 사라지는 것을 막으면서도 반복 테스트는 빠르다.
        /// </summary>
        private bool ConsumeSkipInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            if (!kb.escapeKey.wasPressedThisFrame && !kb.enterKey.wasPressedThisFrame && !kb.spaceKey.wasPressedThisFrame) return false;

            // 끝난 뒤(씬 전환 대기 중)에는 입력을 소비하지 않는다 — 다음 화면이 받아야 한다.
            if (phase == Phase.Subtitles)
            {
                if (subtitles == null || subtitles.Skip()) BeginCredits();
            }
            else if (phase == Phase.Credits) BeginStats();
            else if (phase == Phase.Stats) Finish();
            else return false;
            return true;
        }

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

            var pos = creditsRect.anchoredPosition;
            pos.y += CREDITS_SCROLL_SPEED * Time.unscaledDeltaTime;
            creditsRect.anchoredPosition = pos;

            if (pos.y >= ReferenceResolution.y * 0.5f + creditsHeight * 0.5f) BeginStats();
        }

        /// <summary>
        /// 크레딧 뒤 통계. 창을 띄우지 않고 <b>같은 검은 화면에</b> 이어 붙인다 —
        /// 여기서 런 종료 패널로 넘기면 사망 결과 화면과 같은 물건이라 엔딩의 톤이 끊긴다.
        /// 문구는 <see cref="RunSummaryText"/>가 소유해 결과 패널과 같은 표기를 쓴다.
        /// </summary>
        private void BeginStats()
        {
            phase = Phase.Stats;
            phaseTimer = 0f;
            subtitles?.Clear();
            if (creditsText != null) creditsText.gameObject.SetActive(false);

            if (statsGroup == null || statsBody == null)
            {
                Finish();
                return;
            }

            // 결과 패널과 같은 객체(정산 시점에 굳은 요약)를 읽는다.
            var summary = RunManager.HasInstance ? RunManager.Instance.LastRunSummary : null;
            statsBody.text = summary != null
                ? string.Join("\n", RunSummaryText.AllLines(summary))
                : string.Empty;

            statsGroup.alpha = 0f;
            statsGroup.gameObject.SetActive(true);
            if (skipHint != null) skipHint.text = HINT_TO_TITLE;
        }

        private void TickStats()
        {
            if (statsGroup == null)
            {
                Finish();
                return;
            }

            statsGroup.alpha = Mathf.Clamp01(phaseTimer / STATS_FADE_IN);

            // 아무 것도 누르지 않아도 타이틀로 돌아간다 — 크레딧 뒤에 화면이 멈춰 있으면 끝난 것처럼 보이지 않는다.
            if (phaseTimer >= STATS_AUTO_ADVANCE) Finish();
        }

        /// <summary>
        /// 엔딩 종료 → 호출자가 타이틀로 넘긴다.
        ///
        /// <b>내용만 걷고 검은 배경은 남긴다.</b> 여기서 화면을 열어버리면 씬 전환 페이드가 시작되기 전에
        /// 정지된 전투 화면이 한순간 드러난다 — 엔딩 직후로는 가장 어색한 그림이다.
        /// 배경은 새 씬이 올라온 뒤에 걷는다.
        /// </summary>
        private void Finish()
        {
            if (phase == Phase.Done) return;

            phase = Phase.Done;
            phaseTimer = 0f;

            subtitles?.Clear();
            if (creditsText != null) creditsText.gameObject.SetActive(false);
            if (statsGroup != null) statsGroup.gameObject.SetActive(false);
            if (skipHint != null) skipHint.text = string.Empty;

            SceneManager.sceneLoaded -= HandleSceneLoaded;   // 중복 구독 방지
            SceneManager.sceneLoaded += HandleSceneLoaded;

            // 콜백을 먼저 비우고 호출한다 — 콜백 안에서 다시 재생해도 중첩되지 않게.
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => HideCover();

        private void HideCover()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (body != null) body.SetActive(false);
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        // ───────────────────────── UI 구성 ─────────────────────────

        private void BuildUI(Transform root)
        {
            // 완전 불투명 검정 — 엔딩 뒤로 멈춰 있는 전투 화면이 비치면 몰입이 깨진다.
            body = CreateDimBody(root, 1f);

            subtitles = SubtitleSequence.Create(body.transform);

            creditsText = CreateLabel(body.transform, "Credits", Vector2.zero, new Vector2(CREDITS_WIDTH, CREDITS_FALLBACK_HEIGHT),
                CREDITS_TEXT, 24, new Color(0.86f, 0.86f, 0.96f), TextAnchor.UpperCenter);
            creditsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            creditsText.verticalOverflow = VerticalWrapMode.Overflow;
            creditsText.lineSpacing = 1.3f;
            creditsRect = (RectTransform)creditsText.transform;
            creditsText.gameObject.SetActive(false);

            BuildStats(body.transform);

            skipHint = CreateLabel(body.transform, "SkipHint", new Vector2(0, -460), new Vector2(600, 30),
                string.Empty, 15, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 통계 블록. 제목과 본문을 한 그룹으로 묶어 <see cref="CanvasGroup"/> 하나로 페이드한다 —
        /// 텍스트마다 알파를 따로 만지면 두 값이 어긋난다.
        /// </summary>
        private void BuildStats(Transform parent)
        {
            var group = CreateRect(parent, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560));
            statsGroup = group.AddComponent<CanvasGroup>();

            CreateLabel(group.transform, "StatsTitle", new Vector2(0, 210), new Vector2(700, 50),
                "심연 탈출", 34, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleCenter);

            statsBody = CreateLabel(group.transform, "StatsBody", new Vector2(0, -20), new Vector2(820, 380),
                string.Empty, 21, new Color(0.88f, 0.88f, 0.96f), TextAnchor.UpperCenter);
            statsBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            statsBody.verticalOverflow = VerticalWrapMode.Overflow;
            statsBody.lineSpacing = 1.6f;

            group.SetActive(false);
        }
    }
}
