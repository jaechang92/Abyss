using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// Stage1 첫 플레이 행동 기반 튜토리얼. <see cref="FirstPlayTutorialBootstrap"/>이 Run 씬마다 하나만 만든다.
    ///
    /// 🔑 <b>행동을 확인한다, 입력을 세지 않는다.</b> 이동·점프·대시·공격은 플레이어가 성공 지점에서 내는
    /// <see cref="PlayerCharacter.OnTutorialAction"/>으로, 폼 교체는 성공한 교체의 <see cref="GameEvents.OnFormSwapped"/>로,
    /// 드래프트는 실제 드래프트 세션 중의 <see cref="GameEvents.OnSkillDrafted"/>로 받는다.
    ///
    /// 첫 방은 공간 과제로 안내한다. 첫 적 예약은 디렉터가 소유하며 학습 실패로 문을 잠그지 않는다.
    /// 두 번째 폼이 없으면 교체 안내를 띄우지 않고 기다린다.
    ///
    /// 멈춤: 일시정지·모달 중에는 행동 구독을 떼고 띠를 숨긴다. 사망·포기·결과·Stage2 진입·씬 전환이면
    /// 저장 없이 끝낸다(다음 런에 다시 안내). 저장은 6행동 완료 또는 건너뛰기 때만 한다.
    /// </summary>
    public sealed partial class FirstPlayTutorialController : MonoBehaviour
    {
        private const float OBJECTIVE_DURATION = 6f;
        private const float COMPLETE_DURATION = 3f;
        private const float PAD_SKIP_HOLD_SECONDS = 1.5f;
        private const float REFRESH_INTERVAL = 0.25f;

        private static FirstPlayTutorialController active;

        /// <summary>지금 살아 있는 튜토리얼이 있는가. 부트스트랩의 중복 생성 방지용.</summary>
        public static bool HasActiveInstance => active != null;

        private StageDirector director;
        private PlayerCharacter player;
        private FormController formController;
        private PlayerInput playerInput;
        private FirstPlayTutorialPanel panel;

        // 구독 해제는 이 참조로 한다 — 플레이어가 먼저 파괴돼도(Unity == null) 관리 객체로는 남아 있어 뗄 수 있다.
        private PlayerCharacter subscribedPlayer;

        private bool isSubscribed;
        private bool isPaused;
        private bool isModalOpen;
        private bool isDraftSession;
        private bool isEnding;
        private bool isRunInterrupted;

        private float objectiveTimer = OBJECTIVE_DURATION;
        private float completeTimer;
        private float padSkipHeld;
        private float refreshTimer;
        private bool isDirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        /// <summary>
        /// 컨트롤러를 <paramref name="scene"/>에 만든다. 씬과 함께 파괴되므로 씬 전환이 곧 정리다.
        /// </summary>
        public static FirstPlayTutorialController Create(Scene scene, StageDirector director, PlayerCharacter player)
        {
            if (active != null || director == null || player == null) return null;

            var go = new GameObject("FirstPlayTutorial");
            if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);

            var controller = go.AddComponent<FirstPlayTutorialController>();
            controller.Initialize(director, player);
            return controller;
        }

        private void Initialize(StageDirector stageDirector, PlayerCharacter playerCharacter)
        {
            active = this;
            director = stageDirector;
            player = playerCharacter;
            formController = player.GetComponent<FormController>();
            playerInput = player.GetComponent<PlayerInput>();

            panel = FirstPlayTutorialPanel.Create(transform);
            panel.OnSkipClicked += Skip;

            Subscribe();
            UpdatePlayerSubscription();
        }

        // AddComponent 의 첫 OnEnable 은 Initialize 전에 온다 — 한 번이라도 꺼졌던 뒤의 OnEnable 만 재활성화다.
        private bool wasDisabled;

        private void OnEnable()
        {
            if (!wasDisabled) return;

            // 꺼져 있던 동안 놓친 일시정지·모달·결과 이벤트로 흐름 상태를 믿을 수 없다 — 저장 없이 이 학습을 끝낸다.
            // 다음 런(또는 다음 Run 씬 로드)에 부트스트랩이 다시 만든다.
            isEnding = true;
            Destroy(gameObject);
        }

        /// <summary>꺼지면 저장 없이 띠를 숨기고 모든 구독을 뗀다. 파괴 경로(씬 언로드 포함)도 여기를 먼저 지난다.</summary>
        private void OnDisable()
        {
            wasDisabled = true;
            if (!isRunInterrupted && director != null) director.ReleaseTutorialOpening();
            Unsubscribe();
            UnsubscribeCompleteDismiss();
            padSkipHeld = 0f;
            if (panel != null) panel.SetVisible(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnsubscribeCompleteDismiss();
            if (panel != null) panel.OnSkipClicked -= Skip;
            if (active == this) active = null;
        }

        private void Subscribe()
        {
            if (isSubscribed) return;
            isSubscribed = true;

            GameEvents.OnGamePaused += HandleGamePaused;
            GameEvents.OnGameResumed += HandleGameResumed;
            GameEvents.OnDraftOpened += HandleDraftOpened;
            GameEvents.OnDraftClosed += HandleDraftClosed;
            GameEvents.OnDraftOptionsReady += HandleDraftOptionsReady;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnFormSwapped += HandleFormSwapped;
            GameEvents.OnPlayerDead += HandleRunInterrupted;
            GameEvents.OnRunAbandoned += HandleRunInterrupted;
            GameEvents.OnRunEnded += HandleRunInterrupted;
            InputSystem.onActionChange += HandleActionChange;
            Loc.AddLanguageChangedListener(HandleLanguageChanged);
        }

        private void Unsubscribe()
        {
            SetPlayerSubscribed(false);
            if (!isSubscribed) return;
            isSubscribed = false;

            GameEvents.OnGamePaused -= HandleGamePaused;
            GameEvents.OnGameResumed -= HandleGameResumed;
            GameEvents.OnDraftOpened -= HandleDraftOpened;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnDraftOptionsReady -= HandleDraftOptionsReady;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnFormSwapped -= HandleFormSwapped;
            GameEvents.OnPlayerDead -= HandleRunInterrupted;
            GameEvents.OnRunAbandoned -= HandleRunInterrupted;
            GameEvents.OnRunEnded -= HandleRunInterrupted;
            InputSystem.onActionChange -= HandleActionChange;
            Loc.RemoveLanguageChangedListener(HandleLanguageChanged);
        }

        /// <summary>행동을 세도 되는가 — 일시정지·모달(드래프트 포함)·종료 중에는 세지 않는다.</summary>
        private bool CanCountPlay => !isPaused && !isModalOpen && !isEnding;

        private void UpdatePlayerSubscription() => SetPlayerSubscribed(CanCountPlay && player != null);

        private void SetPlayerSubscribed(bool isWanted)
        {
            if (isWanted)
            {
                if (subscribedPlayer is not null) return;
                subscribedPlayer = player;
                subscribedPlayer.OnTutorialAction += HandlePlayerAction;
                return;
            }

            if (subscribedPlayer is null) return;
            subscribedPlayer.OnTutorialAction -= HandlePlayerAction;
            subscribedPlayer = null;
        }

        private void Update()
        {
            if (isEnding)
            {
                // 완료 문구 표시 중에도 Stage1 을 벗어나거나 참조가 사라지면 바로 걷는다.
                completeTimer -= Time.unscaledDeltaTime;
                if (completeTimer <= 0f || director == null || player == null || director.CurrentStageIndex > 0)
                {
                    DismissComplete();
                }
                return;
            }

            // 플레이어·디렉터가 사라졌거나 Stage1을 벗어났으면 저장 없이 끝낸다.
            if (director == null || player == null || director.CurrentStageIndex > 0)
            {
                Abort();
                return;
            }

            bool isVisible = ShouldShowPanel();
            UpdateSpatialLearning();
            if (isVisible)
            {
                if (!isModalOpen && !isPaused && objectiveTimer > 0f) objectiveTimer -= Time.unscaledDeltaTime;
                UpdatePadSkip();
            }
            else
            {
                padSkipHeld = 0f;
            }

            refreshTimer -= Time.unscaledDeltaTime;
            if (isDirty || refreshTimer <= 0f)
            {
                refreshTimer = REFRESH_INTERVAL;
                isDirty = false;
                Render(isVisible);
            }
        }

        /// <summary>
        /// 패드 Select 길게 누르기. 상태만 읽는다 — 입력을 소비하거나 맵을 바꾸지 않으며,
        /// Start(일시정지)는 보지 않는다.
        /// </summary>
        private void UpdatePadSkip()
        {
            var pad = Gamepad.current;
            if (pad == null || SaveStatusOverlay.IsCapturingInput || !pad.selectButton.isPressed)
            {
                if (padSkipHeld > 0f) isDirty = true;
                padSkipHeld = 0f;
                return;
            }

            padSkipHeld += Time.unscaledDeltaTime;
            isDirty = true;
            if (padSkipHeld >= PAD_SKIP_HOLD_SECONDS) Skip();
        }

        // ───────────────────────── 이벤트 ─────────────────────────

        private void HandleGamePaused()
        {
            isPaused = true;
            OnFlowChanged();
        }

        private void HandleGameResumed()
        {
            isPaused = false;
            OnFlowChanged();
        }

        // DraftOpened 는 드래프트 말고도 이벤트·상점·폼 교체 모달이 정지용으로 빌려 쓴다 — 여기서는 「모달」로만 본다.
        private void HandleDraftOpened()
        {
            isModalOpen = true;
            OnFlowChanged();
        }

        private void HandleDraftClosed()
        {
            isModalOpen = false;
            isDraftSession = false;
            OnFlowChanged();
        }

        // 진짜 드래프트만 선택지를 낸다. 리롤로 다시 와도 같은 세션이다.
        private void HandleDraftOptionsReady(DraftOptions options)
        {
            isDraftSession = true;
            OnFlowChanged();
        }

        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason reason)
        {
            if (isEnding || skill == null || !isDraftSession) return;
            MarkDone(TutorialStep.Draft);
        }

        private void HandleFormSwapped(FormData previous, FormData next)
        {
            if (!CanCountPlay || next == null || previous == next) return;
            MarkDone(TutorialStep.Swap);
        }

        private void HandlePlayerAction(PlayerTutorialAction action)
        {
            if (!CanCountPlay) return;
            RecordSpatialAction(action);
            MarkDone(ToStep(action));
        }

        private void HandleRunInterrupted()
        {
            isRunInterrupted = true;
            Abort();
        }

        private void HandleActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged) isDirty = true;
        }

        private void HandleLanguageChanged(LocalizationLanguage language) => isDirty = true;

        private void OnFlowChanged()
        {
            UpdatePlayerSubscription();
            isDirty = true;
        }

        // ───────────────────────── 끝내기 ─────────────────────────

        /// <summary>건너뛰기 — 생략 플래그를 저장하고 끝낸다. 완료 플래그는 건드리지 않는다.</summary>
        private void Skip()
        {
            if (isEnding) return;
            if (director != null) director.ReleaseTutorialOpening();
            var meta = MetaSaveService.GetInstanceSafe();
            if (meta != null) meta.MarkFirstPlayTutorialSkipped();
            EndSession(showComplete: false);
        }

        /// <summary>6행동 완료 — 완료 플래그를 저장하고 잠시 완료 문구를 보인 뒤 끝낸다.</summary>
        private void Complete()
        {
            if (isEnding) return;
            var meta = MetaSaveService.GetInstanceSafe();
            if (meta != null) meta.MarkFirstPlayTutorialCompleted();
            EndSession(showComplete: true);
        }

        /// <summary>저장 없이 끝낸다(사망·포기·결과·Stage2·참조 소실). 다음 런에 다시 안내한다.</summary>
        private void Abort()
        {
            if (isEnding) return;
            EndSession(showComplete: false);
        }

        private void EndSession(bool showComplete)
        {
            isEnding = true;
            Unsubscribe();

            if (showComplete && panel != null)
            {
                completeTimer = COMPLETE_DURATION;
                panel.SetSpatialVisible(false);
                panel.SetCompact(false);
                panel.SetContent(SafeGet(StringKey.Tutorial_Complete), string.Empty, string.Empty);
                panel.SetVisible(true);
                SubscribeCompleteDismiss();
                return;
            }

            DismissComplete();
        }

        // ───────────────────────── 완료 문구 생명주기 ─────────────────────────
        // 완료 문구(3초) 동안에는 행동·진행 구독이 이미 떨어져 있다. 일시정지·모달·사망·포기·결과·스테이지 클리어가
        // 오면 남은 시간과 무관하게 즉시 걷는다 — 그 화면들 위에 안내가 남지 않게. 저장은 이미 끝났다.

        private bool isCompleteDismissSubscribed;

        private void SubscribeCompleteDismiss()
        {
            if (isCompleteDismissSubscribed) return;
            isCompleteDismissSubscribed = true;

            GameEvents.OnGamePaused += DismissComplete;
            GameEvents.OnDraftOpened += DismissComplete;
            GameEvents.OnPlayerDead += DismissComplete;
            GameEvents.OnRunAbandoned += DismissComplete;
            GameEvents.OnRunEnded += DismissComplete;
            GameEvents.OnStageCleared += HandleStageClearedWhileComplete;
        }

        private void UnsubscribeCompleteDismiss()
        {
            if (!isCompleteDismissSubscribed) return;
            isCompleteDismissSubscribed = false;

            GameEvents.OnGamePaused -= DismissComplete;
            GameEvents.OnDraftOpened -= DismissComplete;
            GameEvents.OnPlayerDead -= DismissComplete;
            GameEvents.OnRunAbandoned -= DismissComplete;
            GameEvents.OnRunEnded -= DismissComplete;
            GameEvents.OnStageCleared -= HandleStageClearedWhileComplete;
        }

        private void HandleStageClearedWhileComplete(StageData stage) => DismissComplete();

        /// <summary>띠를 숨기고 구독을 모두 뗀 뒤 파괴한다. 여러 번 불려도 안전하다.</summary>
        private void DismissComplete()
        {
            isEnding = true;
            Unsubscribe();
            UnsubscribeCompleteDismiss();
            if (panel != null) panel.SetVisible(false);
            if (this != null) Destroy(gameObject);
        }

        private static string SafeGet(string key)
        {
            var loc = LocalizationManager.GetInstanceSafe();
            return loc != null ? loc.Get(key) : string.Empty;
        }

        private static string SafeGetFormat(string key, params object[] args)
        {
            var loc = LocalizationManager.GetInstanceSafe();
            return loc != null ? loc.GetFormat(key, args) : string.Empty;
        }
    }
}
