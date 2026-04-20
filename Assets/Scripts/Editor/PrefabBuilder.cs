#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using FSM.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 프로토타입 프리팹 일괄 생성 툴.
    /// 적 5종 + 플레이어 1종 생성 → EnemyData.spawnPrefab 자동 연결.
    /// 사전 조건: ContentBuilder로 EnemyData/FormData 에셋이 생성되어 있을 것.
    /// </summary>
    public static class PrefabBuilder
    {
        private const string MenuPath = "Tools/Abyss/Generate Prototype Prefabs";
        private const string EnemyPrefabDir = "Assets/Prefabs/Enemies";
        private const string PlayerPrefabDir = "Assets/Prefabs/Player";
        private const string SpriteDir = "Assets/Art/Sprites";
        private const string WhiteSpritePath = SpriteDir + "/WhiteSquare.png";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder",
                "프로토 프리팹 생성:\n" +
                "  · 적 프리팹 5종 (EnemyData별)\n" +
                "  · 플레이어 프리팹 1종\n" +
                "  · WhiteSquare.png 임시 스프라이트\n\n" +
                "EnemyData.spawnPrefab 필드에 역참조를 자동 연결합니다.\n" +
                "이미 존재하는 프리팹은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(EnemyPrefabDir);
            EnsureDir(PlayerPrefabDir);
            EnsureDir(SpriteDir);

            var sprite = GetOrCreateWhiteSprite();
            int enemyCount = BuildAllEnemyPrefabs(sprite);
            bool playerBuilt = BuildPlayerPrefab(sprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PrefabBuilder] 완료 — 적 {enemyCount}종, 플레이어 {(playerBuilt ? 1 : 0)}종. Assets/Prefabs/ 확인.");
        }

        // ==================== Enemy Prefabs ====================
        private static int BuildAllEnemyPrefabs(Sprite sprite)
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            int built = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null) continue;
                if (BuildEnemyPrefab(data, sprite)) built += 1;
            }
            return built;
        }

        private static bool BuildEnemyPrefab(EnemyData data, Sprite sprite)
        {
            string prefabName = ToPascalCase(data.enemyId);
            string prefabPath = $"{EnemyPrefabDir}/{prefabName}.prefab";

            if (File.Exists(prefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                LinkSpawnPrefab(data, existing);
                Debug.Log($"[PrefabBuilder] 건너뜀 (존재): {prefabPath}");
                return false;
            }

            var root = new GameObject(prefabName);
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                rb.freezeRotation = true;

                var col = root.AddComponent<BoxCollider2D>();
                col.size = data.isBoss ? new Vector2(2f, 2f) : Vector2.one;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = GetEnemyColor(data);

                root.AddComponent<StateMachine>();

                EnemyBase enemy = data.isBoss ? root.AddComponent<BossEnemy>() : root.AddComponent<EnemyBase>();
                SetPrivateField(enemy, "data", data);

                if (data.isBoss) root.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                else if (data.isElite) root.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                LinkSpawnPrefab(data, prefab);
                Debug.Log($"[PrefabBuilder] 생성: {prefabPath}");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void LinkSpawnPrefab(EnemyData data, GameObject prefab)
        {
            if (data == null || prefab == null) return;
            var so = new SerializedObject(data);
            var prop = so.FindProperty("spawnPrefab");
            if (prop == null) return;
            prop.objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }

        private static Color GetEnemyColor(EnemyData data)
        {
            if (data.isBoss) return new Color(0.8f, 0.15f, 0.15f);
            if (data.isElite) return new Color(0.95f, 0.6f, 0.1f);
            if (data.isRanged) return new Color(0.3f, 0.55f, 0.9f);
            return new Color(0.55f, 0.55f, 0.58f);
        }

        // ==================== Player Prefab ====================
        private static bool BuildPlayerPrefab(Sprite sprite)
        {
            string prefabPath = $"{PlayerPrefabDir}/Player.prefab";
            if (File.Exists(prefabPath))
            {
                Debug.Log($"[PrefabBuilder] 건너뜀 (존재): {prefabPath}");
                return false;
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

                ConfigurePlayerInput(playerInput);
                WirePlayerFields(player, rb, formController, stateMachine, groundCheck.transform);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PrefabBuilder] 생성: {prefabPath} (PlayerInput Actions = InputSystem_Actions, Behavior = SendMessages)");
                return prefab != null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigurePlayerInput(PlayerInput input)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                Debug.LogWarning($"[PrefabBuilder] {InputActionsPath} 미발견 — PlayerInput.actions 미할당");
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

        private static void WirePlayerFields(PlayerCharacter player, Rigidbody2D body, FormController form, PlayerStateMachine fsm, Transform groundCheck)
        {
            var so = new SerializedObject(player);
            Set(so, "body", body);
            Set(so, "formController", form);
            Set(so, "stateMachine", fsm);
            Set(so, "groundCheck", groundCheck);

            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null) layerProp.intValue = LayerMask.GetMask("Default");

            so.ApplyModifiedProperties();
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        // ==================== White Sprite ====================
        private static Sprite GetOrCreateWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (existing != null) return existing;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(WhiteSpritePath, png);
            AssetDatabase.ImportAsset(WhiteSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            Debug.Log($"[PrefabBuilder] 생성: {WhiteSpritePath}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        }

        // ==================== Helpers ====================
        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static string ToPascalCase(string snake)
        {
            if (string.IsNullOrEmpty(snake)) return string.Empty;
            var parts = snake.Split('_');
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                sb.Append(char.ToUpper(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
        }
    }
}
#endif
