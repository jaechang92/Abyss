using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeConvention.Editor
{
    /// <summary>
    /// 코드 컨벤션 검사 결과 표시 윈도우
    /// </summary>
    public class CodeConventionWindow : EditorWindow
    {
        private List<ConventionViolation> violations = new List<ConventionViolation>();
        private Vector2 scrollPosition;
        private string lastCheckedPath = string.Empty;

        // 필터 옵션
        private bool showErrors = true;
        private bool showWarnings = true;
        private string searchFilter = string.Empty;

        // 정렬 옵션
        private SortMode currentSortMode = SortMode.File;
        private bool sortAscending = true;

        // 스타일 캐시
        private GUIStyle errorStyle;
        private GUIStyle warningStyle;
        private GUIStyle headerStyle;
        private GUIStyle filePathStyle;
        private bool stylesInitialized = false;

        private enum SortMode
        {
            File,
            Line,
            Rule,
            Severity
        }

        [MenuItem("Tools/Code Convention/Convention Checker Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<CodeConventionWindow>("Code Convention");
            window.minSize = new Vector2(600, 400);
        }

        /// <summary>
        /// 검사 결과 표시
        /// </summary>
        public static void ShowResults(List<ConventionViolation> results, string checkedPath)
        {
            var window = GetWindow<CodeConventionWindow>("Code Convention");
            window.violations = results;
            window.lastCheckedPath = checkedPath;
            window.Repaint();
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            errorStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1f, 0.3f, 0.3f) },
                fontStyle = FontStyle.Bold
            };

            warningStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1f, 0.8f, 0.2f) }
            };

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(5, 5, 10, 10)
            };

            filePathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            DrawToolbar();
            DrawSummary();
            DrawViolationsList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 검사 버튼
            if (GUILayout.Button("Check Selection", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                CheckSelection();
            }

            if (GUILayout.Button("Check All Scripts", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                CheckAllScripts();
            }

            GUILayout.Space(20);

            // 필터 토글
            showErrors = GUILayout.Toggle(showErrors, "Errors", EditorStyles.toolbarButton, GUILayout.Width(60));
            showWarnings = GUILayout.Toggle(showWarnings, "Warnings", EditorStyles.toolbarButton, GUILayout.Width(70));

            GUILayout.Space(20);

            // 검색 필터
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // 정렬 옵션
            EditorGUILayout.LabelField("Sort:", GUILayout.Width(35));
            var newSortMode = (SortMode)EditorGUILayout.EnumPopup(currentSortMode, EditorStyles.toolbarPopup, GUILayout.Width(80));
            if (newSortMode != currentSortMode)
            {
                currentSortMode = newSortMode;
                SortViolations();
            }

            if (GUILayout.Button(sortAscending ? "▲" : "▼", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                sortAscending = !sortAscending;
                SortViolations();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int errorCount = violations.Count(v => v.Severity == ViolationSeverity.Error);
            int warningCount = violations.Count(v => v.Severity == ViolationSeverity.Warning);

            EditorGUILayout.LabelField($"검사 경로: {lastCheckedPath}", filePathStyle);
            GUILayout.FlexibleSpace();

            var prevColor = GUI.color;
            GUI.color = errorCount > 0 ? Color.red : Color.green;
            EditorGUILayout.LabelField($"Errors: {errorCount}", GUILayout.Width(80));

            GUI.color = warningCount > 0 ? Color.yellow : Color.green;
            EditorGUILayout.LabelField($"Warnings: {warningCount}", GUILayout.Width(100));
            GUI.color = prevColor;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawViolationsList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            var filteredViolations = GetFilteredViolations();

            if (filteredViolations.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    violations.Count == 0
                        ? "검사 결과가 없습니다. 'Check Selection' 또는 'Check All Scripts' 버튼을 클릭하세요."
                        : "필터 조건에 맞는 결과가 없습니다.",
                    MessageType.Info);
            }
            else
            {
                string currentFile = string.Empty;

                foreach (var violation in filteredViolations)
                {
                    // 파일별 그룹 헤더
                    if (violation.FilePath != currentFile)
                    {
                        currentFile = violation.FilePath;
                        EditorGUILayout.Space(5);
                        DrawFileHeader(currentFile);
                    }

                    DrawViolationItem(violation);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFileHeader(string filePath)
        {
            // 상대 경로로 표시
            string relativePath = filePath.Replace(Application.dataPath, "Assets");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("📄 " + relativePath, headerStyle);

            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawViolationItem(ConventionViolation violation)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // 심각도 아이콘
            var iconStyle = violation.Severity == ViolationSeverity.Error ? errorStyle : warningStyle;
            string icon = violation.Severity == ViolationSeverity.Error ? "❌" : "⚠️";

            EditorGUILayout.LabelField(icon, GUILayout.Width(25));

            // 라인 번호
            EditorGUILayout.LabelField($"Line {violation.LineNumber}", GUILayout.Width(60));

            // 규칙명
            EditorGUILayout.LabelField($"[{violation.RuleName}]", GUILayout.Width(180));

            // 메시지
            EditorGUILayout.LabelField(violation.Message, iconStyle);

            GUILayout.FlexibleSpace();

            // 이동 버튼
            if (GUILayout.Button("Go", GUILayout.Width(30)))
            {
                GoToViolation(violation);
            }

            EditorGUILayout.EndHorizontal();

            // 라인 내용 표시
            if (!string.IsNullOrEmpty(violation.LineContent))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(violation.LineContent, filePathStyle);
                EditorGUI.indentLevel--;
            }
        }

        private List<ConventionViolation> GetFilteredViolations()
        {
            var filtered = violations.AsEnumerable();

            // 심각도 필터
            if (!showErrors)
            {
                filtered = filtered.Where(v => v.Severity != ViolationSeverity.Error);
            }
            if (!showWarnings)
            {
                filtered = filtered.Where(v => v.Severity != ViolationSeverity.Warning);
            }

            // 검색 필터
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lowerFilter = searchFilter.ToLower();
                filtered = filtered.Where(v =>
                    v.FilePath.ToLower().Contains(lowerFilter) ||
                    v.Message.ToLower().Contains(lowerFilter) ||
                    v.RuleName.ToLower().Contains(lowerFilter));
            }

            return filtered.ToList();
        }

        private void SortViolations()
        {
            switch (currentSortMode)
            {
                case SortMode.File:
                    violations = sortAscending
                        ? violations.OrderBy(v => v.FilePath).ThenBy(v => v.LineNumber).ToList()
                        : violations.OrderByDescending(v => v.FilePath).ThenByDescending(v => v.LineNumber).ToList();
                    break;
                case SortMode.Line:
                    violations = sortAscending
                        ? violations.OrderBy(v => v.LineNumber).ToList()
                        : violations.OrderByDescending(v => v.LineNumber).ToList();
                    break;
                case SortMode.Rule:
                    violations = sortAscending
                        ? violations.OrderBy(v => v.RuleName).ToList()
                        : violations.OrderByDescending(v => v.RuleName).ToList();
                    break;
                case SortMode.Severity:
                    violations = sortAscending
                        ? violations.OrderBy(v => v.Severity).ToList()
                        : violations.OrderByDescending(v => v.Severity).ToList();
                    break;
            }
        }

        private void GoToViolation(ConventionViolation violation)
        {
            string relativePath = violation.FilePath.Replace(Application.dataPath, "Assets");
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);

            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, violation.LineNumber);
            }
        }

        private void CheckSelection()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Code Convention", "선택된 파일이 없습니다.", "OK");
                return;
            }

            violations = CodeConventionChecker.CheckAssets(selectedObjects);
            lastCheckedPath = "Selection";
            SortViolations();
            Repaint();
        }

        private void CheckAllScripts()
        {
            string scriptsPath = Application.dataPath;
            violations = CodeConventionChecker.CheckFolder(scriptsPath);
            lastCheckedPath = "All Scripts";
            SortViolations();
            Repaint();
        }
    }
}
