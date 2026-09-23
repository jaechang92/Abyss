using System.Collections.Generic;
using Abyss.Runtime.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 저장 알림(SaveStatusOverlay) 문구의 불변식.
    ///
    /// 이 문구는 <b>진행이 저장되지 않는다</b>는 경고다. 한 언어라도 비면 그 언어 사용자에게는
    /// <c>[SaveStatus_BlockedBody]</c> 같은 키가 뜨고, 경고가 있어도 없는 것과 같아진다.
    /// 설정 패널(SettingsTextTests)과 같은 이유로 세 언어를 모두 채웠는지 고정한다.
    ///
    /// ⚠️ <c>Resources/Data/GameText.csv</c>의 <b>실제 파일</b>을 읽는다.
    /// </summary>
    public sealed class SaveStatusTextTests
    {
        private const string CSV_RESOURCE_PATH = "Data/GameText";

        private static readonly string[] LanguageColumns = { "Korean", "English", "Japanese" };

        /// <summary>오버레이가 쓰는 키 전부. 오버레이에 문구를 늘리면 여기도 늘린다.</summary>
        private static readonly string[] SaveStatusKeys =
        {
            StringKey.SaveStatus_BlockedTitle,
            StringKey.SaveStatus_BlockedBody,
            StringKey.SaveStatus_PathFormat,
            StringKey.SaveStatus_Retry,
            StringKey.SaveStatus_ContinueWithoutSave,
            StringKey.SaveStatus_RetryFailed,
            StringKey.SaveStatus_Badge,
            StringKey.SaveStatus_Restored,
            StringKey.SaveStatus_WriteFailed,
        };

        // StringKey → (언어 컬럼 → 원문).
        private Dictionary<string, Dictionary<string, string>> rows;

        [SetUp]
        public void SetUp()
        {
            var asset = Resources.Load<TextAsset>(CSV_RESOURCE_PATH);
            Assert.IsNotNull(asset, $"GameText.csv를 못 읽었다 — Resources/{CSV_RESOURCE_PATH}.csv 확인.");

            string[] records = CsvParser.SplitCsvRecords(asset.text);
            var colIndex = CsvParser.BuildColumnIndex(records[0]);
            rows = new Dictionary<string, Dictionary<string, string>>();

            for (int i = 1; i < records.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(records[i])) continue;

                string[] values = CsvParser.ParseCsvLine(records[i]);
                string key = CsvParser.GetValue(values, colIndex, "StringKey");
                if (string.IsNullOrWhiteSpace(key) || rows.ContainsKey(key)) continue;

                var byLanguage = new Dictionary<string, string>();
                foreach (string column in LanguageColumns)
                {
                    byLanguage[column] = CsvParser.GetValue(values, colIndex, column);
                }
                rows[key] = byLanguage;
            }
        }

        [Test]
        public void 저장_알림_키가_세_언어_모두_채워져_있다()
        {
            foreach (string key in SaveStatusKeys)
            {
                Assert.IsTrue(rows.ContainsKey(key), $"CSV에 없는 저장 알림 키: {key}");
                foreach (string column in LanguageColumns)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(rows[key][column]), $"{key}의 {column} 칸이 비었다.");
                }
            }
        }

        /// <summary>저장 위치 줄은 경로를 끼워 넣는다 — 한 언어라도 자리표시자가 빠지면 경로가 사라진다.</summary>
        [Test]
        public void 저장_위치_문구는_세_언어_모두_경로_자리를_가진다()
        {
            foreach (string column in LanguageColumns)
            {
                StringAssert.Contains("{0}", rows[StringKey.SaveStatus_PathFormat][column], $"{column} 저장 위치 문구에 {{0}}이 없다.");
            }
        }
    }
}
