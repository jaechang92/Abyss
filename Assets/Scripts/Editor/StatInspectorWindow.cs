#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Player;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 선택한 플레이어·적의 스탯을 보는 창. <c>Abyss Tools ▸ 디버그 ▸ Stat Inspector</c>.
    ///
    /// 🔴 <b>만든 이유</b>(2026-09-16 사용자 요청). 무기 강화가 들어간 뒤 「강화된 스탯을 어디서 보나」에
    /// 답이 없었다 — 배율은 <c>PlayerCharacter.WeaponAttackMult</c> 한 곳에만 있고 HUD·치트 메뉴·인스펙터
    /// 어디에도 안 나왔다(직렬화 안 되는 프로퍼티라 인스펙터 Debug 모드로도 안 보인다).
    /// 제단 프롬프트가 「강화」라고 말해도 <b>수치가 실제로 올랐는지는 볼 곳이 없었다.</b>
    ///
    /// 🔑 <b>값을 계산하지 않고 읽기만 한다.</b> 공격력·이동속도의 식은 런타임이 프로퍼티로 내놓은 것
    /// (<c>LightAttackDamage</c> · <c>TotalAttackMult</c> · <c>EffectiveMoveSpeed</c> · <c>AttackDamage</c>)을
    /// 그대로 찍는다. 창이 따로 곱하면 층을 하나 더할 때 <b>창에 보이는 값만 옛 식으로 남는다</b> — 오류가 안 난다.
    ///
    /// 🔑 <b>배율은 층마다 나눠 보여준다</b>(버프 · 메타 · 무기). 합계만 보이면 「왜 이 값인가」를 못 읽는다.
    /// 1이 아닌 층은 색을 달리해 눈이 바로 간다.
    ///
    /// ⚠️ <b>값을 바꾸지 않는다.</b> 조회 창이다. 바꾸는 일은 치트 메뉴가 한다.
    /// </summary>
    internal sealed partial class StatInspectorWindow : EditorWindow
    {
        private const float LabelWidth = 132f;

        private static readonly Color ChangedColor = new(0.45f, 0.8f, 1f);
        private static readonly Color WarningColor = new(1f, 0.55f, 0.45f);

        // PlayerCharacter 또는 EnemyBase. 파괴되면 Unity 의 == null 이 참이 된다.
        private Component target;
        private bool isLocked;
        private Vector2 scroll;

        private GUIStyle valueStyle;
        private GUIStyle changedStyle;
        private GUIStyle hintStyle;

        internal static void Open()
        {
            var window = GetWindow<StatInspectorWindow>(utility: false, title: "Stat Inspector", focus: true);
            window.minSize = new Vector2(320f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            ResolveFromSelection();
        }

        private void OnSelectionChange()
        {
            if (!isLocked) ResolveFromSelection();
            Repaint();
        }

        /// <summary>
        /// 플레이 중에는 값이 계속 바뀐다. <c>OnInspectorUpdate</c> 는 초당 10회라
        /// 매 프레임 그리는 것보다 가볍고, 눈으로 읽기에는 충분하다.
        /// </summary>
        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void ResolveFromSelection()
        {
            target = ResolveTarget(Selection.activeGameObject);
        }

        /// <summary>
        /// 고른 오브젝트에서 본체를 찾는다. 🔑 <b>부모 쪽으로 올라가며 찾는다</b> —
        /// 씬 뷰에서 클릭하면 대개 그림을 담은 자식(Visual · WeaponSocket)이 잡히기 때문이다.
        /// </summary>
        private static Component ResolveTarget(GameObject go)
        {
            if (go == null) return null;

            var player = go.GetComponentInParent<PlayerCharacter>(true);
            if (player != null) return player;

            return go.GetComponentInParent<EnemyBase>(true);
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            if (target == null)
            {
                // 고정해 둔 적이 죽어 파괴된 경우 — 고정을 풀어야 다음 선택을 따라간다.
                if (isLocked) isLocked = false;
                DrawEmptyState();
                return;
            }

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "플레이 중이 아니다 — 직렬화된 기본값만 보인다.\n배율 · 버프 · 현재 HP · 무기 강화는 플레이 중에 나온다.",
                    MessageType.Info);
            }

            switch (target)
            {
                case PlayerCharacter player:
                    DrawPlayer(player);
                    break;
                case EnemyBase enemy:
                    DrawEnemy(enemy);
                    break;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        /// <summary>스타일은 <c>OnGUI</c> 안에서 만든다 — 도메인 리로드 직후 <c>OnEnable</c> 에서는 <see cref="EditorStyles"/> 가 비어 있을 수 있다.</summary>
        private void EnsureStyles()
        {
            valueStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true };
            changedStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                normal = { textColor = ChangedColor },
            };
            hintStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string title = target == null ? "선택 없음"
                    : $"{(target is PlayerCharacter ? "플레이어" : "적")} · {target.name}";
                GUILayout.Label(title, EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));

                // 🔑 고정 — 적을 보면서 씬을 클릭하면 선택이 바뀌어 창이 따라가 버린다.
                bool nextLocked = GUILayout.Toggle(isLocked, "고정", EditorStyles.toolbarButton, GUILayout.Width(44f));
                if (nextLocked != isLocked)
                {
                    isLocked = nextLocked;
                    if (!isLocked) ResolveFromSelection();
                }

                using (new EditorGUI.DisabledScope(target == null))
                {
                    if (GUILayout.Button("핑", EditorStyles.toolbarButton, GUILayout.Width(32f)))
                    {
                        EditorGUIUtility.PingObject(target.gameObject);
                    }
                }

                if (GUILayout.Button("플레이어", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    SelectPlayer();
                }
            }
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Hierarchy 나 씬 뷰에서 플레이어 또는 적을 고르면 스탯이 나온다.\n그림 자식(Visual 등)을 골라도 본체를 찾아간다.",
                MessageType.None);

            if (!EditorApplication.isPlaying) return;

            // 플레이 중에는 적이 스폰됐다 사라지므로 Hierarchy 에서 찾기가 번거롭다 — 여기서 바로 고르게 한다.
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("살아 있는 적", EditorStyles.boldLabel);

            var enemies = new List<EnemyBase>(FindObjectsByType<EnemyBase>());
            enemies.RemoveAll(e => e == null || e.IsDead);

            if (enemies.Count == 0)
            {
                EditorGUILayout.LabelField("없음", hintStyle);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var enemy in enemies)
            {
                string label = enemy.Data != null
                    ? $"{enemy.Data.displayName}  HP {enemy.CurrentHp}/{enemy.MaxHp}"
                    : enemy.name;
                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    Selection.activeGameObject = enemy.gameObject;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void SelectPlayer()
        {
            var player = FindAnyObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogWarning("[StatInspector] 열린 씬에 PlayerCharacter 가 없다.");
                return;
            }
            Selection.activeGameObject = player.gameObject;
        }

        // ── 줄 그리기 ──────────────────────────────────────────

        private static void Section(string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Row(string label, string value)
        {
            EditorGUILayout.LabelField(label, value, valueStyle);
        }

        private void Row(string label, int value) => Row(label, value.ToString());

        /// <summary>배율 한 줄. 1이 아니면 색을 달리한다 — 여러 층 중 어디가 움직였는지 눈이 바로 가게.</summary>
        private void MultRow(string label, float multiplier)
        {
            bool isChanged = !Mathf.Approximately(multiplier, 1f);
            EditorGUILayout.LabelField(label, Mult(multiplier), isChanged ? changedStyle : valueStyle);
        }

        /// <summary>어긋남 경고. 🔴 조회 창이 결함을 찾는 자리이기도 하다 — 조용히 넘기지 않는다.</summary>
        private static void Warn(string message)
        {
            Color previous = GUI.color;
            GUI.color = WarningColor;
            EditorGUILayout.HelpBox(message, MessageType.Warning);
            GUI.color = previous;
        }

        private static string Mult(float value) => $"×{value:0.00}";
    }
}
#endif
