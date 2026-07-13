#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Enemy;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class PrefabBuilder
    {
        // ==================== Projectile Prefab ====================
        /// <summary>
        /// 원거리 적 공용 발사체 프리팹 생성. Kinematic RB + Trigger CircleCollider2D + Projectile.
        /// 기존 존재 시 forceRebuild 아니면 로드만(컴포넌트 반환).
        /// </summary>
        private static Projectile BuildProjectilePrefab(Sprite sprite, bool forceRebuild)
        {
            string path = $"{AbyssPaths.CombatPrefabs}/EnemyProjectile.prefab";

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
        /// 원거리(isRanged) 및 보스(isBoss) EnemyData.projectilePrefab에 발사체를 연결.
        /// 원거리 적은 직격, 보스는 페이즈 탄막에 동일 발사체를 재사용한다. 연결한 종 수 반환.
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
                if (data == null || !(data.isRanged || data.isBoss)) continue;

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
    }
}
#endif
