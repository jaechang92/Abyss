using System;
using System.Collections.Generic;
using Abyss.Runtime.Meta;
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
    /// 언어 영속화의 SoT는 MetaSaveService(MetaSave.settings.language)다.
    /// 2026-09-03 이전에는 PlayerPrefs에 저장했는데, 볼륨·화면 설정이 이미 세이브로 옮겨진 뒤라
    /// 설정 하나만 다른 곳에 남아 있었다 — 세이브를 지워도 언어만 남고, 세이브를 옮겨도 언어는
    /// 안 따라온다. AudioManager가 2026-07-29에 밟은 길과 같다.
    ///
    /// 초기 언어 결정:
    ///   1) MetaSave.settings.language 우선
    ///   2) 비어 있으면 레거시 PlayerPrefs 값을 1회 이관 (아래 MigrateLegacyLanguage)
    ///   3) 그것도 없으면 Application.systemLanguage 매핑
    ///      Korean → Korean, Japanese → Japanese, 그 외 → English
    /// </summary>
    public sealed class LocalizationManager : SingletonManager<LocalizationManager>
    {
        private const string CSV_RESOURCE_PATH = "Data/GameText";
        /// <summary>
        /// 언어의 옛 저장 위치. 게임 코드에서는 <b>읽고 지우기만</b> 한다 — 이 키에 새로 쓰는
        /// 경로는 없고, MetaSave에 언어가 아직 없을 때 한 번 읽어 옮긴 뒤 삭제한다.
        ///
        /// 공개한 이유는 치트 메뉴가 <b>이관 경로를 재현</b>해야 하기 때문이다. 옛 키는 새로 깐
        /// 환경에는 없어서, 심는 수단이 없으면 이 코드는 실제 사용자의 기기에서 처음 실행된다.
        /// 이름을 저쪽에 복사해 두면 한쪽만 바뀌었을 때 이관이 조용히 멈춘다.
        /// </summary>
        public const string LegacyLanguagePrefKey = "Localization.Language";

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
        /// 변경 시 MetaSave 영속화 + OnLanguageChanged 이벤트 발행.
        ///
        /// 저장이 실패해도 전환 자체는 진행한다 — 세이브에 못 적는 것과 지금 화면의 글자가
        /// 안 바뀌는 것은 별개이고, 후자는 사용자가 방금 누른 것을 무시당한 것처럼 보인다.
        /// </summary>
        public void SetLanguage(LocalizationLanguage language)
        {
            if (currentLanguage == language) return;

            currentLanguage = language;

            var service = MetaSaveService.GetInstanceSafe();
            if (service != null) service.UpdateLanguage(language.ToString());

            OnLanguageChanged?.Invoke(currentLanguage);

            Debug.Log($"[LocalizationManager] 언어 변경 → {currentLanguage}");
        }

        /// <summary>
        /// 초기 언어 결정을 다시 돌려 그 결과를 현재 언어로 삼는다. 결정 규칙 자체가 아니라
        /// <b>입력(세이브·레거시 키)이 바뀌었을 때</b> 쓴다 — 치트로 저장값을 비우거나 옛 키를
        /// 심은 뒤 재시작 없이 같은 순서를 밟아 볼 수 있다.
        ///
        /// <see cref="SetLanguage"/>와 달리 저장하지 않는다. 이쪽은 읽는 방향이고, 여기서 되쓰면
        /// 방금 비운 값이 도로 채워져 "미설정으로 되돌리기"가 성립하지 않는다.
        /// 결정 도중 레거시 이관이 일어나면 그 이관만 저장된다.
        /// </summary>
        public LocalizationLanguage ReloadLanguageFromSave()
        {
            var resolved = ResolveInitialLanguage();
            if (resolved != currentLanguage)
            {
                currentLanguage = resolved;
                OnLanguageChanged?.Invoke(currentLanguage);
                Debug.Log($"[LocalizationManager] 초기 언어 재결정 → {currentLanguage}");
            }
            return resolved;
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
                    // 줄바꿈 이스케이프는 표시되는 텍스트 3열에만 푼다 — 노트 열은 CSV를 읽는
                    // 사람 몫이라 원문 그대로 두는 편이 낫다. 로드 시 한 번만 풀고 Get은 순수하게 둔다.
                    KoreanText = CsvParser.UnescapeNewlines(
                        CsvParser.GetValue(values, colIndex, "Korean")),
                    EnglishText = CsvParser.UnescapeNewlines(
                        CsvParser.GetValue(values, colIndex, "English")),
                    JapaneseText = CsvParser.UnescapeNewlines(
                        CsvParser.GetValue(values, colIndex, "Japanese")),
                };
            }
        }

        /// <summary>
        /// 초기 언어 결정. MetaSave → 레거시 PlayerPrefs 1회 이관 → 시스템 언어 폴백.
        ///
        /// MetaSaveService 조회에 실패하는 경로(단독 씬 재생 등)에서도 동작해야 하므로
        /// GetInstanceSafe를 쓴다 — 여기서 세이브를 자동 생성하면 언어를 읽으려던 것이
        /// 세이브 파일을 만드는 부작용이 된다.
        /// </summary>
        private static LocalizationLanguage ResolveInitialLanguage()
        {
            var service = MetaSaveService.GetInstanceSafe();
            var settings = service != null && service.Current != null ? service.Current.settings : null;

            if (settings != null && TryParseLanguage(settings.language, out var saved)) return saved;
            if (MigrateLegacyLanguage(service, out var legacy)) return legacy;

            return Application.systemLanguage switch
            {
                SystemLanguage.Korean => LocalizationLanguage.Korean,
                SystemLanguage.Japanese => LocalizationLanguage.Japanese,
                _ => LocalizationLanguage.English,
            };
        }

        /// <summary>
        /// 옛 PlayerPrefs 언어 값을 MetaSave로 1회 옮긴다. 옮길 값이 있었으면 true.
        ///
        /// 키 삭제는 <b>저장 성공을 확인한 뒤에만</b> 한다. 순서를 뒤집으면 저장이 실패한 실행에서
        /// 언어 선택이 양쪽 어디에도 남지 않는다 — 그 손실은 다음 실행에 조용히 시스템 언어로
        /// 드러나서, 사용자가 원인을 짚을 단서가 없다.
        ///
        /// 세이브 서비스가 없으면 값만 돌려주고 키는 남긴다. 지금 못 옮겼을 뿐 다음 기회가 있는데
        /// 여기서 지우면 그 기회까지 없앤다.
        /// </summary>
        private static bool MigrateLegacyLanguage(MetaSaveService service, out LocalizationLanguage language)
        {
            if (!TryParseLanguage(PlayerPrefs.GetString(LegacyLanguagePrefKey, string.Empty), out language))
            {
                return false;
            }

            if (service != null)
            {
                // 적고 나서 저장이 실제로 통과했을 때만 옛 키를 지운다.
                service.UpdateLanguage(language.ToString(), autoSave: false);
                if (service.Save())
                {
                    PlayerPrefs.DeleteKey(LegacyLanguagePrefKey);
                    PlayerPrefs.Save();
                    Debug.Log($"[LocalizationManager] 레거시 언어 설정을 MetaSave로 이관 — {language}");
                }
            }

            return true;
        }

        /// <summary>이름 문자열 → 열거형. 비었거나 모르는 이름이면 false.</summary>
        private static bool TryParseLanguage(string name, out LocalizationLanguage language)
        {
            language = default;
            return !string.IsNullOrEmpty(name) && Enum.TryParse(name, out language);
        }
    }
}
