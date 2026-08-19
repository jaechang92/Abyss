using System.Collections.Generic;
using Abyss.Runtime.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 자막(프롤로그·엔딩) 텍스트의 불변식. 개별 문장의 품질이 아니라 <b>여는 자막과 닫는 자막이
    /// 같은 사람을 가리키는가</b>를 본다.
    ///
    /// 이 테스트가 생긴 계기: 프롤로그를 1인칭으로 확정한 시점에 엔딩 자막은 아직 <b>2인칭</b>이었다
    /// ("당신은 심연을 벗어났다"). 둘을 따로 읽으면 어느 쪽도 이상하지 않아서, 한 번에 이어 보기
    /// 전에는 드러나지 않는다 — 프롤로그는 게임 시작에, 엔딩은 완주 뒤에 나오므로 한 세션에서
    /// 나란히 볼 일이 거의 없다. 사람이 못 보는 자리라 테스트로 고정한다.
    ///
    /// 두 번째로 보는 것은 <b>줄바꿈 이스케이프</b>다. 자막의 줄바꿈은 곧 호흡이라
    /// CSV에 두 글자 <c>\n</c>으로 적고 로드 시 푼다(<see cref="CsvParser.UnescapeNewlines"/>).
    /// 이 변환이 빠지면 오류 없이 화면에 <c>\n</c>이 글자로 뜬다.
    ///
    /// ⚠️ <c>Resources/Data/GameText.csv</c>의 <b>실제 파일</b>을 읽는다.
    /// </summary>
    public sealed class SubtitleTextTests
    {
        private const string CSV_RESOURCE_PATH = "Data/GameText";

        private static readonly string[] PrologueKeys =
        {
            StringKey.Story_Prologue_Line1,
            StringKey.Story_Prologue_Line2,
            StringKey.Story_Prologue_Line3,
            StringKey.Story_Prologue_Line4,
        };

        private static readonly string[] EndingKeys =
        {
            StringKey.Story_Ending_Line1,
            StringKey.Story_Ending_Line2,
            StringKey.Story_Ending_Line3,
            StringKey.Story_Ending_Line4,
        };

        // 자막의 화자는 주인공 자신이다. 주인공을 「자네」로 부르는 것은 NPC의 몫이고,
        // 「당신」은 인칭 규약을 정하기 전의 잔재다(12-prologue-ending-text.md §3-1).
        private static readonly string[] BannedSecondPerson = { "당신", "자네", "그대" };

        /// <summary>CSV 원문(이스케이프를 풀지 않은 상태). 로더가 하는 일을 나눠서 보기 위해 원문을 들고 있는다.</summary>
        private Dictionary<string, string> rawKorean;

        [SetUp]
        public void SetUp()
        {
            var asset = Resources.Load<TextAsset>(CSV_RESOURCE_PATH);
            Assert.IsNotNull(asset, $"GameText.csv를 못 읽었다 — Resources/{CSV_RESOURCE_PATH}.csv 확인.");

            string[] records = CsvParser.SplitCsvRecords(asset.text);
            Assert.Greater(records.Length, 1, "CSV 행 수 부족 (헤더+데이터 필요).");

            var colIndex = CsvParser.BuildColumnIndex(records[0]);
            rawKorean = new Dictionary<string, string>();

            for (int i = 1; i < records.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(records[i])) continue;

                string[] values = CsvParser.ParseCsvLine(records[i]);
                string key = CsvParser.GetValue(values, colIndex, "StringKey");
                if (string.IsNullOrWhiteSpace(key) || rawKorean.ContainsKey(key)) continue;

                rawKorean[key] = CsvParser.GetValue(values, colIndex, "Korean");
            }
        }

        [Test]
        public void 자막_키가_전부_CSV에_있고_한국어가_채워져_있다()
        {
            foreach (string key in AllSubtitleKeys())
            {
                Assert.IsTrue(rawKorean.ContainsKey(key), $"CSV에 없는 자막 키: {key}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rawKorean[key]),
                    $"한국어 칸이 비어 있다: {key} — 서사 텍스트는 한국어만 채운다(10-narrative-plan §1-1).");
            }
        }

        /// <summary>
        /// 🔴 이 테스트가 이 파일의 이유다. 프롤로그만 1인칭으로 고치고 엔딩을 두면 조용히 어긋난다.
        /// </summary>
        [Test]
        public void 자막은_2인칭_호칭을_쓰지_않는다()
        {
            foreach (string key in AllSubtitleKeys())
            {
                string text = rawKorean[key];
                foreach (string banned in BannedSecondPerson)
                {
                    Assert.IsFalse(text.Contains(banned),
                        $"{key}에 2인칭 호칭 「{banned}」가 있다 — 자막의 화자는 주인공 자신(1인칭 「나」)이다. " +
                        $"「{banned}」로 부르는 것은 NPC의 몫이다. 실제 값: \"{text}\"");
                }
            }
        }

        [Test]
        public void 줄바꿈은_두_글자_이스케이프로_적고_로드에서_풀린다()
        {
            // 프롤로그 첫 문단은 두 줄이다 — 규약이 살아 있는지 볼 표본으로 고정한다.
            string raw = rawKorean[StringKey.Story_Prologue_Line1];

            Assert.IsFalse(raw.Contains("\n"),
                "CSV 원문에 진짜 줄바꿈이 들어갔다 — 한 키는 한 줄이어야 diff·grep에서 행으로 읽힌다.");
            Assert.IsTrue(raw.Contains("\\n"),
                "프롤로그 1문단이 한 줄이 됐다 — 줄바꿈이 곧 호흡이라 문단 구조가 바뀐 것이다.");

            string unescaped = CsvParser.UnescapeNewlines(raw);
            Assert.IsTrue(unescaped.Contains("\n"), "UnescapeNewlines가 줄바꿈을 풀지 않았다.");
            Assert.IsFalse(unescaped.Contains("\\n"), "이스케이프가 문자 그대로 남았다 — 화면에 \\n이 글자로 뜬다.");
        }

        [Test]
        public void 이스케이프가_없는_텍스트는_그대로_지나간다()
        {
            // 기존 행(백슬래시 없음)의 뜻이 바뀌지 않는다는 것 — 이 규약을 나중에 넣을 수 있었던 근거다.
            const string plain = "또 하나의 낙오자인가.";
            Assert.AreEqual(plain, CsvParser.UnescapeNewlines(plain));
            Assert.AreEqual(string.Empty, CsvParser.UnescapeNewlines(string.Empty));
            Assert.IsNull(CsvParser.UnescapeNewlines(null));
        }

        private static IEnumerable<string> AllSubtitleKeys()
        {
            foreach (string key in PrologueKeys) yield return key;
            foreach (string key in EndingKeys) yield return key;
        }
    }
}
