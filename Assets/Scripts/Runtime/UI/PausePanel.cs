using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 일시정지 패널. 정지 자체는 GameFlowController(FSM)가 소유하고, 이 패널은 '사실' 이벤트
    /// (OnGamePaused/OnGameResumed)에 맞춰 표시만 한다 — 정지 상태의 SoT를 UI가 갖지 않는다.
    ///
    /// 타이틀 복귀·게임 종료는 진행 중인 런을 버리는 행위라 2단계 확인을 둔다(FormReplacementModal 선례).
    /// </summary>
    public sealed class PausePanel : MonoBehaviour
    {
        [Header("루트")]
        [Tooltip("표시/숨김 대상. 이 컴포넌트가 붙은 오브젝트가 아니라 자식 Body를 지정할 것.")]
        [SerializeField] private GameObject root;

        [Header("버튼")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button toTitleButton;
        [SerializeField] private Button quitButton;

        private const string TO_TITLE_DEFAULT = "타이틀로";
        private const string QUIT_DEFAULT = "게임 종료";
        private const string CONFIRM_SUFFIX = " — 확정? (다시 클릭)";

        // 2단계 확인 무장 상태. 한 쪽을 무장하면 다른 쪽은 해제해 오인 클릭을 막는다.
        private bool toTitleArmed;
        private bool quitArmed;
        private Text toTitleLabel;
        private Text quitLabel;

        private void Awake()
        {
            // 이 컴포넌트는 GameEvents를 직접 구독하므로 자기 자신을 끄면 안 된다(OnDisable → 구독 해제 →
            // 다시 켜줄 주체 소멸). 빌더가 root를 자식 Body로 배선하고 그것만 토글한다.
            if (root == null)
            {
                Debug.LogWarning($"[PausePanel] root 미배선 ({name}) — 자기 자신을 토글하면 구독이 끊긴다. Build HUD 재실행 필요.");
            }
            if (resumeButton != null) resumeButton.onClick.AddListener(RequestResume);
            // 설정 패널은 씬에 배치하지 않고 SettingsPanel이 동적 생성해 공유한다(타이틀과 같은 인스턴스).
            if (settingsButton != null) settingsButton.onClick.AddListener(SettingsPanel.Open);
            if (toTitleButton != null)
            {
                toTitleButton.onClick.AddListener(OnToTitleClicked);
                toTitleLabel = toTitleButton.GetComponentInChildren<Text>();
                if (toTitleLabel != null && !string.IsNullOrEmpty(toTitleLabel.text)) toTitleLabel.text = TO_TITLE_DEFAULT;
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
                quitLabel = quitButton.GetComponentInChildren<Text>();
                if (quitLabel != null && !string.IsNullOrEmpty(quitLabel.text)) quitLabel.text = QUIT_DEFAULT;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnGamePaused += HandleGamePaused;
            GameEvents.OnGameResumed += HandleGameResumed;
        }

        private void OnDisable()
        {
            GameEvents.OnGamePaused -= HandleGamePaused;
            GameEvents.OnGameResumed -= HandleGameResumed;
        }

        private void HandleGamePaused() => SetVisible(true);

        private void HandleGameResumed()
        {
            DisarmAll();
            SetVisible(false);
        }

        private void RequestResume() => GameEvents.RaiseResumeRequested();

        private void OnToTitleClicked()
        {
            if (!toTitleArmed)
            {
                Arm(ref toTitleArmed, toTitleLabel, TO_TITLE_DEFAULT);
                Disarm(ref quitArmed, quitLabel, QUIT_DEFAULT);
                return;
            }

            // 정지를 먼저 풀어 timeScale을 되돌린다 — 정지된 채 씬을 넘기면 다음 씬이 멈춘 상태로 시작한다.
            GameEvents.RaiseResumeRequested();

            if (SceneFlowController.HasInstance)
            {
                _ = SceneFlowController.Instance.LoadTitleAsync();
            }
            else
            {
                Debug.LogWarning("[PausePanel] SceneFlowController 미가동 — 타이틀 전환 불가.");
            }
        }

        private void OnQuitClicked()
        {
            if (!quitArmed)
            {
                Arm(ref quitArmed, quitLabel, QUIT_DEFAULT);
                Disarm(ref toTitleArmed, toTitleLabel, TO_TITLE_DEFAULT);
                return;
            }
            QuitGame();
        }

        /// <summary>에디터에서는 Application.Quit이 아무 일도 하지 않으므로 플레이 모드를 끈다.</summary>
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Arm(ref bool armed, Text label, string defaultText)
        {
            armed = true;
            if (label != null) label.text = defaultText + CONFIRM_SUFFIX;
        }

        private void Disarm(ref bool armed, Text label, string defaultText)
        {
            armed = false;
            if (label != null) label.text = defaultText;
        }

        private void DisarmAll()
        {
            Disarm(ref toTitleArmed, toTitleLabel, TO_TITLE_DEFAULT);
            Disarm(ref quitArmed, quitLabel, QUIT_DEFAULT);
        }

        private void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            if (target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
