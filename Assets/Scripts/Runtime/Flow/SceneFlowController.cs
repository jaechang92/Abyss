using Singleton_Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.Flow
{
    /// <summary>
    /// 씬 전환 전담 영속 싱글톤. Bootstrap → Lobby → Run → (Result) → Lobby 흐름을 조율한다.
    /// GameFlowController(게임플레이 내부 FSM)와 역할이 다르다 — 이쪽은 씬 경계 전환만 담당.
    /// Coroutine 금지(ADR-002)라 Awaitable로 비동기 로드한다.
    /// </summary>
    public sealed class SceneFlowController : SingletonManager<SceneFlowController>
    {
        private bool isLoading;
        private ScreenFader fader;

        public bool IsLoading => isLoading;

        // 페이드 오버레이는 최초 씬 로드 시 지연 생성한다(영속, DontDestroyOnLoad).
        private ScreenFader Fader => fader != null ? fader : (fader = ScreenFader.Create());

        public Awaitable LoadBootstrapAsync() => LoadSceneAsync(SceneNames.Bootstrap);
        public Awaitable LoadLobbyAsync() => LoadSceneAsync(SceneNames.Lobby);
        public Awaitable LoadRunAsync() => LoadSceneAsync(SceneNames.Run);

        /// <summary>
        /// 지정 씬을 Single 모드로 비동기 로드. 로딩 중 중복 요청은 무시한다.
        /// 빌드 설정에 없는 씬이면 LoadSceneAsync가 null을 반환하므로 에러 로그 후 종료.
        /// </summary>
        public async Awaitable LoadSceneAsync(string sceneName)
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
            // 페이드/로드 await 중 예외·취소가 나도 isLoading이 true로 고착되면 이후 모든 전환이
            // 상단 가드에서 무시된다. finally로 반드시 복구해 씬 전환 영구 잠금을 막는다.
            try
            {
                Debug.Log($"[SceneFlowController] 씬 로드 시작: {sceneName}");

                // 화면을 검게 덮은 뒤 로드 → 로드 중 씬 전환 끊김을 감춘다.
                await Fader.FadeOutAsync();

                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op == null)
                {
                    Debug.LogError($"[SceneFlowController] 씬 로드 실패 — 빌드 설정(Build Settings)에 '{sceneName}'이(가) 등록되어 있는지 확인.");
                    await Fader.FadeInAsync();  // 실패 시 덮인 화면 복구
                    return;
                }

                while (!op.isDone)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                // 새 씬이 활성화된 뒤 화면을 걷어낸다.
                await Fader.FadeInAsync();

                Debug.Log($"[SceneFlowController] 씬 로드 완료: {sceneName}");
            }
            finally
            {
                isLoading = false;
            }
        }
    }
}
