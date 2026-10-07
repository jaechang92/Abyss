using System.Collections.Generic;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.Flow
{
    /// <summary>실제 씬 전환의 표시용 연결. 저장·정산·런 시작/종료를 발행하지 않는다.</summary>
    public static class JourneyPresentation
    {
        private static readonly HashSet<SceneHandle> attachedScenes = new();
        private static RunSummary pendingReturn;
        public static bool HasSeenArrival { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            attachedScenes.Clear();
            pendingReturn = null;
            HasSeenArrival = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= HandleLoaded;
            SceneManager.sceneLoaded += HandleLoaded;
            SceneManager.sceneUnloaded -= HandleUnloaded;
            SceneManager.sceneUnloaded += HandleUnloaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitial() => Install(SceneManager.GetActiveScene());

        private static void HandleLoaded(Scene scene, LoadSceneMode mode) => Install(scene);
        private static void HandleUnloaded(Scene scene) => attachedScenes.Remove(scene.handle);

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || (scene.name != SceneNames.Run && scene.name != SceneNames.Lobby)) return;
            if (!attachedScenes.Add(scene.handle)) return;
            var go = new GameObject("JourneyPresentation");
            SceneManager.MoveGameObjectToScene(go, scene);
            if (scene.name == SceneNames.Run) go.AddComponent<RunArrivalPresenter>();
            else go.AddComponent<LobbyReturnPresenter>();
        }

        // SceneFlow가 로드 작업을 실제로 확보한 뒤만 호출한다. 실패/중복 요청은 새 귀환을 만들지 않는다.
        public static void CommitTransition(string source, string destination)
        {
            pendingReturn = null;
            if (source != SceneNames.Run || destination != SceneNames.Lobby || !RunManager.HasInstance) return;
            var run = RunManager.Instance;
            if (run.IsRunActive || run.LastRunEndReason != RunEndReason.Death) return;
            var summary = run.LastRunSummary;
            if (summary != null && summary.hasRecord) pendingReturn = summary;
        }

        public static RunSummary ConsumeReturn()
        {
            var summary = pendingReturn;
            pendingReturn = null;
            return summary;
        }

        public static void MarkArrivalSeen() => HasSeenArrival = true;
        public static int CurrentTransitionVersion => SceneFlowController.HasInstance
            ? SceneFlowController.Instance.TransitionVersion : 0;
    }
}
