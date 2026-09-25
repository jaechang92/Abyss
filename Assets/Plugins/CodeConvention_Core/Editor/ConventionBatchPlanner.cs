using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeConvention.Editor
{
    public sealed class ConventionBatchFile
    {
        public string Path { get; set; }
        public ConventionRenamePlan Plan { get; set; }
        public int AcceptedCount { get; set; }
        public List<string> Skipped { get; } = new List<string>();
        public bool IsSelected { get; set; } = true;
    }

    public static class ConventionBatchPlanner
    {
        public static ConventionBatchFile Build(string path, string source, IEnumerable<ConventionViolation> violations)
        {
            var result = new ConventionBatchFile { Path = path };
            string working = source;
            int replacements = 0;
            var seen = new HashSet<string>();
            foreach (var violation in violations.OrderBy(v => v.LineNumber))
            {
                if (violation.Severity != ViolationSeverity.Warning ||
                    !seen.Add(violation.LineNumber + ":" + violation.RuleName + ":" + violation.MatchedName)) continue;
                var suggestion = ConventionSuggestion.Create(violation);
                if (!suggestion.CanPlanLocalRename)
                {
                    result.Skipped.Add($"{violation.LineNumber}줄 [{violation.RuleName}]: 자동 수정 범위 밖입니다. {suggestion.Advice}");
                    continue;
                }
                // 원래 진단 줄이 변경되었다면 억지로 새 선언에 연결하지 않고 건너뛴다.
                if (!ConventionLocalRename.TryCreate(working, violation, suggestion.Name, out var plan, out string reason))
                {
                    result.Skipped.Add($"{violation.LineNumber}줄 {violation.MatchedName}: {reason}");
                    continue;
                }
                working = plan.Updated;
                replacements += plan.ReplacementCount;
                result.AcceptedCount++;
            }
            if (result.AcceptedCount > 0)
            {
                var preview = new StringBuilder();
                string[] before = source.Split('\n');
                string[] after = working.Split('\n');
                for (int i = 0; i < before.Length; i++)
                    if (before[i] != after[i])
                        preview.AppendLine($"{i + 1}: - {before[i].TrimEnd('\r')}").AppendLine($"{i + 1}: + {after[i].TrimEnd('\r')}");
                result.Plan = new ConventionRenamePlan { Original = source, Updated = working,
                    ReplacementCount = replacements, Preview = preview.ToString() };
            }
            return result;
        }
    }
}
