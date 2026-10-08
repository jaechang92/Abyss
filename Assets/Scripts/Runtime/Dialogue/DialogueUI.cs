using System;
using Abyss.Runtime.Localization;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>
    /// 씬에 1개 존재하는 공용 대화 박스 UI. NPC가 Play(data, onComplete)로 호출한다.
    /// 진행 입력은 Space/Enter 또는 패드 확인 버튼(상호작용 키 g와 분리해 시작-진행 충돌 방지).
    /// 대사·화자명은 Loc.Get(StringKey)로 다국어 조회한다.
    ///
    /// 열려 있는 동안 선택을 내부 포커스 자리에 붙잡는다 — 패드 확인 버튼은 EventSystem의 Submit이기도 해서,
    /// 뒤 메뉴 버튼에 선택이 남아 있으면 대사 한 줄을 넘기는 입력이 그 버튼까지 눌러 버린다(프롤로그 FocusSink 선례).
    /// </summary>
    public sealed class DialogueUI : MonoBehaviour
    {
        private const string KEYBOARD_INPUT_LABEL = "Space / Enter";
        private const string PAD_INPUT_PREFIX = "Pad ";

        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Text hintLabel;
        [SerializeField] private DialoguePortraitPresenter portraitPresenter;

        private DialogueLine[] lines;
        private int index;
        private Action onComplete;
        // PlayTracked 호출자의 종료 콜백 — 종료 이유를 받는다. 기존 onComplete와 동시에 차지 않는다.
        private Action<DialogueEndReason> onEnded;
        // 대화 세대. 세션을 열거나 끝낼 때마다 오른다 — 이전 콜백 안에서 더 새 대화가 열렸는지 판별한다.
        private int session;

        // 연 프레임. 대화를 시작한 입력이 같은 프레임에 첫 줄을 넘기지 않게 한다.
        private int startFrame = -1;

        // 표시 전용 포커스 자리와, 그것을 쥐기 전 뒤 화면의 선택.
        private GameObject focusSink;
        private GameObject previousSelection;

        // 안내 문구를 마지막으로 그린 조건. 패드·언어가 바뀌었을 때만 다시 그린다.
        private bool hasShownHint;
        private string shownPadLabel;
        private LocalizationLanguage shownLanguage;

        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>상위 화면(저장 모달·설정·도감)이 입력과 선택을 쥐고 있는지. 이때 대화는 넘기지도, 선택을 가져가지도 않는다.</summary>
        private static bool IsUpperModalOwningInput =>
            MenuButtonNavigation.IsUpperOverlayOpen || SettingsPanel.WasClosedThisFrame;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            portraitPresenter?.Clear();
        }

        // 비활성화·파괴(씬 이탈 포함): 포커스를 돌려주고 PlayTracked 호출자에게만 중단을 알린다.
        // 기존 Play 호출자의 완료 콜백은 예전처럼 부르지 않는다 — 언로드 중에 폼 패널 같은 후속 동작이 시작되지 않게.
        private void OnDisable()
        {
            ReleaseFocus();
            InterruptTracked();
        }

        private void OnDestroy()
        {
            ReleaseFocus();
            InterruptTracked();
        }

        /// <summary>DialogueData 재생. 라인이 없으면 즉시 complete 호출.</summary>
        public void Play(DialogueData data, Action complete)
            => Play(data != null ? data.lines : null, complete);

        /// <summary>대사 라인 배열 직접 재생(스토리 챕터 등). 라인이 없으면 즉시 complete 호출.</summary>
        public void Play(DialogueLine[] dialogueLines, Action complete)
        {
            // 이미 재생 중에 다른 호출자가 Play하면, 이전 onComplete를 먼저 정리해
            // 이전 NPC의 busy/InputLocked 고착(이동 불가)을 막는다.
            // 이유를 받는 호출자가 재생 중이었다면 교체로 알린다(정상 완료가 아니다).
            if (ReplaceCurrentSession())
            {
                // 이전 콜백이 그 사이 더 새 대화를 열었다 — 그 대화가 주인이다. 이번 요청은 교체된 것으로 끝낸다.
                complete?.Invoke();
                return;
            }

            if (dialogueLines == null || dialogueLines.Length == 0)
            {
                Close();
                complete?.Invoke();
                return;
            }

            lines = dialogueLines;
            onComplete = complete;
            index = 0;
            startFrame = Time.frameCount;
            if (root != null) root.SetActive(true);
            RefreshHint(true);
            ShowLine();
            KeepFocus();
        }

        /// <summary>
        /// 종료 이유를 받는 재생 — 정상 완료와 교체·중단·빈 대사를 구분해야 하는 새 호출자용(발견 반응 등).
        /// <see cref="DialogueEndReason.Completed"/>는 마지막 줄을 넘겨 닫혔을 때만 온다. 콜백은 한 번만 온다.
        /// 재생 중이던 기존 호출자는 예전 Play와 같이 완료 콜백으로 정리한다.
        /// </summary>
        public void PlayTracked(DialogueLine[] dialogueLines, Action<DialogueEndReason> ended)
        {
            if (ReplaceCurrentSession())
            {
                // 이전 콜백이 그 사이 더 새 대화를 열었다 — 그 대화를 지우지 않고 이번 요청을 교체로 끝낸다.
                ended?.Invoke(DialogueEndReason.Replaced);
                return;
            }

            if (dialogueLines == null || dialogueLines.Length == 0)
            {
                Close();
                ended?.Invoke(DialogueEndReason.Empty);
                return;
            }
            if (root == null || !isActiveAndEnabled)
            {
                Close();
                ended?.Invoke(DialogueEndReason.Interrupted);
                return;
            }

            lines = dialogueLines;
            onEnded = ended;
            index = 0;
            startFrame = Time.frameCount;
            root.SetActive(true);
            RefreshHint(true);
            ShowLine();
            KeepFocus();
        }

        private void Update()
        {
            if (!IsOpen) return;
            // 중단(비활성화) 뒤 다시 켜졌는데 화면만 남은 경우 — 줄이 없으니 닫는다(넘기기 입력으로 빈 배열을 읽지 않게).
            if (lines == null)
            {
                Close();
                return;
            }

            KeepFocus();
            RefreshHint(false);

            if (WasAdvancePressed()) Advance();
        }

        /// <summary>
        /// 한 프레임에 한 줄만 넘긴다 — 키보드와 패드를 같은 프레임에 함께 눌러도 판정은 하나다.
        /// 키보드가 없어도 패드만으로 넘길 수 있다.
        /// </summary>
        private bool WasAdvancePressed()
        {
            if (Time.frameCount == startFrame) return false;
            if (IsUpperModalOwningInput) return false;

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                return true;

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private void ShowLine()
        {
            var line = lines[index];
            portraitPresenter?.ShowSpeaker(line.speakerKey);
            if (speakerLabel != null) speakerLabel.text = Loc.Get(line.speakerKey);
            if (bodyLabel != null) bodyLabel.text = Loc.Get(line.textKey);
        }

        private void Advance()
        {
            index++;
            if (index >= lines.Length)
            {
                // 콜백을 먼저 떼어 낸 뒤 닫는다 — Close의 SetActive가 OnDisable을 불러도 중단으로 잘못 끝나지 않게.
                var cb = onComplete;
                var ended = onEnded;
                onComplete = null;
                onEnded = null;
                session++;
                Close();
                cb?.Invoke();
                ended?.Invoke(DialogueEndReason.Completed);
                return;
            }
            ShowLine();
        }

        /// <summary>
        /// 새 재생 요청 직전, 현재 세션의 콜백·줄을 모두 떼어 내고 세대를 올린 뒤 이전 콜백을 부른다.
        /// 기존 Play 호출자는 화면이 열려 있을 때만 완료 콜백으로, 이유를 받는 호출자는 교체로 끝난다.
        /// 이전 콜백이 그 안에서 더 새 대화를 열었으면 true — 호출자는 그 대화를 덮지 않고 물러난다.
        /// </summary>
        private bool ReplaceCurrentSession()
        {
            var prevComplete = IsOpen ? onComplete : null;
            var prevEnded = onEnded;
            onComplete = null;
            onEnded = null;
            lines = null;
            int mySession = ++session;

            prevComplete?.Invoke();
            prevEnded?.Invoke(DialogueEndReason.Replaced);

            return session != mySession && lines != null;
        }

        /// <summary>
        /// 비활성화·파괴 중 중단. 화면(root)은 여기서 건드리지 않는다 — 파괴·비활성화 도중의 SetActive를 피하고,
        /// 다시 켜지면 <see cref="Update"/>가 줄 없는 화면을 닫는다. 줄은 비워 넘기기 입력이 와도 진행하지 않는다.
        /// 콜백을 먼저 비우고 부른다(재진입 시 두 번 오지 않게).
        /// </summary>
        private void InterruptTracked()
        {
            var ended = onEnded;
            if (ended == null) return;
            onEnded = null;
            lines = null;
            session++;
            ended.Invoke(DialogueEndReason.Interrupted);
        }

        private void Close()
        {
            ReleaseFocus();
            portraitPresenter?.Clear();
            if (root != null) root.SetActive(false);
            lines = null;
        }

        // ───────────────────────── 안내 문구 ─────────────────────────

        /// <summary>
        /// 다음 줄 안내. 패드가 있으면 실제 확인 버튼 이름을 함께 적는다.
        /// <c>Dialogue_NextHint</c>는 보스 등장 연출이 고정 문구로 같이 쓰므로 형식 문자열로 바꾸지 않고,
        /// 프롤로그·엔딩과 같은 <c>Story_SequenceNextHintFormat</c>을 쓴다.
        /// </summary>
        private void RefreshHint(bool isForced)
        {
            if (hintLabel == null) return;

            string padLabel = GamepadConfirmLabel();
            var language = Loc.CurrentLanguage;
            if (!isForced && hasShownHint && padLabel == shownPadLabel && language == shownLanguage) return;

            hasShownHint = true;
            shownPadLabel = padLabel;
            shownLanguage = language;
            hintLabel.text = Loc.GetFormat(StringKey.Story_SequenceNextHintFormat, InputLabel(padLabel));
        }

        private static string InputLabel(string padLabel)
            => padLabel == null ? KEYBOARD_INPUT_LABEL : KEYBOARD_INPUT_LABEL + " / " + PAD_INPUT_PREFIX + padLabel;

        /// <summary>연결된 패드의 실제 확인 버튼 이름(Xbox A, PlayStation Cross 등). 패드가 없으면 null.</summary>
        private static string GamepadConfirmLabel()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return null;

            var button = gamepad.buttonSouth;
            if (!string.IsNullOrEmpty(button.shortDisplayName)) return button.shortDisplayName;
            if (!string.IsNullOrEmpty(button.displayName)) return button.displayName;
            return button.name;
        }

        // ───────────────────────── 포커스 ─────────────────────────

        /// <summary>
        /// 선택을 포커스 자리에 붙잡는다. 처음 가져올 때 뒤 화면의 선택을 기억한다.
        /// 상위 화면이 선택을 쥔 동안에는 손대지 않는다 — 닫히며 돌려준 선택을 다음 프레임에 다시 가져온다.
        /// </summary>
        private void KeepFocus()
        {
            if (IsUpperModalOwningInput) return;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;

            var sink = EnsureFocusSink();
            if (sink == null) return;

            var selected = eventSystem.currentSelectedGameObject;
            if (selected == sink) return;
            if (selected != null && previousSelection == null) previousSelection = selected;
            eventSystem.SetSelectedGameObject(sink);
        }

        /// <summary>
        /// 포커스 자리가 선택을 쥐고 있을 때만 이전 선택을 돌려준다. 이전 선택이 사라졌거나 못 누르는 상태면 비운다.
        /// 다른 화면이 쥔 선택은 그대로 둔다.
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

        /// <summary>
        /// 대화 박스 아래에 표시 전용 포커스 자리를 한 번 만든다(씬 배선은 그대로).
        /// 그래픽·클릭 콜백·탐색이 없어 Submit·Move가 와도 아무 일도 일어나지 않는다.
        /// </summary>
        private GameObject EnsureFocusSink()
        {
            if (focusSink != null) return focusSink;
            if (root == null) return null;

            focusSink = new GameObject("FocusSink", typeof(RectTransform));
            var rect = (RectTransform)focusSink.transform;
            rect.SetParent(root.transform, false);
            rect.sizeDelta = Vector2.zero;

            var sink = focusSink.AddComponent<Selectable>();
            sink.transition = Selectable.Transition.None;
            sink.navigation = new Navigation { mode = Navigation.Mode.None };
            return focusSink;
        }
    }
}
