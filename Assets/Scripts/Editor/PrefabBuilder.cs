#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Combat;
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
        private const string RebuildEnemiesMenuPath = "Tools/Abyss/Rebuild Enemy Prefabs (Force)";
        private const string RebuildPlayerMenuPath = "Tools/Abyss/Rebuild Player Prefab (Force)";
        private const string EnemyPrefabDir = "Assets/Prefabs/Enemies";
        private const string PlayerPrefabDir = "Assets/Prefabs/Player";
        private const string CombatPrefabDir = "Assets/Prefabs/Combat";
        private const string SpriteDir = "Assets/Art/Sprites";
        private const string WhiteSpritePath = SpriteDir + "/WhiteSquare.png";
        private const string EnemySpriteDir = "Assets/Art/Sprites/Enemies";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string SetupImportMenuPath = "Tools/Abyss/Setup Enemy Sprite Import Settings";

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

            RunBuild(forceRebuildEnemies: false, forceRebuildPlayer: false);
        }

        [MenuItem(RebuildEnemiesMenuPath)]
        public static void RebuildEnemies()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder — Force Rebuild",
                "기존 적 프리팹을 전부 삭제하고 재생성합니다.\n" +
                "(EnemyVisuals/Nametag 자식 등 최신 구조가 필요할 때 사용)\n\n" +
                "EnemyData.spawnPrefab 역참조는 자동으로 재연결됩니다.\n" +
                "플레이어 프리팹은 영향을 받지 않습니다.",
                "재생성", "취소");
            if (!proceed) return;

            RunBuild(forceRebuildEnemies: true, forceRebuildPlayer: false);
        }

        [MenuItem(RebuildPlayerMenuPath)]
        public static void RebuildPlayer()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder — Force Rebuild Player",
                "기존 Player 프리팹을 삭제하고 재생성합니다.\n" +
                "(AttackPoint/AttackEffect 자식 등 최신 구조가 필요할 때 사용)\n\n" +
                "씬에 배치된 Player 인스턴스는 변경되지 않으므로,\n" +
                "필요 시 씬에서 Player 제거 후 프리팹 드롭하여 교체하세요.",
                "재생성", "취소");
            if (!proceed) return;

            RunBuild(forceRebuildEnemies: false, forceRebuildPlayer: true);
        }

        private static void RunBuild(bool forceRebuildEnemies, bool forceRebuildPlayer)
        {
            EnsureDir(EnemyPrefabDir);
            EnsureDir(PlayerPrefabDir);
            EnsureDir(CombatPrefabDir);
            EnsureDir(SpriteDir);

            // 적 스프라이트 임포트 설정 자동 적용 (PPU·FilterMode 일괄)
            SetupEnemySpriteImportSettings();

            var sprite = GetOrCreateWhiteSprite();
            int enemyCount = BuildAllEnemyPrefabs(sprite, forceRebuildEnemies);
            bool playerBuilt = BuildPlayerPrefab(sprite, forceRebuildPlayer);

            // 발사체 프리팹 생성 후 isRanged 적 EnemyData에 자동 연결.
            var projectile = BuildProjectilePrefab(sprite, forceRebuildEnemies);
            int rangedLinked = LinkProjectileToRangedEnemies(projectile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PrefabBuilder] 완료 — 적 {enemyCount}종{(forceRebuildEnemies ? " (force)" : "")}, " +
                      $"플레이어 {(playerBuilt ? 1 : 0)}종{(forceRebuildPlayer ? " (force)" : "")}, " +
                      $"발사체 1종(원거리 적 {rangedLinked}종 연결).");
        }

        // ==================== Enemy Prefabs ====================
        private static int BuildAllEnemyPrefabs(Sprite sprite, bool forceRebuild)
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            int built = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null) continue;
                if (BuildEnemyPrefab(data, sprite, forceRebuild)) built += 1;
            }
            return built;
        }

        private static bool BuildEnemyPrefab(EnemyData data, Sprite sprite, bool forceRebuild)
        {
            string prefabName = ToPascalCase(data.enemyId);
            string prefabPath = $"{EnemyPrefabDir}/{prefabName}.prefab";

            if (File.Exists(prefabPath))
            {
                if (forceRebuild)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }
                else
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    LinkSpawnPrefab(data, existing);
                    Debug.Log($"[PrefabBuilder] 건너뜀 (존재): {prefabPath}");
                    return false;
                }
            }

            var root = new GameObject(prefabName);
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f;
                rb.freezeRotation = true;

                var col = root.AddComponent<BoxCollider2D>();
                col.size = data.isBoss ? new Vector2(2f, 2f) : Vector2.one;

                // 도트 스프라이트 우선 로드. 없으면 WhiteSquare 폴백 + 틴팅 유지.
                var enemySprite = GetEnemySpriteByEnemyId(data.enemyId, sprite);
                bool hasDotSprite = enemySprite != sprite;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = enemySprite;
                sr.color = hasDotSprite ? Color.white : GetEnemyColor(data);
                sr.sortingOrder = 0;

                root.AddComponent<EnemyVisuals>();
                root.AddComponent<StateMachine>();

                EnemyBase enemy = data.isBoss ? root.AddComponent<BossEnemy>() : root.AddComponent<EnemyBase>();
                SetPrivateField(enemy, "data", data);

                if (data.isBoss) root.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                else if (data.isElite) root.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

                AttachNametag(root.transform, data);

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

        // ==================== Projectile Prefab ====================
        /// <summary>
        /// 원거리 적 공용 발사체 프리팹 생성. Kinematic RB + Trigger CircleCollider2D + Projectile.
        /// 기존 존재 시 forceRebuild 아니면 로드만(컴포넌트 반환).
        /// </summary>
        private static Projectile BuildProjectilePrefab(Sprite sprite, bool forceRebuild)
        {
            string path = $"{CombatPrefabDir}/EnemyProjectile.prefab";

            if (File.Exists(path))
            {
                if (forceRebuild)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                else
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Debug.Log($"[PrefabBuilder] 건너뜀 (존재): {path}");
                    return existing != null ? existing.GetComponent<Projectile>() : null;
                }
            }

            var root = new GameObject("EnemyProjectile");
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                var col = root.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.4f;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 0.6f, 0.1f); // 주황 — 화살/탄
                sr.sortingOrder = 3;

                root.AddComponent<Projectile>();
                root.transform.localScale = new Vector3(0.45f, 0.18f, 1f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabBuilder] 생성: {path}");
                return prefab != null ? prefab.GetComponent<Projectile>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// isRanged인 모든 EnemyData.projectilePrefab에 발사체를 연결. 연결한 종 수 반환.
        /// </summary>
        private static int LinkProjectileToRangedEnemies(Projectile projectile)
        {
            if (projectile == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            int linked = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null || !data.isRanged) continue;

                var so = new SerializedObject(data);
                var prop = so.FindProperty("projectilePrefab");
                if (prop == null) continue;
                prop.objectReferenceValue = projectile;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                linked++;
            }
            return linked;
        }

        /// <summary>
        /// enemyId 기반으로 Assets/Art/Sprites/Enemies/{enemyId}.png 스프라이트를 로드.
        /// 파일이 없으면 fallback(WhiteSquare) 반환.
        /// </summary>
        private static Sprite GetEnemySpriteByEnemyId(string enemyId, Sprite fallback)
        {
            if (string.IsNullOrEmpty(enemyId)) return fallback;
            string path = $"{EnemySpriteDir}/{enemyId}.png";
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return loaded != null ? loaded : fallback;
        }

        /// <summary>
        /// Assets/Art/Sprites/Enemies/*.png 전체를 순회하여 픽셀아트 임포트 설정 일괄 적용.
        /// TextureType=Sprite, FilterMode=Point, PPU=16, Mipmap 끔, 압축 없음.
        /// </summary>
        [MenuItem(SetupImportMenuPath)]
        public static void SetupEnemySpriteImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { EnemySpriteDir });
            int count = 0;
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType           = TextureImporterType.Sprite;
                importer.spriteImportMode      = SpriteImportMode.Single;
                importer.filterMode            = FilterMode.Point;
                importer.textureCompression    = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit   = 16;
                importer.mipmapEnabled         = false;
                importer.SaveAndReimport();
                count++;
            }

            if (count > 0)
                Debug.Log($"[PrefabBuilder] 적 스프라이트 임포트 설정 적용 완료: {count}개 (PPU=16, FilterMode=Point, 압축 없음)");
        }

        private static Color GetEnemyColor(EnemyData data)
        {
            if (data.isBoss) return new Color(0.8f, 0.15f, 0.15f);
            if (data.isElite) return new Color(0.95f, 0.6f, 0.1f);
            if (data.isRanged) return new Color(0.3f, 0.55f, 0.9f);
            return new Color(0.55f, 0.55f, 0.58f);
        }

        /// <summary>
        /// 적 머리 위에 식별용 TextMesh 부착. displayName 비어있으면 enemyId 사용.
        /// 부모 transform의 scale을 역보정해 텍스트가 일정 크기로 보이도록 localScale 조정.
        /// </summary>
        private static void AttachNametag(Transform parent, EnemyData data)
        {
            string label = string.IsNullOrEmpty(data.displayName) ? data.enemyId : data.displayName;
            if (string.IsNullOrEmpty(label)) return;

            var go = new GameObject("Nametag");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            Vector3 parentScale = parent.localScale;
            go.transform.localScale = new Vector3(
                parentScale.x > 0.01f ? 1f / parentScale.x : 1f,
                parentScale.y > 0.01f ? 1f / parentScale.y : 1f,
                1f);

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = label;
            textMesh.characterSize = 0.12f;
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 10;
            }
        }

        // ==================== Player Prefab ====================
        private static bool BuildPlayerPrefab(Sprite sprite)
        {
            return BuildPlayerPrefab(sprite, forceRebuild: false);
        }

        private static bool BuildPlayerPrefab(Sprite sprite, bool forceRebuild)
        {
            string prefabPath = $"{PlayerPrefabDir}/Player.prefab";

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

            var dark = AssetDatabase.LoadAssetAtPath<FormData>("Assets/Data/Forms/DarkBlade.asset");
            var archer = AssetDatabase.LoadAssetAtPath<FormData>("Assets/Data/Forms/VoidArcher.asset");

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
