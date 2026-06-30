#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 활성 씬에 다단점프 검증용 테스트 플랫폼(발판)을 일괄 배치하는 에디터 툴.
    /// 단일 평지(Ground)만으로는 폼별 다단점프(jumpCount)를 검증할 수 없어,
    /// 지상 → 단계별 발판 → 중앙 고지대(더블 점프 필수) → 하강 구조의 아치형 점프 코스를 만든다.
    ///
    /// 점프 물리 기준 (PlayerCharacter.Movement / Player.prefab):
    ///   jumpForce = 12, gravityScale = 3 → 단일 점프 ≈ 2.45 유닛, 더블 점프 ≈ 4~5 유닛.
    ///   중앙 Platform_03은 직전 발판과 top-to-top 간격 3.0 유닛이라 단일 점프로 닿지 않아 더블 점프가 필수.
    ///
    /// 발판 생성 로직은 <see cref="EditorPlatformFactory"/>가 담당(RoomLayoutBuilder와 공유).
    /// 재실행 시 기존 "Platforms" 루트의 자식을 모두 제거하고 다시 생성하므로 멱등하다.
    /// </summary>
    public static class PlatformBuilder
    {
        private const string PlatformsRootName = "Platforms";

        /// <summary>발판 1개 명세 (위치, 크기).</summary>
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
        // Platform_03은 더블 점프 필수 구간(★). 폭 45.6 Ground 범위(x: ±22.8) 안에 배치.
        private static readonly PlatformSpec[] Specs =
        {
            new PlatformSpec("Platform_01", new Vector2(-12f, 0.75f), new Vector2(4f,   0.5f)),
            new PlatformSpec("Platform_02", new Vector2(-6f,  2.5f),  new Vector2(3.5f, 0.5f)),
            new PlatformSpec("Platform_03", new Vector2(0f,   5.5f),  new Vector2(3f,   0.5f)), // ★ 더블 점프 필수
            new PlatformSpec("Platform_04", new Vector2(6f,   2.5f),  new Vector2(3.5f, 0.5f)),
            new PlatformSpec("Platform_05", new Vector2(12f,  0.75f), new Vector2(4f,   0.5f)),
        };

        [MenuItem(AbyssMenu.BuildPlatforms)]
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

            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var sprite = EditorPlatformFactory.LoadWhiteSquare();
            var frictionless = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();

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
            {
                EditorPlatformFactory.CreatePlatform(
                    root.transform, spec.Name, spec.Position, spec.Size,
                    groundLayer, sprite, frictionless, EditorPlatformFactory.DefaultPlatformColor);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            Debug.Log($"[PlatformBuilder] 테스트 플랫폼 {Specs.Length}개 배치 완료 — " +
                      $"루트='{PlatformsRootName}'. 중앙 Platform_03은 더블 점프 필수 구간입니다.");
        }

        [MenuItem(AbyssMenu.ClearPlatforms)]
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
    }
}
#endif
