#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 발판(플랫폼) GameObject 생성 공통 로직 — PlatformBuilder / RoomLayoutBuilder 공유 SoT.
    /// Ground 레이어 + BoxCollider2D + 마찰 0 머티리얼 + WhiteSquare 스프라이트 조합을 일관되게 생성한다.
    /// 마찰 0: 플랫폼 측면 접촉 시 플레이어가 벽에 달라붙어 떨어지지 않는 문제 방지
    /// (Unity 2D 마찰은 두 콜라이더의 기하평균 sqrt(fa*fb)이라 플랫폼만 0이면 접촉 마찰이 0).
    /// </summary>
    internal static class EditorPlatformFactory
    {
        public const string GroundLayerName = "Ground";
        public const string WhiteSquarePath = "Assets/Art/Sprites/WhiteSquare.png";
        public const string PhysicsDir = "Assets/Data/Physics";
        public const string FrictionlessPath = "Assets/Data/Physics/Frictionless.physicsMaterial2D";

        // 발판 기본 색상 — Ground(흰색)와 구분되도록 따뜻한 갈색 톤.
        public static readonly Color DefaultPlatformColor = new Color(0.78f, 0.47f, 0.24f, 1f);

        /// <summary>Ground 레이어 인덱스. 없으면 Default(0)로 대체하고 경고.</summary>
        public static int GetGroundLayer()
        {
            int layer = LayerMask.NameToLayer(GroundLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[EditorPlatformFactory] '{GroundLayerName}' 레이어 없음 → Default(0) 대체. " +
                                 "접지 판정이 안 될 수 있으니 레이어 설정을 확인하세요.");
                layer = 0;
            }
            return layer;
        }

        public static Sprite LoadWhiteSquare()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            if (sprite == null)
                Debug.LogWarning($"[EditorPlatformFactory] 스프라이트 누락: {WhiteSquarePath} — 비주얼 없이 콜라이더만 생성됩니다.");
            return sprite;
        }

        /// <summary>마찰 0 PhysicsMaterial2D 에셋을 로드하거나 없으면 생성한다.</summary>
        public static PhysicsMaterial2D GetOrCreateFrictionlessMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (mat != null) return mat;

            if (!Directory.Exists(PhysicsDir)) Directory.CreateDirectory(PhysicsDir);

            mat = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, FrictionlessPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EditorPlatformFactory] 마찰 0 PhysicsMaterial2D 생성: {FrictionlessPath}");
            return mat;
        }

        /// <summary>
        /// 발판 1개를 parent 아래에 생성한다. size는 transform.localScale로 적용(콜라이더 1x1 기준).
        /// </summary>
        public static GameObject CreatePlatform(
            Transform parent, string name, Vector2 position, Vector2 size,
            int groundLayer, Sprite sprite, PhysicsMaterial2D material, Color color)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.layer = groundLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;            // 1x1 콜라이더가 transform.localScale로 실제 크기가 됨.
            collider.sharedMaterial = material;     // 마찰 0 → 측면에 달라붙지 않고 미끄러져 떨어짐.

            if (sprite != null)
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = -1;         // 플레이어/적보다 뒤에 렌더되도록.
            }
            return go;
        }
    }
}
#endif
