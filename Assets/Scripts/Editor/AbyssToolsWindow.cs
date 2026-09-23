#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// Abyss 에디터 도구를 모아 놓은 창. <c>Tools ▸ Abyss ▸ Abyss Tools</c>.
    ///
    /// 🔴 <b>메뉴 바에 도구를 늘어놓는 것을 그만둔 이유</b>(2026-09-16 사용자 지적).
    /// 33개가 네 그룹에 흩어져 있었고, 메뉴 항목은 <b>이름 한 줄이 전부</b>라
    /// 「Prototype Content 와 Stage 1 Content 중 무엇을 먼저 돌려야 하는가」 같은 것이
    /// 화면 어디에도 없었다. 창은 <b>설명을 같이 보여줄 수 있다</b> — 그것이 옮긴 가장 큰 이유다.
    ///
    /// 🔑 <b>한 화면에 한 묶음만 둔다</b>(2026-09-16 2차 요청). 접기(foldout)로 다섯 묶음을
    /// 한 화면에 쌓으면, 펼친 상태에 따라 같은 도구가 매번 다른 높이에 나타나 <b>손이 위치를
    /// 못 외운다.</b> 탭은 묶음마다 스크롤 위치가 고정되므로 두 번째부터는 눈으로 안 찾아도 된다.
    ///
    /// 🔑 <b>줄마다 설명이 왼쪽, 버튼이 오른쪽이다.</b> 버튼을 왼쪽에 두면 이름·설명이
    /// 버튼 폭만큼 밀려 줄마다 시작 위치가 달라진다. 오른쪽 한 칸에 모아 두면
    /// <b>누를 곳이 한 세로줄</b>이라 훑는 눈과 누르는 손이 안 엇갈린다.
    ///
    /// 🔑 <b>도구 목록은 여기 없다.</b> <see cref="AbyssToolRegistry"/>가 갖는다 —
    /// 화면을 그리는 코드와 무엇이 있는지의 목록이 한 파일에 섞이면, 도구를 더할 때마다
    /// 렌더링 코드를 헤집게 된다.
    /// </summary>
    internal sealed class AbyssToolsWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Abyss/Abyss Tools";
        private const string TabPrefKey = "Abyss.ToolsWindow.Tab";

        private const float RunButtonWidth = 76f;
        private const float RowMinHeight = 42f;

        private static readonly Color DestructiveTint = new(1f, 0.72f, 0.66f);

        private Vector2 scroll;
        private string search = string.Empty;
        private int tab;
        private GUIStyle nameStyle;
        private GUIStyle summaryStyle;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<AbyssToolsWindow>(utility: false, title: "Abyss Tools", focus: true);
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            tab = EditorPrefs.GetInt(TabPrefKey, 0);
        }

        private void OnGUI()
        {
            EnsureStyles();

            var groups = AbyssToolRegistry.All;
            if (groups.Length == 0) return;

            DrawTabs(groups);
            DrawSearchBar();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (string.IsNullOrWhiteSpace(search))
            {
                DrawGroup(groups[Mathf.Clamp(tab, 0, groups.Length - 1)]);
            }
            else
            {
                DrawSearchResults(groups);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 스타일은 <c>OnGUI</c> 안에서 만든다. <c>OnEnable</c> 에서 <see cref="EditorStyles"/> 를 읽으면
        /// 아직 준비되지 않아 <c>null</c> 이 나오는 때가 있다(도메인 리로드 직후).
        /// </summary>
        private void EnsureStyles()
        {
            nameStyle ??= new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            summaryStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private void DrawTabs(AbyssToolRegistry.Group[] groups)
        {
            var labels = new string[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                labels[i] = $"{groups[i].Tab} ({groups[i].Tools.Length})";
            }

            int next = GUILayout.Toolbar(Mathf.Clamp(tab, 0, groups.Length - 1), labels,
                                         GUILayout.Height(24f));
            if (next == tab) return;

            tab = next;
            EditorPrefs.SetInt(TabPrefKey, tab);

            // 탭을 바꾸면 스크롤을 맨 위로 — 앞 탭에서 내려둔 만큼 내려간 채 열리면
            // 짧은 탭에서는 빈 화면이 보인다.
            scroll = Vector2.zero;
            GUI.FocusControl(null);
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("검색", GUILayout.Width(32f));
                search = EditorGUILayout.TextField(search);
                if (GUILayout.Button("지움", EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    search = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                EditorGUILayout.LabelField("검색 중에는 탭을 넘어 전부에서 찾는다.", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(2f);
        }

        private void DrawGroup(AbyssToolRegistry.Group group)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(group.Title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(group.Note))
            {
                EditorGUILayout.LabelField(group.Note, summaryStyle);
            }
            EditorGUILayout.Space(4f);

            for (int i = 0; i < group.Tools.Length; i++)
            {
                DrawTool(group.Tools[i]);
            }
        }

        /// <summary>
        /// 검색 결과. <b>탭을 무시하고 전부에서 찾는다</b> — 어느 탭에 있는지 알면 검색할 이유가 없다.
        /// 대신 줄마다 어느 묶음인지 밝혀, 다음번에는 탭으로 바로 갈 수 있게 한다.
        /// </summary>
        private void DrawSearchResults(AbyssToolRegistry.Group[] groups)
        {
            int shown = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                List<AbyssToolRegistry.AbyssTool> hits = AbyssToolRegistry.Filter(groups[i], search);
                if (hits.Count == 0) continue;

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField($"{groups[i].Title}  ({hits.Count})", EditorStyles.boldLabel);
                for (int j = 0; j < hits.Count; j++)
                {
                    DrawTool(hits[j]);
                }
                shown += hits.Count;
            }

            if (shown == 0)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox($"'{search}' 에 걸리는 도구가 없다.", MessageType.Info);
            }
        }

        /// <summary>도구 한 줄 — 왼쪽에 이름·설명, 오른쪽에 실행 버튼.</summary>
        private void DrawTool(AbyssToolRegistry.AbyssTool tool)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox,
                                                      GUILayout.MinHeight(RowMinHeight)))
            {
                // 왼쪽 — 이름과 설명. 남는 폭을 전부 가져가야 오른쪽 버튼 열이 한 줄로 맞는다.
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(tool.Name, nameStyle);
                    EditorGUILayout.LabelField(tool.Summary, summaryStyle);
                    GUILayout.FlexibleSpace();
                }

                // 오른쪽 — 실행. 되돌리기 어려운 것은 색과 말머리로 눈에 띄게 둔다.
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(RunButtonWidth)))
                {
                    GUILayout.FlexibleSpace();

                    Color previous = GUI.backgroundColor;
                    if (tool.IsDestructive) GUI.backgroundColor = DestructiveTint;

                    string label = tool.IsDestructive ? "⚠ 실행" : "실행";
                    if (GUILayout.Button(label, GUILayout.Height(24f), GUILayout.Width(RunButtonWidth)))
                    {
                        // 🔴 OnGUI 밖에서 실행한다. 씬을 열고 저장하는 도구(Build * Scene)가 레이아웃 그룹 한가운데서
                        //    돌면 창의 GUI 상태가 끊겨 스코프를 닫을 때 "EndLayoutGroup: BeginLayoutGroup must be
                        //    called first." 가 3건 뜬다. 도구 자체는 끝까지 돌지만 오류가 실제 실패를 가린다.
                        EditorApplication.delayCall += () => Run(tool);
                    }

                    GUI.backgroundColor = previous;
                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// 도구를 실행한다. 되돌리기 어려운 것은 <b>한 번 묻는다</b> —
        /// 버튼이 한 세로줄로 늘어선 화면에서는 오클릭이 메뉴보다 쉽다.
        ///
        /// ⚠️ 예외를 삼키지 않는다. 도구가 실패하면 콘솔에 그대로 나와야 원인을 찾는다 —
        /// 창이 조용히 닫히거나 아무 일도 안 일어난 것처럼 보이는 쪽이 훨씬 나쁘다.
        /// </summary>
        private static void Run(AbyssToolRegistry.AbyssTool tool)
        {
            if (tool.Run == null) return;

            if (tool.IsDestructive
                && !EditorUtility.DisplayDialog("Abyss Tools",
                    $"'{tool.Name}' 을(를) 실행한다.\n\n{tool.Summary}\n\n되돌리기 어렵다. 계속할까?",
                    "실행", "취소"))
            {
                return;
            }

            tool.Run();
        }
    }
}
#endif
