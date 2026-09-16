#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 가로로 이어 붙인 시트를 <b>격자로 슬라이스</b>한다 — 임포트 설정 · 칸 · 피벗 · 이름을 한 번에.
    ///
    /// 🔴 <b>왜 생겼나</b>(2026-09-17). 캐릭터 시트와 무기 각도 시트의 <c>.meta</c> 를 <b>파이썬으로 네 번</b> 손으로 썼다.
    /// guid·spriteID 를 새로 발급하고 331줄 중 의도한 줄만 바뀌었는지 대조하는 방식이었고, 폼 3벌이면
    /// 27장이 더 필요했다. <c>.meta</c> 는 Unity 가 쓰는 파일이다 — Unity API 로 쓴다.
    ///
    /// 🔑 <b>이미 같은 슬라이스면 건드리지 않는다.</b> 클립은 스프라이트를 ID 로 물고 있어,
    /// 다시 자르며 ID 가 바뀌면 <b>오류 없이 그림만 사라진다.</b> 칸 수·위치·피벗·이름이 모두 같으면 건너뛰고,
    /// 다르더라도 <b>같은 이름의 기존 ID 는 재사용</b>한다.
    ///
    /// ⚠️ <c>TextureImporter.spritesheet</c> 는 쓰지 않는다 — Unity 6 에서 CS0618 이다.
    /// 2D Sprite 패키지의 <see cref="ISpriteEditorDataProvider"/> 가 대체 경로다.
    /// </summary>
    internal static class SpriteSheetSlicer
    {
        private const string Tag = "[SpriteSheetSlicer]";

        /// <summary>캐릭터 시트 규약 — <c>PlayerAnimationBuilder.CharacterSheet</c> 와 같은 자리.</summary>
        private const string CharacterFolder = "Assets/Art/Sprites/Characters";
        private const string CharacterSuffix = "_southeast.png";
        private const int CharacterCell = 92;

        /// <summary>
        /// 캐릭터 발밑 피벗. 92 칸의 아래에서 15px(발밑 y=77) = 0.163.
        /// 🔴 <b>시트 조립기(<c>build_form_sheets.py</c> 의 <c>footY 77</c>) · 손 앵커(<c>hand_anchors.py --pivot-y 0.163</c>)와
        /// 같은 값이어야 한다.</b> 하나만 바뀌면 몸과 무기가 조용히 어긋난다.
        /// </summary>
        private static readonly Vector2 CharacterPivot = new(0.5f, 0.163f);

        internal enum Result { Sliced, AlreadySliced, Failed }

        /// <summary>캐릭터 시트 전부(<c>*_southeast.png</c>)를 92 격자 · 발밑 피벗으로 슬라이스한다.</summary>
        public static void SliceCharacterSheets()
        {
            string[] paths = Directory.GetFiles(CharacterFolder, "*" + CharacterSuffix)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();

            int sliced = 0, skipped = 0, failed = 0;
            foreach (string path in paths)
            {
                switch (SliceGrid(path, CharacterCell, CharacterCell, CharacterPivot))
                {
                    case Result.Sliced: sliced++; break;
                    case Result.AlreadySliced: skipped++; break;
                    default: failed++; break;
                }
            }

            string message = $"{Tag} 캐릭터 시트 {paths.Length}장 — 슬라이스 {sliced} · 그대로 {skipped} · 실패 {failed}";
            if (failed > 0) Debug.LogError(message);
            else Debug.Log(message);
        }

        /// <summary>
        /// 시트 하나를 <paramref name="cellWidth"/>×<paramref name="cellHeight"/> 격자로 자른다.
        /// 이름은 <c>{파일명}_{번호}</c> — 번호는 위 행부터 왼→오 (<c>PlayerAnimationBuilder.FrameIndexOf</c> 가 읽는 규약).
        /// </summary>
        public static Result SliceGrid(string path, int cellWidth, int cellHeight, Vector2 pivot)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"{Tag} 텍스처가 아니다: {path}");
                return Result.Failed;
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width % cellWidth != 0 || height % cellHeight != 0)
            {
                Debug.LogError($"{Tag} {path} — {width}x{height} 가 {cellWidth}x{cellHeight} 로 나눠떨어지지 않는다");
                return Result.Failed;
            }

            bool settingsChanged = EnsurePixelSpriteSettings(importer);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            SpriteRect[] existing = provider.GetSpriteRects();
            SpriteRect[] wanted = BuildRects(Path.GetFileNameWithoutExtension(path),
                                             width, height, cellWidth, cellHeight, pivot, existing);

            if (!settingsChanged && SameSlicing(existing, wanted)) return Result.AlreadySliced;

            provider.SetSpriteRects(wanted);

            // 이름 ↔ ID 표. 빠뜨리면 새 스프라이트가 파일 ID 를 못 얻어 참조가 안 잡힌다.
            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(wanted.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();

            Debug.Log($"{Tag} {path} — {wanted.Length}칸 ({cellWidth}x{cellHeight}) · 피벗 {pivot}");
            return Result.Sliced;
        }

        /// <summary>
        /// 픽셀아트 스프라이트 설정. 🔴 <b>PPU 는 <c>PixelScale</c> 에서 읽는다</b> — 값이 두 곳에 있으면
        /// 한쪽만 옳은 배율로 보이던 적이 있다(화면 배율 SoT).
        /// </summary>
        private static bool EnsurePixelSpriteSettings(TextureImporter importer)
        {
            float ppu = Abyss.Runtime.Camera.PixelScale.PixelsPerUnit;
            bool isCorrect = importer.textureType == TextureImporterType.Sprite
                             && importer.spriteImportMode == SpriteImportMode.Multiple
                             && Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                             && importer.filterMode == FilterMode.Point
                             && importer.textureCompression == TextureImporterCompression.Uncompressed
                             && !importer.mipmapEnabled;
            if (isCorrect) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return true;
        }

        private static SpriteRect[] BuildRects(string baseName, int width, int height,
                                               int cellWidth, int cellHeight, Vector2 pivot,
                                               SpriteRect[] existing)
        {
            var existingIds = new Dictionary<string, SpriteRect>();
            foreach (SpriteRect rect in existing) existingIds[rect.name] = rect;

            int columns = width / cellWidth;
            int rows = height / cellHeight;
            var result = new List<SpriteRect>(columns * rows);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    string name = $"{baseName}_{row * columns + column}";
                    var rect = new SpriteRect
                    {
                        name = name,
                        // 텍스처 좌표는 아래에서 위로 — 위 행이 0번이 되게 뒤집는다.
                        rect = new Rect(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                    };
                    // 기존 이름이면 ID 를 물려받는다 — 클립이 그 ID 를 물고 있다. 새 칸이면 명시적으로 발급한다.
                    rect.spriteID = existingIds.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate();
                    result.Add(rect);
                }
            }
            return result.ToArray();
        }

        private static bool SameSlicing(SpriteRect[] existing, SpriteRect[] wanted)
        {
            if (existing.Length != wanted.Length) return false;
            for (int i = 0; i < wanted.Length; i++)
            {
                SpriteRect a = existing[i], b = wanted[i];
                if (a.name != b.name || a.rect != b.rect || a.alignment != b.alignment) return false;
                if (Vector2.Distance(a.pivot, b.pivot) > 0.001f) return false;
            }
            return true;
        }
    }
}
#endif
