#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Camera;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// Stage1 채택 아트(2026-09-30 사용자 선택 v1 방향) 규격 · 임포트 · 배치 도구. 적용 진입점은 <c>AllRooms.cs</c>(Stage1 전 방).
    ///
    /// 채택 아트는 같은 2172x724 캔버스에서 나눈 세 장이다 — 배경 한 장 · 지면 한 장 · 발판 석판 한 장.
    /// 🔑 <b>원본 픽셀은 바꾸지 않는다.</b> 자르기·축소 대신 sprite rect(사용 영역)·피벗·테두리(9-slice)·표시 배율로 충돌선에 맞춘다.
    /// <list type="bullet">
    /// <item>배경 — 한 장 + 약한 시차(반복 없음). 모든 Stage1 방(보스 포함)이 같은 그림을 쓴다.</item>
    /// <item>지면 — 공용 지면 콜라이더 폭 그대로 한 장.</item>
    /// <item>테라스 — 지면 그림의 <b>같은 사용 영역에 테두리를 준 두 번째 sprite</b>(<c>terrace</c>). 윗단 벽돌 줄(64px)과 좌우 끝 벽돌(124·115px)을
    /// 고정하고 가운데 돌 채움을 반복(Tiled)한다 — 수직 측면·윗면이 원본 그대로 나온다. 표시 밀도 48px/유닛(배경과 같다).</item>
    /// <item>석판 — 석판 한 장을 폭에 맞춰 같은 비율로 줄인다(대표방에서 사용자가 확인한 방식). 받침은 윗선 아래 장식이다.</item>
    /// </list>
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        public const string RoomArtFolder = AbyssPaths.EnvironmentArt + "/stage1_room3_crowd";
        public const string StageArtRootName = "RoomArt_Stage1";
        /// <summary>이전 대표방 전용 적용(Room3_Crowd)이 만든 루트 이름 접두어 — 전 방 적용·복원이 함께 지운다.</summary>
        private const string RoomArtRootPrefix = "RoomArt_";

        private const string RoomArtLog = "[Stage1RoomArt]";
        private const string GroundSkinName = "GroundSkin";
        private const string RoomArtBackgroundName = "Background";
        private const string RoomArtGroundName = "Ground";
        internal const string TerraceArtName = "RoomArtTerrace";
        internal const string SlabArtName = "RoomArtSlab";

        /// <summary>테라스 표시 밀도 — 원본 48px = 1유닛(배경과 같다 · FHD 화면 2px/원본px).</summary>
        internal const float TerracePixelsPerUnit = 48f;
        /// <summary>테라스 sprite 윗 테두리 = 지면 그림의 윗단 벽돌 줄(위쪽 기준 y 261..324). 이보다 낮은 테라스는 그림이 지면 속으로 내려간다.</summary>
        internal const int TerraceCapPx = 64;
        internal const int TerraceLeftPx = 124;    // 왼쪽 끝 벽돌 + 이음 줄(x 0..123)
        internal const int TerraceRightPx = 115;   // 오른쪽 끝 벽돌 + 이음 줄(x 2057..2171)
        internal static float TerraceCapUnits => TerraceCapPx / TerracePixelsPerUnit;

        /// <summary>채택 아트 파일 한 장 — 원본 크기 검사와 실질 alpha 경계 검사 대상.</summary>
        internal readonly struct RoomArtFile
        {
            public readonly string Key;
            public readonly string AssetId;
            public readonly string Role;
            public readonly RectInt UsedRect;
            public readonly bool HasAlpha;

            public RoomArtFile(string key, string assetId, string role, RectInt usedRect, bool hasAlpha)
            {
                Key = key;
                AssetId = assetId;
                Role = role;
                UsedRect = usedRect;
                HasAlpha = hasAlpha;
            }

            public string Path => $"{RoomArtFolder}/{Key}.png";

            /// <summary>원본 전체를 한 장으로 쓰는가(배경 — Single). 아니면 사용 영역 sprite 를 가진 Multiple.</summary>
            public bool IsWhole => UsedRect.x == 0 && UsedRect.y == 0 && UsedRect.width == RoomArtSourceWidth && UsedRect.height == RoomArtSourceHeight;
        }

        /// <summary>한 파일 안의 sprite 하나 — 이름·사용 영역(Unity 아래 원점)·피벗·테두리(왼·아래·오른·위 px).</summary>
        internal readonly struct RoomArtSprite
        {
            public readonly string Name;
            public readonly string FileKey;
            public readonly RectInt Rect;
            public readonly Vector2 Pivot;
            public readonly Vector4 Border;

            public RoomArtSprite(string name, string fileKey, RectInt rect, Vector2 pivot, Vector4 border)
            {
                Name = name;
                FileKey = fileKey;
                Rect = rect;
                Pivot = pivot;
                Border = border;
            }
        }

        internal const string KeyBackground = "background";
        internal const string KeyGround = "ground";
        internal const string KeyPlatform = "platform";
        internal const string KeyTerrace = "terrace";

        private static readonly RectInt GroundUsedRect = new(0, 122, 2172, 341);
        private static readonly RectInt PlatformUsedRect = new(44, 178, 2085, 316);

        /// <summary>
        /// 원본 이미지 위쪽 기준 실질 bbox(alpha ≥ 128) → Unity 아래 원점 rect: y = 724 − 아래끝.
        /// 지면 y 261..602 → (0,122,2172,341) · 발판 x 44..2129, y 230..546 → (44,178,2085,316).
        /// </summary>
        internal static readonly RoomArtFile[] RoomArtFiles =
        {
            new(KeyBackground, "S1-B01", "합성 배경 한 장 · 약한 시차", new RectInt(0, 0, RoomArtSourceWidth, RoomArtSourceHeight), false),
            new(KeyGround, "S1-F01", "지면·테라스(사용 영역 윗선 = 충돌 윗면)", GroundUsedRect, true),
            new(KeyPlatform, "S1-P01", "발판 석판(상판 윗선 = 충돌 윗면, 받침은 장식)", PlatformUsedRect, true),
        };

        /// <summary>임포트 뒤 있어야 하는 sprite. 피벗은 전부 윗변 가운데(배경만 중앙) — 그림 윗선을 충돌 윗면 한 점에 놓는다.</summary>
        internal static readonly RoomArtSprite[] RoomArtSprites =
        {
            new(KeyBackground, KeyBackground, new RectInt(0, 0, RoomArtSourceWidth, RoomArtSourceHeight), new Vector2(0.5f, 0.5f), Vector4.zero),
            new(KeyGround, KeyGround, GroundUsedRect, new Vector2(0.5f, 1f), Vector4.zero),
            new(KeyTerrace, KeyGround, GroundUsedRect, new Vector2(0.5f, 1f), new Vector4(TerraceLeftPx, 0f, TerraceRightPx, TerraceCapPx)),
            new(KeyPlatform, KeyPlatform, PlatformUsedRect, new Vector2(0.5f, 1f), Vector4.zero),
        };

        // ───────────────────────────────────────────────────────────── 임포트

        /// <summary>
        /// 채택 아트 임포트 규약 — PPU 32 · Point · 무압축 · 밉맵 끔 · 최대 4096(원본 2172 보존) · NPOT 그대로 · Full Rect(Tiled 필수).
        /// 배경은 Single 중앙 피벗, 지면·발판은 <see cref="RoomArtSprites"/> 목록 그대로의 Multiple. 이미 같으면 재임포트하지 않는다.
        /// </summary>
        private static void EnsureRoomArtImport()
        {
            foreach (var file in RoomArtFiles)
            {
                if (AssetImporter.GetAtPath(file.Path) is not TextureImporter importer) continue;

                var mode = file.IsWhole ? SpriteImportMode.Single : SpriteImportMode.Multiple;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool isChanged =
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != mode ||
                    !Mathf.Approximately(importer.spritePixelsPerUnit, Ppu) ||
                    importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.mipmapEnabled ||
                    importer.maxTextureSize < RoomArtMaxTexturePx ||
                    importer.npotScale != TextureImporterNPOTScale.None ||
                    importer.wrapMode != TextureWrapMode.Clamp ||
                    !importer.alphaIsTransparency ||
                    settings.spriteMeshType != SpriteMeshType.FullRect ||
                    (file.IsWhole && settings.spriteAlignment != (int)SpriteAlignment.Center);

                if (isChanged)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = mode;
                    importer.spritePixelsPerUnit = Ppu;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = RoomArtMaxTexturePx;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;

                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    if (file.IsWhole) settings.spriteAlignment = (int)SpriteAlignment.Center;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    Debug.Log($"{RoomArtLog} 임포트 규약 적용: {file.Path}");
                }

                if (!file.IsWhole) EnsureSpriteRects(importer, file);
            }
        }

        /// <summary>Multiple 스프라이트 rect 를 규격 목록으로 맞춘다(2D Sprite 패키지 경로 — spritesheet 는 Unity 6 CS0618). 기존 spriteID 는 유지한다.</summary>
        private static void EnsureSpriteRects(TextureImporter importer, RoomArtFile file)
        {
            var wanted = RoomArtSprites.Where(s => s.FileKey == file.Key).ToList();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var existing = provider.GetSpriteRects();
            bool isSame = existing.Length == wanted.Count && wanted.All(w => existing.Any(e =>
                e.name == w.Name && e.rect == ToRect(w.Rect) && e.alignment == SpriteAlignment.Custom && e.pivot == w.Pivot && e.border == w.Border));
            if (isSame) return;

            var rects = wanted.Select(w => new SpriteRect
            {
                name = w.Name,
                spriteID = existing.FirstOrDefault(e => e.name == w.Name)?.spriteID ?? GUID.Generate(),
                rect = ToRect(w.Rect),
                alignment = SpriteAlignment.Custom,
                pivot = w.Pivot,
                border = w.Border,
            }).ToArray();

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToArray());
            provider.Apply();
            importer.SaveAndReimport();
            Debug.Log($"{RoomArtLog} sprite rect 적용: {file.Path} — {string.Join(", ", wanted.Select(w => $"{w.Name} {w.Rect} 테두리 {w.Border}"))}");
        }

        private static Rect ToRect(RectInt r) => new(r.x, r.y, r.width, r.height);

        /// <summary>임포트 뒤 sprite 하나 — 배경은 주 스프라이트, 나머지는 이름이 같은 하위 스프라이트.</summary>
        private static Sprite LoadRoomArtSprite(RoomArtSprite spec)
        {
            var file = RoomArtFiles.First(f => f.Key == spec.FileKey);
            return file.IsWhole
                ? AssetDatabase.LoadAssetAtPath<Sprite>(file.Path)
                : AssetDatabase.LoadAllAssetsAtPath(file.Path).OfType<Sprite>().FirstOrDefault(s => s.name == spec.Name);
        }

        // ───────────────────────────────────────────────────────────── 배치

        /// <summary>Stage1 공용 방 아트 루트 — 배경 한 장 + 지면 한 장. 테라스·석판 그림은 각 방 지형의 스킨이다.</summary>
        private static GameObject BuildStageArtRoot(Transform envRoot, Dictionary<string, Sprite> sprites, Rect groundRect, float restCameraY)
        {
            var artRoot = CreateChild(envRoot, StageArtRootName);
            artRoot.localPosition = Vector3.zero;

            AddRoomArtBackground(artRoot, sprites[KeyBackground], restCameraY);
            // 지면 — 사용 영역 윗선을 충돌 윗면에, 가로는 충돌 폭 그대로. 세로는 같은 배율(비율 보존)이라 원본 두께를 따른다.
            PlaceScaled(artRoot, RoomArtGroundName, sprites[KeyGround], new Vector2(groundRect.center.x, groundRect.yMax), groundRect.width);
            return artRoot.gameObject;
        }

        /// <summary>
        /// 합성 배경 한 장 + 약한 시차. 반복하지 않는다(wrap 0) — 폭·높이가 카메라 이동 전체를 덮는지는 검증이 계산한다.
        /// y 는 기존 층과 같은 규약 — 지면 위 기준 카메라 위치에서 그림 중심이 화면 중심(base = depth·기준y). x 는 방 중심 0.
        /// </summary>
        private static void AddRoomArtBackground(Transform parent, Sprite sprite, float restCameraY)
        {
            var layer = new GameObject(RoomArtBackgroundName);
            layer.transform.SetParent(parent, false);
            layer.transform.position = new Vector3(0f, RoomArtBackgroundDepth * restCameraY, 30f);
            layer.transform.localScale = Vector3.one * (Ppu / RoomArtBackgroundPixelsPerUnit);

            var sr = layer.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = OrderFar;

            layer.AddComponent<ParallaxLayer>().Configure(RoomArtBackgroundDepth, 0f);
        }

        /// <summary>윗변 가운데 피벗 스프라이트를 한 점에 놓고 가로 폭이 <paramref name="width"/> 가 되게 같은 비율로 키운다. 부모 월드 배율은 1이어야 한다.</summary>
        private static void PlaceScaled(Transform parent, string name, Sprite sprite, Vector2 topCenter, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(topCenter.x, topCenter.y, 0f);
            go.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = OrderTerrain;
        }

        /// <summary>
        /// 지형 하나에 새 아트 스킨(<c>Stage1Skin</c> — 기존 C2 스킨 규약 그대로 발판의 자식, 부모 배율을 되돌린 월드 배율 1).
        /// 테라스(높이 ≥ 석판 두께 초과)는 9-slice Tiled — 폭·윗선이 콜라이더와 같고, 윗단 줄보다 낮으면 그림이 지면 쪽으로 내려간다.
        /// 석판은 한 장을 폭에 맞춰 줄인다.
        /// </summary>
        private static GameObject BuildTerrainSkin(Transform platform, Dictionary<string, Sprite> sprites, bool isTerrace)
        {
            var rect = WorldRect(platform.GetComponent<BoxCollider2D>());
            Vector3 lossy = platform.lossyScale;
            if (Mathf.Approximately(lossy.x, 0f) || Mathf.Approximately(lossy.y, 0f)) return null;

            var skin = new GameObject(SkinName);
            skin.transform.SetParent(platform, false);
            skin.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f);
            skin.transform.position = platform.position;

            if (!isTerrace)
            {
                PlaceScaled(skin.transform, SlabArtName, sprites[KeyPlatform], new Vector2(rect.center.x, rect.yMax), rect.width);
                skin.transform.GetChild(0).GetComponent<SpriteRenderer>().sortingOrder = OrderTerrain + 1;
                return skin;
            }

            float scale = Ppu / TerracePixelsPerUnit;
            float drawnHeight = Mathf.Max(rect.height, TerraceCapUnits);
            var go = new GameObject(TerraceArtName);
            go.transform.SetParent(skin.transform, false);
            go.transform.position = new Vector3(rect.center.x, rect.yMax, 0f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[KeyTerrace];
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(rect.width / scale, drawnHeight / scale);
            // 지면(-5) 위에 — 지면 속으로 내려간 부분이 지면 윗줄을 덮어 「지면 위에 놓인 단」으로 읽힌다.
            sr.sortingOrder = OrderTerrain + 1;
            return skin;
        }

        /// <summary>적용 결과 크기·픽셀 밀도 요약(로그용).</summary>
        private static string DescribeRoomArt(Transform artRoot)
        {
            var lines = new List<string>();
            foreach (var sr in artRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var d = DrawnRect(sr);
                float density = sr.drawMode == SpriteDrawMode.Simple ? sr.sprite.rect.width / d.width : TerracePixelsPerUnit;
                lines.Add($"  {sr.name}: {d.width:0.###}x{d.height:0.###}유닛 {Fmt(d)} · 원본 {density:0.##}px/유닛");
            }
            return string.Join("\n", lines);
        }
    }
}
#endif
