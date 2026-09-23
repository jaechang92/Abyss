using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Run;
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

            if (!SceneFlowController.HasInstance)
            {
                // 나갈 수 없으면 버리지도 않는다 — 버린 런 위에 남으면 사망해도 결과 창이 안 뜬다.
                Debug.LogWarning("[PausePanel] SceneFlowController 미가동 — 타이틀 전환 불가. 런을 유지한다.");
                return;
            }
            if (SceneFlowController.Instance.IsLoading) return;   // 이미 나가는 중(키보드 Submit 재입력 등)

            // 🔴 정지를 풀지 않는다(2026-09-23). 예전에는 여기서 해제를 요청해, 페이드·로드 동안 게임이 다시 돌고
            //    그 사이 ESC로 다시 정지하면 정지 상태가 타이틀·로비까지 따라갈 수 있었다. 정지된 채 나가고,
            //    씬 경계의 회수는 SceneFlowController가 맡는다(페이드·로드는 unscaled라 timeScale 0에서도 돈다).
            //
            // 🔴 런 포기는 전환이 확정된 순간에 한다. 로드가 실패하면 포기하지 않아야 정지 메뉴로 돌아온
            //    플레이어가 살아 있는 런을 이어갈 수 있다. 포기를 빼먹으면 isRunActive가 true로 남아
            //    재입장 시 StartNewRun이 초기화를 건너뛴다(레벨·골드·무기·통계 이월).
            //    EndRun이 아니라 AbandonRun인 이유: EndRun의 OnRunEnded가 결과 패널을 띄우고
            //    흐름 FSM을 Result로 보내 타이틀 전환과 부딪힌다.
            //    콜백은 정적 메서드다 — 옛 씬이 파괴된 뒤에 이 패널을 건드리는 경로를 만들지 않는다.
            _ = SceneFlowController.Instance.LoadTitleAsync(AbandonCurrentRun);
        }

        private static void AbandonCurrentRun()
        {
            if (RunManager.HasInstance) RunManager.Instance.AbandonRun();
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
