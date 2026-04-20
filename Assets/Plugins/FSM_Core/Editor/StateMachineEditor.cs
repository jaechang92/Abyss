using UnityEngine;
using UnityEditor;
using FSM.Core;

namespace FSM.Core.Editor
{
    [CustomEditor(typeof(StateMachine))]
    public class StateMachineEditor : UnityEditor.Editor
    {
        private StateMachine stateMachine;

        private void OnEnable()
        {
            stateMachine = (StateMachine)target;
        }

        public override void OnInspectorGUI()
        {
            // 기본 Inspector 그리기
            DrawDefaultInspector();

            // 구분선 추가
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 실시간 상태 정보 표시
            EditorGUILayout.LabelField("실시간 상태 정보", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                // 실행 상태 표시
                var runningStyle = new GUIStyle(EditorStyles.textField);
                runningStyle.normal.textColor = stateMachine.IsRunning ? Color.green : Color.red;

                EditorGUILayout.LabelField("실행 상태", stateMachine.IsRunning ? "실행 중" : "중지됨");

                // 현재 상태 표시 (강조)
                var currentStateStyle = new GUIStyle(EditorStyles.textField);
                currentStateStyle.normal.textColor = Color.cyan;
                currentStateStyle.fontStyle = FontStyle.Bold;

                EditorGUILayout.LabelField("현재 상태", string.IsNullOrEmpty(stateMachine.CurrentStateId) ? "None" : stateMachine.CurrentStateId);

                // 이전 상태 표시
                var previousStateStyle = new GUIStyle(EditorStyles.textField);
                previousStateStyle.normal.textColor = Color.yellow;

                EditorGUILayout.LabelField("이전 상태", string.IsNullOrEmpty(stateMachine.PreviousStateId) ? "None" : stateMachine.PreviousStateId);

                // 등록된 상태 개수
                EditorGUILayout.LabelField("등록된 상태 수", stateMachine.States.Count.ToString());

                // 등록된 전환 개수
                EditorGUILayout.LabelField("등록된 전환 수", stateMachine.Transitions.Count.ToString());
            }

            // Application이 실행 중일 때만 상태 목록 표시
            if (Application.isPlaying && stateMachine.States.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("등록된 상태 목록", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(true))
                {
                    foreach (var kvp in stateMachine.States)
                    {
                        var style = new GUIStyle(EditorStyles.textField);

                        // 현재 상태는 녹색으로 강조
                        if (kvp.Key == stateMachine.CurrentStateId)
                        {
                            style.normal.textColor = Color.green;
                            style.fontStyle = FontStyle.Bold;
                        }
                        // 이전 상태는 노란색으로 표시
                        else if (kvp.Key == stateMachine.PreviousStateId)
                        {
                            style.normal.textColor = Color.yellow;
                        }

                        EditorGUILayout.LabelField($"  • {kvp.Key}", style);
                    }
                }
            }

            // 런타임 제어 버튼들
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("런타임 제어", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("FSM 시작"))
                    {
                        if (!stateMachine.IsRunning)
                        {
                            stateMachine.StartStateMachine();
                        }
                    }

                    if (GUILayout.Button("FSM 중지"))
                    {
                        if (stateMachine.IsRunning)
                        {
                            stateMachine.Stop();
                        }
                    }
                }
            }

            // Inspector가 실시간으로 업데이트되도록 함
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }
}