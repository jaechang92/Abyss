using Abyss.Runtime.Flow;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// Run 씬이 열릴 때 첫 플레이 튜토리얼을 붙인다. 씬·빌더를 다시 돌릴 필요가 없도록 코드로만 연결한다.
    ///
    /// 만드는 조건: Run 씬 · StageDirector 와 PlayerCharacter 가 있음 · 메타 세이브가 있고 완료/생략 기록이 없음 ·
    /// 살아 있는 튜토리얼이 없음. 하나라도 어긋나면 아무것도 하지 않는다(예외 없이 넘어간다).
    /// </summary>
    public static class FirstPlayTutorialBootstrap
    {
        // 도메인 리로드를 꺼도 구독이 겹치지 않게 떼고 다시 붙인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneNames.Run) return;
            if (FirstPlayTutorialController.HasActiveInstance) return;

            var meta = MetaSaveService.GetInstanceSafe();
            if (meta == null || !meta.ShouldShowFirstPlayTutorial) return;

            var director = Object.FindAnyObjectByType<StageDirector>();
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (director == null || player == null) return;

            FirstPlayTutorialController.Create(scene, director, player);
        }
    }
}
