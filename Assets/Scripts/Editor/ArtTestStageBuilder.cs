#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// stage1 환경 아트를 실제로 돌려 보는 테스트 씬 빌더 — <c>Assets/Scenes/ArtTest.unity</c>.
    ///
    /// 🔴 <b>이 씬은 게임이 아니라 자(尺)다.</b> `NEXT_TASKS` 3번의 판정 네 개를
    /// 눈으로 확인할 수 있게 배치하는 것이 전부이고, 판정이 끝나면 역할이 끝난다.
    ///
    /// <list type="number">
    /// <item><b>⑪ 벽과 지면이 갈리는가</b> — 같은 높이의 띠를 <c>x=0</c>에서 맞대 놓았다.
    /// 이음매를 걸어서 지나가며 재질이 바뀌는지 본다. <c>base_tile_id</c>로 이었더니
    /// 「이어짐」은 얻고 「구분」을 잃은 것 같다는 것이 이 씬을 만든 직접적인 이유다.</item>
    /// <item><b>픽셀 밀도가 맞는가</b> — 배경 160x96은 PPU 32에서 5x3유닛이라
    /// 카메라(17.8x10유닛)를 <b>못 채운다</b>. <see cref="BackgroundUpscale"/>로 채우는데,
    /// 그 순간 <b>배경 픽셀이 프롭 픽셀보다 4배 커진다</b>. 그게 눈에 거슬리는지가 판정 대상이다.</item>
    /// <item><b>bg_near 정렬</b> — 이 한 장만 159x96이다(나머지는 159x95). 1px 어긋남이
    /// 보이는지 본다.</item>
    /// <item><b>배경 아래 7단 / 캐릭터 위 3단</b> — 계측으로는 배경 4장 모두 위 3단 0.00%다.
    /// 그런데 <b>타일셋은 위 3단을 7.9%·11.7% 쓴다</b> — 바닥이 캐릭터 대역을 먹고 있다.
    /// 축척 표지(<c>2유닛</c>)를 바닥 위에 세워 두었으니 실루엣이 떠 보이는지 본다.</item>
    /// </list>
    ///
    /// ⚠️ <b>메뉴를 다시 누르면 씬을 새로 만든다.</b> 손으로 만진 것은 날아간다 —
    /// 이 씬에서 값을 정했으면 <b>여기가 아니라 코드에 옮겨 적을 것.</b>
    ///
    /// 조작은 <see cref="ArtTestPanner"/>: 방향키 이동 · Q·E 확대축소 · R 원위치.
    /// </summary>
    internal static partial class ArtTestStageBuilder
    {
        private const float Ppu = EnvironmentArtImporter.PixelsPerUnit;

        /// <summary>배경 확대 배율. <b>정수여야 한다</b> — 아니면 픽셀이 찌그러진다.</summary>
        private const int BackgroundUpscale = 4;

        private const float CameraSize = 5f;

        /// <summary>지면 윗줄 타일의 중심 y. 윗면(밟는 자리)은 여기서 +0.5다.</summary>
        private const float GroundRowY = -3f;
        private const float SurfaceY = GroundRowY + 0.5f;

        private const float GroundMinX = -46f;
        private const float SeamX = 0f;
        private const float WallMaxX = 14f;

        // 정렬 순서 — 하나의 SortingLayer(Default)만 있으므로 order로만 가른다.
        private const int OrderSky = -40;
        private const int OrderFar = -30;
        private const int OrderMid = -20;
        private const int OrderTiles = 0;
        private const int OrderProps = 5;
        private const int OrderSheets = 10;
        private const int OrderNear = 20;
        private const int OrderLabel = 30;

        [MenuItem(AbyssMenu.BuildArtTestStage)]
        public static void Build()
        {
            if (!Directory.Exists(AbyssPaths.Stage1Art))
            {
                Debug.LogError($"[ArtTestStageBuilder] 아트가 없다: {AbyssPaths.Stage1Art}\n" +
                               "Art_Source/curated/stage1_rift_entrance/ 에서 복사되었는지 확인할 것.");
                return;
            }

            EnvironmentArtImporter.Apply();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = BuildCamera();
            BuildBackgrounds();
            BuildGroundAndWall();
            BuildProps();
            BuildTilesetSheets();
            BuildScaleReference();
            BuildReadout(cam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, AbyssPaths.ArtTestScene);
            AssetDatabase.Refresh();

            Debug.Log($"[ArtTestStageBuilder] 완료 → {AbyssPaths.ArtTestScene}\n" +
                      $"  PPU {Ppu} · 배경 확대 x{BackgroundUpscale} · 카메라 ortho {CameraSize}\n" +
                      "  재생 후 방향키로 x=0 이음매를 지나가며 벽/지면이 갈리는지 볼 것.");
        }

        // ───────────────────────────────────────────────────────────── 카메라

        private static UnityEngine.Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(-4f, 0f, -10f);

            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 램프 최암(#0B0E11). 배경이 못 덮는 곳이 검게 비면 밀도 문제가 안 보인다.
            cam.backgroundColor = new Color32(0x0B, 0x0E, 0x11, 0xFF);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            go.AddComponent<ArtTestPanner>();
            return cam;
        }

        // ───────────────────────────────────────────────────────────── 배경

        /// <summary>
        /// 배경 네 층. 먼 것부터 깊이가 0에 가깝고, <c>bg_near</c>만 1을 넘는다 —
        /// 조립본이 그 층을 "a foreground overlay"라고 적어 뒀기 때문이다.
        /// </summary>
        private static void BuildBackgrounds()
        {
            var root = new GameObject("Backgrounds").transform;

            AddLayer(root, "bg_sky", depth: 0.05f, order: OrderSky, z: 40f);
            AddLayer(root, "bg_far", depth: 0.25f, order: OrderFar, z: 30f);
            AddLayer(root, "bg_boss_arena", depth: 0.5f, order: OrderMid, z: 20f);
            AddLayer(root, "bg_near", depth: 1.15f, order: OrderNear, z: -2f);
        }

        private static void AddLayer(Transform parent, string spriteName, float depth, int order, float z)
        {
            var sprite = LoadSprite(spriteName);
            if (sprite == null) return;

            var layer = new GameObject(spriteName);
            layer.transform.SetParent(parent, false);
            layer.transform.position = new Vector3(0f, 0f, z);
            layer.transform.localScale = Vector3.one * BackgroundUpscale;

            // 한 장의 폭(확대 전 유닛). 3장을 이어 붙여야 되감을 때 이음매가 안 보인다.
            float tileWidth = sprite.rect.width / Ppu;
            for (int i = -1; i <= 1; i++)
            {
                var piece = new GameObject($"{spriteName}_{i + 1}");
                piece.transform.SetParent(layer.transform, false);
                piece.transform.localPosition = new Vector3(tileWidth * i, 0f, 0f);

                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
            }

            layer.AddComponent<ParallaxLayer>()
                 .Configure(depth, tileWidth * BackgroundUpscale);
        }

        // ───────────────────────────────────────────────────────────── 읽을거리

        /// <summary>
        /// 씬 안에 숫자를 적어 둔다. <b>"보기에 이상하다"와 "왜 이상한가"는 다른 것</b>이고,
        /// 그 사이를 메우는 것이 이 표다. 화면 좌상단이 아니라 월드에 두는 이유는
        /// UI 캔버스가 얽히면 아트를 보러 온 씬에 UI 의존이 생기기 때문이다.
        /// </summary>
        private static void BuildReadout(UnityEngine.Camera cam)
        {
            float screenUnitsY = cam.orthographicSize * 2f;
            float screenUnitsX = screenUnitsY * 16f / 9f;
            float bgPixel = BackgroundUpscale;   // 프롭 픽셀 1개당 배경 픽셀 크기 비

            string text = string.Join("\n", new[]
            {
                "stage1 아트 테스트 — 방향키 이동 · Q/E 확대 · R 원위치",
                $"PPU {Ppu:0}  (타일 32px = 1유닛,  플레이어 2유닛 = 64px)",
                $"카메라 ortho {cam.orthographicSize:0.#}  →  화면 {screenUnitsX:0.#} x {screenUnitsY:0.#} 유닛",
                $"배경 160x96 = 5 x 3 유닛  →  x{BackgroundUpscale} 확대해 {5 * BackgroundUpscale} x {3 * BackgroundUpscale} 유닛",
                $"⚠ 그래서 배경 픽셀이 프롭 픽셀보다 {bgPixel:0}배 크다 — 거슬리면 배경을 크게 다시 뽑아야 한다",
                "판정: ① x=0 이음매에서 벽/지면이 갈리는가  ② 배경 픽셀 크기  ③ bg_near 1px  ④ 바닥이 캐릭터를 먹는가",
            });

            CreateLabel("Readout", text, new Vector3(-12.5f, 4.4f, -5f), 0.22f,
                        new Color32(0xBA, 0xC4, 0xC9, 0xFF), TextAnchor.UpperLeft);
        }

        // ───────────────────────────────────────────────────────────── 공용

        private static Sprite LoadSprite(string fileName)
        {
            string path = $"{AbyssPaths.Stage1Art}/{fileName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[ArtTestStageBuilder] 스프라이트 없음: {path}");
            return sprite;
        }

        private static Sprite LoadTile(string fileName)
        {
            string path = $"{AbyssPaths.Stage1Tiles}/{fileName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[ArtTestStageBuilder] 타일 없음: {path}");
            return sprite;
        }

        private static GameObject CreateSprite(Transform parent, string name, Sprite sprite,
                                               Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return go;
        }

        /// <summary>월드 공간 라벨. 캔버스를 안 쓰는 이유는 <see cref="BuildReadout"/> 주석 참조.</summary>
        private static TextMesh CreateLabel(string name, string text, Vector3 position,
                                            float size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = color;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Left;

            var renderer = go.GetComponent<MeshRenderer>();
            if (mesh.font != null) renderer.sharedMaterial = mesh.font.material;
            renderer.sortingOrder = OrderLabel;
            return mesh;
        }
    }
}
#endif
