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
    }
}
