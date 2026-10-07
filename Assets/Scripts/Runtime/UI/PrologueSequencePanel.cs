using System;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// 자막 호흡은 <see cref="EndingSequencePanel"/>과 같은 <see cref="SubtitleSequence"/>를 쓴다.
    /// 네 장면은 자막 단계·시간을 읽어 움직이며, 동적 오버레이 안에서만 표시한다.
    /// </summary>
    public sealed class PrologueSequencePanel : MonoBehaviour
    {
        // 키보드는 기존 키 그대로. 패드는 확인(buttonSouth)만 — B·Start는 다른 메뉴의 취소·일시정지와 헷갈린다.
        private const string KEYBOARD_INPUT_LABEL = "ESC / Enter / Space";
        private const string PAD_INPUT_PREFIX = "Pad ";

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
        private PrologueCutPresentation cuts;
        private Text skipHint;

        // 재생·검은 화면 동안 EventSystem 선택을 붙잡는 자리. 비워 두면 뒤 타이틀 버튼이 선택을 쥔 채라
        // UI 모듈의 Submit이 이 패널의 Update보다 먼저 그 버튼을 눌러 자막 도중 로비로 넘어간다.
        private GameObject focusSink;
        private GameObject previousSelection;

        private bool isPlaying;
        private int startFrame;
        private float coverTimer;
        private Action onFinished;

        // 안내 문구를 마지막으로 그린 조건. 패드 연결·교체·언어 변경 때만 다시 그린다.
        private string shownHintKey;
        private string shownPadLabel;
        private LocalizationLanguage shownLanguage;

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
            RefreshCuts();
            RefreshHint(true);

            body.SetActive(true);
            KeepFocus();
        }

        private void Update()
        {
            if (body == null || !body.activeSelf) return;

            // 검은 화면만 남은 동안에도 선택을 쥔다 — 씬이 바뀌기 전까지 뒤 메뉴가 입력을 받으면 안 된다.
            KeepFocus();

            if (!isPlaying)
            {
                // 자막은 끝났고 검은 화면만 남은 상태 — 새 씬이 오지 않으면 상한에서 걷는다.
                coverTimer += Time.unscaledDeltaTime;
                if (coverTimer >= COVER_TIMEOUT) HideCover();
                return;
            }

            RefreshHint(false);

            // 저장 모달이 닫힌 뒤 자막과 장면을 같은 지점에서 이어 간다.
            if (SaveStatusOverlay.IsCapturingInput) return;

            if (ConsumeSkipInput()) return;

            // 타이틀 씬은 정지 상태가 아니지만 unscaled로 통일한다 — 자막 호흡이 timeScale에 끌려다닐 이유가 없다.
            if (subtitles == null || subtitles.Tick(Time.unscaledDeltaTime)) Finish();
            else RefreshCuts();
        }

        /// <summary>
        /// 건너뛰기. 엔딩과 같은 관습으로 <b>한 문단씩</b> 넘긴다 —
        /// 한 번 잘못 눌러 프롤로그 전체가 사라지지 않게.
        /// 키보드와 패드를 같은 프레임에 함께 눌러도 판정은 하나라 한 문단만 넘어간다.
        /// </summary>
        private bool ConsumeSkipInput()
        {
            if (Time.frameCount == startFrame) return false;
            if (SaveStatusOverlay.IsCapturingInput) return false;
            if (!WasAdvancePressed()) return false;

            if (subtitles == null || subtitles.Skip()) Finish();
            else RefreshCuts();
            return true;
        }

        private void RefreshCuts()
        {
            if (subtitles != null) cuts?.Show(subtitles.ParagraphIndex, subtitles.ElapsedSeconds);
        }

        /// <summary>키보드 ESC/Enter/Space 또는 패드 확인 버튼. 키보드가 없어도 패드만으로 넘길 수 있다.</summary>
        private static bool WasAdvancePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>
        /// 재생 중에만 다음 단계 안내를 보인다. 매 프레임 불리지만 패드·언어가 바뀌었을 때만 다시 그린다.
        /// </summary>
        private void RefreshHint(bool isForced)
        {
            if (skipHint == null) return;

            string hintKey = isPlaying ? StringKey.Story_SequenceNextHintFormat : null;
            string padLabel = GamepadConfirmLabel();
            var language = Loc.CurrentLanguage;
            if (!isForced && hintKey == shownHintKey && padLabel == shownPadLabel && language == shownLanguage) return;

            shownHintKey = hintKey;
            shownPadLabel = padLabel;
            shownLanguage = language;
            skipHint.text = hintKey == null ? string.Empty : Loc.GetFormat(hintKey, InputLabel(padLabel));
        }

        private static string InputLabel(string padLabel)
            => padLabel == null ? KEYBOARD_INPUT_LABEL : KEYBOARD_INPUT_LABEL + " / " + PAD_INPUT_PREFIX + padLabel;

        /// <summary>
        /// 연결된 패드의 실제 확인 버튼 이름(Xbox A, PlayStation Cross 등). 패드가 없으면 null.
        /// 장치가 이름을 주지 않으면 컨트롤 이름으로 물러선다 — 특정 패드의 표기를 가정하지 않는다.
        /// </summary>
        private static string GamepadConfirmLabel()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return null;

            var button = gamepad.buttonSouth;
            if (!string.IsNullOrEmpty(button.shortDisplayName)) return button.shortDisplayName;
            if (!string.IsNullOrEmpty(button.displayName)) return button.displayName;
            return button.name;
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
            cuts?.Clear();
            RefreshHint(true);

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
            ReleaseFocus();
            if (body != null) body.SetActive(false);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ReleaseFocus();
        }

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>
        /// 선택을 포커스 자리에 붙잡는다. 처음 가져올 때 뒤 화면의 선택을 기억한다.
        /// 저장 모달이 입력을 쥔 동안에는 손대지 않는다 — 모달이 닫히며 돌려준 선택을 다음 프레임에 다시 가져온다.
        /// 마우스로 빈 곳을 눌러 선택이 풀려도 여기서 되돌린다.
        /// </summary>
        private void KeepFocus()
        {
            if (focusSink == null || SaveStatusOverlay.IsCapturingInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == focusSink) return;
            if (selected != null && previousSelection == null) previousSelection = selected;
            eventSystem.SetSelectedGameObject(focusSink);
        }

        /// <summary>
        /// 포커스 자리가 선택을 쥐고 있을 때만 이전 선택을 돌려준다. 이전 선택이 씬과 함께 사라졌거나
        /// 못 누르는 상태면 비운다. 다른 화면(저장 모달·새 씬)이 쥔 선택은 그대로 둔다.
        /// </summary>
        private void ReleaseFocus()
        {
            var restore = previousSelection;
            previousSelection = null;

            var eventSystem = EventSystem.current;
            if (eventSystem == null || focusSink == null) return;
            if (eventSystem.currentSelectedGameObject != focusSink) return;

            eventSystem.SetSelectedGameObject(CanRestore(restore) ? restore : null);
        }

        private static bool CanRestore(GameObject target)
        {
            // 파괴된 오브젝트는 Unity 비교에서 null이다.
            if (target == null || !target.activeInHierarchy) return false;
            return target.TryGetComponent<Selectable>(out var selectable) && selectable.IsInteractable();
        }

        private void BuildUI(Transform root)
        {
            // 완전 불투명 검정 — 뒤에 남은 타이틀 메뉴가 비치면 "떨어지는 중"이 되지 않는다.
            body = CreateDimBody(root, 1f);

            cuts = new PrologueCutPresentation(body.transform);
            subtitles = SubtitleSequence.Create(body.transform);
            var subtitleRect = (RectTransform)body.transform.Find("Subtitle");
            subtitleRect.anchoredPosition = new Vector2(0f, -320f);
            subtitleRect.sizeDelta = new Vector2(1100f, 200f);

            skipHint = CreateLabel(body.transform, "SkipHint", new Vector2(0, -460), new Vector2(600, 30),
                string.Empty, 15, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleCenter);

            // 표시 전용 — 그래픽·클릭 콜백·탐색이 없어 Submit·Move가 와도 아무 일도 일어나지 않는다.
            focusSink = CreateRect(body.transform, "FocusSink", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var sink = focusSink.AddComponent<Selectable>();
            sink.transition = Selectable.Transition.None;
            sink.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }
}
