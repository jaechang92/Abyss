#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Combat;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class PrefabBuilder
    {
        // ==================== Player Projectile Prefabs ====================
        /// <summary>
        /// 플레이어 원거리 기본 공격 발사체 2종(궁수 화살 · 투척사 단검).
        ///
        /// 🔴 <b>적 발사체 프리팹을 재사용하지 않는다</b> — 적 것은 주황이라, 같은 것을 쓰면
        /// 화면에서 누구 탄인지 안 읽힌다(15-ranged-basic-attack §5). 컴포넌트 구성은 같다.
        /// 발사 수치(속도 · 수명 · 관통)는 프리팹이 아니라 <c>FormData</c> 가 갖는다(ContentBuilder).
        /// </summary>
        /// <returns>생성 또는 로드한 프리팹 수.</returns>
        private static int BuildPlayerProjectilePrefabs(Sprite whiteSprite, bool forceRebuild)
        {
            int count = 0;

            // 화살 — 그림이 아직 없다. 가는 막대 자리표시자(공허궁수 보라).
            if (BuildPlayerProjectilePrefab(AbyssPaths.PlayerArrowPrefab, "PlayerArrow", whiteSprite,
                    new Color(0.72f, 0.55f, 1f), new Vector3(0.6f, 0.12f, 1f), 0.3f, Vector2.zero, forceRebuild) != null)
            {
                count++;
            }

            // 단검 — 발사 순간 장착 무기 그림으로 바뀐다. 기본은 이 빠진 단검, 없으면 흰 사각형.
            var dagger = AssetDatabase.LoadAssetAtPath<Sprite>(AbyssPaths.DefaultDaggerSprite);
            if (dagger == null)
            {
                Debug.LogWarning($"[PrefabBuilder] 단검 그림 없음({AbyssPaths.DefaultDaggerSprite}) — 흰 사각형으로 대신한다.");
            }

            if (BuildPlayerProjectilePrefab(AbyssPaths.PlayerDaggerPrefab, "PlayerDagger",
                    dagger != null ? dagger : whiteSprite, Color.white,
                    dagger != null ? Vector3.one : new Vector3(0.4f, 0.12f, 1f),
                    0.25f,
                    // 피벗이 자루라 판정을 칼날 쪽(그림 기준 오른쪽 위 45°)으로 민다. 자루에 두면 칼끝이 적을 지나간 뒤에 맞는다.
                    dagger != null ? new Vector2(0.2f, 0.2f) : Vector2.zero,
                    forceRebuild) != null)
            {
                count++;
            }

            return count;
        }

        private static Projectile BuildPlayerProjectilePrefab(string path, string name, Sprite sprite, Color color,
                                                              Vector3 scale, float colliderRadius, Vector2 colliderOffset,
                                                              bool forceRebuild)
        {
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

            var root = new GameObject(name);
            try
            {
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;

                var col = root.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius;
                col.offset = colliderOffset;

                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.sortingOrder = 3;

                root.AddComponent<Projectile>();
                root.transform.localScale = scale;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabBuilder] 생성: {path}");
                return prefab != null ? prefab.GetComponent<Projectile>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
