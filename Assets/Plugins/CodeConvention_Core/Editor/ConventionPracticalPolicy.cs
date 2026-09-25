using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeConvention.Editor
{
    // 실용 기준: 접두어 모양보다 명확한 상태/옵션 표현을 허용한다.
    public static class ConventionPracticalPolicy
    {
        private static readonly HashSet<string> predicateWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "is", "has", "have", "can", "was", "were", "had", "should", "will", "did", "does",
            "use", "uses", "show", "allow", "enable", "disable", "require", "requires", "need", "needs",
            "skip", "auto", "overwrite", "activate", "find", "log", "follow", "constrain", "start", "enter",
            "expect", "resist", "resists"
        };
        private static readonly HashSet<string> stateWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "active", "activated", "added", "affordable", "assigned", "attempted", "available", "blocked", "braced",
            "built", "busy", "changed", "closed", "complete", "completed", "confirm", "consumed", "dirty", "disabled",
            "enabled", "equipped", "expected", "explodes", "first", "flip", "flips", "found", "heavy", "increased",
            "initialized", "invalid", "loaded", "locked", "loop", "missing", "new", "open", "out", "pending",
            "pressed", "proceed", "queued", "quitting", "ran", "range", "ready", "released", "removed", "running",
            "saved", "selected", "smoothed", "success", "succeeded", "failed", "true", "false", "used", "valid", "vertical", "visible", "armed"
        };

        public static bool IsDescriptiveBoolean(string name)
        {
            if (!Regex.IsMatch(name ?? "", @"^[A-Za-z][A-Za-z0-9]*$")) return false;
            if (string.Equals(name, "prettyPrint", StringComparison.OrdinalIgnoreCase)) return true;
            var words = Regex.Matches(name, @"[A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[0-9]+")
                .Cast<Match>().Select(m => m.Value).ToList();
            return words.Any(predicateWords.Contains) || words.Any(stateWords.Contains);
        }
    }

    // 소스의 가장 안쪽 타입만 판별한다. 파일 단위 예외나 이름 접미사(Data 등) 추측은 사용하지 않는다.
    public sealed class ConventionDataContext
    {
        private sealed class TypeScope
        {
            public string Name;
            public string BaseName;
            public bool IsData;
            public int Start;
            public int End;
        }

        private readonly string[] lines;
        private readonly int[] offsets;
        private readonly List<TypeScope> scopes = new List<TypeScope>();

        public ConventionDataContext(string source)
        {
            // 위치를 보존하면서 주석과 문자열을 지워 본문 속 가짜 타입 선언을 제외한다.
            string masked = Regex.Replace(source,
                "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])'",
                m => new string(m.Value.Select(c => c == '\n' || c == '\r' ? c : ' ').ToArray()));
            lines = masked.Split('\n');
            offsets = new int[lines.Length];
            for (int i = 1; i < offsets.Length; i++) offsets[i] = offsets[i - 1] + lines[i - 1].Length + 1;
            var closeBraces = new Dictionary<int, int>();
            var stack = new Stack<int>();
            for (int i = 0; i < masked.Length; i++)
            {
                if (masked[i] == '{') stack.Push(i);
                else if (masked[i] == '}' && stack.Count > 0) closeBraces[stack.Pop()] = i;
            }
            const string TYPE_PATTERN = @"(?<attributes>(?:\[[^\]]*\]\s*)*)(?:(?:public|private|protected|internal|sealed|abstract|partial|readonly|ref|static)\s+)*(?<kind>class|struct)\s+(?<name>\w+)(?:\s*<[^>{}]+>)?(?<tail>[^;{}]*)\{";
            foreach (Match match in Regex.Matches(masked, TYPE_PATTERN))
            {
                int start = match.Index + match.Length - 1;
                if (!closeBraces.TryGetValue(start, out int end)) continue;
                string attributes = match.Groups["attributes"].Value;
                var baseType = Regex.Match(match.Groups["tail"].Value, @":\s*(?:\w+\.)*(?<name>\w+)");
                string baseName = baseType.Groups["name"].Value;
                scopes.Add(new TypeScope { Name = match.Groups["name"].Value, BaseName = baseName,
                    IsData = match.Groups["kind"].Value == "struct" ||
                        Regex.IsMatch(attributes, @"\b(?:Serializable|SerializableAttribute|CreateAssetMenu|CreateAssetMenuAttribute)\b") ||
                        baseName == "MonoBehaviour" || baseName == "ScriptableObject",
                    Start = start, End = end });
            }
        }

        public bool IsDataField(int lineNumber, string name)
        {
            int index = lineNumber - 1;
            if (index < 0 || index >= lines.Length || string.IsNullOrEmpty(name)) return false;
            // 프로퍼티/상수/static/readonly 필드는 데이터 예외에 포함하지 않는다.
            var field = Regex.Match(lines[index], @"\bpublic\s+(?!(?:static|readonly|const|event)\b)\w+(?:<[^>]+>)?(?:\[\])?\s+" +
                Regex.Escape(name) + @"\s*(?:;|=(?!>))");
            if (!field.Success) return false;
            // 명시적으로 직렬화에서 제외한 멤버는 일반 명명 규칙을 적용한다.
            string attributeLines = lines[index].Substring(0, field.Index);
            for (int i = index - 1; i >= 0 && (string.IsNullOrWhiteSpace(lines[i]) || Regex.IsMatch(lines[i].Trim(), @"^(?:\[[^\]]*\]\s*)+$")); i--)
                attributeLines = lines[i] + "\n" + attributeLines;
            if (Regex.IsMatch(attributeLines, @"\b(?:NonSerialized|NonSerializedAttribute)\b")) return false;
            int position = offsets[index] + field.Index;
            var scope = scopes.Where(t => t.Start < position && position < t.End).OrderByDescending(t => t.Start).FirstOrDefault();
            var visited = new HashSet<TypeScope>();
            while (scope != null && visited.Add(scope))
            {
                if (scope.IsData) return true;
                var bases = scopes.Where(t => t.Name == scope.BaseName).ToList();
                scope = bases.Count == 1 ? bases[0] : null;
            }
            return false;
        }
    }
}
