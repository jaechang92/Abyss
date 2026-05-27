using System;

namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// CSV 한 행에 대응하는 런타임 DTO. ScriptableObject가 아닌 일반 클래스로 두어
    /// Resources/Data/GameText.csv를 매 부팅 시 직접 파싱한 결과를 보관한다.
    ///
    /// SO를 거치지 않는 이유:
    ///  - 텍스트는 자산 참조가 없는 순수 문자열이라 SO의 장점이 작음
    ///  - 라이브 단계에서 CSV만 교체해 핫픽스 가능
    ///  - SO 캐시 레이어가 없어 "CSV 수정 후 임포트 깜빡" 사고 차단
    /// </summary>
    [Serializable]
    public class LocalizationTextRow
    {
        public string StringKey;
        public string Category;
        public string LocationNote;
        public string ConditionNote;
        public string FormatArgsNote;

        public string KoreanText;
        public string EnglishText;
        public string JapaneseText;

        /// <summary>
        /// 언어별 텍스트 조회. 선택 언어 텍스트가 비어 있으면
        /// 한국어 → 영어 → 빈 문자열 순으로 폴백.
        /// 최종 빈 문자열은 LocalizationManager가 [stringKey] 리터럴로 노출한다.
        /// </summary>
        public string GetText(LocalizationLanguage language)
        {
            string raw = language switch
            {
                LocalizationLanguage.Korean => KoreanText,
                LocalizationLanguage.English => EnglishText,
                LocalizationLanguage.Japanese => JapaneseText,
                _ => null,
            };

            if (!string.IsNullOrEmpty(raw)) return raw;
            if (!string.IsNullOrEmpty(KoreanText)) return KoreanText;
            if (!string.IsNullOrEmpty(EnglishText)) return EnglishText;
            return string.Empty;
        }
    }
}
