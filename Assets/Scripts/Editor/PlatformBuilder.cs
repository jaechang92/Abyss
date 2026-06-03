#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 활성 씬에 다단점프 검증용 테스트 플랫폼(발판)을 일괄 배치하는 에디터 툴.
    /// 현재 PrototypeArena는 단일 평지(Ground)만 있어 폼별 다단점프(jumpCount)를 검증할 수 없다.
    /// 본 툴은 지상 → 단계별 발판 → 중앙 고지대(더블 점프 필수) → 하강 구조의 아치형 점프 코스를 만든다.
    ///
    /// 점프 물리 기준 (PlayerCharacter.Movement / Player.prefab):
    ///   jumpForce = 12, gravityScale = 3 → 단일 점프 ≈ 2.45 유닛, 더블 점프 ≈ 4~5 유닛.
    ///   중앙 P3는 직전 발판과 top-to-top 간격 3.0 유닛이라 단일 점프(2.45)로 닿지 않아 더블 점프가 필수.
    ///
    /// 재실행 시 기존 "Platforms" 루트의 자식을 모두 제거하고 다시 생성하므로 멱등하다.
    /// 발판은 Ground 레이어(6)에 배치되어 플레이어 접지 판정(groundLayer)에 잡힌다.
    /// </summary>
    public static class PlatformBuilder
    {
        private const string BuildMenu = "Tools/Abyss/Build Test Platforms in Active Scene";
        private const string ClearMenu = "Tools/Abyss/Clear Test Platforms in Active Scene";

        private const string PlatformsRootName = "Platforms";
        private const string GroundLayerName = "Ground";
        private const string WhiteSquarePath = "Assets/Art/Sprites/WhiteSquare.png";

        // 마찰 0 머티리얼 — 플랫폼 측면 접촉 시 플레이어가 벽에 달라붙어 떨어지지 않는 문제 방지.
        // Unity 2D 마찰은 두 콜라이더의 기하평균(sqrt(fa*fb))이라 플랫폼만 0이면 접촉 마찰이 0이 됨.
        private const string PhysicsDir = "Assets/Data/Physics";
        private const string FrictionlessPath = "Assets/Data/Physics/Frictionless.physicsMaterial2D";

        // 발판 색상 — Ground(흰색)와 구분되도록 따뜻한 갈색 톤.
        private static readonly Color PlatformColor = new Color(0.78f, 0.47f, 0.24f, 1f);

        /// <summary>발판 1개 명세 (월드 위치, 크기). 크기는 transform.localScale로 적용.</summary>
        private struct PlatformSpec
        {
            public string Name;
            public Vector2 Position;
            public Vector2 Size;

            public PlatformSpec(string name, Vector2 position, Vector2 size)
            {
                Name = name;
                Position = position;
                Size = size;
            }
        }

        // 지상(Ground top ≈ y=-0.5) 기준 아치형 점프 코스.
        // P3는 더블 점프 필수 구간(★). 폭 45.6 Ground 범위(x: ±22.8) 안에 배치.
        private static readonly PlatformSpec[] Specs =
        {
            new PlatformSpec("Platform_01", new Vector2(-12f, 0.75f), new Vector2(4f,   0.5f)),
            new PlatformSpec("Platform_02", new Vector2(-6f,  2.5f),  new Vector2(3.5f, 0.5f)),
            new PlatformSpec("Platform_03", new Vector2(0f,   5.5f),  new Vector2(3f,   0.5f)), // ★ 더블 점프 필수
            new PlatformSpec("Platform_04", new Vector2(6f,   2.5f),  new Vector2(3.5f, 0.5f)),
            new PlatformSpec("Platform_05", new Vector2(12f,  0.75f), new Vector2(4f,   0.5f)),
        };

        [MenuItem(BuildMenu)]
        public static void BuildTestPlatforms()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("PlatformBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "PlatformBuilder",
                $"다단점프 검증용 테스트 플랫폼 {Specs.Length}개를 활성 씬에 배치합니다.\n" +
                "  · 지상 → 계단식 발판 → 중앙 고지대(더블 점프 필수) → 하강 아치 구조\n" +
                "  · Ground 레이어(접지 판정 대상)에 BoxCollider2D + 스프라이트로 생성\n\n" +
                "기존 'Platforms' 루트가 있으면 자식을 모두 교체합니다.",
                "배치", "취소");
            if (!proceed) return;

            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                Debug.LogWarning($"[PlatformBuilder] '{GroundLayerName}' 레이어를 찾지 못해 Default(0)로 대체합니다. " +
                                 "접지 판정이 안 될 수 있으니 레이어 설정을 확인하세요.");
                groundLayer = 0;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            if (sprite == null)
                Debug.LogWarning($"[PlatformBuilder] 스프라이트 누락: {WhiteSquarePath} — 비주얼 없이 콜라이더만 생성됩니다.");

            var frictionless = GetOrCreateFrictionlessMaterial();

            // 루트 확보 (기존 자식 정리 → 멱등 재생성).
            var root = GameObject.Find(PlatformsRootName);
            if (root == null)
            {
                root = new GameObject(PlatformsRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Platforms root");
            }
            else
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
            }

            foreach (var spec in Specs)
                CreatePlatform(root.transform, spec, groundLayer, sprite, frictionless);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            Debug.Log($"[PlatformBuilder] 테스트 플랫폼 {Specs.Length}개 배치 완료 — " +
                      $"레이어='{LayerMask.LayerToName(groundLayer)}', 루트='{PlatformsRootName}'. " +
                      "중앙 Platform_03은 더블 점프 필수 구간입니다.");
        }

        [MenuItem(ClearMenu)]
        public static void ClearTestPlatforms()
        {
            var root = GameObject.Find(PlatformsRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("PlatformBuilder", $"'{PlatformsRootName}' 루트가 없습니다.", "확인");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PlatformBuilder] '{PlatformsRootName}' 루트 및 모든 발판 제거 완료.");
        }

        private static void CreatePlatform(
            Transform parent, PlatformSpec spec, int groundLayer, Sprite sprite, PhysicsMaterial2D material)
        {
            var go = new GameObject(spec.Name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {spec.Name}");
            go.layer = groundLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(spec.Position.x, spec.Position.y, 0f);
            go.transform.localScale = new Vector3(spec.Size.x, spec.Size.y, 1f);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one; // 1x1 콜라이더가 transform.localScale로 실제 크기가 됨.
            collider.sharedMaterial = material; // 마찰 0 → 측면에 달라붙지 않고 미끄러져 떨어짐.

            if (sprite != null)
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = PlatformColor;
                renderer.sortingOrder = -1; // 플레이어/적보다 뒤에 렌더되도록.
            }
        }

        /// <summary>
        /// 마찰 0 PhysicsMaterial2D 에셋을 로드하거나 없으면 생성한다.
        /// 플랫폼 측면 마찰로 플레이어가 벽에 붙어 떨어지지 않는 문제를 방지하기 위함.
        /// </summary>
        private static PhysicsMaterial2D GetOrCreateFrictionlessMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(FrictionlessPath);
            if (mat != null) return mat;

            if (!Directory.Exists(PhysicsDir)) Directory.CreateDirectory(PhysicsDir);

            mat = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, FrictionlessPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlatformBuilder] 마찰 0 PhysicsMaterial2D 생성: {FrictionlessPath}");
            return mat;
        }
    }
}
#endif
