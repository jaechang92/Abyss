using System;
using System.Collections.Generic;
using UnityEngine;
using Singleton_Core;

namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// 다국어 텍스트 런타임 매니저.
    /// 부팅 시 Resources/Data/GameText.csv를 1회 파싱하여 모든 언어를 메모리에 보관한다.
    /// CSV 직접 로드 방식 — 라이브 단계에서 CSV만 교체해 핫픽스 가능.
    ///
    /// 사용:
    ///   var loc = LocalizationManager.Instance;  // 또는 Loc.Get 정적 헬퍼
    ///   text.text = loc.Get(StringKey.Common_Confirm);
    ///   text.text = loc.GetFormat(StringKey.Result_KillCountFormat, killCount);
    ///
    /// 초기 언어 결정:
    ///   1) PlayerPrefs("Localization.Language") 우선
    ///   2) 없으면 Application.systemLanguage 매핑
    ///      Korean → Korean, Japanese → Japanese, 그 외 → English
    /// </summary>
    public sealed class LocalizationManager : SingletonManager<LocalizationManager>
    {
        private const string CSV_RESOURCE_PATH = "Data/GameText";
        private const string PREF_LANGUAGE = "Localization.Language";

        private static readonly string[] requiredHeaders =
        {
            "StringKey", "Korean", "English", "Japanese"
        };

        private Dictionary<string, LocalizationTextRow> textTable;
        private LocalizationLanguage currentLanguage;
        private bool isInitialized;

        /// <summary>현재 선택된 언어. SetLanguage로 변경.</summary>
        public LocalizationLanguage CurrentLanguage => currentLanguage;

        /// <summary>CSV 파싱 + 초기 언어 결정 완료 여부.</summary>
        public bool IsInitialized => isInitialized;

        /// <summary>등록된 StringKey 수.</summary>
        public int KeyCount => textTable?.Count ?? 0;

        /// <summary>
        /// 언어 변경 시 발행. UI 컴포넌트(LocalizedText 등)가 구독하여 자동 갱신한다.
        /// </summary>
        public event Action<LocalizationLanguage> OnLanguageChanged;

        protected override void OnSingletonAwake()
        {
            LoadCsv();
            currentLanguage = ResolveInitialLanguage();
            isInitialized = true;

            Debug.Log(
                $"[LocalizationManager] 초기화 완료 — " +
                $"Language: {currentLanguage}, Keys: {KeyCount}");
        }

        /// <summary>
        /// StringKey로 현재 언어 텍스트 조회.
        /// 누락 시 "[stringKey]" 형태로 반환하고 Warning 로그 — 누락 가시화.
        /// </summary>
        public string Get(string stringKey)
        {
            if (string.IsNullOrEmpty(stringKey))
                return string.Empty;

            if (textTable == null
                || !textTable.TryGetValue(stringKey, out var row))
            {
                Debug.LogWarning(
                    $"[LocalizationManager] 미정의 StringKey: \"{stringKey}\"");
                return $"[{stringKey}]";
            }

            return row.GetText(currentLanguage);
        }

        /// <summary>
        /// 포맷 인자가 있는 텍스트 조회. string.Format 래퍼.
        /// 인자 수 불일치는 원본 텍스트로 폴백 + 경고.
        /// </summary>
        public string GetFormat(string stringKey, params object[] args)
        {
            string raw = Get(stringKey);
            if (string.IsNullOrEmpty(raw)) return raw;
            if (args == null || args.Length == 0) return raw;

            try
            {
                return string.Format(raw, args);
            }
            catch (FormatException e)
            {
                Debug.LogWarning(
                    $"[LocalizationManager] GetFormat 실패 \"{stringKey}\" " +
                    $"({e.Message}). 원본 텍스트로 폴백.");
                return raw;
            }
        }

        /// <summary>
        /// 언어 전환. 이전 언어와 같으면 무시.
        /// 변경 시 OnLanguageChanged 이벤트 발행 + PlayerPrefs 영속화.
        /// </summary>
        public void SetLanguage(LocalizationLanguage language)
        {
            if (currentLanguage == language) return;

            currentLanguage = language;
            PlayerPrefs.SetString(PREF_LANGUAGE, language.ToString());
            OnLanguageChanged?.Invoke(currentLanguage);

            Debug.Log($"[LocalizationManager] 언어 변경 → {currentLanguage}");
        }

        /// <summary>
        /// 진단/디버그용 — 등록된 모든 StringKey 열거.
        /// </summary>
        public IReadOnlyCollection<string> AllKeys
            => textTable != null
                ? (IReadOnlyCollection<string>)textTable.Keys
                : Array.Empty<string>();

        // --- 내부 ---

        private void LoadCsv()
        {
            textTable = new Dictionary<string, LocalizationTextRow>();

            var asset = Resources.Load<TextAsset>(CSV_RESOURCE_PATH);
            if (asset == null)
            {
                Debug.LogError(
                    $"[LocalizationManager] CSV 로드 실패 — " +
                    $"Resources/{CSV_RESOURCE_PATH}.csv 가 없습니다.");
                return;
            }

            string[] records = CsvParser.SplitCsvRecords(asset.text);
            if (records.Length < 2)
            {
                Debug.LogError("[LocalizationManager] CSV 행 수 부족 (헤더+데이터 필요).");
                return;
            }

            var colIndex = CsvParser.BuildColumnIndex(records[0]);
            var missing = CsvParser.ValidateRequiredHeaders(colIndex, requiredHeaders);

            if (missing.Count > 0)
            {
                Debug.LogError(
                    "[LocalizationManager] 필수 헤더 누락: " + string.Join(", ", missing));
                return;
            }

            // 1행은 헤더, 2행(인덱스 1)부터 데이터
            for (int i = 1; i < records.Length; i++)
            {
                string line = records[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] values = CsvParser.ParseCsvLine(line);

                string stringKey = CsvParser.GetValue(values, colIndex, "StringKey");
                if (string.IsNullOrWhiteSpace(stringKey)) continue;

                if (textTable.ContainsKey(stringKey))
                {
                    Debug.LogWarning(
                        $"[LocalizationManager] StringKey 중복 — \"{stringKey}\" (기존 값 유지).");
                    continue;
                }

                textTable[stringKey] = new LocalizationTextRow
                {
                    StringKey = stringKey,
                    Category = CsvParser.GetValue(values, colIndex, "Category"),
                    LocationNote = CsvParser.GetValue(values, colIndex, "Location"),
                    ConditionNote = CsvParser.GetValue(values, colIndex, "Condition"),
                    FormatArgsNote = CsvParser.GetValue(values, colIndex, "FormatArgs"),
                    KoreanText = CsvParser.GetValue(values, colIndex, "Korean"),
                    EnglishText = CsvParser.GetValue(values, colIndex, "English"),
                    JapaneseText = CsvParser.GetValue(values, colIndex, "Japanese"),
                };
            }
        }

        /// <summary>
        /// 초기 언어 결정. PlayerPrefs 우선 → 시스템 언어 폴백.
        /// </summary>
        private static LocalizationLanguage ResolveInitialLanguage()
        {
            string saved = PlayerPrefs.GetString(PREF_LANGUAGE, string.Empty);
            if (!string.IsNullOrEmpty(saved)
                && Enum.TryParse<LocalizationLanguage>(saved, out var parsed))
            {
                return parsed;
            }

            return Application.systemLanguage switch
            {
                SystemLanguage.Korean => LocalizationLanguage.Korean,
                SystemLanguage.Japanese => LocalizationLanguage.Japanese,
                _ => LocalizationLanguage.English,
            };
        }
    }
}
