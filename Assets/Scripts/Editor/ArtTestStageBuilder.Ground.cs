#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 테스트 씬의 <b>바닥·벽·프롭</b> 배치. 진입점과 배경은 <c>ArtTestStageBuilder.cs</c>에 있다.
    ///
    /// 🔴 <b>배치가 곧 질문이다.</b> 지면 띠와 벽 띠를 <c>x=0</c>에서 <b>맞대 놓은 것</b>이
    /// ⑪("벽과 지면이 갈리는가")를 묻는 방식이고, 재질 견본 두 장을 나란히 띄운 것이
    /// 같은 질문을 걸어 다니지 않고 묻는 방식이다. 둘 다 필요하다 —
    /// <b>나란히 놓으면 다르고 걸어서 지나가면 같은</b> 경우가 실제로 있다.
    /// </summary>
    internal static partial class ArtTestStageBuilder
    {
        /// <summary>바닥 속을 몇 줄 깔 것인가. 카메라 아래로 안 비면 된다.</summary>
        private const int GroundDepthRows = 3;

        // ───────────────────────────────────────────────────────────── 바닥·벽

        private static void BuildGroundAndWall()
        {
            var root = new GameObject("Terrain").transform;

            LayStrip(root, "Ground", GroundMinX, SeamX, "ground");
            LayStrip(root, "WallMaterial", SeamX, WallMaxX, "wall");

            CreateLabel("SeamLabel",
                        "◀ 지면 타일          이음매          벽 타일 ▶\n같은 높이로 맞대 놓았다 — 걸어서 지나가며 재질이 바뀌는지 본다",
                        new Vector3(SeamX, SurfaceY + 2.4f, -5f), 0.2f,
                        new Color32(0xD2, 0xD9, 0xDB, 0xFF), TextAnchor.LowerCenter);

            BuildVerticalWall(root);
            BuildMaterialSwatches(root);
        }

        /// <summary>
        /// 윗줄은 <c>*_top</c>(위 두 모서리가 표면 재질인 wang_12), 아래는 <c>*_interior</c>(wang_0).
        /// 타일 한 칸이 1유닛이라 x를 정수로 놓고 중심을 +0.5 한다.
        /// </summary>
        private static void LayStrip(Transform parent, string name, float fromX, float toX, string kind)
        {
            var top = LoadTile($"{kind}_top");
            var interior = LoadTile($"{kind}_interior");
            if (top == null || interior == null) return;

            var strip = new GameObject(name).transform;
            strip.SetParent(parent, false);

            for (float x = fromX; x < toX; x += 1f)
            {
                CreateSprite(strip, $"{kind}_top_{x:0}", top,
                             new Vector3(x + 0.5f, GroundRowY, 0f), OrderTiles);

                for (int row = 1; row <= GroundDepthRows; row++)
                    CreateSprite(strip, $"{kind}_fill_{x:0}_{row}", interior,
                                 new Vector3(x + 0.5f, GroundRowY - row, 0f), OrderTiles);
            }
        }

        /// <summary>
        /// 벽을 <b>세워서도</b> 본다. 벽 타일은 바닥으로 깔면 그럴듯해도, 세로 면으로 세우면
        /// 이음매 방향이 달라져 무늬가 안 맞는 일이 있다 — 그건 눕혀 놓고는 안 보인다.
        /// </summary>
        private static void BuildVerticalWall(Transform parent)
        {
            var interior = LoadTile("wall_interior");
            var top = LoadTile("wall_top");
            if (interior == null || top == null) return;

            var wall = new GameObject("VerticalWall").transform;
            wall.SetParent(parent, false);

            const float wallX = WallMaxX + 1f;
            const int height = 6;

            for (int col = 0; col < 2; col++)
            {
                for (int row = 0; row < height; row++)
                    CreateSprite(wall, $"wall_{col}_{row}", interior,
                                 new Vector3(wallX + col, SurfaceY + 0.5f + row, 0f), OrderTiles);

                CreateSprite(wall, $"wall_cap_{col}", top,
                             new Vector3(wallX + col, SurfaceY + 0.5f + height, 0f), OrderTiles);
            }

            CreateLabel("WallLabel", "세운 벽 — 무늬 방향이 맞는가",
                        new Vector3(wallX + 1f, SurfaceY + height + 2f, -5f), 0.2f,
                        new Color32(0x99, 0xA6, 0xAC, 0xFF), TextAnchor.LowerCenter);
        }

        /// <summary>
        /// 재질 견본 — 지면·벽의 표면 타일을 <b>맞붙여</b> 띄운다.
        /// 걸어 다니며 보는 것과 나란히 보는 것은 다른 판정이라 둘 다 둔다.
        /// </summary>
        private static void BuildMaterialSwatches(Transform parent)
        {
            var swatch = new GameObject("Swatches").transform;
            swatch.SetParent(parent, false);

            var pairs = new[]
            {
                ("ground_surface", -1.5f), ("wall_surface", -0.5f),
                ("ground_top", 0.7f), ("wall_top", 1.7f),
                ("ground_interior", 2.9f), ("wall_interior", 3.9f),
            };

            const float y = 2.2f;
            foreach ((string tile, float offset) in pairs)
            {
                var sprite = LoadTile(tile);
                if (sprite == null) continue;

                var go = CreateSprite(swatch, tile, sprite, new Vector3(SeamX + 4f + offset, y, 0f), OrderSheets);
                go.transform.localScale = Vector3.one * 2f;   // 32px 타일은 그냥 보기엔 작다
            }

            CreateLabel("SwatchLabel",
                        "재질 견본 — 왼쪽이 지면, 오른쪽이 벽 (surface / top / interior 짝)",
                        new Vector3(SeamX + 5.5f, y + 1.6f, -5f), 0.2f,
                        new Color32(0x99, 0xA6, 0xAC, 0xFF), TextAnchor.LowerCenter);
        }

        // ───────────────────────────────────────────────────────────── 프롭

        /// <summary>
        /// 프롭은 전부 <b>발밑 피벗</b>이라 y를 바닥 윗면에 맞추면 그대로 선다
        /// (<see cref="EnvironmentArtImporter"/>). 뜨거나 파묻히면 피벗이 틀린 것이다.
        /// </summary>
        private static void BuildProps()
        {
            var root = new GameObject("Props").transform;

            PlaceRow(root, "Landmarks", new[]
            {
                "prop_landmark_1", "prop_landmark_2", "prop_landmark_3", "prop_landmark_4",
            }, startX: -41f, step: 3.5f);

            PlaceRow(root, "Interactive", new[]
            {
                "prop_interactive_1_1", "prop_interactive_1_2",
                "prop_interactive_2_1", "prop_interactive_2_2",
                "prop_interactive_3_1", "prop_interactive_3_2",
            }, startX: -27f, step: 2.2f);

            PlaceRow(root, "Decor", new[]
            {
                "prop_decor_1", "prop_decor_2", "prop_decor_3", "prop_decor_4", "prop_decor_5",
            }, startX: -13f, step: 1.8f);

            PlaceRow(root, "Hazards", new[]
            {
                "prop_hazard_1", "prop_hazard_2", "prop_hazard_3",
            }, startX: -4.4f, step: 1.8f);

            // 발판은 공중에 뜬 것이 정상이다 — "nothing holding it up"이 문구에 있다.
            var platforms = new GameObject("Platforms").transform;
            platforms.SetParent(root, false);
            string[] tiles = { "tile_platform_1", "tile_platform_2", "tile_platform_3" };
            for (int i = 0; i < tiles.Length; i++)
            {
                var sprite = LoadSprite(tiles[i]);
                if (sprite == null) continue;
                CreateSprite(platforms, tiles[i], sprite,
                             new Vector3(-20f + i * 2.2f, SurfaceY + 2.2f, 0f), OrderProps);
            }
        }

        private static void PlaceRow(Transform parent, string groupName, string[] names,
                                     float startX, float step)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(parent, false);

            for (int i = 0; i < names.Length; i++)
            {
                var sprite = LoadSprite(names[i]);
                if (sprite == null) continue;
                CreateSprite(group, names[i], sprite,
                             new Vector3(startX + i * step, SurfaceY, 0f), OrderProps);
            }

            CreateLabel($"{groupName}Label", groupName,
                        new Vector3(startX - 0.4f, SurfaceY - 0.7f, -5f), 0.16f,
                        new Color32(0x7C, 0x8A, 0x8C, 0xFF), TextAnchor.UpperLeft);
        }

        // ───────────────────────────────────────────────────────────── 타일시트·축척

        /// <summary>16타일 시트를 통째로 띄운다 — 안 쓴 타일까지 한눈에 본다.</summary>
        private static void BuildTilesetSheets()
        {
            var root = new GameObject("TilesetSheets").transform;

            var ground = LoadSprite("tileset_ground");
            var wall = LoadSprite("tileset_wall");
            if (ground != null) CreateSprite(root, "sheet_ground", ground, new Vector3(4f, 7.5f, 0f), OrderSheets);
            if (wall != null) CreateSprite(root, "sheet_wall", wall, new Vector3(9f, 7.5f, 0f), OrderSheets);

            CreateLabel("SheetLabel", "지면 시트 / 벽 시트 (각 128px = 4유닛, 32px 타일 16장)",
                        new Vector3(6.5f, 10f, -5f), 0.2f,
                        new Color32(0x99, 0xA6, 0xAC, 0xFF), TextAnchor.LowerCenter);
        }

        /// <summary>
        /// 축척 표지 — <b>2유닛</b>. 플레이어 콜라이더 높이이고, PPU 32에서 64px이다.
        /// 🔑 캐릭터를 안 띄우는 대신 이것이 "사람이 여기 서면 이만하다"를 말한다.
        /// 바닥 타일이 램프 위 3단을 7.9%나 쓰고 있어 <b>실루엣이 바닥에 묻히는지</b>를
        /// 이 표지 옆에서 본다.
        /// </summary>
        private static void BuildScaleReference()
        {
            var square = EditorPlatformFactory.LoadWhiteSquare();
            if (square == null) return;

            var go = CreateSprite(null, "ScaleReference_2units", square,
                                  new Vector3(-44f, SurfaceY + 1f, 0f), OrderProps);

            Vector3 unit = square.bounds.size;
            go.transform.localScale = new Vector3(
                unit.x > 0f ? 1f / unit.x : 1f,
                unit.y > 0f ? 2f / unit.y : 1f,
                1f);

            // 램프 위 3단 중 가장 밝은 색 — 배경이 절대 안 쓰는 대역이라 대비 기준이 된다.
            go.GetComponent<SpriteRenderer>().color = new Color32(0xD2, 0xD9, 0xDB, 0xFF);

            CreateLabel("ScaleLabel",
                        "2 유닛 = 플레이어 키 = 64px\n(색은 램프 위 3단 — 배경이 안 쓰는 대역)",
                        new Vector3(-44f, SurfaceY + 2.4f, -5f), 0.18f,
                        new Color32(0xD2, 0xD9, 0xDB, 0xFF), TextAnchor.LowerCenter);
        }
    }
}
#endif
