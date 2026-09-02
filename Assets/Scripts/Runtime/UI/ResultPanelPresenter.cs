using Abyss.Runtime.Events;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 런 종료 결과 패널. Analyst MF-6 — 7항목 + P-21 abyss_earned 라인 + 즉시 재시작 플로우.
    /// OnRunEnded 구독 → 활성화 + 기본 포커스 [즉시 재시작] 버튼.
    /// Enter/Space 즉시 재시작, 버튼 클릭 동일.
    ///
    /// <b>이 패널은 사망 전용이다.</b> 완주(<see cref="RunEndReason.Cleared"/>)는
    /// <see cref="EndingSequencePanel"/>이 자막·크레딧에 이어 통계까지 검은 화면에 직접 보여주고
    /// 타이틀로 나간다 — 엔딩 뒤에 [즉시 재시작]이 있는 런 종료 창을 띄우면 톤이 끊긴다.
    /// 여기서는 분기와 엔딩 종료 후 씬 전환만 담당한다(런 종료 후 화면 순서를 이미 이 컴포넌트가
    /// 쥐고 있어서 — 별도 오케스트레이터를 만들면 씬 배선이 하나 더 늘어난다).
    /// </summary>
    public sealed class ResultPanelPresenter : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;

        [Header("통계 7항목 (MF-6)")]
        [SerializeField] private Text killsText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text dominantFormText;
        [SerializeField] private Text formsUsedText;
        [SerializeField] private Text skillsText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text elapsedText;

        [Header("메타 정산 (P-21)")]
        [SerializeField] private Text abyssEarnedText;

        [Header("버튼")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Text restartLabel;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Text lobbyLabel;

        /// <summary>직전 런이 완주로 끝났는가. 이 패널을 띄울지 엔딩에 넘길지가 여기에 갈린다.</summary>
        private static bool IsClearedRun =>
            RunManager.HasInstance && RunManager.Instance.LastRunEndReason == RunEndReason.Cleared;

        private void Awake()
        {
            if (root != null)
            {
                root.SetActive(false);

                // 결과 패널을 모달 층 위로 올린다. 씬 배선이 아니라 여기서 하는 이유는 두 가지다 —
                // ① 빌더에서 하면 정렬 하나를 위해 ResultPanel 자식 전체를 재생성해야 하고(빌더가
                //    파괴 후 재구성한다), 그 과정에서 씬의 fileID가 전면 재발급돼 손배선이 끊긴다.
                // ② 동적 오버레이 패널 대부분이 이미 런타임에 층을 정한다(UiFactory.CreateOverlayCanvas).
                //    씬에 배치됐다는 이유만으로 규약을 이원화할 근거가 없다.
                UiFactory.ApplyOverlaySorting(root, UiSortingOrder.RunResult);
            }
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(HandleReturnToLobby);
        }

        private void OnEnable()
        {
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                HandleRestart();
            }
        }

        private void HandleRunEnded()
        {
            if (!IsClearedRun)
            {
                ShowPanel();
                return;
            }

            // 완주: 자막 → 크레딧 → 통계를 엔딩이 전부 맡는다. 이 패널은 뜨지 않는다.
            EndingSequencePanel.Play(HandleEndingFinished);
        }

        /// <summary>엔딩(통계 포함)이 끝난 뒤. 완주는 로비가 아니라 타이틀로 돌아간다.</summary>
        private void HandleEndingFinished()
        {
            // 크레딧·통계를 건너뛰어도 엔딩에 도달한 것으로 본다 — 경로를 끝까지 밟은 것은 같다.
            MetaSaveService.Instance.MarkEndingSeen();

            Time.timeScale = 1f;
            if (Abyss.Runtime.Flow.SceneFlowController.HasInstance)
            {
                _ = Abyss.Runtime.Flow.SceneFlowController.Instance.LoadTitleAsync();
            }
            else
            {
                Debug.LogWarning("[ResultPanelPresenter] SceneFlowController 미가동 — 타이틀 전환 불가.");
            }
        }

        private void ShowPanel()
        {
            if (RunManager.HasInstance)
            {
                // 진행 중 통계(Stats)가 아니라 정산 시점에 굳은 요약을 읽는다 —
                // 세이브에 들어간 것과 화면에 뜨는 것이 같은 객체다.
                Populate(RunManager.Instance.LastRunSummary);
            }
            if (root != null) root.SetActive(true);
            FocusRestartButton();
        }

        private void FocusRestartButton()
        {
            if (EventSystem.current == null || restartButton == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        private void Populate(RunSummary stats)
        {
            if (stats == null) return;

            // 이 패널은 사망 전용이다(완주는 EndingSequencePanel이 통계까지 맡는다).
            if (titleText != null) titleText.text = "런 종료";
            if (restartLabel != null) restartLabel.text = "즉시 재시작 (Enter)";
            if (lobbyLabel != null) lobbyLabel.text = "로비로";

            // 문구는 RunSummaryText가 소유한다 — 엔딩 통계 화면과 같은 표기를 쓰기 위함.
            if (killsText != null) killsText.text = RunSummaryText.Kills(stats);
            if (comboText != null) comboText.text = RunSummaryText.Combo(stats);
            if (dominantFormText != null) dominantFormText.text = RunSummaryText.DominantForm(stats);
            if (formsUsedText != null) formsUsedText.text = RunSummaryText.FormsUsed(stats);
            if (skillsText != null) skillsText.text = RunSummaryText.Skills(stats);
            if (stageText != null) stageText.text = RunSummaryText.Stage(stats);
            if (elapsedText != null) elapsedText.text = RunSummaryText.Elapsed(stats);
            if (abyssEarnedText != null) abyssEarnedText.text = RunSummaryText.AbyssEarned(stats);
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;

            // 씬 분리: 명시적으로 Run 씬을 재로드(buildIndex 의존 제거).
            // SceneFlowController 미가동(분리 전 상태) 시 기존 동작으로 폴백.
            if (Abyss.Runtime.Flow.SceneFlowController.HasInstance)
            {
                _ = Abyss.Runtime.Flow.SceneFlowController.Instance.LoadRunAsync();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        /// <summary>두 번째 버튼. 이 패널은 사망 전용이므로 목적지는 다음 런을 준비하는 로비다.</summary>
        private void HandleReturnToLobby()
        {
            Time.timeScale = 1f;

            // 로비 씬으로 복귀. SceneFlowController 미가동(분리 전 상태) 시 안전하게 무시.
            if (Abyss.Runtime.Flow.SceneFlowController.HasInstance)
            {
                _ = Abyss.Runtime.Flow.SceneFlowController.Instance.LoadLobbyAsync();
            }
            else
            {
                Debug.LogWarning("[ResultPanelPresenter] SceneFlowController 미가동 — 로비 전환 불가.");
            }
        }
    }
}
