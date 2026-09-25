using System;
using UnityEditor;
using UnityEngine;

namespace CodeConvention.Editor
{
    public sealed class ConventionFixWindow : EditorWindow
    {
        private ConventionViolation violation;
        private ConventionRenamePlan plan;
        private string candidate;
        private string reason;
        private Vector2 scroll;

        internal static void Show(ConventionViolation target)
        {
            var window = GetWindow<ConventionFixWindow>(true, "컨벤션 수정 미리보기", true);
            window.minSize = new Vector2(620, 360);
            window.violation = target;
            window.candidate = ConventionSuggestion.Create(target).Name;
            window.BuildPreview();
        }

        private void BuildPreview()
        {
            plan = null;
            try
            {
                string source = ConventionFileEdit.ReadSource(violation.FilePath);
                ConventionLocalRename.TryCreate(source, violation, candidate, out plan, out reason);
            }
            catch (Exception e) { reason = e.Message; }
        }

        private void OnGUI()
        {
            if (violation == null)
            {
                EditorGUILayout.HelpBox("코드가 다시 로드되었습니다. 검사 창에서 경고를 다시 선택하세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField(violation.FilePath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField($"{violation.LineNumber}줄 · {violation.MatchedName}");
            EditorGUI.BeginChangeCheck();
            candidate = EditorGUILayout.TextField("변경할 이름", candidate);
            if (EditorGUI.EndChangeCheck()) { plan = null; reason = "변경된 이름으로 미리보기를 갱신하세요."; }
            if (GUILayout.Button("미리보기 갱신")) BuildPreview();
            if (plan == null)
                EditorGUILayout.HelpBox(reason ?? "미리보기를 생성하세요.", MessageType.Info);
            else
            {
                EditorGUILayout.LabelField($"선언과 지역 참조 {plan.ReplacementCount}곳 변경");
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.TextArea(plan.Preview, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.HelpBox("적용 전 열린 IDE 파일을 저장하세요. 원본은 Library/CodeConventionBackups에 보관됩니다. 적용 후 Unity 컴파일과 해당 동작을 확인하세요.", MessageType.None);
            using (new EditorGUI.DisabledScope(plan == null || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("코드에 적용"))
                {
                    var pending = plan;
                    string path = violation.FilePath;
                    plan = null;
                    EditorApplication.delayCall += () => Apply(path, pending);
                }
        }

        private void Apply(string path, ConventionRenamePlan pending)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                ConventionFileEdit.Apply(path, pending);
                CodeConventionWindow.RefreshEditedFile(path);
                Close();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                reason = e.Message;
                if (this != null) Repaint();
                EditorUtility.DisplayDialog("적용할 수 없습니다", e.Message, "확인");
            }
        }
    }
}
