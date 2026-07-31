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

            ApplyScreenSettings();

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

            // 영속 시스템 준비 완료 → 타이틀 씬으로 전환.
            // StartNewRun은 Run 씬의 StageDirector가 단일 소유자로 호출한다(여기서 직접 호출하지 않음).
            // 씬 흐름: Bootstrap → Title → (게임 시작) → Lobby → (던전 입장) → Run.
            _ = SceneFlowController.Instance.LoadTitleAsync();
        }

        /// <summary>
        /// 저장된 화면 설정(해상도·창 모드)을 부팅 시 1회 복원한다.
        ///
        /// screenWidth/Height가 0이면 "미설정"이므로 <b>아무것도 건드리지 않는다</b> —
        /// isFullscreen의 기본값(true)만 믿고 적용하면 설정을 한 번도 만진 적 없는 첫 실행에서
        /// 빌드의 기본 창 모드를 임의로 뒤집게 된다. 두 값은 SettingsPanel이 항상 함께 저장하므로
        /// width &gt; 0은 "사용자가 설정을 저장한 적 있음"과 동치다.
        ///
        /// 에디터에서는 Screen.SetResolution이 무시되므로 실제 복원 확인은 빌드에서 해야 한다.
        /// </summary>
        private static void ApplyScreenSettings()
        {
            var settings = MetaSaveService.Instance.Current?.settings;
            if (settings == null || settings.screenWidth <= 0 || settings.screenHeight <= 0)
            {
                Debug.Log("[AbyssBootstrap] 저장된 화면 설정 없음 — 기본 화면 유지");
                return;
            }

            // 전체화면은 테두리 없는 창(FullScreenWindow)으로 통일한다 — 독점 전체화면은 알트탭 복귀가 느리고
            // 다중 모니터에서 문제를 일으키기 쉽다. MetaSettings가 bool 하나만 갖는 것과도 맞는다.
            var mode = settings.isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(settings.screenWidth, settings.screenHeight, mode);
            Debug.Log($"[AbyssBootstrap] 화면 설정 복원 {settings.screenWidth}x{settings.screenHeight} ({mode})");
        }

        /// <summary>
        /// 도메인 리로드 비활성화 대비 정적 상태 리셋.
        /// Enter Play Mode Options에서 도메인 리로드를 끄면 isInitialized가 이전 플레이 세션 값으로 잔존해
        /// 재초기화가 스킵될 수 있으므로 플레이 세션마다 명시적으로 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => isInitialized = false;
    }
}
