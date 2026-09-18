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
        // ==================== Arc Projectile Prefab ====================
        /// <summary>
        /// 곡사 폭발탄 프리팹 생성. 직진탄과 나눈 이유는 <see cref="ArcProjectile"/> 주석 참조.
        /// 중력을 자체 적분하므로 RB는 Kinematic·gravityScale 0으로 둔다(직진탄과 동일).
        /// </summary>
        private static ArcProjectile BuildArcProjectilePrefab(Sprite sprite, bool forceRebuild)
        {
            string path = $"{AbyssPaths.CombatPrefabs}/EnemyArcShell.prefab";

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
                    return existing != null ? existing.GetComponent<ArcProjectile>() : null;
                }
            }

            var root = new GameObject("EnemyArcShell");
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                var col = root.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.5f;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 0.35f, 0.12f); // 직진탄(주황)보다 붉게 — 다른 위협임을 색으로 구분
                sr.sortingOrder = 3;

                root.AddComponent<ArcProjectile>();
                // 직진탄처럼 길쭉하게 만들지 않는다 — 포탄은 회전하며 날아가므로 둥근 편이 읽기 쉽다.
                root.transform.localScale = new Vector3(0.34f, 0.34f, 1f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabBuilder] 생성: {path}");
                return prefab != null ? prefab.GetComponent<ArcProjectile>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 포물선 <b>화살</b> 프리팹 생성. 터지지 않는 곡사탄이라 포탄과 그림·크기가 다르다 —
        /// 길쭉하고 창백한 회백색(원거리 사수 몸의 강조색과 같은 재료)이며, 진행 방향으로 회전하므로
        /// 떨어질 때 촉이 아래를 향한다.
        /// </summary>
        private static ArcProjectile BuildArcArrowPrefab(Sprite sprite, bool forceRebuild)
        {
            string path = $"{AbyssPaths.CombatPrefabs}/EnemyArcArrow.prefab";

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
                    return existing != null ? existing.GetComponent<ArcProjectile>() : null;
                }
            }

            var root = new GameObject("EnemyArcArrow");
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                var col = root.AddComponent<CircleCollider2D>();
                // 폭발 반경이 없어 맞으려면 겹쳐야 한다 — 포탄(0.5)보다 조금 넉넉히 잡는다.
                col.isTrigger = true;
                col.radius = 0.45f;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.82f, 0.82f, 0.78f); // 창백한 회백 — "나무도 쇠도 아닌" 화살(1권 18장)
                sr.sortingOrder = 3;

                root.AddComponent<ArcProjectile>();
                root.transform.localScale = new Vector3(0.5f, 0.16f, 1f); // 직진탄보다 길고 가늘게

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabBuilder] 생성: {path}");
                return prefab != null ? prefab.GetComponent<ArcProjectile>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// <c>usesArcProjectile</c>이 켜진 적에게 곡사탄을 연결한다.
        /// 직진탄(<see cref="LinkProjectileToRangedEnemies"/>)은 원거리 전체에 연결되므로,
        /// 곡사병도 직진탄 참조를 함께 갖는다 — 이건 낭비가 아니라 <b>폴백</b>이다.
        /// 곡사 배선이 실패해도 그 적이 무해해지지 않는다.
        ///
        /// 🔴 <b>곡사탄은 두 종류다.</b> 터지는 포탄(화염 박격포)과 터지지 않는 화살(원거리 사수)은
        /// 그림도 거동도 달라 한 프리팹으로 못 쓴다 — <see cref="EnemyData.arcExplodes"/>가 어느 쪽인지 정하고,
        /// 여기서 그 값으로 고른다. 예전에는 켜진 적 전부에 같은 프리팹을 물렸다.
        /// </summary>
        private static int LinkArcProjectileToMortars(ArcProjectile arcShell, ArcProjectile arcArrow)
        {
            if (arcShell == null && arcArrow == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            int linked = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null || !data.usesArcProjectile) continue;

                ArcProjectile shot = data.arcExplodes ? arcShell : arcArrow;
                if (shot == null) continue;

                var so = new SerializedObject(data);
                var prop = so.FindProperty("arcProjectilePrefab");
                if (prop == null) continue;
                prop.objectReferenceValue = shot;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(data);
                linked++;
            }
            return linked;
        }

        private static int LinkProjectileToRangedEnemies(Projectile projectile)
        {
            if (projectile == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:EnemyData");
            int linked = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null || !(data.isRanged || data.IsBoss)) continue;

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
