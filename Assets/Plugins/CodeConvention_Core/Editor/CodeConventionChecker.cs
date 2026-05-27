using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CodeConvention.Editor
{
    /// <summary>
    /// 코드 컨벤션 위반 정보
    /// </summary>
    public class ConventionViolation
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string RuleName { get; set; }
        public string Message { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string LineContent { get; set; }
    }

    /// <summary>
    /// 위반 심각도
    /// </summary>
    public enum ViolationSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// 코드 컨벤션 검사기
    /// CLAUDE.md에 정의된 규칙들을 검사
    /// </summary>
    public static class CodeConventionChecker
    {
        // 검사 규칙 정의
        private static readonly List<ConventionRule> rules = new List<ConventionRule>
        {
            // 규칙 1: 클래스명은 PascalCase
            new ConventionRule
            {
                Name = "ClassNamePascalCase",
                Description = "클래스명은 PascalCase로 작성",
                Pattern = @"\bclass\s+([a-z][a-zA-Z0-9]*)",
                Severity = ViolationSeverity.Error,
                MessageFormat = "클래스명은 PascalCase로 작성: '{0}'"
            },

            // 규칙 2: 메서드명은 PascalCase
            // - readonly 한정자 추가 지원 (static readonly 필드 선언이 메서드로 오인되지 않도록)
            // - 메서드명 자리에 C# 한정자/예약어가 오면 제외 (튜플 타입 (string, ...)을 함수 시그니처로 오인하는 백트래킹 방지)
            new ConventionRule
            {
                Name = "MethodNamePascalCase",
                Description = "메서드명은 PascalCase로 작성",
                Pattern = @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:abstract\s+)?(?:\w+(?:<[^>]+>)?)\s+(?!(?:readonly|volatile|const|static|abstract|virtual|override|sealed|unsafe|extern|partial|async|new)\b)([a-z][a-zA-Z0-9]*)\s*\(",
                Severity = ViolationSeverity.Error,
                MessageFormat = "메서드명은 PascalCase로 작성: '{0}'",
                ExcludePatterns = new[] { @"^(get|set|add|remove)_" }
            },

            // 규칙 3: private 변수/필드가 PascalCase이면 오류 (camelCase 또는 _camelCase 허용)
            new ConventionRule
            {
                Name = "PrivateFieldCamelCase",
                Description = "private 필드는 camelCase 또는 _camelCase로 작성",
                Pattern = @"\bprivate\s+(?:readonly\s+)?(?:\w+(?:<[^>]+>)?)\s+([A-Z][a-zA-Z0-9]*)\s*[;=]",
                Severity = ViolationSeverity.Error,
                MessageFormat = "private 필드는 camelCase 또는 _camelCase로 작성: '{0}'"
            },

            // 규칙 4: 상수는 UPPER_SNAKE_CASE
            new ConventionRule
            {
                Name = "ConstantUpperCase",
                Description = "상수는 UPPER_SNAKE_CASE로 작성",
                Pattern = @"\bconst\s+\w+\s+([a-z][a-zA-Z0-9]*)\s*=",
                Severity = ViolationSeverity.Warning,
                MessageFormat = "상수는 UPPER_SNAKE_CASE로 작성: '{0}'"
            },

            // 규칙 5: bool 타입은 is/has/can 접두어 권장
            new ConventionRule
            {
                Name = "BoolNamingConvention",
                Description = "bool 타입은 is/has/can 접두어 사용 권장",
                Pattern = @"\bbool\s+(?!is|has|can|Is|Has|Can)([a-zA-Z][a-zA-Z0-9]*)\s*[;=]",
                Severity = ViolationSeverity.Warning,
                MessageFormat = "bool 변수는 is/has/can 접두어 권장: '{0}'"
            },

            // 규칙 6: 인터페이스는 I 접두어
            new ConventionRule
            {
                Name = "InterfacePrefix",
                Description = "인터페이스는 I 접두어 사용",
                Pattern = @"\binterface\s+([^I][a-zA-Z0-9]*)",
                Severity = ViolationSeverity.Error,
                MessageFormat = "인터페이스는 I 접두어 필요: '{0}'"
            },

            // 규칙 7: 이벤트는 On + 동사
            new ConventionRule
            {
                Name = "EventNamingConvention",
                Description = "이벤트는 On + 동사 형식 권장",
                Pattern = @"\bevent\s+\w+(?:<[^>]+>)?\s+(?!On)([A-Z][a-zA-Z0-9]*)\s*;",
                Severity = ViolationSeverity.Warning,
                MessageFormat = "이벤트는 On 접두어 권장: '{0}'"
            },

            // 규칙 8: SerializeField 필드는 camelCase
            new ConventionRule
            {
                Name = "SerializeFieldCamelCase",
                Description = "SerializeField 필드는 camelCase로 작성",
                Pattern = @"\[SerializeField\]\s*(?:private|protected)?\s*\w+\s+([A-Z][a-zA-Z0-9]*)\s*[;=]",
                Severity = ViolationSeverity.Warning,
                MessageFormat = "SerializeField 필드는 camelCase로 작성: '{0}'"
            },

            // 규칙 9: public 필드는 PascalCase (프로퍼티처럼)
            new ConventionRule
            {
                Name = "PublicFieldPascalCase",
                Description = "public 필드는 PascalCase로 작성",
                Pattern = @"\bpublic\s+(?!class|interface|struct|enum|event|delegate)(?:\w+(?:<[^>]+>)?)\s+([a-z][a-zA-Z0-9]*)\s*[;{=]",
                Severity = ViolationSeverity.Warning,
                MessageFormat = "public 필드는 PascalCase로 작성: '{0}'",
                ExcludePatterns = new[] { @"\(" } // 메서드 제외
            }
        };

        /// <summary>
        /// 파일 라인 수 제한
        /// </summary>
        private const int MaxLineCount = 500;

        /// <summary>
        /// 단일 파일 검사
        /// </summary>
        public static List<ConventionViolation> CheckFile(string filePath)
        {
            var violations = new List<ConventionViolation>();

            if (!File.Exists(filePath) || !filePath.EndsWith(".cs"))
            {
                return violations;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodeConvention] 파일 읽기 실패: {filePath}, {e.Message}");
                return violations;
            }

            // 라인 수 검사
            if (lines.Length > MaxLineCount)
            {
                violations.Add(new ConventionViolation
                {
                    FilePath = filePath,
                    LineNumber = 1,
                    RuleName = "MaxLineCount",
                    Message = $"파일 라인 수 초과: {lines.Length}줄 (최대 {MaxLineCount}줄)",
                    Severity = ViolationSeverity.Warning,
                    LineContent = $"Total: {lines.Length} lines"
                });
            }

            // 각 라인별 규칙 검사
            bool inMultiLineComment = false;
            //bool inString = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;

                // 멀티라인 주석 처리
                if (line.Contains("/*"))
                {
                    inMultiLineComment = true;
                }
                if (line.Contains("*/"))
                {
                    inMultiLineComment = false;
                    continue;
                }
                if (inMultiLineComment)
                {
                    continue;
                }

                // 단일 라인 주석 제거
                string codeLine = RemoveComments(line);
                if (string.IsNullOrWhiteSpace(codeLine))
                {
                    continue;
                }

                // 문자열 리터럴 제거 (문자열 내용은 검사하지 않음)
                codeLine = RemoveStringLiterals(codeLine);

                // 각 규칙 검사
                foreach (var rule in rules)
                {
                    if (IsViolation(codeLine, rule, out string matchedContent))
                    {
                        violations.Add(new ConventionViolation
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            RuleName = rule.Name,
                            Message = string.Format(rule.MessageFormat, matchedContent),
                            Severity = rule.Severity,
                            LineContent = line.Trim()
                        });
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// 폴더 내 모든 .cs 파일 검사
        /// </summary>
        public static List<ConventionViolation> CheckFolder(string folderPath)
        {
            var violations = new List<ConventionViolation>();

            if (!Directory.Exists(folderPath))
            {
                return violations;
            }

            string[] csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);

            foreach (string file in csFiles)
            {
                // Editor 폴더 내 파일은 일부 규칙 완화 가능 (현재는 동일 적용)
                violations.AddRange(CheckFile(file));
            }

            return violations;
        }

        /// <summary>
        /// 선택된 에셋들 검사
        /// </summary>
        public static List<ConventionViolation> CheckAssets(UnityEngine.Object[] assets)
        {
            var violations = new List<ConventionViolation>();

            foreach (var asset in assets)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(asset);

                if (Directory.Exists(path))
                {
                    violations.AddRange(CheckFolder(path));
                }
                else if (path.EndsWith(".cs"))
                {
                    violations.AddRange(CheckFile(path));
                }
            }

            return violations;
        }

        /// <summary>
        /// 규칙 위반 여부 검사
        /// </summary>
        private static bool IsViolation(string line, ConventionRule rule, out string matchedContent)
        {
            matchedContent = string.Empty;

            var match = Regex.Match(line, rule.Pattern);
            if (!match.Success)
            {
                return false;
            }

            // 제외 패턴 확인
            if (rule.ExcludePatterns != null)
            {
                foreach (var excludePattern in rule.ExcludePatterns)
                {
                    if (Regex.IsMatch(match.Value, excludePattern))
                    {
                        return false;
                    }
                }
            }

            // 캡처 그룹이 있으면 해당 내용, 없으면 전체 매치
            matchedContent = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
            return true;
        }

        /// <summary>
        /// 단일 라인 주석 제거
        /// </summary>
        private static string RemoveComments(string line)
        {
            int commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
            {
                // 문자열 내부의 // 는 제외
                bool inString = false;
                for (int i = 0; i < commentIndex; i++)
                {
                    if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    {
                        inString = !inString;
                    }
                }
                if (!inString)
                {
                    return line.Substring(0, commentIndex);
                }
            }
            return line;
        }

        /// <summary>
        /// 문자열 리터럴 제거
        /// </summary>
        private static string RemoveStringLiterals(string line)
        {
            // 간단한 문자열 리터럴 제거 (복잡한 케이스는 일부 누락 가능)
            return Regex.Replace(line, @"""[^""\\]*(?:\\.[^""\\]*)*""", "\"\"");
        }
    }

    /// <summary>
    /// 컨벤션 규칙 정의
    /// </summary>
    internal class ConventionRule
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string MessageFormat { get; set; }
        public string[] ExcludePatterns { get; set; }
    }
}
