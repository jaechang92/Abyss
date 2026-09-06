namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// 지원 언어 정의. GameText.csv의 언어 컬럼(Korean/English/Japanese)과 1:1 대응.
    /// 신규 언어 추가 시:
    ///  1) 이 enum에 항목 추가
    ///  2) LocalizationTextRow에 텍스트 필드 + GetText 분기 추가
    ///  3) GameText.csv에 컬럼 추가
    ///  4) <see cref="LocalizationLanguageNames"/>에 자기 표기 추가
    /// </summary>
    public enum LocalizationLanguage
    {
        Korean = 0,
        English = 1,
        Japanese = 2,
    }

    /// <summary>
    /// 언어 선택 UI가 보여 줄 언어 이름.
    ///
    /// 🔑 <b>이 문자열만은 번역하지 않는다.</b> 각 언어를 <b>그 언어 자신의 표기</b>로 적는다
    /// (한국어 / English / 日本語). 언어를 바꾸려는 사람은 <b>지금 화면의 글자를 못 읽는 상태</b>일
    /// 수 있어서, 한국어 UI가 「일본어」라고 적어 두면 일본어만 읽는 사용자는 자기 항목을 못 찾는다.
    /// 현재 언어에 따라 바뀌면 안 되므로 CSV(번역 대상)가 아니라 코드에 둔다.
    ///
    /// 이 목록은 <see cref="LocalizationLanguage"/>와 짝이다 — enum에 항목을 늘리면 여기도 늘린다.
    /// 빠뜨리면 열거형 이름("Japanese")이 그대로 뜬다(조용히 틀리는 대신 눈에 띄게 틀린다).
    /// </summary>
    public static class LocalizationLanguageNames
    {
        public static string GetNativeName(LocalizationLanguage language) => language switch
        {
            LocalizationLanguage.Korean => "한국어",
            LocalizationLanguage.English => "English",
            LocalizationLanguage.Japanese => "日本語",
            _ => language.ToString(),
        };
    }
}
