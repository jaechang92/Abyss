using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeConvention.Editor
{
    public sealed class ConventionRenamePlan
    {
        public string Original { get; internal set; }
        public string Updated { get; internal set; }
        public int ReplacementCount { get; internal set; }
        public string Preview { get; internal set; }
    }

    // 전체 텍스트 치환 대신 제한된 지역 변수 범위의 식별자 토큰만 수정한다.
    // 멤버/매개변수/복잡한 구문은 IDE의 의미 기반 이름 변경에 맡긴다.
    public static class ConventionLocalRename
    {
        private static readonly Regex tokens = new Regex(
            "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])'|@?[\\p{L}_][\\p{L}\\p{Nd}_]*|[0-9]+|[^\\s]",
            RegexOptions.CultureInvariant);

        public static bool TryCreate(string source, ConventionViolation violation, string newName,
            out ConventionRenamePlan plan, out string reason)
        {
            plan = null;
            reason = "";
            if (violation.RuleName != "BoolNamingConvention" && violation.RuleName != "ConstantUpperCase")
                return Reject("이 규칙은 IDE에서 참조와 데이터 연결을 확인하며 수정하세요.", out reason);
            string oldName = violation.MatchedName;
            if (string.IsNullOrEmpty(oldName) || newName == oldName ||
                !Regex.IsMatch(newName ?? "", @"^[A-Za-z][A-Za-z0-9_]*$"))
                return Reject("추천 이름을 확인하세요. 영문자로 시작하는 새 이름이 필요합니다.", out reason);
            if (violation.RuleName == "ConstantUpperCase" && !Regex.IsMatch(newName, @"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$"))
                return Reject("상수 이름은 UPPER_SNAKE_CASE로 입력하세요.", out reason);
            if (violation.RuleName == "BoolNamingConvention" && !Regex.IsMatch(newName, @"^(?:is|has|can)[A-Z][A-Za-z0-9]*$"))
                return Reject("지역 bool 이름은 is/has/can 다음 대문자로 입력하세요.", out reason);

            // 조건부 컴파일·문자열 보간의 이름 바인딩은 이 경량 편집기로 추론하지 않는다.
            if (Regex.IsMatch(source, @"(?m)^\s*#") || source.Contains("$\"") ||
                source.Contains("$@\"") || source.Contains("@$\"") || source.Contains("\"\"\""))
                return Reject("이 파일은 조건부 컴파일 또는 보간/raw 문자열을 사용합니다. IDE 이름 변경을 이용하세요.", out reason);

            var all = tokens.Matches(source).Cast<Match>().ToList();
            if (all.Any(m => m.Value == "\"" || m.Value == "'" || m.Value == "$" || m.Value == "\\"))
                return Reject("해석할 수 없는 문자열 또는 식별자 표현이 있습니다. IDE에서 수정하세요.", out reason);
            var code = all.Where(m => !IsComment(m.Value) && !IsLiteral(m.Value)).ToList();
            string[] lines = source.Split('\n');
            int lineIndex = violation.LineNumber - 1;
            if (lineIndex < 0 || lineIndex >= lines.Length || lines[lineIndex].Trim() != violation.LineContent)
                return Reject("검사 후 코드가 변경되었습니다. 다시 검사하세요.", out reason);

            // 필드와 매개변수는 제외하고, 한 줄에 하나인 지역 선언만 허용한다.
            var declaration = Regex.Match(lines[lineIndex],
                @"^\s*(?:const\s+(?:bool|byte|sbyte|short|ushort|int|uint|long|ulong|char|float|double|decimal|string)|bool)\s+(?<name>" +
                Regex.Escape(oldName) + @")\s*=\s*(?!>)");
            if (!declaration.Success)
                return Reject("자동 적용은 메서드 안의 단독 bool 변수·지역 상수 선언을 지원합니다. 필드·매개변수는 IDE에서 수정하세요.", out reason);
            int lineStart = 0;
            for (int i = 0; i < lineIndex; i++) lineStart += lines[i].Length + 1;
            int position = lineStart + declaration.Groups["name"].Index;
            int declarationIndex = code.FindIndex(m => m.Index == position && m.Value == oldName);
            if (declarationIndex < 0)
                return Reject("실제 코드 선언을 찾지 못했습니다. 주석이나 문자열의 내용은 수정하지 않습니다.", out reason);

            var opens = new Stack<int>();
            var closes = new Dictionary<int, int>();
            List<int> parents = null;
            for (int i = 0; i < code.Count; i++)
            {
                if (i == declarationIndex) parents = opens.ToList();
                if (code[i].Value == "{") opens.Push(i);
                if (code[i].Value == "}")
                {
                    if (opens.Count == 0) return Reject("중괄호 구조를 확인할 수 없습니다.", out reason);
                    closes[opens.Pop()] = i;
                }
            }
            if (opens.Count != 0 || parents == null || parents.Count == 0)
                return Reject("지역 범위를 확인할 수 없습니다.", out reason);
            int methodOpen = parents.Where(p => IsMethodBody(code, p)).DefaultIfEmpty(-1).First();
            if (methodOpen < 0)
                return Reject("메서드 내부임을 확인할 수 없습니다. 필드·프로퍼티는 IDE에서 수정하세요.", out reason);
            int scopeOpen = parents[0];
            int scopeClose = closes[scopeOpen];
            int methodClose = closes[methodOpen];

            // 이름 충돌은 파일 전체에서 보수적으로 차단한다.
            if (code.Any(m => m.Value.TrimStart('@') == newName))
                return Reject("파일 안에 같은 이름이 이미 있습니다. 다른 이름을 선택하세요.", out reason);
            if (Regex.IsMatch(source, @"\b(?:class|struct|interface|enum|record|using)\s+" + Regex.Escape(oldName) + @"\b"))
                return Reject("같은 이름의 타입 또는 별칭이 있습니다. IDE 이름 변경을 이용하세요.", out reason);
            if (code.Any(m => m.Value == "@" + oldName))
                return Reject("이스케이프된 같은 이름이 있습니다. IDE 이름 변경을 이용하세요.", out reason);
            string methodText = source.Substring(code[methodOpen].Index,
                code[methodClose].Index - code[methodOpen].Index);
            if (Regex.IsMatch(methodText, @"\b(?:nameof|typeof|sizeof|dynamic)\b|\bnew\s*\{"))
                return Reject("이름에 의존하는 표현이 포함되어 있습니다. IDE에서 변경 영향을 확인하세요.", out reason);
            if (all.Any(m => IsLiteral(m.Value) && m.Index > code[methodOpen].Index &&
                m.Index < code[methodClose].Index && m.Value.Contains(oldName)))
                return Reject("같은 이름을 포함한 문자열이 있습니다. 문자열 계약을 IDE에서 확인하세요.", out reason);

            var replacements = new List<Match>();
            for (int i = declarationIndex; i < scopeClose; i++)
            {
                if (code[i].Value != oldName) continue;
                string previous = i == 0 ? "" : code[i - 1].Value;
                string next = i + 1 < code.Count ? code[i + 1].Value : "";
                // 다른 객체의 멤버·명명된 인수는 같은 철자여도 다른 심볼이다.
                if (previous == "." || next == ":") continue;
                if (previous == "<" || next == ">" || next == "[" && i + 2 < code.Count && code[i + 2].Value == "]")
                    return Reject("타입 이름과 구분하기 어려운 참조가 있습니다. IDE에서 수정하세요.", out reason);
                if (i != declarationIndex &&
                    (Regex.IsMatch(previous, @"^[A-Za-z_]\w*$") &&
                     previous != "return" && previous != "throw" ||
                     next == "(" || next == "<" || next == "=" && (previous == "{" || previous == ",")))
                    return Reject("같은 이름의 선언이나 구분하기 어려운 참조가 있습니다. IDE 이름 변경을 이용하세요.", out reason);
                replacements.Add(code[i]);
            }
            // 다중 변수 선언은 잘못된 범위 추정을 피하기 위해 제외한다.
            int terminator = code.FindIndex(declarationIndex, m => m.Value == ";");
            if (terminator < 0 || terminator > scopeClose || code.Skip(declarationIndex).Take(terminator - declarationIndex).Any(m => m.Value == ","))
                return Reject("복합 초기화 또는 다중 선언은 IDE에서 수정하세요.", out reason);

            var updated = new StringBuilder(source);
            foreach (var match in replacements.OrderByDescending(m => m.Index))
                updated.Remove(match.Index, match.Length).Insert(match.Index, newName);
            string result = updated.ToString();
            string[] afterLines = result.Split('\n');
            var preview = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != afterLines[i])
                    preview.AppendLine($"{i + 1}: - {lines[i].TrimEnd('\r')}").AppendLine($"{i + 1}: + {afterLines[i].TrimEnd('\r')}");
            plan = new ConventionRenamePlan { Original = source, Updated = result,
                ReplacementCount = replacements.Count, Preview = preview.ToString() };
            return true;
        }

        private static bool IsMethodBody(List<Match> code, int brace)
        {
            if (brace == 0 || code[brace - 1].Value != ")") return false;
            int start = brace - 1;
            while (start > 0 && code[start - 1].Value != ";" && code[start - 1].Value != "{" && code[start - 1].Value != "}") start--;
            var header = code.Skip(start).Take(brace - start).Select(m => m.Value).ToList();
            return header.Any(v => v == "public" || v == "private" || v == "internal" || v == "protected") &&
                !header.Any(v => v == "=" || v == "=>" || v == "delegate" || v == "new" ||
                    v == "class" || v == "struct" || v == "record" || v == "interface" || v == "enum");
        }

        private static bool IsComment(string value) => value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith("/*", StringComparison.Ordinal);
        private static bool IsLiteral(string value) => value.StartsWith("\"", StringComparison.Ordinal) || value.StartsWith("@\"", StringComparison.Ordinal) || value.StartsWith("'", StringComparison.Ordinal);
        private static bool Reject(string message, out string reason) { reason = message; return false; }
    }
}
