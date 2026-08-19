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
        // ══════════════ 플레이어 기하 SoT ══════════════
        //
        // 🔴 루트 스케일은 반드시 1이다. 예전에는 (1,2,1)이었는데, 그것은 4x4px 흰 사각형을
        //    몸통 모양으로 늘리려던 값이었다. 그런데 **콜라이더도 스케일을 탄다** —
        //    코드가 size(1,2)로 "1x2를 의도"했는데 실제 월드 몸통은 1x4가 됐다(2026-08-20 확인).
        //    그림이 2유닛인데 몸통이 4유닛이라 발이 콜라이더 바닥보다 1유닛 위에 떠 있었다.
        //
        // 🔑 자식 좌표는 전부 **콜라이더에서 파생**한다. 예전에는 스케일 조합에 맞춘 손계산
        //    좌표였고(GroundCheck (0,-0.5), AttackPoint (0,0.25), 이펙트 (1.4,0.7)),
        //    콜라이더를 조금만 건드려도 접지가 죽어 조작이 통째로 먹통이 됐다.
        private const float BODY_W = 1f;
        private const float BODY_H = 2f;                 // 그림 세로와 같다(PPU 128 x 256px)
        private static readonly Vector2 BodyOffset = new(0f, BODY_H * 0.5f);   // 발밑이 y=0

        private static float ColliderBottomY => BodyOffset.y - BODY_H * 0.5f;   // = 0

        // 🔴 접지 프로브는 콜라이더 바닥 **정확히 그 지점**이 아니라 살짝 아래를 본다.
        // 물리 접촉 간격(기본 0.01)과 부동소수 흔들림 때문에 경계에 두면 판정이 오락가락하고,
        // 그러면 Fall 상태에서 안 빠져나와 점프 횟수가 초기화되지 않는다(2026-08-20 증상).
        private const float GROUND_PROBE_DROP = 0.06f;
        private static float FootY => ColliderBottomY - GROUND_PROBE_DROP;
        private static float WaistY => BodyOffset.y;                  // 본체 중앙
        private static float FrontX => BODY_W * 0.9f;                 // 공격 지점 = 몸 앞

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
                    RewirePlayerPrefab(prefabPath, sprite);
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
                col.size = new Vector2(BODY_W, BODY_H);
                col.offset = BodyOffset;

                // 스케일 1 — 인스펙터 숫자가 곧 월드 크기다.
                root.transform.localScale = Vector3.one;

                // 🔴 시각과 물리를 분리한다(4-1 아트).
                // 루트 스케일 (1,2,1)은 흰 사각형 placeholder를 몸통 모양으로 늘리려던 것이다.
                // 실제 그림을 루트에 놓으면 세로로 2배 늘어나고, 스케일을 되돌리면
                // 콜라이더(size 1x2 -> 월드 1x4)까지 같이 줄어 물리가 바뀐다.
                // -> SpriteRenderer를 자식으로 내리고 그 자식이 (1,0.5,1)로 늘림을 상쇄한다.
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one;   // 루트가 1이므로 상쇄가 필요 없다
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.25f, 0.85f, 0.9f);
                visual.AddComponent<Abyss.Runtime.Form.FormVisualPresenter>();

                root.AddComponent<StateMachine>();
                var formController = root.AddComponent<FormController>();
                var playerInput = root.AddComponent<PlayerInput>();
                var player = root.AddComponent<PlayerCharacter>();
                var stateMachine = root.AddComponent<PlayerStateMachine>();

                var groundCheck = new GameObject("GroundCheck");
                groundCheck.transform.SetParent(root.transform, false);
                groundCheck.transform.localPosition = new Vector3(0f, FootY, 0f);

                var attackPoint = new GameObject("AttackPoint");
                attackPoint.transform.SetParent(root.transform, false);
                attackPoint.transform.localPosition = new Vector3(FrontX, WaistY, 0f);

                var effectGo = new GameObject("AttackEffect");
                effectGo.transform.SetParent(attackPoint.transform, false);
                effectGo.transform.localPosition = Vector3.zero;
                // 예전 (1.4, 0.7)은 부모 y=2를 상쇄해 월드에서 (1.4, 1.4)를 만들던 값이다.
                // 루트가 1이 된 지금은 그 의도를 그대로 쓴다.
                effectGo.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
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

            SetGroundLayerMask(so);

            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// 접지 판정 레이어 마스크를 <b>플랫폼을 만드는 쪽과 같은 출처</b>에서 가져온다.
        ///
        /// 🔴 예전에는 여기서 <c>LayerMask.GetMask("Default")</c>를 박아 넣었는데,
        /// 지면은 <see cref="EditorPlatformFactory.GroundLayerName"/>("Ground")에 생성된다 —
        /// 마스크가 지면을 아예 안 봐서 <b>접지가 항상 false</b>였다. 그러면 Fall 상태에서
        /// 안 빠져나오고 점프 횟수도 초기화되지 않는다(2026-08-20에 실제로 겪었다).
        ///
        /// 인스펙터에서 손으로 고쳐도 <b>프리팹을 다시 만들면 되돌아간다.</b>
        /// 그래서 값을 박지 않고 플랫폼 생성기와 같은 함수에서 파생시킨다.
        /// </summary>
        private static void SetGroundLayerMask(SerializedObject so)
        {
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp == null) return;

            int layer = EditorPlatformFactory.GetGroundLayer();
            layerProp.intValue = 1 << layer;
            Debug.Log($"[PrefabBuilder] groundLayer = {LayerMask.LayerToName(layer)}({layer}) " +
                      "— 플랫폼 생성기와 같은 출처.");
        }

        /// <summary>기존 프리팹의 groundLayer도 같은 값으로 보정한다.</summary>
        private static bool FixGroundLayer(GameObject root)
        {
            var player = root.GetComponent<PlayerCharacter>();
            if (player == null) return false;

            var so = new SerializedObject(player);
            var prop = so.FindProperty("groundLayer");
            if (prop == null) return false;

            int expected = 1 << EditorPlatformFactory.GetGroundLayer();
            if (prop.intValue == expected) return false;

            prop.intValue = expected;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);
            return true;
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

        /// <summary>
        /// 기존 프리팹 보정. 프리팹이 이미 있으면 전체 재생성 대신 이 경로를 탄다 —
        /// 강제 재생성은 씬에 배치된 인스턴스의 참조를 끊기 때문이다.
        ///
        /// 🔴 <b>슬롯만 재연결하면 구조 변경이 영원히 반영되지 않는다.</b> 2026-08-20에 실제로 그랬다:
        /// Visual 자식 분리(시각/물리)를 생성 경로에만 넣었더니, 프리팹이 이미 있어 이 경로로 빠지고
        /// 자식이 안 생겼다. 그래서 여기서 <b>있어야 할 것이 없으면 만든다</b>.
        ///
        /// 같은 자리에서 <b>빈 스프라이트도 채운다</b> — 프리팹의 SpriteRenderer가
        /// <c>m_Sprite: {fileID: 0}</c>으로 비어 있었다(커밋된 상태에서도). 비어 있으면 화면에
        /// 아무것도 안 그려지는데 <b>오류는 안 난다.</b>
        /// </summary>
        private static void RewirePlayerPrefab(string prefabPath, Sprite sprite)
        {
            // LoadPrefabContents / SaveAsPrefabAsset 경로는 Unity 6 일부 버전에서
            // HideFlags.DontSaveInEditor assertion 경고를 유발. 직접 에셋 컴포넌트 수정.
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[PrefabBuilder] 기존 프리팹 로드 실패: {prefabPath}");
                return;
            }

            bool dirty = false;

            var form = root.GetComponent<FormController>();
            if (form != null)
            {
                AssignFormSlots(form);
                EditorUtility.SetDirty(form);
                dirty = true;
            }

            dirty |= NormalizePlayerGeometry(root);
            dirty |= FixGroundLayer(root);
            dirty |= EnsurePlayerVisual(root, sprite);

            if (!dirty) return;
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PrefabBuilder] 기존 Player 프리팹 보정 완료: {prefabPath}");
        }

        /// <summary>
        /// 기존 프리팹의 기하를 SoT 값으로 정규화한다 — 루트 스케일 1, 콜라이더,
        /// 그리고 <b>콜라이더에서 파생되는 자식 좌표</b>.
        ///
        /// 🔴 이 보정이 없으면 옛 프리팹은 루트 스케일 (1,2,1) + 월드 몸통 1x4로 남는다.
        /// 콜라이더만 손으로 고치면 GroundCheck가 따라오지 않아 <b>접지가 죽고 조작이 먹통</b>이 된다
        /// (2026-08-20에 실제로 겪었다). 값을 한 곳에서 파생시키는 것이 그 재발을 막는 유일한 방법이다.
        /// </summary>
        private static bool NormalizePlayerGeometry(GameObject root)
        {
            bool changed = false;

            if (root.transform.localScale != Vector3.one)
            {
                root.transform.localScale = Vector3.one;
                changed = true;
            }

            var col = root.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                var size = new Vector2(BODY_W, BODY_H);
                if (col.size != size) { col.size = size; changed = true; }
                if (col.offset != BodyOffset) { col.offset = BodyOffset; changed = true; }
            }

            changed |= MoveChild(root, "GroundCheck", new Vector3(0f, FootY, 0f));
            changed |= MoveChild(root, "AttackPoint", new Vector3(FrontX, WaistY, 0f));

            var effect = root.transform.Find("AttackPoint/AttackEffect");
            var effectScale = new Vector3(1.4f, 1.4f, 1f);
            if (effect != null && effect.localScale != effectScale)
            {
                effect.localScale = effectScale;
                EditorUtility.SetDirty(effect.gameObject);
                changed = true;
            }

            if (changed) Debug.Log("[PrefabBuilder] 플레이어 기하 정규화 — 스케일 1, 몸통 " +
                                   $"{BODY_W}x{BODY_H}, 자식 좌표 콜라이더 파생.");
            return changed;
        }

        private static bool MoveChild(GameObject root, string name, Vector3 localPos)
        {
            var tf = root.transform.Find(name);
            if (tf == null || tf.localPosition == localPos) return false;
            tf.localPosition = localPos;
            EditorUtility.SetDirty(tf.gameObject);
            return true;
        }

        /// <summary>
        /// Visual 자식 + SpriteRenderer + <see cref="FormVisualPresenter"/>를 보장한다.
        /// 이미 갖춰져 있으면 아무것도 안 한다(몇 번 돌려도 안전).
        /// </summary>
        private static bool EnsurePlayerVisual(GameObject root, Sprite sprite)
        {
            bool changed = false;

            var visualTf = root.transform.Find("Visual");
            if (visualTf == null)
            {
                // 루트에 남아 있던 옛 SpriteRenderer는 지운다 — 남기면 두 번 그려진다.
                var legacy = root.GetComponent<SpriteRenderer>();
                if (legacy != null) Object.DestroyImmediate(legacy, true);

                var go = new GameObject("Visual");
                go.transform.SetParent(root.transform, false);
                visualTf = go.transform;
                changed = true;
            }

            // 루트가 스케일 1로 정규화됐으므로 상쇄가 필요 없다.
            if (visualTf.localScale != Vector3.one)
            {
                visualTf.localScale = Vector3.one;
                changed = true;
            }

            var sr = visualTf.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = visualTf.gameObject.AddComponent<SpriteRenderer>();
                sr.color = new Color(0.25f, 0.85f, 0.9f);
                changed = true;
            }
            if (sr.sprite == null && sprite != null)
            {
                sr.sprite = sprite;   // 비어 있으면 채운다(폴백 흰 사각형)
                changed = true;
            }

            if (visualTf.GetComponent<Abyss.Runtime.Form.FormVisualPresenter>() == null)
            {
                visualTf.gameObject.AddComponent<Abyss.Runtime.Form.FormVisualPresenter>();
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(visualTf.gameObject);
            return changed;
        }
    }
}
#endif
