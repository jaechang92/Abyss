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
            string prefabPath = $"{AbyssPaths.EnemyPrefabs}/{prefabName}.prefab";

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

                EnemyBase enemy = AddEnemyComponent(root, data);
                SetPrivateField(enemy, "data", data);

                if (data.isBoss) root.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                else if (data.enemyId is "midboss_sentinel" or "midboss_throne_warden") root.transform.localScale = new Vector3(1.45f, 1.45f, 1f);
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

        /// <summary>
        /// enemyId 기반으로 Assets/Art/Sprites/Enemies/{enemyId}.png 스프라이트를 로드.
        /// 파일이 없으면 fallback(WhiteSquare) 반환.
        /// </summary>
        private static Sprite GetEnemySpriteByEnemyId(string enemyId, Sprite fallback)
        {
            if (string.IsNullOrEmpty(enemyId)) return fallback;
            string path = $"{AbyssPaths.EnemySprites}/{enemyId}.png";
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return loaded != null ? loaded : fallback;
        }

        /// <summary>
        /// Assets/Art/Sprites/Enemies/*.png 전체를 순회하여 픽셀아트 임포트 설정 일괄 적용.
        /// TextureType=Sprite, FilterMode=Point, PPU=16, Mipmap 끔, 압축 없음.
        /// </summary>
        [MenuItem(AbyssMenu.GenerateSpriteImport)]
        public static void SetupEnemySpriteImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AbyssPaths.EnemySprites });
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

        /// <summary>
        /// enemyId/분류에 맞는 적 컴포넌트를 부착해 반환.
        /// 보스별 고유 패턴 클래스(MidBossSentinelBoss·FlameSerpentBoss)는 enemyId로 분기하고,
        /// 그 외 isBoss는 기본 BossEnemy(부채꼴 볼리), 일반 적은 EnemyBase를 사용한다.
        /// MidBossSentinel은 isElite지만 페이즈 패턴을 위해 BossEnemy 파생으로 승격된다.
        /// </summary>
        private static EnemyBase AddEnemyComponent(GameObject root, EnemyData data)
        {
            switch (data.enemyId)
            {
                case "midboss_sentinel":
                {
                    var sentinel = root.AddComponent<MidBossSentinelBoss>();
                    SetPrivateField(sentinel, "phaseChangeSfx", LoadSfx("boss_phase"));
                    SetPrivateField(sentinel, "telegraphSfx", LoadSfx("boss_telegraph"));
                    SetPrivateField(sentinel, "slashSfx", LoadSfx("sentinel_slash"));
                    return sentinel;
                }
                case "boss_flame_serpent":
                {
                    var serpent = root.AddComponent<FlameSerpentBoss>();
                    SetPrivateField(serpent, "phaseChangeSfx", LoadSfx("boss_phase"));
                    SetPrivateField(serpent, "telegraphSfx", LoadSfx("boss_telegraph"));
                    SetPrivateField(serpent, "smashSfx", LoadSfx("serpent_smash"));
                    SetPrivateField(serpent, "breatheSfx", LoadSfx("serpent_breath"));
                    return serpent;
                }
                case "midboss_throne_warden":
                {
                    // Stage3 중간보스 — 전용 AI 없이 회전베기 패턴을 그대로 쓴다(스탯·이름만 차별화).
                    var warden = root.AddComponent<MidBossSentinelBoss>();
                    SetPrivateField(warden, "phaseChangeSfx", LoadSfx("boss_phase"));
                    SetPrivateField(warden, "telegraphSfx", LoadSfx("boss_telegraph"));
                    SetPrivateField(warden, "slashSfx", LoadSfx("sentinel_slash"));
                    return warden;
                }
                case "boss_thronebound":
                {
                    var thronebound = root.AddComponent<ThroneboundBoss>();
                    SetPrivateField(thronebound, "phaseChangeSfx", LoadSfx("boss_phase"));
                    SetPrivateField(thronebound, "telegraphSfx", LoadSfx("boss_telegraph"));
                    // 전용 효과음(thronebound_blink/slash)이 아직 없다 — PlaySfx는 null이면 무동작이라
                    // 침묵보다는 기존 베기 소리를 빌려 둔다. 정식 SFX는 4-2에서 교체(placeholder 추적).
                    SetPrivateField(thronebound, "blinkSfx", LoadSfx("skill_phantom_step"));
                    SetPrivateField(thronebound, "slashSfx", LoadSfx("sentinel_slash"));
                    return thronebound;
                }
            }

            if (data.isBoss)
            {
                var boss = root.AddComponent<BossEnemy>();
                SetPrivateField(boss, "phaseChangeSfx", LoadSfx("boss_phase"));
                return boss;
            }
            return root.AddComponent<EnemyBase>();
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
    }
}
#endif
