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
                col.size = data.IsBoss ? new Vector2(2f, 2f) : Vector2.one;

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

                // 🔴 <b>등급 확대는 「그림이 아직 임시일 때」의 임시방편이다.</b> 그림이 붙은 등급은 확대를 뺀다 —
                // 크기를 그림이 정해야 <c>08-silhouette §1</c> 의 세로 px 규격이 화면 크기와 같은 뜻이 되고,
                // 비정수 배율(1.25·1.45·1.6)이 픽셀을 뭉개지 않는다(<c>PixelScale</c> 는 FHD 3배 정수 배율이다).
                // ✅ <b>정예는 뺐다</b>(2026-09-21) — 엘리트 사냥꾼 그림 84px 이 1.25배로 3.28유닛이 되어
                // 규격 2.5유닛을 31% 넘었고, <b>중간보스 규격 3.0유닛보다 커졌다.</b>
                // 84px = 2.63유닛으로 규격에 맞는다. 🔴 <b>남은 둘은 그림을 붙일 때 같이 뺀다</b> —
                // 그때 그림 목표는 중간보스 96px · 보스 128~160px 이다(pro 의 size 상한 128 에 보스가 걸린다).
                // ✅ <b>감시자 거인도 뺐다</b>(2026-09-21) — 그림 99x98px 이 이미 3.06유닛이라 1.45배면 4.4유닛으로
                // 규격(중간보스 3.0)을 47% 넘고, 🔴 <b>콜라이더까지 1.89x4.35 로 불어 `attackRange 2.0` 이 안 닿았다</b>
                // (최소 중심 거리 1.86 — 정면으로 완전히 붙어야 겨우 0.14 여유라 Attack 상태로 전이가 안 됐다.
                //  대신 반경 3.1 짜리 회전베기만 계속 돌았다 — 사용자 지적).
                // 🔴 <b>왕좌의 파수관은 그림이 아직 없어 1.45 를 남긴다</b> — 그림을 붙일 때 같이 뺀다.
                if (data.IsBoss) root.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                else if (data.enemyId is "midboss_throne_warden") root.transform.localScale = new Vector3(1.45f, 1.45f, 1f);

                AttachNametag(root.transform, data);

                // 애니메이션이 구워진 적이면 정지 그림을 Visual 자식으로 옮긴다 — 강제 재생성이 배선을 지우지 않게.
                EnemyAnimationBuilder.TryApplyTo(root, data);

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
        public static void SetupEnemySpriteImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AbyssPaths.EnemySprites });
            int count = 0;
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;

                // 🔴 하위 폴더는 애니메이션 시트다(Enemies/{적}/{방향}/ · PPU 32 · Multiple) — FindAssets 는 하위까지 훑어서,
                //    거르지 않으면 이 메뉴가 시트를 Single·PPU 16 으로 되돌리고 클립이 문 스프라이트가 전부 사라진다.
                if (Path.GetDirectoryName(assetPath)?.Replace('\\', '/') != AbyssPaths.EnemySprites) continue;

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
                case "boss_abyss_keeper":
                {
                    // Stage1 첫 보스 — 기본 볼리에 예고를 붙인 전용 클래스(C2). 볼리 직렬값은 BossEnemy 기본값 그대로.
                    var keeper = root.AddComponent<AbyssKeeperBoss>();
                    SetPrivateField(keeper, "phaseChangeSfx", LoadSfx("boss_phase"));
                    SetPrivateField(keeper, "telegraphSfx", LoadSfx("boss_telegraph"));
                    return keeper;
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

            if (data.IsBoss)
            {
                var boss = root.AddComponent<BossEnemy>();
                SetPrivateField(boss, "phaseChangeSfx", LoadSfx("boss_phase"));
                return boss;
            }
            return root.AddComponent<EnemyBase>();
        }

        private static Color GetEnemyColor(EnemyData data)
        {
            if (data.IsBoss) return new Color(0.8f, 0.15f, 0.15f);
            if (data.IsElite) return new Color(0.95f, 0.6f, 0.1f);
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
