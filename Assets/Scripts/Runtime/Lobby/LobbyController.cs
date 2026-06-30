using Abyss.Runtime.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비 씬 진입점. "시작" 버튼 또는 Enter/Space 입력으로 Run 씬을 로드한다.
    /// 씬 분리 2단계 — 현재는 런 시작만 담당하는 빈 골격.
    /// 3단계에서 폼 선택 UI + RunStartContext 주입으로 확장 예정.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button startButton;

        private void Awake()
        {
            // 로비 진입 시점에는 직전 런의 timeScale 변경이 남아 있을 수 있으므로 정상화.
            Time.timeScale = 1f;
            if (startButton != null) startButton.onClick.AddListener(StartRun);
        }

        private void Start()
        {
            // EventSystem.current는 EventSystem.OnEnable에서 설정되므로
            // OnEnable이 아닌 Start에서 포커스해야 준비 완료가 보장된다.
            FocusStartButton();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                StartRun();
            }
        }

        private void FocusStartButton()
        {
            if (EventSystem.current == null || startButton == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        /// <summary>
        /// Run 씬으로 전환. SceneFlowController는 영속 싱글톤이라
        /// Bootstrap을 거치지 않고 로비를 직접 플레이해도 자동 생성된다.
        /// </summary>
        private void StartRun()
        {
            Time.timeScale = 1f;
            _ = SceneFlowController.Instance.LoadRunAsync();
        }
    }
}
