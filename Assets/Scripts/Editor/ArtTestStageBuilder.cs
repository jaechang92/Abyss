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

        /// <summary>FHD 1920x1080 에 그려질 때의 배율. <see cref="CameraSize"/>가 이 값을 만든다.</summary>
        private const int FhdScale = Abyss.Runtime.Camera.PixelScale.ScreenUpscale;

        /// <summary>
        /// 라벨 폰트 크기. <b>이 값은 글자의 화면 크기가 아니라 글꼴 텍스처의 해상도다</b> —
        /// 크게 둘수록 선명하고, 실제 크기는 <c>characterSize</c>가 정한다.
        /// 그래서 이 숫자는 안 만지고 <see cref="CharacterSizeFor"/>로 월드 높이를 준다.
        /// </summary>
        private const int LabelFontSize = 64;

        /// <summary>
        /// 카메라 세로 반높이. <b>FHD 1920x1080에서 정수 배율이 나오는 값이다</b> —
        /// 11.25유닛 x PPU 32 = 360 아트px 이고 1080/360 = 정확히 3배.
        ///
        /// 🔴 <b>이 숫자는 취향이 아니라 산수다.</b> 옛 값 ortho 5 면 세로가 320 아트px 이라
        /// 1080/320 = 3.375배 — 정수가 아니라서 <b>픽셀이 어떤 줄은 3배, 어떤 줄은 4배로
        /// 그려진다.</b> 도트가 고르지 않게 보이는데 원인은 그림이 아니다.
        ///
        /// 🔴 2026-09-10 — <b>값을 여기서 뺐다.</b> 이 파일이 5.625 를 먼저 알아냈는데 Run·Lobby·Title
        /// 세 씬은 5 로 남아 있었다. <b>테스트 씬만 옳은 배율로 보고 있었다는 뜻이다</b> —
        /// 아트를 판정하던 화면과 게임 화면이 서로 다른 배율이었다.
        /// 이제 <see cref="Abyss.Runtime.Camera.PixelScale"/> 가 SoT 다.
        /// </summary>
        private const float CameraSize = Abyss.Runtime.Camera.PixelScale.OrthographicSize;

        /// <summary>지면 윗줄 타일의 중심 y. 윗면(밟는 자리)은 여기서 +0.5다.</summary>
        private const float GroundRowY = -3f;
        private const float SurfaceY = GroundRowY + 0.5f;

        private const float GroundMinX = -52f;
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
            float artPixelsY = screenUnitsY * Ppu;
            float artPixelsX = screenUnitsX * Ppu;
            float bgPixel = BackgroundUpscale;   // 프롭 픽셀 1개당 배경 픽셀 크기 비

            string text = string.Join("\n", new[]
            {
                "stage1 아트 테스트 — 방향키 이동 · Q/E 확대 · R 원위치",
                $"PPU {Ppu:0}  ·  타일 32px = 1유닛  ·  플레이어 2유닛 = 64px",
                $"화면 {screenUnitsX:0.##} x {screenUnitsY:0.##} 유닛 = {artPixelsX:0} x {artPixelsY:0} 아트px",
                $"FHD 1920x1080 에서 정확히 x{FhdScale} — 정수라서 픽셀이 고르게 그려진다 (ortho {cam.orthographicSize:0.###})",
                $"배경 160x96 을 x{BackgroundUpscale} 확대 → 640x384 아트px (가로 딱 맞고 세로 24px 여유)",
                $"⚠ 배경 1px = {BackgroundUpscale * FhdScale} 스크린px,  프롭 1px = {FhdScale} 스크린px — {bgPixel:0}배 차이",
                "판정: ① x=0 이음매에서 벽/지면이 갈리는가  ② bg_near 1px  ③ 바닥이 캐릭터를 먹는가(왼쪽 끝 폼 4종 · 벽 위 1종)",
            });

            CreateLabel("Readout", text, new Vector3(-9.6f, 5.2f, -5f), 0.32f,
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

        /// <summary>
        /// 폼 캐릭터 스프라이트. <b>바닥이 캐릭터를 먹는지</b>는 진짜 실루엣이 있어야 판정된다 —
        /// 2유닛 표지는 축척은 말해도 「읽히는가」는 못 말한다(2026-09-09 사용자 지적).
        /// </summary>
        private static Sprite LoadForm(string fileName)
        {
            string path = $"{AbyssPaths.FormSprites}/{fileName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[ArtTestStageBuilder] 폼 스프라이트 없음: {path}");
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

        /// <summary>
        /// <c>TextMesh</c>의 한 줄 높이(월드 유닛) → <c>characterSize</c>.
        ///
        /// 🔴 <b>2026-09-09에 이걸 안 해서 라벨이 화면을 덮었다.</b> <c>characterSize</c>를
        /// 「크기」로 읽고 0.22 같은 값을 그대로 넘겼는데, 실제 한 줄 높이는
        /// <c>fontSize x characterSize / 10</c> = <b>1.4유닛</b>이었다 — 화면 세로가
        /// 11.25유닛이니 <b>한 줄이 화면의 1/8</b>이다.
        ///
        /// 📌 <c>characterSize</c>는 크기가 아니라 <b>배율</b>이고, 배율은 무엇에 대한 배율인지를
        /// 모르면 값을 못 고른다. 그래서 부르는 쪽은 언제나 <b>월드 높이</b>로 말한다.
        /// (10은 Unity가 fontSize 1점을 월드 0.1유닛으로 잡는 데서 온다 — 근사값이다.)
        /// </summary>
        private static float CharacterSizeFor(float worldLineHeight)
            => worldLineHeight * 10f / LabelFontSize;

        /// <summary>
        /// 월드 공간 라벨. <paramref name="lineHeight"/>는 <b>한 줄의 월드 높이(유닛)</b>다.
        /// 캔버스를 안 쓰는 이유는 <see cref="BuildReadout"/> 주석 참조.
        /// </summary>
        private static TextMesh CreateLabel(string name, string text, Vector3 position,
                                            float lineHeight, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            mesh.fontSize = LabelFontSize;
            mesh.characterSize = CharacterSizeFor(lineHeight);
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
