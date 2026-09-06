using System;

namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// LocalizationManager 호출을 한 줄로 줄여주는 정적 헬퍼.
    /// 매 호출이 Instance 참조를 거치지만 SingletonManager는 캐시된 참조라 비용 무시 가능.
    /// LocalizationManager는 DontDestroyOnLoad라 재생성 우려 없음.
    ///
    /// 사용:
    ///   text.text = Loc.Get(StringKey.Common_Confirm);
    ///   text.text = Loc.GetFormat(StringKey.Result_KillCountFormat, killCount);
    ///   Loc.SetLanguage(LocalizationLanguage.English);
    /// </summary>
    public static class Loc
    {
        /// <summary>StringKey의 현재 언어 텍스트 조회.</summary>
        public static string Get(string stringKey)
            => LocalizationManager.Instance.Get(stringKey);

        /// <summary>포맷 인자가 있는 텍스트 조회.</summary>
        public static string GetFormat(string stringKey, params object[] args)
            => LocalizationManager.Instance.GetFormat(stringKey, args);

        /// <summary>현재 선택된 언어.</summary>
        public static LocalizationLanguage CurrentLanguage
            => LocalizationManager.Instance.CurrentLanguage;

        /// <summary>언어 전환. MetaSave 영속화 + OnLanguageChanged 이벤트 발행.</summary>
        public static void SetLanguage(LocalizationLanguage language)
            => LocalizationManager.Instance.SetLanguage(language);

        /// <summary>
        /// 언어 변경 구독. <see cref="LocalizedText"/>가 쓴다.
        ///
        /// 등록은 <c>Instance</c>(없으면 만든다)를 거친다 — 구독하려는 쪽은 이미 글자를 그릴 참이라
        /// 어차피 매니저가 필요하다. 종료 중에는 Instance가 null을 돌려주므로 그때는 그냥 넘어간다.
        /// </summary>
        public static void AddLanguageChangedListener(Action<LocalizationLanguage> handler)
        {
            var manager = LocalizationManager.Instance;
            if (manager != null) manager.OnLanguageChanged += handler;
        }

        /// <summary>
        /// 언어 변경 구독 해제.
        ///
        /// 등록과 달리 <c>GetInstanceSafe</c>를 쓴다. 해제는 <c>OnDisable</c>·<c>OnDestroy</c>에서
        /// 불리고 그 경로는 <b>씬 정리·애플리케이션 종료 도중</b>에도 지나간다 — 거기서 <c>Instance</c>를
        /// 부르면 <b>치우는 중에 새 매니저를 만들려 든다.</b> 없으면 뗄 것도 없으니 그냥 넘어가는 게 맞다.
        /// </summary>
        public static void RemoveLanguageChangedListener(Action<LocalizationLanguage> handler)
        {
            var manager = LocalizationManager.GetInstanceSafe();
            if (manager != null) manager.OnLanguageChanged -= handler;
        }
    }
}
