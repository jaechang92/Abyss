#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 환경 아트(<c>Assets/Art/Environment</c>) 임포트 설정 일괄 적용.
    ///
    /// 🔑 <b>PPU를 한 값으로 통일한다 — 이것이 이 파일의 존재 이유다.</b>
    /// 픽셀 아트에서 레이어마다 PPU가 다르면 <b>픽셀 크기가 달라지고</b>, 그건 한 화면에
    /// 두 해상도가 섞여 보인다는 뜻이다. 인스펙터를 열기 전에는 안 보이는 종류의 어긋남이라
    /// (<see cref="FormSpriteImporter"/>가 같은 이유로 존재한다) 손으로 만지지 않는다.
    ///
    /// <list type="bullet">
    /// <item><b>PPU 32</b> — 타일 한 칸(32px)이 정확히 1유닛. 플레이어(콜라이더 2유닛)는 2칸 키다.
    /// 폼 스프라이트가 PPU 128인 것과 어긋나 보이지만 그쪽은 <b>손그림 256px</b>이라
    /// 같은 2유닛을 훨씬 촘촘한 픽셀로 그린다 — 축척은 같고 밀도만 다르다.</item>
    /// <item><b>Point 필터 · 무압축</b> — 도트다. Bilinear면 뭉개지고 압축이면 팔레트가 깨진다.
    /// ΔE로 집행해 둔 10색이 임포트에서 도로 흐트러지면 앞 공정이 통째로 무의미해진다.</item>
    /// <item><b>피벗</b> — 프롭은 <b>발밑(BottomCenter)</b>. 지면에 놓이는 것들이라 발이 기준이어야
    /// 층·발판에서 안 뜬다. 배경·타일은 중앙.</item>
    /// </list>
    ///
    /// ⚠️ <b>배경은 PPU를 안 낮췄다.</b> 160x96을 PPU 32로 읽으면 5x3유닛이라
    /// 카메라(ortho 5 → 17.8x10유닛)를 못 채운다. 채우는 일은 임포트가 아니라
    /// <see cref="ArtTestStageBuilder"/>의 확대 배율이 한다 — <b>배율을 눈에 보이는 곳에 두려는
    /// 것이고, 그게 이 테스트 스테이지가 재려는 값이다.</b>
    /// </summary>
    public static class EnvironmentArtImporter
    {
        /// <summary>월드 픽셀 밀도. 32px 타일 = 1유닛.</summary>
        public const float PixelsPerUnit = 32f;

        [MenuItem(AbyssMenu.ApplyEnvironmentArtImport)]
        public static void Apply()
        {
            if (!Directory.Exists(AbyssPaths.EnvironmentArt))
            {
                Debug.LogWarning($"[EnvironmentArtImporter] 폴더 없음: {AbyssPaths.EnvironmentArt}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AbyssPaths.EnvironmentArt });
            int applied = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                    if (ApplyTo(imp, path)) applied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[EnvironmentArtImporter] {applied}/{guids.Length}장 적용 (PPU {PixelsPerUnit} · Point · 무압축).");
        }

        /// <summary>
        /// 파일 하나에 규약을 적용한다. 이미 같은 값이면 재임포트하지 않는다 —
        /// 30장 넘는 텍스처를 메뉴 누를 때마다 다시 굽지 않으려는 것이다.
        /// </summary>
        private static bool ApplyTo(TextureImporter imp, string path)
        {
            bool isProp = IsProp(path);
            var alignment = isProp ? SpriteAlignment.BottomCenter : SpriteAlignment.Center;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);

            bool changed =
                imp.textureType != TextureImporterType.Sprite ||
                imp.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit) ||
                imp.filterMode != FilterMode.Point ||
                imp.textureCompression != TextureImporterCompression.Uncompressed ||
                imp.mipmapEnabled ||
                imp.wrapMode != TextureWrapMode.Clamp ||
                !imp.alphaIsTransparency ||
                settings.spriteAlignment != (int)alignment;

            if (!changed) return false;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.maxTextureSize = 2048;

            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            imp.SetTextureSettings(settings);

            imp.SaveAndReimport();
            return true;
        }

        /// <summary>지면에 놓이는 것인가 — 피벗을 발밑으로 둘 대상.</summary>
        private static bool IsProp(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("prop_") || name.StartsWith("tile_platform");
        }
    }
}
#endif
