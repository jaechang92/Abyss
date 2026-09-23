#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Camera;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 로비 바닥·벽(충돌)과 그 위에 입히는 환경 아트.
    ///
    /// 로비는 Stage1 「균열 입구」 세트를 그대로 입는다(사용자 결정 2026-09-23) — 안내자의 첫 대사가
    /// 「심연의 입구다」이고, 로비만의 새 배경은 이미지 생성이 필요하다. 배경 층·타일 그리기 규칙은
    /// <see cref="Stage1EnvironmentWiring"/>의 것을 그대로 불러 쓴다 — 같은 그림을 두 규칙으로 깔면
    /// 확대 배율·정렬이 두 씬에서 조용히 갈라진다.
    ///
    /// 🔴 <b>표시만 한다.</b> 충돌은 <see cref="CreateGround"/>의 그레이박스 오브젝트가 그대로 갖고,
    /// 여기서는 그 렌더러를 끄고 자식이 아닌 별도 루트에 그림을 깐다. Run 과 달리 로비는 빌더가 씬 전체를
    /// 새로 만드므로 런타임 토글(StageEnvironmentPresenter) 없이 빌드 시점에 끈다.
    /// 에셋이 하나라도 없으면 경고만 남기고 그레이박스를 그대로 둔다 — 로비가 비어 보이는 것보다 낫다.
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 바닥 판의 중심과 두께 — 플레이어 스폰 높이를 여기서 파생시킨다.
        // 값을 양쪽에 적어 두면 바닥을 옮길 때 캐릭터만 공중에 남는다.
        private const float FloorCenterY = -3.5f;
        private const float FloorThickness = 1f;
        private static float FloorTopY => FloorCenterY + FloorThickness * 0.5f;

        private const float FloorWidth = 30f;
        private const float WallX = 14f;
        private const float WallHeight = 8f;

        /// <summary>
        /// 바닥 그림은 콜라이더(30)보다 넓게 깐다 — 카메라가 벽 가까이 가면 화면 끝이 바닥 밖(±15)을 비춘다.
        /// 카메라 반폭 10 + 벽 위치 14 를 넘기면 된다.
        /// </summary>
        private const float GroundSkinWidth = 52f;

        /// <summary>바닥 윗줄 아래에 깔 속 타일 줄 수 — 카메라 기준 높이에서 화면 아래(-5.6)가 비지 않을 만큼.</summary>
        private const int GroundInteriorRows = 4;

        /// <summary>장식 소품 정렬 — 배경(-20 이하)보다 앞, 지형(-5)·캐릭터(0)보다 뒤.</summary>
        private const int OrderDecor = -10;

        private const string EnvironmentArtRootName = "EnvironmentArt";

        // 벽 타일은 Stage1 목록에 없다 — 로비에서만 Tiled 로 그린다.
        private static readonly string[] LobbyTiledTexturePaths =
        {
            AbyssPaths.Stage1Tiles + "/wall_top.png",
            AbyssPaths.Stage1Tiles + "/wall_interior.png",
        };

        /// <summary>바닥 위 장식(발밑 피벗). x 는 NPC·포털 자리를 피한다 — 포털 8 · 오른벽 14 사이.</summary>
        private static readonly (string name, float x)[] DecorProps =
        {
            ("prop_landmark_1", 11.5f),
        };

        private static GameObject CreateGround()
        {
            var root = new GameObject("Environment");
            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var sprite = EditorPlatformFactory.LoadWhiteSquare();
            var mat = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();
            var color = EditorPlatformFactory.DefaultPlatformColor;

            EditorPlatformFactory.CreatePlatform(root.transform, "Floor", new Vector2(0f, FloorCenterY), new Vector2(FloorWidth, FloorThickness), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallLeft", new Vector2(-WallX, 0f), new Vector2(1f, WallHeight), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallRight", new Vector2(WallX, 0f), new Vector2(1f, WallHeight), groundLayer, sprite, mat, color);
            return root;
        }

        private static void ApplyEnvironmentArt(GameObject ground, PlayerCameraFollow follow)
        {
            // 재임포트가 먼저다 — 불러 둔 스프라이트 참조가 재임포트 뒤에 낡을 수 있다.
            Stage1EnvironmentWiring.EnsureFullRect();
            EnsureLobbyFullRect();

            var sprites = LoadLobbyEnvironmentSprites();
            if (sprites == null) return;

            // 카메라가 바닥에 선 캐릭터를 잡을 때의 높이 — 배경 그림 중심을 여기에 맞춘다(Stage1 과 같은 규칙).
            float cameraOffsetY = new SerializedObject(follow).FindProperty("offset").vector3Value.y;
            float restCameraY = FloorTopY + cameraOffsetY;

            var root = new GameObject(EnvironmentArtRootName).transform;

            float referenceHeight = sprites["bg_sky"].rect.height;
            Stage1EnvironmentWiring.AddBackground(root, sprites["bg_sky"], Stage1EnvironmentWiring.DepthSky, Stage1EnvironmentWiring.OrderSky, 40f, restCameraY, referenceHeight);
            Stage1EnvironmentWiring.AddBackground(root, sprites["bg_far"], Stage1EnvironmentWiring.DepthFar, Stage1EnvironmentWiring.OrderFar, 30f, restCameraY, referenceHeight);
            Stage1EnvironmentWiring.AddBackground(root, sprites["bg_near"], Stage1EnvironmentWiring.DepthNear, Stage1EnvironmentWiring.OrderNear, 20f, restCameraY, referenceHeight);

            BuildLobbyGroundSkin(root, sprites);
            BuildLobbyWallSkin(root, sprites, -WallX);
            BuildLobbyWallSkin(root, sprites, WallX);
            PlaceDecor(root, sprites);

            // 그림이 다 깔린 뒤에 그레이박스를 끈다. 콜라이더는 그대로다.
            foreach (var graybox in ground.GetComponentsInChildren<SpriteRenderer>(true)) graybox.enabled = false;
        }

        private static void BuildLobbyGroundSkin(Transform root, Dictionary<string, Sprite> sprites)
        {
            int order = Stage1EnvironmentWiring.OrderTerrain;
            Stage1EnvironmentWiring.CreateTiled(root, "GroundTop", sprites["ground_top"], new Vector2(GroundSkinWidth, 1f),
                new Vector2(0f, FloorTopY - 0.5f), order);
            Stage1EnvironmentWiring.CreateTiled(root, "GroundInterior", sprites["ground_interior"], new Vector2(GroundSkinWidth, GroundInteriorRows),
                new Vector2(0f, FloorTopY - 1f - GroundInteriorRows * 0.5f), order);
        }

        /// <summary>벽 콜라이더 사각형(폭 1 · 높이 8 · 중심 y 0)을 정확히 덮는다 — 윗머리 한 칸 + 몸통.</summary>
        private static void BuildLobbyWallSkin(Transform root, Dictionary<string, Sprite> sprites, float x)
        {
            int order = Stage1EnvironmentWiring.OrderTerrain;
            float top = WallHeight * 0.5f;
            float body = WallHeight - 1f;
            string side = x < 0f ? "Left" : "Right";
            Stage1EnvironmentWiring.CreateTiled(root, $"Wall{side}Top", sprites["wall_top"], new Vector2(1f, 1f),
                new Vector2(x, top - 0.5f), order);
            Stage1EnvironmentWiring.CreateTiled(root, $"Wall{side}Body", sprites["wall_interior"], new Vector2(1f, body),
                new Vector2(x, top - 1f - body * 0.5f), order);
        }

        private static void PlaceDecor(Transform root, Dictionary<string, Sprite> sprites)
        {
            foreach (var (name, x) in DecorProps)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                // 발밑(BottomCenter) 피벗 — 바닥 윗면에 선다.
                go.transform.position = new Vector3(x, FloorTopY, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[name];
                sr.sortingOrder = OrderDecor;
            }
        }

        private static Dictionary<string, Sprite> LoadLobbyEnvironmentSprites()
        {
            var paths = new Dictionary<string, string>
            {
                ["bg_sky"] = $"{AbyssPaths.Stage1Art}/bg_sky.png",
                ["bg_far"] = $"{AbyssPaths.Stage1Art}/bg_far.png",
                ["bg_near"] = $"{AbyssPaths.Stage1Art}/bg_near.png",
                ["ground_top"] = $"{AbyssPaths.Stage1Tiles}/ground_top.png",
                ["ground_interior"] = $"{AbyssPaths.Stage1Tiles}/ground_interior.png",
                ["wall_top"] = $"{AbyssPaths.Stage1Tiles}/wall_top.png",
                ["wall_interior"] = $"{AbyssPaths.Stage1Tiles}/wall_interior.png",
            };
            foreach (var (name, _) in DecorProps) paths[name] = $"{AbyssPaths.Stage1Art}/{name}.png";

            var result = paths.ToDictionary(pair => pair.Key, pair => AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value));
            var missing = result.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList();
            if (missing.Count == 0) return result;

            Debug.LogWarning($"[LobbySceneBuilder] 환경 스프라이트 없음 — 그레이박스로 둔다: {string.Join(", ", missing)}");
            return null;
        }

        /// <summary>벽 타일 두 장을 Full Rect 로 — Tiled 반복은 Full Rect 메시여야 한다. 이미 그러면 재임포트하지 않는다.</summary>
        private static void EnsureLobbyFullRect()
        {
            foreach (string path in LobbyTiledTexturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.FullRect) continue;

                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                Debug.Log($"[LobbySceneBuilder] Full Rect 로 재임포트: {path}");
            }
        }
    }
}
#endif
