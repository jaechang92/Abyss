#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abyss.Runtime.Camera;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 채택 아트 규격 검사(원본 파일·Unity 임포트)와 그림/충돌 일치·배경 덮임 계산. 씬을 열거나 저장하지 않는 순수 검사만 둔다 —
    /// 씬 검증 진입점은 <c>AllRoomsValidate.cs</c>. 로그 접두어 <c>[S1-ROOMART-FILE]</c>(원본 파일) · <c>[S1-ROOMART-IMPORT]</c>(Unity 가 읽은 스프라이트).
    ///
    /// 카메라 값은 구조 조사 보고서 §4 와 <c>StageDirector.Map.cs</c> 경계: 화면 11.25유닛 높이 · 16:9 · 지면 윗면 -0.5 ·
    /// 추적 offset +2 · 맵 방 카메라 경계 x ±(길이/2 + 벽 1), y 하한 = 지면 − 5.5.
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        /// <summary>채택 아트 원본 크기 — 세 장 모두 같은 캔버스다. 다르면 사용 영역 rect 가 무의미해지므로 보류한다.</summary>
        internal const int RoomArtSourceWidth = 2172;
        internal const int RoomArtSourceHeight = 724;

        /// <summary>임포트 최대 크기. 원본 2172 를 줄이지 않는다(EnvironmentArtImporter 의 2048 에서 이 폴더만 제외).</summary>
        internal const int RoomArtMaxTexturePx = 4096;

        /// <summary>합성 배경 시차 깊이(0 = 화면에 붙음). 대표방에서 사용자가 확인한 값 0.15.</summary>
        internal const float RoomArtBackgroundDepth = 0.15f;

        /// <summary>
        /// 배경 표시 밀도 — 원본 48px = 1유닛. FHD(1080/11.25 = 화면 96px/유닛)에서 원본 1px = 화면 정확히 2px 이다.
        /// → 45.25 x 15.08유닛. 지면(45.6 폭 → 47.63px/유닛)과 0.8% 차이라 같은 캔버스의 픽셀 크기가 거의 같게 보인다.
        /// </summary>
        internal const float RoomArtBackgroundPixelsPerUnit = 48f;

        /// <summary>실질 영역 판정 alpha(원본 bbox 측정과 같은 기준 128).</summary>
        private const byte RoomArtSolidAlpha = 128;
        private const float RoomArtMinGroundDepth = 5.5f;
        private const float RoomArtScreenAspect = 16f / 9f;

        // StageDirector.Map.cs 맵 방 카메라 경계 상수(private)를 그대로 옮긴다 — 값이 바뀌면 여기도 바꾼다.
        private const float RoomArtWallThickness = 1f;
        private const float RoomArtCameraBelowGround = 5.5f;

        // ───────────────────────────────────────────────────────────── 파일 규격

        /// <summary>
        /// 원본 PNG 를 임포트 설정과 무관하게 직접 읽는다 — 크기가 2172x724 이고, alpha ≥ 128 실질 경계가 사용 영역 rect 와 정확히 같은가.
        /// rect 밖의 희미한 alpha(1..127)는 센 값만 남긴다(지운 것이 아니라 쓰지 않는다).
        /// </summary>
        internal static void CheckRoomArtFiles(C2Report report)
        {
            foreach (var file in RoomArtFiles)
            {
                string label = $"{file.AssetId} {file.Key}.png";
                if (!File.Exists(file.Path))
                {
                    report.Check(label, false, $"없음 — {file.Path} ({file.Role})");
                    continue;
                }
                if (!TryReadPng(file.Path, out int width, out int height, out RectInt solid, out int faintOutside, out bool isOpaque))
                {
                    report.Check(label, false, $"PNG 로 읽히지 않음 — {file.Path}");
                    continue;
                }

                string size = $"{width}x{height}px";
                if (width != RoomArtSourceWidth || height != RoomArtSourceHeight)
                {
                    report.Check(label, false, $"{size} · 원본 {RoomArtSourceWidth}x{RoomArtSourceHeight}px 필요(자르기·축소 금지)");
                    continue;
                }

                if (!file.HasAlpha)
                {
                    report.Check(label, isOpaque, $"{size} · 불투명 한 장 {(isOpaque ? "" : "— 투명 픽셀 있음")}");
                    continue;
                }

                bool isMatch = solid.Equals(file.UsedRect);
                report.Check(label, isMatch,
                             $"{size} · 실질 alpha≥{RoomArtSolidAlpha} 경계 {FmtRect(solid)} vs 사용 영역 {FmtRect(file.UsedRect)} · " +
                             $"사용 영역 밖 희미한 alpha {faintOutside}px(제외, 미편집)");
            }
        }

        /// <summary>
        /// Unity 가 읽은 결과가 규격과 같은가 — 텍스처가 줄지 않았고(2172x724), sprite rect·피벗·테두리가 목록과 같다.
        /// 모두 맞으면 이름 → 스프라이트, 하나라도 다르면 null.
        /// </summary>
        internal static Dictionary<string, Sprite> CheckRoomArtImport(C2Report report)
        {
            var result = new Dictionary<string, Sprite>();
            bool isOk = true;
            foreach (var spec in RoomArtSprites)
            {
                var file = RoomArtFiles.First(f => f.Key == spec.FileKey);
                string label = $"{file.AssetId} {spec.Name} 임포트";
                var sprite = File.Exists(file.Path) ? LoadRoomArtSprite(spec) : null;
                if (sprite == null)
                {
                    report.Check(label, false, $"스프라이트로 읽히지 않음 — {file.Path}#{spec.Name}(적용 버튼이 임포트 규약을 맞춘다)");
                    isOk = false;
                    continue;
                }

                var texture = sprite.texture;
                var wantRect = ToRect(spec.Rect);
                Vector2 pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
                bool isMatch = texture.width == RoomArtSourceWidth && texture.height == RoomArtSourceHeight &&
                               sprite.rect == wantRect && Near(pivot.x, spec.Pivot.x) && Near(pivot.y, spec.Pivot.y) &&
                               sprite.border == spec.Border &&
                               Mathf.Approximately(sprite.pixelsPerUnit, Ppu) && texture.filterMode == FilterMode.Point;
                report.Check(label, isMatch,
                             $"텍스처 {texture.width}x{texture.height} · rect {sprite.rect} (규격 {wantRect}) · 피벗 {pivot} (규격 {spec.Pivot}) · " +
                             $"테두리 {sprite.border} (규격 {spec.Border}) · PPU {sprite.pixelsPerUnit} · {texture.filterMode}");
                isOk &= isMatch;
                result[spec.Name] = sprite;
            }
            return isOk ? result : null;
        }

        private static bool TryReadPng(string path, out int width, out int height, out RectInt solid, out int faintOutside, out bool isOpaque)
        {
            width = 0;
            height = 0;
            solid = default;
            faintOutside = 0;
            isOpaque = true;
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
                width = texture.width;
                height = texture.height;

                // Texture2D 는 아래 줄부터 — 좌표가 곧 Unity sprite rect 좌표(아래 원점)다.
                var pixels = texture.GetPixels32();
                int xMin = width, yMin = height, xMax = -1, yMax = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte a = pixels[y * width + x].a;
                        if (a < 255) isOpaque = false;
                        if (a < RoomArtSolidAlpha) continue;
                        xMin = Mathf.Min(xMin, x);
                        xMax = Mathf.Max(xMax, x);
                        yMin = Mathf.Min(yMin, y);
                        yMax = Mathf.Max(yMax, y);
                    }
                }
                if (xMax >= 0) solid = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte a = pixels[y * width + x].a;
                        if (a > 0 && a < RoomArtSolidAlpha && !solid.Contains(new Vector2Int(x, y))) faintOutside++;
                    }
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static string FmtRect(RectInt r) => $"({r.x},{r.y},{r.width},{r.height})";

        // ───────────────────────────────────────────────────────────── 배경 덮임

        /// <summary>
        /// 합성 배경 한 장이 맵 방 카메라가 갈 수 있는 모든 위치에서 화면을 덮는가.
        /// 층 중심의 화면 기준 위치 = (−depth·camX, depth·(기준y − camY)) — 카메라 범위 네 모서리만 보면 된다(선형).
        /// <paramref name="maxCameraY"/> 는 그 방 최고 착지면 + 추적 offset + 최다 점프 최고점(검증이 실제 값으로 계산).
        /// 덮으면 null 과 요약, 못 덮으면 사유.
        /// </summary>
        internal static string CheckBackgroundCoverage(Sprite background, float mapLength, float groundTop, float restCameraY,
                                                       float maxCameraY, out string summary)
        {
            float halfH = PixelScale.OrthographicSize;
            float halfW = halfH * RoomArtScreenAspect;
            float scale = Ppu / RoomArtBackgroundPixelsPerUnit;
            Vector2 half = (Vector2)background.bounds.size * scale * 0.5f;
            float d = RoomArtBackgroundDepth;

            // 카메라 가로 범위(StageDirector.Map 경계 + PlayerCameraFollow 클램프). 방이 화면보다 좁으면 가운데 고정.
            float boundX = mapLength * 0.5f + RoomArtWallThickness;
            float camX = Mathf.Max(0f, boundX - halfW);
            float camYMin = groundTop - RoomArtCameraBelowGround + halfH;

            float marginX = half.x - (halfW + d * camX);
            float marginTop = half.y - (halfH + d * (maxCameraY - restCameraY));
            float marginBottom = half.y - (halfH + d * (restCameraY - camYMin));
            float coveredCamY = restCameraY + (half.y - halfH) / d;
            float seenFraction = Mathf.Min(1f, (halfW + d * camX) / half.x);

            summary = $"배경 {half.x * 2f:0.###}x{half.y * 2f:0.###}유닛 · 카메라 x ±{camX:0.###} · y {camYMin:0.###}~{maxCameraY:0.###} → " +
                      $"여백 좌우 {marginX:0.###} · 위 {marginTop:0.###} · 아래 {marginBottom:0.###} · 덮는 최고 카메라 y {coveredCamY:0.###} · 보이는 가로 {seenFraction:P0}";
            if (marginX >= 0f && marginTop >= 0f && marginBottom >= 0f) return null;
            return $"배경이 시야를 못 덮는다 — {summary}";
        }

        // ───────────────────────────────────────────────────────────── 그림·충돌 일치

        /// <summary>공용 지면 그림 — 좌우·윗선이 충돌과 같고 윗선 아래 ≥ 5.5유닛. 맞으면 null.</summary>
        internal static string CheckGroundArtFit(Transform artRoot, Rect groundRect)
        {
            var ground = artRoot.Find(RoomArtGroundName)?.GetComponent<SpriteRenderer>();
            if (ground == null || ground.sprite == null) return $"지면 렌더러({RoomArtGroundName}) 없음";

            var g = DrawnRect(ground);
            var lines = new List<string>();
            if (!Near(g.xMin, groundRect.xMin) || !Near(g.xMax, groundRect.xMax) || !Near(g.yMax, groundRect.yMax))
                lines.Add($"지면 그림 {Fmt(g)} vs 충돌 {Fmt(groundRect)} — 좌우·윗선이 같아야 함");
            if (g.yMin > groundRect.yMax - RoomArtMinGroundDepth + Epsilon)
                lines.Add($"지면 그림 {Fmt(g)} — 윗선 아래 ≥ {RoomArtMinGroundDepth}유닛 깊이여야 함");
            return lines.Count == 0 ? null : string.Join(" · ", lines);
        }

        /// <summary>
        /// 지형 하나의 새 아트 스킨 — 스킨 1개, 좌우·윗선이 충돌과 같고 그림이 윗선 아래로만 내려온다.
        /// 테라스는 추가로 충돌 아래끝까지 덮는다(윗단 줄보다 낮으면 지면 쪽으로 더 내려가도 된다). 맞으면 null.
        /// </summary>
        internal static string CheckTerrainSkinFit(Transform platform, bool isTerrace)
        {
            var skins = platform.Cast<Transform>().Where(c => c.name == SkinName).ToList();
            if (skins.Count != 1) return $"{PathOf(platform)} 스킨 {skins.Count}개";

            string artName = isTerrace ? TerraceArtName : SlabArtName;
            var sr = skins[0].Find(artName)?.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return $"{PathOf(platform)} {artName} 렌더러 없음";

            var c = WorldRect(platform.GetComponent<BoxCollider2D>());
            var d = DrawnRect(sr);
            bool isFit = Near(d.xMin, c.xMin) && Near(d.xMax, c.xMax) && Near(d.yMax, c.yMax) && d.yMin < c.yMax
                         && (!isTerrace || d.yMin <= c.yMin + Epsilon);
            return isFit ? null : $"{PathOf(platform)} 충돌 {Fmt(c)} vs 그림 {Fmt(d)}";
        }
    }
}
#endif
