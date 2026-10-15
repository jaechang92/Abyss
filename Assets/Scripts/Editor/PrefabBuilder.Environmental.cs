#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Enemy;
using FSM.Core;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class PrefabBuilder
    {
        private const string SPIDER_DATA = "Assets/Resources/Data/Enemies/BurrowSpider.asset";
        private const string SPIDER_PROFILE = "Assets/Resources/Data/Enemies/Profiles/BurrowSpiderProfile.asset";
        private const string SPIDER_PREFAB = "Assets/Prefabs/Enemies/BurrowSpider.prefab";

        [MenuItem("Abyss/Build/Environmental/Burrow Spider (Create Missing Only)")]
        public static void CreateMissingBurrowSpider()
        {
            if (File.Exists(SPIDER_PREFAB))
            {
                Debug.Log("[PrefabBuilder] BurrowSpider exists; skipped without overwriting assets or GUIDs.");
                return;
            }
            // 공용 그림을 만들거나 재임포트하지 않는다.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AbyssPaths.WhiteSquare);
            if (sprite == null)
            {
                Debug.LogError("[PrefabBuilder] Existing WhiteSquare sprite is required; no files changed.");
                return;
            }
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(SPIDER_DATA);
            var profile = AssetDatabase.LoadAssetAtPath<EnvironmentalEnemyProfile>(SPIDER_PROFILE);
            if ((File.Exists(SPIDER_DATA) && data == null) || (File.Exists(SPIDER_PROFILE) && profile == null)
                || (data != null && data.enemyId != "burrow_spider"))
            {
                Debug.LogError("[PrefabBuilder] Existing asset has an unexpected type/id; creation stopped.");
                return;
            }
            bool isNewData = data == null;
            EnsureDir("Assets/Resources/Data/Enemies/Profiles");
            EnsureDir(AbyssPaths.EnemyPrefabs);
            AssetDatabase.Refresh();
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EnvironmentalEnemyProfile>();
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("windup").floatValue = 0.7f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(profile, SPIDER_PROFILE);
            }
            if (isNewData)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.enemyId = "burrow_spider";
                data.displayName = "잠복 거미";
                data.baseHp = 30;
                data.baseDamage = 10;
                data.moveSpeed = 2f;
                data.detectionRange = 6f;
                data.attackRange = 1f;
                data.attackCooldown = 1.5f;
                data.attackWindup = 0.7f;
                data.attackRecovery = 1.1f;
                data.staggerDuration = 0.3f;
                data.deathLingerDuration = 1f;
                data.patrolRadius = 0f;
                data.expReward = 15;
                data.goldReward = 3;
                data.bodyTint = Color.white;
                data.hitSfx = LoadSfx("enemy_hit");
                data.deathSfx = LoadSfx("enemy_death");
                data.attackSfx = LoadSfx("skill_swift_slash");
                AssetDatabase.CreateAsset(data, SPIDER_DATA);
            }
            var root = new GameObject("BurrowSpider");
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.8f, 0.6f);
                var visual = new GameObject("Visual_TemporaryShape");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(0.8f / sprite.bounds.size.x,
                    0.6f / sprite.bounds.size.y, 1f);
                visual.transform.localPosition = -Vector3.Scale(sprite.bounds.center, visual.transform.localScale);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(0.55f, 0.48f, 0.42f);
                root.AddComponent<EnemyVisuals>();
                root.AddComponent<StateMachine>();
                EnemyBase enemy = AddEnemyComponent(root, data);
                SetPrivateField(enemy, "data", data);
                SetPrivateField(enemy, "profile", profile);
                SetPrivateField(enemy, "physicalBody", collider);
                SetPrivateField(enemy, "placeholderVisual", renderer);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, SPIDER_PREFAB);
                if (isNewData) LinkSpawnPrefab(data, prefab);
                else Debug.Log("[PrefabBuilder] Existing EnemyData preserved; inspect its spawnPrefab link manually.");
                AssetDatabase.SaveAssets();
                Debug.Log("[PrefabBuilder] BurrowSpider created; it starts on the Ground beneath its placed position. "
                    + "No room or reinforcement pool was changed; art is temporary.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
