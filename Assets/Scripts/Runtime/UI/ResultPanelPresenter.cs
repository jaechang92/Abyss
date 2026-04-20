using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 런 종료 결과 패널. Analyst MF-6 — 7항목 표시 + 즉시 재시작 플로우.
    /// OnRunEnded 구독 → 활성화 + 기본 포커스 [즉시 재시작] 버튼.
    /// Enter/Space 즉시 재시작, 버튼 클릭 동일.
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

        [Header("버튼")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Text restartLabel;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
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
            if (RunManager.HasInstance)
            {
                Populate(RunManager.Instance.Stats);
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

        private void Populate(RunStats stats)
        {
            if (stats == null) return;

            if (titleText != null) titleText.text = "런 종료";
            if (restartLabel != null) restartLabel.text = "즉시 재시작 (Enter)";

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
        }

        private void HandleRestart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
