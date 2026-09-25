using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeConvention.Editor
{
    public partial class CodeConventionWindow
    {
        public static bool TryGetAssetPath(string path, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
                string fullPath = Path.GetFullPath(path.Replace('\\', '/')).Replace('\\', '/');
                // 경로 구분자와 대소문자 차이를 정규화하고 AssetsBackup 같은 접두어 충돌은 제외한다.
                if (!fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase)) return false;
                assetPath = "Assets/" + fullPath.Substring(assetsRoot.Length + 1);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += RestoreResults;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RestoreResults;
        }

        private void RememberFiles()
        {
            checkedFiles = violations.Select(v => Path.GetFullPath(v.FilePath)).Distinct().ToList();
        }

        private void RestoreResults()
        {
            if (this == null || checkedFiles == null || checkedFiles.Count == 0) return;
            violations = checkedFiles.Where(File.Exists).SelectMany(CodeConventionChecker.CheckFile).ToList();
            SortViolations();
            Repaint();
        }

        internal static void RefreshEditedFile(string path)
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<CodeConventionWindow>())
            {
                string full = Path.GetFullPath(path);
                window.violations.RemoveAll(v => string.Equals(Path.GetFullPath(v.FilePath), full, StringComparison.OrdinalIgnoreCase));
                window.violations.AddRange(CodeConventionChecker.CheckFile(full));
                window.checkedFiles.Add(full);
                window.checkedFiles = window.checkedFiles.Distinct().ToList();
                window.SortViolations();
                window.Repaint();
            }
        }

        private void DrawFixToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("경고를 모아서 미리보기 → 일괄 적용", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("일괄 수정 미리보기", GUILayout.Width(150)))
                {
                    var snapshot = GetFilteredViolations().Where(v => v.Severity == ViolationSeverity.Warning).ToList();
                    EditorApplication.delayCall += () => ConventionBatchWindow.Show(snapshot);
                }
            using (new EditorGUI.DisabledScope(!ConventionFileEdit.CanRestore || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("최근 적용 묶음 복원", GUILayout.Width(150)))
                    EditorApplication.delayCall += RestoreLastEdit;
            EditorGUILayout.EndHorizontal();
        }

        private static void RestoreLastEdit()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                string[] paths;
                AssetDatabase.StartAssetEditing();
                try { paths = ConventionFileEdit.RestoreBatch(); }
                finally { AssetDatabase.StopAssetEditing(); }
                foreach (string path in paths) RefreshEditedFile(path);
            }
            catch (Exception e) { EditorUtility.DisplayDialog("복원할 수 없습니다", e.Message, "확인"); }
            finally { AssetDatabase.Refresh(); }
        }

        private void DrawSuggestion(ConventionViolation violation)
        {
            if (violation.Severity != ViolationSeverity.Warning) return;
            var suggestion = ConventionSuggestion.Create(violation);
            string text = string.IsNullOrEmpty(suggestion.Name) ? suggestion.Advice :
                $"추천: {violation.MatchedName} → {suggestion.Name}\n{suggestion.Advice}";
            EditorGUILayout.HelpBox(text, MessageType.None);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(suggestion.Name) && GUILayout.Button("추천 이름 복사", GUILayout.Width(110)))
                EditorGUIUtility.systemCopyBuffer = suggestion.Name;
            if (suggestion.CanPlanLocalRename)
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("수정 미리보기", GUILayout.Width(110)))
                        ConventionFixWindow.Show(violation);
            }
            else if (GUILayout.Button("코드에서 확인", GUILayout.Width(110))) GoToViolation(violation);
            EditorGUILayout.EndHorizontal();
        }
    }
}
