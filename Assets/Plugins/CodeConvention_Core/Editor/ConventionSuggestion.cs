using System;
using System.Text.RegularExpressions;

namespace CodeConvention.Editor
{
    public sealed class ConventionSuggestion
    {
        public string Name { get; private set; }
        public string Advice { get; private set; }
        public bool CanPlanLocalRename { get; private set; }

        public static ConventionSuggestion Create(ConventionViolation violation)
        {
            string name = violation.MatchedName ?? string.Empty;
            string cleanName = name.TrimStart('_');
            var suggestion = new ConventionSuggestion();
            switch (violation.RuleName)
            {
                case "BoolNamingConvention":
                    bool isPublic = Regex.IsMatch(violation.LineContent ?? "", @"\bpublic\b");
                    suggestion.Name = (isPublic ? "Is" : "is") + Pascal(cleanName);
                    suggestion.Advice = "참/거짓의 의미를 드러내세요. changed·requiresAlive 같은 명확한 상태/옵션은 허용합니다. 모호한 이름에는 is/has/can을 권장합니다.";
                    suggestion.CanPlanLocalRename = true;
                    break;
                case "ConstantUpperCase":
                    suggestion.Name = Regex.Replace(Regex.Replace(cleanName,
                        @"([A-Z]+)([A-Z][a-z])", "$1_$2"), @"([a-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();
                    suggestion.Advice = "상수는 UPPER_SNAKE_CASE로 작성합니다.";
                    suggestion.CanPlanLocalRename = true;
                    break;
                case "PublicFieldPascalCase":
                    suggestion.Name = Pascal(cleanName);
                    suggestion.Advice = "public 필드는 PascalCase를 권장합니다. IDE 이름 변경으로 참조를 함께 수정하고, 직렬화된 필드는 기존 데이터 이전도 확인하세요.";
                    break;
                case "SerializeFieldCamelCase":
                    suggestion.Name = cleanName.Length == 0 ? "" : char.ToLowerInvariant(cleanName[0]) + cleanName.Substring(1);
                    suggestion.Advice = "직렬화 필드는 camelCase를 사용하세요. 이름 변경 시 FormerlySerializedAs와 코드 참조를 함께 검토해야 합니다.";
                    break;
                case "EventNamingConvention":
                    suggestion.Name = "On" + Pascal(cleanName);
                    suggestion.Advice = "이벤트는 On 접두어를 권장합니다. 구독·해제·발행 참조를 IDE 이름 변경으로 함께 수정하세요.";
                    break;
                case "MaxLineCount":
                    suggestion.Advice = "책임 단위로 파일을 분리하세요. 기존 클래스 계약을 유지해야 하면 partial을 검토하세요. 자동 분할은 지원하지 않습니다.";
                    break;
                default:
                    suggestion.Advice = "규칙과 실제 선언을 확인한 뒤 IDE에서 수정하세요.";
                    break;
            }
            return suggestion;
        }

        private static string Pascal(string name)
        {
            return name.Length == 0 ? "" : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}
