using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeConvention.Editor
{
    /// <summary>
    /// Project 윈도우 우클릭 컨텍스트 메뉴 연동
    /// </summary>
    public static class CodeConventionContextMenu
    {
        /// <summary>
        /// 선택된 파일/폴더의 코드 컨벤션 검사
        /// Assets 메뉴 및 Project 윈도우 우클릭 메뉴에서 접근 가능
        /// </summary>
        [MenuItem("Assets/Check Code Convention", false, 1000)]
        private static void CheckCodeConvention()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Code Convention", "선택된 파일이 없습니다.", "OK");
                return;
            }

            // 검사 실행
            var violations = CodeConventionChecker.CheckAssets(selectedObjects);

            // 검사 경로 문자열 생성
            string checkedPath = GetCheckedPathString(selectedObjects);

            // 결과 표시
            if (violations.Count == 0)
            {
                EditorUtility.DisplayDialog("Code Convention",
                    $"✅ 컨벤션 위반 없음!\n\n검사 대상: {checkedPath}",
                    "OK");
            }
            else
            {
                // 윈도우에 결과 표시
                CodeConventionWindow.ShowResults(violations, checkedPath);

                // 요약 다이얼로그
                int errorCount = violations.Count(v => v.Severity == ViolationSeverity.Error);
                int warningCount = violations.Count(v => v.Severity == ViolationSeverity.Warning);

                EditorUtility.DisplayDialog("Code Convention",
                    $"컨벤션 위반 발견!\n\n" +
                    $"❌ Errors: {errorCount}\n" +
                    $"⚠️ Warnings: {warningCount}\n\n" +
                    $"Code Convention 윈도우에서 상세 내용을 확인하세요.",
                    "OK");
            }
        }

        /// <summary>
        /// 메뉴 활성화 조건 - .cs 파일 또는 폴더가 선택되었을 때만 활성화
        /// </summary>
        [MenuItem("Assets/Check Code Convention", true)]
        private static bool CheckCodeConventionValidation()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0) return false;

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);

                // .cs 파일이거나 폴더인 경우 활성화
                if (path.EndsWith(".cs") || AssetDatabase.IsValidFolder(path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 선택된 폴더만 검사 (하위 포함)
        /// </summary>
        [MenuItem("Assets/Check Code Convention (Folder Only)", false, 1001)]
        private static void CheckCodeConventionFolderOnly()
        {
            var selectedObjects = Selection.objects;
            var folderViolations = new List<ConventionViolation>();
            var checkedFolders = new List<string>();

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    string fullPath = System.IO.Path.GetFullPath(path);
                    folderViolations.AddRange(CodeConventionChecker.CheckFolder(fullPath));
                    checkedFolders.Add(path);
                }
            }

            if (checkedFolders.Count == 0)
            {
                EditorUtility.DisplayDialog("Code Convention", "선택된 폴더가 없습니다.", "OK");
                return;
            }

            string checkedPath = string.Join(", ", checkedFolders);

            if (folderViolations.Count == 0)
            {
                EditorUtility.DisplayDialog("Code Convention",
                    $"✅ 컨벤션 위반 없음!\n\n검사 폴더: {checkedPath}",
                    "OK");
            }
            else
            {
                CodeConventionWindow.ShowResults(folderViolations, checkedPath);

                int errorCount = folderViolations.Count(v => v.Severity == ViolationSeverity.Error);
                int warningCount = folderViolations.Count(v => v.Severity == ViolationSeverity.Warning);

                EditorUtility.DisplayDialog("Code Convention",
                    $"컨벤션 위반 발견!\n\n" +
                    $"❌ Errors: {errorCount}\n" +
                    $"⚠️ Warnings: {warningCount}\n\n" +
                    $"Code Convention 윈도우에서 상세 내용을 확인하세요.",
                    "OK");
            }
        }

        /// <summary>
        /// 폴더 메뉴 활성화 조건
        /// </summary>
        [MenuItem("Assets/Check Code Convention (Folder Only)", true)]
        private static bool CheckCodeConventionFolderOnlyValidation()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0) return false;

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 빠른 검사 - 결과를 콘솔에만 출력
        /// </summary>
        [MenuItem("Assets/Quick Check Convention (Console)", false, 1002)]
        private static void QuickCheckConvention()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("[CodeConvention] 선택된 파일이 없습니다.");
                return;
            }

            var violations = CodeConventionChecker.CheckAssets(selectedObjects);
            string checkedPath = GetCheckedPathString(selectedObjects);

            if (violations.Count == 0)
            {
                Debug.Log($"[CodeConvention] ✅ 컨벤션 위반 없음 - {checkedPath}");
            }
            else
            {
                int errorCount = violations.Count(v => v.Severity == ViolationSeverity.Error);
                int warningCount = violations.Count(v => v.Severity == ViolationSeverity.Warning);

                Debug.Log($"[CodeConvention] 검사 완료 - {checkedPath}");
                Debug.Log($"[CodeConvention] ❌ Errors: {errorCount}, ⚠️ Warnings: {warningCount}");

                foreach (var violation in violations)
                {
                    string relativePath = violation.FilePath.Replace(Application.dataPath, "Assets");
                    string logMessage = $"[{violation.RuleName}] {relativePath}:{violation.LineNumber} - {violation.Message}";

                    if (violation.Severity == ViolationSeverity.Error)
                    {
                        Debug.LogError(logMessage);
                    }
                    else
                    {
                        Debug.LogWarning(logMessage);
                    }
                }
            }
        }

        [MenuItem("Assets/Quick Check Convention (Console)", true)]
        private static bool QuickCheckConventionValidation()
        {
            return CheckCodeConventionValidation();
        }

        /// <summary>
        /// 선택된 객체들의 경로 문자열 생성
        /// </summary>
        private static string GetCheckedPathString(Object[] selectedObjects)
        {
            var paths = new List<string>();

            foreach (var obj in selectedObjects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                paths.Add(path);
            }

            if (paths.Count == 1)
            {
                return paths[0];
            }
            else if (paths.Count <= 3)
            {
                return string.Join(", ", paths);
            }
            else
            {
                return $"{paths[0]} 외 {paths.Count - 1}개";
            }
        }
    }
}
