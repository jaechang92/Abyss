using UnityEngine;
using Abyss.Runtime.Analytics;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using GAS.Core;
using ObjectPool_Core;
using SaveSystem_Core;

namespace Abyss.Runtime.Bootstrap
{
    /// <summary>
    /// 씬 진입 시 최우선 실행되는 부트스트랩.
    /// 퍼시스턴트 싱글톤의 초기화 순서를 명시적으로 보장한다.
    /// 순서: SaveSystem → MetaSaveService → AbilitySystem → PoolManager → RunManager → HitstopController → AnalyticsLogger → AudioManager → LocalizationManager
    /// (GameFlowController, UIRoot는 씬 배치로 해결됨)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class AbyssBootstrap : MonoBehaviour
    {
        private static bool isInitialized;

        private void Awake()
        {
            if (isInitialized)
            {
                Destroy(gameObject);
                return;
            }

            isInitialized = true;
            DontDestroyOnLoad(gameObject);

            InitializeCoreSystems();
        }

        private static void InitializeCoreSystems()
        {
            Debug.Log("[AbyssBootstrap] 초기화 시작");

            _ = SaveSystem.Instance;
            Debug.Log($"[AbyssBootstrap] SaveSystem 준비 완료 (persistentDataPath: {Application.persistentDataPath})");

            _ = MetaSaveService.Instance;
            Debug.Log($"[AbyssBootstrap] MetaSaveService 준비 완료 (abyss 누적 {MetaSaveService.Instance.Current.abyssShardsTotal})");

            _ = AbilitySystem.Instance;
            Debug.Log("[AbyssBootstrap] AbilitySystem 준비 완료");

            _ = PoolManager.Instance;
            Debug.Log("[AbyssBootstrap] PoolManager 준비 완료");

            _ = RunManager.Instance;
            Debug.Log("[AbyssBootstrap] RunManager 준비 완료");

            _ = HitstopController.Instance;
            Debug.Log("[AbyssBootstrap] HitstopController 준비 완료");

            _ = AnalyticsLogger.Instance;
            Debug.Log("[AbyssBootstrap] AnalyticsLogger 준비 완료");

            _ = AudioManager.Instance;
            Debug.Log("[AbyssBootstrap] AudioManager 준비 완료");

            _ = LocalizationManager.Instance;
            Debug.Log($"[AbyssBootstrap] LocalizationManager 준비 완료 (lang: {LocalizationManager.Instance.CurrentLanguage}, keys: {LocalizationManager.Instance.KeyCount})");

            Debug.Log("[AbyssBootstrap] 초기화 완료");

            // 영속 시스템 준비 완료 → 로비 씬으로 전환.
            // StartNewRun은 Run 씬의 StageDirector가 단일 소유자로 호출한다(여기서 직접 호출하지 않음).
            // 씬 분리 2단계: Bootstrap → Lobby → (시작) → Run.
            _ = SceneFlowController.Instance.LoadLobbyAsync();
        }
    }
}
