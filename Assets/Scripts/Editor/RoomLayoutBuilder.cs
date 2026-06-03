#if UNITY_EDITOR
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 룸별 지형 레이아웃을 활성 씬에 일괄 생성하고 <see cref="RoomLayoutController"/>에 자동 와이어링하는 툴.
    /// 6개 룸(Room1~6)에 서로 다른 발판 배치를 만들어 룸마다 다른 플레이 경험을 준다.
    ///
    /// 구조: "RoomLayouts"(RoomLayoutController) 아래 RoomLayout_01~06 루트, 각 루트에 발판 N개.
    /// 모든 루트는 동일 원점에 겹쳐 있고 런타임에 RoomLayoutController가 진입 룸 루트만 활성화한다.
    /// 발판 생성은 <see cref="EditorPlatformFactory"/> 공유(마찰 0 + Ground 레이어).
    ///
    /// 재실행 시 RoomLayouts 컴포넌트는 유지하고 자식 레이아웃 루트만 교체하므로 멱등하다.
    /// </summary>
    public static class RoomLayoutBuilder
    {
        private const string BuildMenu = "Tools/Abyss/Build Room Layouts in Active Scene";
        private const string ClearMenu = "Tools/Abyss/Clear Room Layouts in Active Scene";

        private const string LayoutsRootName = "RoomLayouts";
        private const string RoomDir = "Assets/Data/Rooms";
        private const string StandalonePlatformsName = "Platforms";

        private struct Spec
        {
            public Vector2 Pos;
            public Vector2 Size;
            public Spec(float x, float y, float w, float h)
            {
                Pos = new Vector2(x, y);
                Size = new Vector2(w, h);
            }
        }

        private struct RoomLayout
        {
            public string RoomAsset;  // Assets/Data/Rooms/{RoomAsset}.asset
            public string RootName;   // RoomLayout_0N
            public Spec[] Platforms;
        }

        // Ground top ≈ y=-0.5, spawnPoints x=-6,-3,0,3,6 기준. 룸마다 다른 지형 컨셉.
        private static readonly RoomLayout[] Layouts =
        {
            // Room1_Intro: 입문 — 개방된 평지에 낮은 양옆 발판만(학습용).
            new RoomLayout
            {
                RoomAsset = "Room1_Intro", RootName = "RoomLayout_01",
                Platforms = new[] { new Spec(-8f, 0.75f, 3f, 0.5f), new Spec(8f, 0.75f, 3f, 0.5f) }
            },
            // Room2_Skirmish: 비대칭 — 좌측 계단식 + 우측 단일 발판.
            new RoomLayout
            {
                RoomAsset = "Room2_Skirmish", RootName = "RoomLayout_02",
                Platforms = new[] { new Spec(-9f, 1f, 3f, 0.5f), new Spec(-4f, 2.5f, 3f, 0.5f), new Spec(6f, 1.25f, 4f, 0.5f) }
            },
            // Room3_Crowd: 수직성 — 중앙 고지대(더블 점프 필수) + 양옆 낮은 발판.
            new RoomLayout
            {
                RoomAsset = "Room3_Crowd", RootName = "RoomLayout_03",
                Platforms = new[] { new Spec(0f, 3f, 4f, 0.5f), new Spec(-8f, 1.2f, 3f, 0.5f), new Spec(8f, 1.2f, 3f, 0.5f) }
            },
            // Room4_Elite: 엄폐 — 양쪽 높은 기둥 + 상단 다리(더블 점프).
            new RoomLayout
            {
                RoomAsset = "Room4_Elite", RootName = "RoomLayout_04",
                Platforms = new[] { new Spec(-5f, 1.5f, 1f, 4f), new Spec(5f, 1.5f, 1f, 4f), new Spec(0f, 4.5f, 3f, 0.5f) }
            },
            // Room5_Ambush: 상승 계단 → 고지대 매복 구도.
            new RoomLayout
            {
                RoomAsset = "Room5_Ambush", RootName = "RoomLayout_05",
                Platforms = new[]
                {
                    new Spec(-10f, 0.75f, 3f, 0.5f), new Spec(-5f, 2f, 3f, 0.5f),
                    new Spec(0f, 3.25f, 3f, 0.5f), new Spec(5f, 4.5f, 3f, 0.5f)
                }
            },
            // Room6_Boss: 개방 아레나 — 보스 회피 공간 확보 위해 중앙 비우고 대칭 양옆 발판만.
            new RoomLayout
            {
                RoomAsset = "Room6_Boss", RootName = "RoomLayout_06",
                Platforms = new[] { new Spec(-9f, 2f, 4f, 0.5f), new Spec(9f, 2f, 4f, 0.5f) }
            },
        };

        [MenuItem(BuildMenu)]
        public static void BuildRoomLayouts()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("RoomLayoutBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "RoomLayoutBuilder",
                $"룸별 지형 레이아웃 {Layouts.Length}개를 생성하고 RoomLayoutController에 와이어링합니다.\n" +
                "  · RoomLayouts 루트 + RoomLayout_01~06 (룸마다 다른 발판 배치)\n" +
                "  · 런타임에 진입 룸 레이아웃만 활성화(나머지 비활성)\n\n" +
                "기존 RoomLayouts가 있으면 자식 레이아웃을 교체합니다.",
                "생성", "취소");
            if (!proceed) return;

            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var sprite = EditorPlatformFactory.LoadWhiteSquare();
            var frictionless = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();

            // RoomLayouts 루트 + 컨트롤러 확보 (컴포넌트 유지, 자식만 정리).
            var rootGo = GameObject.Find(LayoutsRootName);
            if (rootGo == null)
            {
                rootGo = new GameObject(LayoutsRootName);
                Undo.RegisterCreatedObjectUndo(rootGo, "Create RoomLayouts root");
            }
            else
            {
                for (int i = rootGo.transform.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(rootGo.transform.GetChild(i).gameObject);
            }

            var controller = rootGo.GetComponent<RoomLayoutController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<RoomLayoutController>(rootGo);
            }

            // 레이아웃 루트 + 발판 생성, 바인딩용 (room, root) 수집.
            var rooms = new RoomData[Layouts.Length];
            var roots = new GameObject[Layouts.Length];
            int missingRooms = 0;

            for (int li = 0; li < Layouts.Length; li++)
            {
                var layout = Layouts[li];

                var room = AssetDatabase.LoadAssetAtPath<RoomData>($"{RoomDir}/{layout.RoomAsset}.asset");
                if (room == null)
                {
                    Debug.LogWarning($"[RoomLayoutBuilder] RoomData 누락: {RoomDir}/{layout.RoomAsset}.asset " +
                                     "— 먼저 'Tools/Abyss/Build Stage 1 Content' 실행 필요. 루트는 생성하되 바인딩 비움.");
                    missingRooms++;
                }
                rooms[li] = room;

                var layoutRoot = new GameObject(layout.RootName);
                Undo.RegisterCreatedObjectUndo(layoutRoot, $"Create {layout.RootName}");
                layoutRoot.transform.SetParent(rootGo.transform, false);
                roots[li] = layoutRoot;

                for (int pi = 0; pi < layout.Platforms.Length; pi++)
                {
                    var spec = layout.Platforms[pi];
                    EditorPlatformFactory.CreatePlatform(
                        layoutRoot.transform, $"Platform_{pi + 1:D2}", spec.Pos, spec.Size,
                        groundLayer, sprite, frictionless, EditorPlatformFactory.DefaultPlatformColor);
                }

                // 에디터 미리보기: 첫 룸만 활성, 나머지 비활성.
                layoutRoot.SetActive(li == 0);
            }

            // RoomLayoutController 바인딩 와이어링.
            var so = new SerializedObject(controller);
            so.FindProperty("defaultLayoutIndex").intValue = 0;
            var bindingsProp = so.FindProperty("bindings");
            bindingsProp.arraySize = Layouts.Length;
            for (int i = 0; i < Layouts.Length; i++)
            {
                var element = bindingsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("room").objectReferenceValue = rooms[i];
                element.FindPropertyRelative("layoutRoot").objectReferenceValue = roots[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // 정적 테스트 플랫폼이 남아 있으면 룸 레이아웃과 겹치므로 안내.
            if (GameObject.Find(StandalonePlatformsName) != null)
            {
                Debug.LogWarning($"[RoomLayoutBuilder] 정적 '{StandalonePlatformsName}' 루트가 씬에 남아 있습니다. " +
                                 "룸 레이아웃과 겹치므로 'Tools/Abyss/Clear Test Platforms in Active Scene'로 제거 권장.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = rootGo;
            EditorGUIUtility.PingObject(rootGo);

            Debug.Log($"[RoomLayoutBuilder] 룸 레이아웃 {Layouts.Length}개 생성·와이어링 완료" +
                      (missingRooms > 0 ? $" (RoomData 누락 {missingRooms}개 — 바인딩 비움)" : "") +
                      ". 첫 룸 레이아웃만 미리보기 활성.");
        }

        [MenuItem(ClearMenu)]
        public static void ClearRoomLayouts()
        {
            var root = GameObject.Find(LayoutsRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("RoomLayoutBuilder", $"'{LayoutsRootName}' 루트가 없습니다.", "확인");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[RoomLayoutBuilder] '{LayoutsRootName}' 루트 및 모든 룸 레이아웃 제거 완료.");
        }
    }
}
#endif
