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
    /// 완주(<see cref="RunEndReason.Cleared"/>) 시에는 <see cref="EndingSequencePanel"/>을 먼저 재생하고
    /// 크레딧이 끝난 뒤에 이 패널을 띄운다(완주 루프 계획 2-2). 런 종료 후 화면 순서를 이미 이 컴포넌트가
    /// 쥐고 있어서 여기에 두었다 — 별도 오케스트레이터를 만들면 씬 배선이 하나 더 늘어난다.
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

        // 패널이 켜진 프레임. 크레딧을 끝낸 Enter가 같은 프레임에 '즉시 재시작'까지 누르는 것을 막는다.
        private int activatedFrame = -1;

        /// <summary>직전 런이 완주로 끝났는가. 제목·버튼·엔딩 재생 여부가 여기에 갈린다.</summary>
        private static bool IsClearedRun =>
            RunManager.HasInstance && RunManager.Instance.LastRunEndReason == RunEndReason.Cleared;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
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
            // 켜진 첫 프레임은 입력을 받지 않는다 — 크레딧을 넘긴 그 Enter가 재시작까지 삼킨다.
            if (Time.frameCount == activatedFrame) return;

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

            // 완주: 엔딩 자막 → 크레딧을 먼저 보여주고 그다음 통계.
            // 승리 직후 숫자부터 띄우면 연출이 끊긴다.
            EndingSequencePanel.Play(HandleEndingFinished);
        }

        private void HandleEndingFinished()
        {
            // 크레딧을 건너뛰어도 엔딩에 도달한 것으로 본다 — 경로를 끝까지 밟은 것은 같다.
            MetaSaveService.Instance.MarkEndingSeen();
            ShowPanel();
        }

        private void ShowPanel()
        {
            if (RunManager.HasInstance)
            {
                Populate(RunManager.Instance.Stats);
            }
            if (root != null) root.SetActive(true);
            activatedFrame = Time.frameCount;
            FocusRestartButton();
        }

        private void FocusRestartButton()
        {
            if (EventSystem.current == null || restartButton == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        private void Populate(RunStats stats)
        {
            if (stats == null) return;

            // 완주와 사망은 같은 패널을 쓰되 제목과 두 번째 버튼의 목적지가 다르다.
            // 라벨만 갈아끼우므로 ResultPanelBuilder 수정·메뉴 재실행이 필요 없다.
            bool cleared = IsClearedRun;
            if (titleText != null) titleText.text = cleared ? "심연 탈출" : "런 종료";
            if (restartLabel != null) restartLabel.text = "즉시 재시작 (Enter)";
            if (lobbyLabel != null) lobbyLabel.text = cleared ? "타이틀로" : "로비로";

            if (killsText != null) killsText.text = $"처치 수: {stats.enemiesKilled}";
            if (comboText != null) comboText.text = $"최장 콤보: {stats.maxCombo}";

            string dominantId = stats.GetDominantFormId();
            float ratio = stats.GetFormRatio(dominantId);
            if (dominantFormText != null)
            {
                dominantFormText.text = string.IsNullOrEmpty(dominantId)
                    ? "주 사용 폼: —"
                    : $"주 사용 폼: {dominantId} ({ratio:P0})";
            }

            if (formsUsedText != null)
            {
                formsUsedText.text = stats.formsUsed.Count == 0
                    ? "사용 폼: —"
                    : "사용 폼: " + string.Join(", ", stats.formsUsed);
            }

            if (skillsText != null)
            {
                skillsText.text = stats.draftedSkillIds.Count == 0
                    ? "드래프트 스킬: —"
                    : $"드래프트 스킬 {stats.draftedSkillIds.Count}개: " + string.Join(", ", stats.draftedSkillIds);
            }

            if (stageText != null)
            {
                stageText.text = string.IsNullOrEmpty(stats.stageReached) ? "도달: —" : $"도달: {stats.stageReached}";
            }

            if (elapsedText != null)
            {
                int mins = Mathf.FloorToInt(stats.totalElapsedSeconds / 60f);
                int secs = Mathf.FloorToInt(stats.totalElapsedSeconds % 60f);
                elapsedText.text = $"경과: {mins:D2}:{secs:D2}";
            }

            if (abyssEarnedText != null)
            {
                int earned = RunManager.HasInstance ? RunManager.Instance.LastRunAbyssShardsEarned : 0;
                int total = MetaSaveService.Instance.Current.abyssShardsTotal;
                abyssEarnedText.text = $"Abyss 획득: +{earned}  (누적 {total})";
            }
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

        /// <summary>
        /// 두 번째 버튼. 사망은 로비로(다음 런 준비), 완주는 타이틀로 —
        /// 엔딩까지 본 뒤에는 "게임을 한 바퀴 끝냈다"는 지점이 타이틀이다.
        /// </summary>
        private void HandleReturnToLobby()
        {
            Time.timeScale = 1f;

            // SceneFlowController 미가동(분리 전 상태) 시 안전하게 무시.
            if (!Abyss.Runtime.Flow.SceneFlowController.HasInstance)
            {
                Debug.LogWarning("[ResultPanelPresenter] SceneFlowController 미가동 — 씬 전환 불가.");
                return;
            }

            var flow = Abyss.Runtime.Flow.SceneFlowController.Instance;
            _ = IsClearedRun ? flow.LoadTitleAsync() : flow.LoadLobbyAsync();
        }
    }
}
