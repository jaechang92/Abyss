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

        public bool IsLoading => isLoading;

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
            Debug.Log($"[SceneFlowController] 씬 로드 시작: {sceneName}");

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneFlowController] 씬 로드 실패 — 빌드 설정(Build Settings)에 '{sceneName}'이(가) 등록되어 있는지 확인.");
                isLoading = false;
                return;
            }

            while (!op.isDone)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            isLoading = false;
            Debug.Log($"[SceneFlowController] 씬 로드 완료: {sceneName}");
        }
    }
}
