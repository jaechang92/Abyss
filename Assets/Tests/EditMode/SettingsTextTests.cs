using System;
using System.Collections.Generic;
using Abyss.Runtime.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 설정 패널이 쓰는 텍스트와 언어 목록의 불변식.
    ///
    /// 설정 패널은 <b>언어를 고르는 곳이면서 그 자신도 번역 대상</b>이다. 그래서 다른 화면과 달리
    /// 한 언어만 채워도 되는 곳이 아니다 — 영어로 바꾼 사용자가 다시 한국어로 돌아오려면
    /// <b>영어 화면의 「Language」 라벨을 읽을 수 있어야 한다.</b> 한 칸이라도 비면 그 언어에
    /// 갇히는 자리가 생긴다. 세 언어를 전부 채웠는지를 여기서 고정한다.
    ///
    /// ⚠️ <c>Resources/Data/GameText.csv</c>의 <b>실제 파일</b>을 읽는다.
    /// </summary>
    public sealed class SettingsTextTests
    {
        private const string CSV_RESOURCE_PATH = "Data/GameText";

        private static readonly string[] LanguageColumns = { "Korean", "English", "Japanese" };

        /// <summary>설정 패널에 고정 문구로 붙는 키 전부. 패널에 라벨을 늘리면 여기도 늘린다.</summary>
        private static readonly string[] SettingsKeys =
        {
            StringKey.Menu_Settings,          // 패널 제목
            StringKey.Settings_MasterVolume,
            StringKey.Settings_BgmVolume,
            StringKey.Settings_SfxVolume,
            StringKey.Settings_Fullscreen,
            StringKey.Settings_Resolution,
            StringKey.Settings_Language,
            StringKey.Settings_KeyGuide,
            StringKey.Common_Close,           // 닫기 버튼
        };

        // StringKey → (언어 컬럼 → 원문). 이스케이프는 풀지 않은 상태다.
        private Dictionary<string, Dictionary<string, string>> rows;

        [SetUp]
        public void SetUp()
        {
            var asset = Resources.Load<TextAsset>(CSV_RESOURCE_PATH);
            Assert.IsNotNull(asset, $"GameText.csv를 못 읽었다 — Resources/{CSV_RESOURCE_PATH}.csv 확인.");

            string[] records = CsvParser.SplitCsvRecords(asset.text);
            Assert.Greater(records.Length, 1, "CSV 행 수 부족 (헤더+데이터 필요).");

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

        /// <summary>
        /// 🔑 이 테스트가 이 파일의 이유다. 한 칸이 비면 그 언어에서 라벨이 <c>[Settings_Language]</c>로
        /// 뜨고, 거기서 언어를 되돌리려던 사용자는 자기가 무엇을 누르는지 모르게 된다.
        /// </summary>
        [Test]
        public void 설정_키가_세_언어_모두_채워져_있다()
        {
            foreach (string key in SettingsKeys)
            {
                Assert.IsTrue(rows.ContainsKey(key), $"CSV에 없는 설정 키: {key}");

                foreach (string column in LanguageColumns)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(rows[key][column]),
                        $"{key}의 {column} 칸이 비었다 — 설정 패널은 언어를 고르는 곳이라 " +
                        $"그 언어를 읽는 사용자가 자기 화면에서 이 라벨을 읽을 수 있어야 한다.");
                }
            }
        }

        [Test]
        public void 조작_안내는_두_글자_이스케이프로_두_줄을_적는다()
        {
            foreach (string column in LanguageColumns)
            {
                string raw = rows[StringKey.Settings_KeyGuide][column];

                Assert.IsFalse(raw.Contains("\n"),
                    $"{column} 조작 안내에 진짜 줄바꿈이 들어갔다 — 한 키는 CSV에서 한 줄이어야 한다.");
                Assert.IsTrue(raw.Contains("\\n"),
                    $"{column} 조작 안내가 한 줄이 됐다 — 키 목록이 가로로 흘러 패널 밖으로 나간다.");

                string unescaped = CsvParser.UnescapeNewlines(raw);
                Assert.AreEqual(2, unescaped.Split('\n').Length,
                    $"{column} 조작 안내는 두 줄이어야 한다(현재 폭·높이 기준).");
            }
        }

        /// <summary>
        /// 언어 이름은 번역하지 않는다 — 각 언어를 그 언어 자신의 표기로 보여 준다.
        /// 열거형에 언어를 늘리고 표기를 빠뜨리면 열거형 이름("Japanese")이 그대로 뜬다.
        /// </summary>
        [Test]
        public void 모든_언어가_자기_표기를_갖는다()
        {
            var seen = new HashSet<string>();

            foreach (LocalizationLanguage language in Enum.GetValues(typeof(LocalizationLanguage)))
            {
                string nativeName = LocalizationLanguageNames.GetNativeName(language);

                Assert.IsFalse(string.IsNullOrWhiteSpace(nativeName),
                    $"{language}의 표기가 비었다.");
                Assert.AreNotEqual(language.ToString(), nativeName,
                    $"{language}의 표기가 열거형 이름 그대로다 — " +
                    $"LocalizationLanguageNames에 항목을 추가하는 것을 빠뜨렸다.");
                Assert.IsTrue(seen.Add(nativeName),
                    $"표기 「{nativeName}」가 두 언어에 쓰였다 — 셀렉터에서 구분되지 않는다.");
            }
        }

        [Test]
        public void 언어_열거형과_CSV_언어_컬럼_수가_같다()
        {
            int enumCount = Enum.GetValues(typeof(LocalizationLanguage)).Length;

            Assert.AreEqual(enumCount, LanguageColumns.Length,
                "지원 언어가 늘었는데 CSV 컬럼(또는 이 테스트의 목록)이 따라오지 않았다 — " +
                "LocalizationLanguage 주석의 추가 절차 4단계를 확인할 것.");
        }
    }
}
