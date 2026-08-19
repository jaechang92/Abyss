using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Abyss.Runtime.Localization
{
    /// <summary>
    /// CSV 파싱 공통 유틸리티. RFC4180 호환 (쉼표 구분, 따옴표 지원, 셀 내부 줄바꿈 보존).
    /// 현재는 LocalizationManager 전용이나, 추후 다른 데이터 임포터로 확장 가능.
    /// </summary>
    public static class CsvParser
    {
        /// <summary>
        /// RFC4180 호환 단일 라인 파싱. 셀 내부 줄바꿈은 처리하지 않는다 (라인 단위).
        /// </summary>
        public static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool isInQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (isInQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        isInQuotes = !isInQuotes;
                    }
                }
                else if (c == ',' && !isInQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString());

            return fields.ToArray();
        }

        /// <summary>
        /// 헤더 라인에서 컬럼명 → 인덱스 딕셔너리 생성.
        /// </summary>
        public static Dictionary<string, int> BuildColumnIndex(string headerLine)
        {
            string cleaned = RemoveBom(headerLine);
            string[] headers = ParseCsvLine(cleaned);
            var colIndex = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                colIndex[headers[i].Trim()] = i;
            }

            return colIndex;
        }

        /// <summary>
        /// 필수 헤더 검증. 누락된 헤더 목록 반환 (정상이면 빈 리스트).
        /// </summary>
        public static List<string> ValidateRequiredHeaders(
            Dictionary<string, int> colIndex, string[] required)
        {
            var missing = new List<string>();
            foreach (string header in required)
            {
                if (!colIndex.ContainsKey(header))
                {
                    missing.Add(header);
                }
            }
            return missing;
        }

        /// <summary>
        /// 컬럼 인덱스 기반 안전한 값 추출. 키 없거나 인덱스 초과 시 빈 문자열.
        /// </summary>
        public static string GetValue(
            string[] values, Dictionary<string, int> colIndex, string key)
        {
            if (!colIndex.TryGetValue(key, out int idx)) return "";
            return idx < values.Length ? values[idx].Trim() : "";
        }

        /// <summary>
        /// CSV 텍스트를 단순 라인 배열로 분리. 따옴표 내부 줄바꿈은 보존하지 않는다.
        /// 멀티라인 셀이 있는 CSV에는 SplitCsvRecords()를 사용한다.
        /// </summary>
        public static string[] SplitLines(string csvText)
        {
            string cleaned = RemoveBom(csvText);
            return cleaned.Split(
                new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        /// <summary>
        /// RFC4180 호환 레코드 분할. 따옴표 내부의 줄바꿈을 셀 내부 데이터로 유지하면서
        /// 레코드 단위로 자른다. 셀에 줄바꿈이 포함된 CSV(예: 포맷 인자 노트 컬럼)에 사용.
        /// </summary>
        public static string[] SplitCsvRecords(string csvText)
        {
            string cleaned = RemoveBom(csvText);
            var records = new List<string>();
            var current = new StringBuilder();
            bool isInQuotes = false;

            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];

                if (c == '"')
                {
                    if (isInQuotes
                        && i + 1 < cleaned.Length
                        && cleaned[i + 1] == '"')
                    {
                        current.Append('"').Append('"');
                        i++;
                        continue;
                    }
                    isInQuotes = !isInQuotes;
                    current.Append('"');
                }
                else if ((c == '\n' || c == '\r') && !isInQuotes)
                {
                    if (c == '\r'
                        && i + 1 < cleaned.Length
                        && cleaned[i + 1] == '\n')
                    {
                        i++;
                    }
                    records.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                records.Add(current.ToString());
            }

            return records.ToArray();
        }

        /// <summary>
        /// UTF-8 BOM 제거. 첫 문자가 U+FEFF면 1글자 잘라낸다.
        /// </summary>
        public static string RemoveBom(string text)
        {
            if (text.Length > 0 && text[0] == '﻿')
            {
                return text.Substring(1);
            }
            return text;
        }

        /// <summary>
        /// 셀 안의 두 글자 <c>\n</c>을 실제 줄바꿈으로 푼다.
        ///
        /// RFC4180은 따옴표로 감싸면 셀 안에 진짜 줄바꿈을 넣을 수 있게 하지만, 그 방식은
        /// <b>한 레코드가 여러 물리 행에 걸치게 만들어</b> 스프레드시트 밖(diff·grep·리뷰)에서
        /// 행 단위로 읽히지 않는다. 자막 문단처럼 줄바꿈이 곧 호흡인 텍스트가 늘어나는 참에
        /// 이스케이프 표기로 통일한다 — <b>한 키는 언제나 한 줄이다.</b>
        ///
        /// 기존 CSV에 백슬래시가 한 글자도 없어(2026-08-19 전수 확인) 과거 행의 뜻은 바뀌지 않는다.
        /// </summary>
        public static string UnescapeNewlines(string value)
            => string.IsNullOrEmpty(value) ? value : value.Replace("\\n", "\n");

        // --- 타입 파싱 헬퍼 (향후 데이터 임포터 확장용) ---

        public static int ParseInt(string val, int defaultVal)
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultVal;
            return int.TryParse(val, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int result)
                ? result : defaultVal;
        }

        public static float ParseFloat(string val, float defaultVal)
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultVal;
            return float.TryParse(val, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float result)
                ? result : defaultVal;
        }

        public static bool ParseBool(string val, bool defaultVal)
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultVal;
            if (val == "1"
                || val.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (val == "0"
                || val.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;
            return defaultVal;
        }

        public static T ParseEnum<T>(string val, T defaultVal)
            where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultVal;
            if (Enum.TryParse<T>(val, true, out var result))
                return result;
            return defaultVal;
        }
    }
}
