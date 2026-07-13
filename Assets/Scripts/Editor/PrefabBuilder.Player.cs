#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using FSM.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.EditorTools
{
    public static partial class PrefabBuilder
    {
        // ==================== Player Prefab ====================
        private static bool BuildPlayerPrefab(Sprite sprite)
        {
            return BuildPlayerPrefab(sprite, forceRebuild: false);
        }

        private static bool BuildPlayerPrefab(Sprite sprite, bool forceRebuild)
        {
            string prefabPath = $"{AbyssPaths.PlayerPrefabs}/Player.prefab";

            if (File.Exists(prefabPath))
            {
                if (forceRebuild)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }
                else
                {
                    RewirePlayerPrefab(prefabPath);
                    return false;
                }
            }

            var root = new GameObject("Player");
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                rb.freezeRotation = true;

                var col = root.AddComponent<BoxCollider2D>();
                col.size = new Vector2(1f, 2f);
                col.offset = new Vector2(0f, 0.5f);

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.25f, 0.85f, 0.9f);
                root.transform.localScale = new Vector3(1f, 2f, 1f);

                root.AddComponent<StateMachine>();
                var formController = root.AddComponent<FormController>();
                var playerInput = root.AddComponent<PlayerInput>();
                var player = root.AddComponent<PlayerCharacter>();
                var stateMachine = root.AddComponent<PlayerStateMachine>();

                var groundCheck = new GameObject("GroundCheck");
                groundCheck.transform.SetParent(root.transform, false);
                groundCheck.transform.localPosition = new Vector3(0f, -0.5f, 0f);

                var attackPoint = new GameObject("AttackPoint");
                attackPoint.transform.SetParent(root.transform, false);
                // 부모 localScale.x=1, .y=2. X는 1유닛 앞, Y는 본체 중앙(0.25 → 실제 0.5).
                attackPoint.transform.localPosition = new Vector3(0.9f, 0.25f, 0f);

                var effectGo = new GameObject("AttackEffect");
                effectGo.transform.SetParent(attackPoint.transform, false);
                effectGo.transform.localPosition = Vector3.zero;
                effectGo.transform.localScale = new Vector3(1.4f, 0.7f, 1f);
                var effectSr = effectGo.AddComponent<SpriteRenderer>();
                effectSr.sprite = sprite;
                effectSr.color = new Color(1f, 0.95f, 0.3f, 0.85f);
                effectSr.sortingOrder = 5;
                effectSr.enabled = false;
                var attackEffect = effectGo.AddComponent<AttackEffect>();
                SetPrivateField(attackEffect, "sr", effectSr);

                ConfigurePlayerInput(playerInput);
                WirePlayerFields(player, rb, formController, stateMachine, groundCheck.transform, attackPoint.transform, attackEffect);
                AssignFormSlots(formController);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PrefabBuilder] 생성: {prefabPath} (AttackPoint/AttackEffect 포함, PlayerInput Actions = InputSystem_Actions, Behavior = SendMessages)");
                return prefab != null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePlayerInput(PlayerInput input)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AbyssPaths.InputActions);
            if (actions == null)
            {
                Debug.LogWarning($"[PrefabBuilder] {AbyssPaths.InputActions} 미발견 — PlayerInput.actions 미할당");
                return;
            }

            var so = new SerializedObject(input);
            var actionsProp = so.FindProperty("m_Actions");
            if (actionsProp != null) actionsProp.objectReferenceValue = actions;

            var defaultMapProp = so.FindProperty("m_DefaultActionMap");
            if (defaultMapProp != null) defaultMapProp.stringValue = "Player";

            var notifyProp = so.FindProperty("m_NotificationBehavior");
            if (notifyProp != null) notifyProp.enumValueIndex = (int)PlayerNotifications.SendMessages;

            so.ApplyModifiedProperties();
        }

        private static void WirePlayerFields(PlayerCharacter player, Rigidbody2D body, FormController form, PlayerStateMachine fsm, Transform groundCheck, Transform attackPoint, AttackEffect attackEffect)
        {
            var so = new SerializedObject(player);
            Set(so, "body", body);
            Set(so, "formController", form);
            Set(so, "stateMachine", fsm);
            Set(so, "groundCheck", groundCheck);
            Set(so, "attackPoint", attackPoint);
            Set(so, "attackEffect", attackEffect);

            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null) layerProp.intValue = LayerMask.GetMask("Default");

            so.ApplyModifiedProperties();
        }

        private static void AssignFormSlots(FormController form)
        {
            if (form == null) return;

            var dark = AssetDatabase.LoadAssetAtPath<FormData>($"{AbyssPaths.Forms}/DarkBlade.asset");
            var archer = AssetDatabase.LoadAssetAtPath<FormData>($"{AbyssPaths.Forms}/VoidArcher.asset");

            if (dark == null || archer == null)
            {
                Debug.LogWarning("[PrefabBuilder] FormData 에셋 미발견 — FormController.slots 미할당. ContentBuilder 먼저 실행 필요.");
                return;
            }

            var so = new SerializedObject(form);
            var slotsProp = so.FindProperty("slots");
            if (slotsProp != null && slotsProp.isArray)
            {
                slotsProp.arraySize = 2;
                slotsProp.GetArrayElementAtIndex(0).objectReferenceValue = dark;
                slotsProp.GetArrayElementAtIndex(1).objectReferenceValue = archer;
                so.ApplyModifiedProperties();
                Debug.Log($"[PrefabBuilder] FormController.slots 할당: [0]={dark.name} [1]={archer.name}");
            }
        }

        private static void RewirePlayerPrefab(string prefabPath)
        {
            // LoadPrefabContents / SaveAsPrefabAsset 경로는 Unity 6 일부 버전에서
            // HideFlags.DontSaveInEditor assertion 경고를 유발. 직접 에셋 컴포넌트 수정.
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[PrefabBuilder] 기존 프리팹 로드 실패: {prefabPath}");
                return;
            }

            var form = root.GetComponent<FormController>();
            if (form != null)
            {
                AssignFormSlots(form);
                EditorUtility.SetDirty(form);
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssets();
                Debug.Log($"[PrefabBuilder] 기존 Player 프리팹 FormController.slots 재연결: {prefabPath}");
            }
        }
    }
}
#endif
