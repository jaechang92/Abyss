using System;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 프롤로그 자막(4-3). 타이틀에서 "새 게임"을 고른 뒤, 로비로 넘어가기 전에 한 번 재생한다.
    ///
    /// <b>세이브당 1회다</b>(<see cref="MetaRecords.hasSeenPrologue"/>). 프롤로그는 주인공 1인칭
    /// 확정 서술이라 매 런 반복되면 "추락은 한 번뿐"이라는 전제가 무너진다. 이후의 죽음·재시작은
    /// 재추락이 아니라 파편을 배우는 과정이다(00-concept USP-2).
    ///
    /// 연출은 <see cref="EndingSequencePanel"/>과 같은 <see cref="SubtitleSequence"/>를 쓴다 —
    /// 여는 자막과 닫는 자막의 호흡은 한 곳에서만 정해진다. 씬이 아니라 동적 오버레이인 이유도
    /// 엔딩과 같다(텍스트뿐이라 씬이 가진 것을 하나도 쓰지 않는다).
    /// </summary>
    public sealed class PrologueSequencePanel : MonoBehaviour
    {
        private const string HINT_SKIP = "ESC / Enter — 건너뛰기";

        // 씬 전환이 끝내 오지 않을 때 검은 화면이 영원히 남지 않도록 하는 상한(엔딩 선례).
        private const float COVER_TIMEOUT = 5f;

        /// <summary>
        /// 프롤로그 문단 순서. 텍스트 자체는 GameText.csv에 있다(서사 텍스트 규약: 10-narrative-plan §5).
        ///
        /// 인칭 규약: <b>자막은 주인공 1인칭「나」, NPC 대사는 주인공을 「자네」로 부른다.</b>
        /// 1인칭이되 단정하지 않는다 — 폼이 계속 바뀌는 본편이 "아직 나였다"의 소멸 과정이 된다.
        ///
        /// 마지막 문단의 <i>내려다보고 있었다</i>는 로비 첫 대사 "또 하나의 낙오자인가"가
        /// <b>응답</b>이 되게 하는 자리다. 기록자를 등장시키지 않고 암시만 남긴다
        /// (이름 「하란」의 회수는 M4 히든 엔딩 — 12-prologue-ending-text.md §2-2).
        /// </summary>
        private static readonly string[] ParagraphKeys =
        {
            StringKey.Story_Prologue_Line1,
            StringKey.Story_Prologue_Line2,
            StringKey.Story_Prologue_Line3,
            StringKey.Story_Prologue_Line4,
        };

        private static PrologueSequencePanel instance;

        private GameObject body;
        private SubtitleSequence subtitles;
        private Text skipHint;

        private bool isPlaying;
        private int startFrame;
        private float coverTimer;
        private Action onFinished;

        /// <summary>
        /// 프롤로그를 재생한다. <paramref name="onFinished"/>는 마지막 문단이 끝난 뒤 한 번 호출된다 —
        /// 호출자가 로비로 넘기는 지점이다.
        ///
        /// <b>여기서 시청 기록을 남기지 않는다.</b> 재생 여부 판단은 호출자(<see cref="TitleMenuPanel"/>)의
        /// 몫이고, 이 패널은 "틀면 튼다". 판단과 재생을 한 곳에 섞으면 치트로 다시 보는 경로가
        /// 자기 자신을 도로 잠근다.
        /// </summary>
        public static void Play(Action onFinished)
        {
            EnsureInstance();
            if (instance == null)
            {
                onFinished?.Invoke();
                return;
            }

            instance.onFinished = onFinished;
            instance.Restart();
        }

        /// <summary>도메인 리로드 비활성화 대비 정적 상태 리셋(AbyssBootstrap 선례).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private static void EnsureInstance()
        {
            if (instance != null) return;

            var go = CreateOverlayCanvas("PrologueSequencePanel", UiSortingOrder.Sequence);
            // 자막이 끝나면 씬이 바뀌는데, 그 뒤에 검은 화면을 걷어야 하므로 영속으로 둔다.
            DontDestroyOnLoad(go);

            instance = go.AddComponent<PrologueSequencePanel>();
            instance.BuildUI(go.transform);
            instance.body.SetActive(false);
        }

        private void Restart()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;   // 직전 재생이 씬 전환을 기다리다 만 상태일 수 있다

            isPlaying = true;
            coverTimer = 0f;

            // 🔴 진입 트리거가 키 입력이다 — 타이틀에서 Enter로 "새 게임"을 누른 그 프레임에
            // 이 패널의 Update가 돌면 같은 Enter가 첫 문단을 즉시 넘긴다. 엔딩은 보스 처치로
            // 진입해 이 문제가 없었지만 여기서는 100% 재현되므로, 재생을 시작한 프레임의 입력은 버린다.
            startFrame = Time.frameCount;

            subtitles?.Restart(SubtitleSequence.Localize(ParagraphKeys));
            if (skipHint != null) skipHint.text = HINT_SKIP;

            body.SetActive(true);
        }

        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            if (!isPlaying)
            {
                // 자막은 끝났고 검은 화면만 남은 상태 — 새 씬이 오지 않으면 상한에서 걷는다.
                coverTimer += Time.unscaledDeltaTime;
                if (coverTimer >= COVER_TIMEOUT) HideCover();
                return;
            }

            if (ConsumeSkipInput()) return;

            // 타이틀 씬은 정지 상태가 아니지만 unscaled로 통일한다 — 자막 호흡이 timeScale에 끌려다닐 이유가 없다.
            if (subtitles == null || subtitles.Tick(Time.unscaledDeltaTime)) Finish();
        }

        /// <summary>
        /// 건너뛰기. 엔딩과 같은 관습으로 <b>한 문단씩</b> 넘긴다 —
        /// 한 번 잘못 눌러 프롤로그 전체가 사라지지 않게.
        /// </summary>
        private bool ConsumeSkipInput()
        {
            if (Time.frameCount == startFrame) return false;

            var kb = Keyboard.current;
            if (kb == null) return false;
            if (!kb.escapeKey.wasPressedThisFrame && !kb.enterKey.wasPressedThisFrame && !kb.spaceKey.wasPressedThisFrame) return false;

            if (subtitles == null || subtitles.Skip()) Finish();
            return true;
        }

        /// <summary>
        /// 프롤로그 종료 → 호출자가 로비로 넘긴다.
        ///
        /// <b>내용만 걷고 검은 배경은 남긴다</b>(엔딩과 같은 이유) — 여기서 화면을 열면
        /// 씬 전환 페이드가 시작되기 전에 타이틀 메뉴가 한순간 다시 드러난다.
        /// </summary>
        private void Finish()
        {
            if (!isPlaying) return;

            isPlaying = false;
            coverTimer = 0f;

            subtitles?.Clear();
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

        private void BuildUI(Transform root)
        {
            // 완전 불투명 검정 — 뒤에 남은 타이틀 메뉴가 비치면 "떨어지는 중"이 되지 않는다.
            body = CreateDimBody(root, 1f);

            subtitles = SubtitleSequence.Create(body.transform);

            skipHint = CreateLabel(body.transform, "SkipHint", new Vector2(0, -460), new Vector2(600, 30),
                string.Empty, 15, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);
        }
    }
}
