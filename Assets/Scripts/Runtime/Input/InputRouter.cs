using Abyss.Runtime.Events;
using Singleton_Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Input
{
    /// <summary>
    /// Gameplay/UI 입력 맵을 상호 배타적으로 활성화하는 라우터.
    /// 드래프트 오픈 시 UI 모드로, 닫힘 시 Gameplay 모드로 자동 전환.
    /// Analyst §6 - InputRouter 싱글톤으로 모드 토글.
    /// </summary>
    public sealed class InputRouter : SingletonManager<InputRouter>
    {
        public enum InputMode
        {
            Disabled,
            Gameplay,
            UI
        }

        [Header("Input Actions 자원")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string gameplayMapName = "Player";
        [SerializeField] private string uiMapName = "UI";

        [Header("초기 모드")]
        [SerializeField] private InputMode initialMode = InputMode.Gameplay;

        private InputActionMap gameplayMap;
        private InputActionMap uiMap;
        private InputMode currentMode = InputMode.Disabled;

        public InputMode CurrentMode => currentMode;
        public InputActionAsset Actions => inputActions;
        public InputActionMap GameplayMap => gameplayMap;
        public InputActionMap UIMap => uiMap;

        protected override void Awake()
        {
            base.Awake();
            ResolveMaps();
        }

        private void OnEnable()
        {
            GameEvents.OnDraftOpened += HandleDraftOpened;
            GameEvents.OnDraftClosed += HandleDraftClosed;
            GameEvents.OnGamePaused += HandleGamePaused;
            GameEvents.OnGameResumed += HandleGameResumed;
        }

        private void OnDisable()
        {
            GameEvents.OnDraftOpened -= HandleDraftOpened;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnGamePaused -= HandleGamePaused;
            GameEvents.OnGameResumed -= HandleGameResumed;
        }

        private void Start()
        {
            if (inputActions != null) SwitchMode(initialMode);
        }

        public void SwitchMode(InputMode mode)
        {
            currentMode = mode;

            if (gameplayMap != null)
            {
                if (mode == InputMode.Gameplay) gameplayMap.Enable();
                else gameplayMap.Disable();
            }
            if (uiMap != null)
            {
                if (mode == InputMode.UI) uiMap.Enable();
                else uiMap.Disable();
            }

            Debug.Log($"[InputRouter] Mode → {mode}");
        }

        /// <summary>
        /// InputActionAsset이 런타임에 교체되거나 처음 할당된 경우 호출.
        /// </summary>
        public void ResolveMaps()
        {
            if (inputActions == null)
            {
                Debug.LogWarning("[InputRouter] InputActionAsset 미할당 — 씬의 InputRouter 컴포넌트에 연결 필요.");
                return;
            }

            gameplayMap = inputActions.FindActionMap(gameplayMapName);
            uiMap = inputActions.FindActionMap(uiMapName);

            if (gameplayMap == null) Debug.LogWarning($"[InputRouter] '{gameplayMapName}' 맵 미발견");
            if (uiMap == null) Debug.LogWarning($"[InputRouter] '{uiMapName}' 맵 미발견");
        }

        private void HandleDraftOpened() => SwitchMode(InputMode.UI);
        private void HandleDraftClosed() => SwitchMode(InputMode.Gameplay);

        // 정지가 실제로 수락됐을 때만 모드를 바꾼다(거부된 요청에 반응하면 드래프트 중 게임플레이 입력이 살아난다).
        private void HandleGamePaused() => SwitchMode(InputMode.UI);
        private void HandleGameResumed() => SwitchMode(InputMode.Gameplay);
    }
}
