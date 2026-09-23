using System;
using Abyss.Runtime.Input;
using Singleton_Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 씬 전환 전담 영속 싱글톤. Bootstrap → Lobby → Run → (Result) → Lobby 흐름을 조율한다.
    /// GameFlowController(게임플레이 내부 FSM)와 역할이 다르다 — 이쪽은 씬 경계 전환만 담당.
    /// Coroutine 금지(ADR-002)라 Awaitable로 비동기 로드한다.
    ///
    /// <b>정지와 전환의 경계</b>(2026-09-23). 씬 <b>안</b>의 정지는 GameFlowController(FSM)가 소유하고,
    /// 씬 <b>경계</b>는 여기가 소유한다. 전환 중(<see cref="IsLoading"/>)에는 FSM이 정지·해제 요청을 받지 않으므로
    /// 페이드 동안 정지 상태가 바뀌지 않는다. 옛 씬의 FSM은 씬과 함께 파괴돼 자기가 건 정지를 풀 수 없으니,
    /// 새 씬에 들어서는 순간 여기서 회수한다(<see cref="ReleaseCarriedPause"/>) — 새 씬은 항상 정지 해제 상태로 시작한다.
    ///
    /// 결말별 계약:
    /// · 성공 — 새 씬의 Awake/OnEnable 뒤·Start 전에 회수, 그다음 페이드인.
    /// · 실패(씬 미등록) — 전환이 확정되지 않았다. 확정 콜백·회수 모두 없음. 옛 씬의 정지는 FSM이 그대로 쥔다.
    /// · 중복 요청 — 무시. 확정 콜백 없음.
    /// · 확정 뒤 취소·예외(영속 객체 파괴 = 앱 종료 경로) — finally에서 회수. 옛 씬에 정지를 남기지 않는다.
    /// </summary>
    public sealed class SceneFlowController : SingletonManager<SceneFlowController>
    {
        private bool isLoading;
        private ScreenFader fader;

        public bool IsLoading => isLoading;

        // 페이드 오버레이는 최초 씬 로드 시 지연 생성한다(영속, DontDestroyOnLoad).
        private ScreenFader Fader => fader != null ? fader : (fader = ScreenFader.Create());

        public Awaitable LoadBootstrapAsync() => LoadSceneAsync(SceneNames.Bootstrap);
        public Awaitable LoadTitleAsync() => LoadSceneAsync(SceneNames.Title);
        public Awaitable LoadLobbyAsync() => LoadSceneAsync(SceneNames.Lobby);
        public Awaitable LoadRunAsync() => LoadSceneAsync(SceneNames.Run);

        /// <summary>
        /// 타이틀 전환 + 전환 확정 콜백. 런 포기처럼 <b>전환이 실제로 일어날 때만</b> 해야 하는 일을 싣는다
        /// (<see cref="LoadSceneAsync"/>의 onCommitted 참조).
        /// </summary>
        public Awaitable LoadTitleAsync(Action onCommitted) => LoadSceneAsync(SceneNames.Title, onCommitted);

        /// <summary>
        /// 지정 씬을 Single 모드로 비동기 로드. 로딩 중 중복 요청은 무시한다.
        /// 빌드 설정에 없는 씬이면 LoadSceneAsync가 null을 반환하므로 에러 로그 후 종료.
        /// </summary>
        /// <param name="onCommitted">
        /// 전환 확정 시점(페이드아웃이 끝나고 로드 작업이 실제로 시작된 직후, 옛 씬이 아직 살아 있을 때)에
        /// 한 번 호출된다. 실패·중복 요청이면 호출되지 않는다. 예외는 로그로 남기고 전환을 계속한다 —
        /// 이미 로드가 시작돼 되돌릴 수 없다.
        /// </param>
        public async Awaitable LoadSceneAsync(string sceneName, Action onCommitted = null)
        {
            if (isLoading)
            {
                Debug.LogWarning($"[SceneFlowController] 이미 씬 로딩 중 — 중복 요청 무시: {sceneName}");
                return;
            }
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneFlowController] 씬 이름이 비어 있음");
                return;
            }

            isLoading = true;
            bool isCommitted = false;
            bool isPauseReleased = false;

            // 새 씬의 Awake/OnEnable 뒤, Start 전에 불린다. 로드 완료를 기다린 뒤에 회수하면
            // 새 씬이 Start에서 건 정지(시작 직후 드래프트·모달)를 덮어쓸 수 있다.
            void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (isPauseReleased || scene.name != sceneName) return;
                ReleaseCarriedPause();
                isPauseReleased = true;
            }

            // 페이드/로드 await 중 예외·취소가 나도 isLoading이 true로 고착되면 이후 모든 전환이
            // 상단 가드에서 무시된다. finally로 반드시 복구해 씬 전환 영구 잠금을 막는다.
            try
            {
                Debug.Log($"[SceneFlowController] 씬 로드 시작: {sceneName}");

                // 화면을 검게 덮은 뒤 로드 → 로드 중 씬 전환 끊김을 감춘다.
                await Fader.FadeOutAsync();

                SceneManager.sceneLoaded += HandleSceneLoaded;
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError($"[SceneFlowController] 씬 로드 실패 — 빌드 설정(Build Settings)에 '{sceneName}'이(가) 등록되어 있는지 확인.");
                    await TryFadeInAsync();  // 실패 시 덮인 화면 복구. 정지는 옛 씬의 FSM이 그대로 쥔다
                    return;
                }

                isCommitted = true;
                InvokeCommitted(onCommitted, sceneName);

                while (!op.isDone)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                // sceneLoaded를 놓친 경우의 안전망(이름 불일치 등). 정상 경로에서는 이미 회수됐다.
                if (!isPauseReleased)
                {
                    ReleaseCarriedPause();
                    isPauseReleased = true;
                }

                // 새 씬이 활성화된 뒤 화면을 걷어낸다.
                await Fader.FadeInAsync();

                Debug.Log($"[SceneFlowController] 씬 로드 완료: {sceneName}");
            }
            catch (OperationCanceledException)
            {
                // 영속 객체 파괴(앱 종료 경로). 페이더도 함께 사라지므로 화면 복구를 시도하지 않는다.
                throw;
            }
            catch (Exception e)
            {
                // 🔴 등록되지 않은 씬 이름은 Unity 버전에 따라 null 대신 예외를 던진다. 예외로 빠져나가면
                //    검은 페이드가 걷히지 않아 조작 불가 화면이 남는다 — null 경로와 같은 복구를 보장한다.
                Debug.LogException(e);
                Debug.LogError(isCommitted
                    ? $"[SceneFlowController] 씬 전환 중 예외 — 로드가 이미 시작됐다: {sceneName}"
                    : $"[SceneFlowController] 씬 전환 실패(전환 미확정) — 화면을 복구하고 이전 씬에 머문다: {sceneName}");
                await TryFadeInAsync();
            }
            finally
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                // 확정된 전환이 취소·예외로 끊겨도 옛 씬의 정지를 남기지 않는다.
                if (isCommitted && !isPauseReleased) ReleaseCarriedPause();
                isLoading = false;
            }
        }

        /// <summary>
        /// 덮인 화면을 걷어낸다. 복구 자체가 실패해도(페이더 파괴·취소) 삼킨다 — 원래 실패를 가리거나
        /// 예외를 바꿔치기하지 않기 위함이다. 이때는 검은 화면이 남을 수 있어 경고를 남긴다.
        /// </summary>
        private async Awaitable TryFadeInAsync()
        {
            try
            {
                await Fader.FadeInAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SceneFlowController] 화면 복구 실패 — 검은 화면이 남을 수 있다: {e.GetType().Name}");
            }
        }

        private static void InvokeCommitted(Action onCommitted, string sceneName)
        {
            if (onCommitted == null) return;
            try
            {
                onCommitted();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"[SceneFlowController] 전환 확정 콜백 예외 — 로드가 이미 시작돼 전환은 계속한다: {sceneName}");
            }
        }

        /// <summary>
        /// 씬 경계의 정지 회수. 씬을 넘어 사는 전역 상태 둘만 되돌린다 — <c>Time.timeScale</c>과
        /// 영속 <see cref="InputRouter"/>의 입력 모드(정지 중 UI 모드로 바뀐 채 남는다).
        ///
        /// 사실 이벤트(OnGameResumed)는 발행하지 않는다. 그 발행자는 FSM 하나이고, 받을 옛 씬 구독자
        /// (PausePanel 등)는 이미 파괴됐다. 파괴 중인 구독자를 부르는 경로를 만들지 않는다.
        /// </summary>
        private static void ReleaseCarriedPause()
        {
            Time.timeScale = 1f;
            InputRouter.GetInstanceSafe()?.SwitchMode(InputRouter.InputMode.Gameplay);
        }
    }
}
