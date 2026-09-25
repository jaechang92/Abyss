using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeConvention.Editor
{
    public sealed class ConventionBatchWindow : EditorWindow
    {
        private List<ConventionBatchFile> files;
        private Vector2 scroll;
        private bool isShowSkipped;
        private bool isPending;

        internal static void Show(IEnumerable<ConventionViolation> violations)
        {
            var window = GetWindow<ConventionBatchWindow>(true, "컨벤션 일괄 수정", true);
            window.minSize = new Vector2(740, 450);
            var snapshot = violations.ToList();
            window.files = new List<ConventionBatchFile>();
            window.isPending = false;
            foreach (var group in snapshot.GroupBy(v => Path.GetFullPath(v.FilePath), StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    window.files.Add(ConventionBatchPlanner.Build(group.Key, ConventionFileEdit.ReadSource(group.Key), group));
                }
                catch (Exception e)
                {
                    var skipped = new ConventionBatchFile { Path = group.Key };
                    skipped.Skipped.Add(e.Message);
                    window.files.Add(skipped);
                }
            }
        }

        private void OnGUI()
        {
            if (files == null)
            {
                EditorGUILayout.HelpBox("코드가 다시 로드되었습니다. 검사 창에서 일괄 미리보기를 다시 여세요.", MessageType.Info);
                return;
            }
            var selected = files.Where(f => f.Plan != null && f.IsSelected).ToList();
            EditorGUILayout.LabelField($"선택 {selected.Count}개 파일 · {selected.Sum(f => f.AcceptedCount)}개 경고 · 제외 {files.Sum(f => f.Skipped.Count)}건", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("현재 검색·필터에 보이는 경고가 대상입니다. IDE에서 모두 저장한 뒤 변경을 확인하세요. 여러 파일을 함께 적용하고 마지막에 한 번 갱신합니다.", MessageType.Info);
            isShowSkipped = EditorGUILayout.Toggle("제외 사유 보기", isShowSkipped);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var file in files)
            {
                if (file.Plan != null)
                {
                    file.IsSelected = EditorGUILayout.ToggleLeft($"{file.Path} ({file.AcceptedCount}건)", file.IsSelected, EditorStyles.boldLabel);
                    EditorGUILayout.TextArea(file.Plan.Preview);
                }
                if (isShowSkipped && file.Skipped.Count > 0)
                    EditorGUILayout.HelpBox(file.Path + "\n" + string.Join("\n", file.Skipped), MessageType.None);
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(selected.Count == 0 || isPending || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("선택 파일 일괄 적용"))
                {
                    var pending = selected.ToDictionary(f => f.Path, f => f.Plan, StringComparer.OrdinalIgnoreCase);
                    isPending = true;
                    EditorApplication.delayCall += () => Apply(pending);
                }
        }

        private void Apply(Dictionary<string, ConventionRenamePlan> plans)
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                isPending = false;
                return;
            }
            try
            {
                AssetDatabase.StartAssetEditing();
                try { ConventionFileEdit.ApplyBatch(plans); }
                finally { AssetDatabase.StopAssetEditing(); }
                foreach (string path in plans.Keys) CodeConventionWindow.RefreshEditedFile(path);
                Close();
            }
            catch (Exception e)
            {
                isPending = false;
                EditorUtility.DisplayDialog("일괄 적용 중단", e.Message, "확인");
            }
            finally { AssetDatabase.Refresh(); }
        }
    }
}
