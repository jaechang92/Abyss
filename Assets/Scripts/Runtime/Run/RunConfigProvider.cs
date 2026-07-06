using UnityEngine;

namespace Abyss.Runtime.Run
{
    /// <summary>
    /// RunConfig(런 전역 설정 SO)의 단일 진입점(SoT). Resources/Data/RunConfig를 1회 로드해 캐싱하며,
    /// 초기화 순서·싱글턴 생명주기와 무관하게 어디서든 같은 인스턴스를 반환한다.
    /// (P1-B #9: RunManager/FormController/Health의 3중 Resources.Load + 폴백 중복을 여기로 통합)
    /// SerializeField로 명시 주입된 config가 있으면 그것을 우선(에디터 오버라이드), 없으면 공유 Current로 폴백.
    /// </summary>
    public static class RunConfigProvider
    {
        private const string RESOURCE_PATH = "Data/RunConfig";
        private static RunConfig cached;

        /// <summary>
        /// 공유 RunConfig. 최초 접근 시 Resources에서 로드해 캐싱한다.
        /// 로드 실패 시 null을 반환하며(호출부 폴백 책임), 경고를 1회성으로 남긴다.
        /// </summary>
        public static RunConfig Current
        {
            get
            {
                if (cached == null)
                {
                    cached = Resources.Load<RunConfig>(RESOURCE_PATH);
                    if (cached == null)
                    {
                        Debug.LogWarning($"[RunConfigProvider] RunConfig 로드 실패 — Assets/Resources/{RESOURCE_PATH}.asset 확인 필요. 호출부 폴백 기본값 사용.");
                    }
                }
                return cached;
            }
        }

        /// <summary>
        /// 명시 주입된 config를 우선 사용하되, null이면 공유 Current로 폴백해 반환한다.
        /// 컴포넌트의 SerializeField 오버라이드와 전역 SoT를 한 줄로 합류시키는 헬퍼.
        /// </summary>
        public static RunConfig Resolve(RunConfig injected) => injected != null ? injected : Current;

        /// <summary>
        /// 도메인 리로드 비활성화(빠른 플레이 진입) 대비 정적 캐시 초기화.
        /// Resources.Load는 동일 인스턴스를 돌려주므로 필수는 아니나 위생 목적.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;
    }
}
