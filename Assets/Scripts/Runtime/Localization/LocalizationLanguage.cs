namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// 지원 언어 정의. GameText.csv의 언어 컬럼(Korean/English/Japanese)과 1:1 대응.
    /// 신규 언어 추가 시:
    ///  1) 이 enum에 항목 추가
    ///  2) LocalizationTextRow에 텍스트 필드 + GetText 분기 추가
    ///  3) GameText.csv에 컬럼 추가
    /// </summary>
    public enum LocalizationLanguage
    {
        Korean = 0,
        English = 1,
        Japanese = 2,
    }
}
