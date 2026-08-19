#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 폼 스프라이트(<c>Assets/Art/Sprites/Forms</c>) 임포트 설정 일괄 적용. 4-1 아트.
    ///
    /// 손으로 매번 인스펙터를 만지면 폼마다 값이 갈린다 — 특히 <b>PPU가 하나만 달라도</b>
    /// 그 폼만 크기가 다르게 보이는데, 인스펙터를 열기 전에는 안 보인다.
    ///
    /// 🔑 도트 스프라이트와 설정이 다르다:
    /// <list type="bullet">
    /// <item><b>PPU 128</b> — 세로 256px 그림이 정확히 2유닛(플레이어 콜라이더 높이)이 된다.
    /// 적 도트는 PPU 16이지만 손그림은 그 격자에 맞출 이유가 없다.</item>
    /// <item><b>필터 Bilinear</b> — 도트가 아니라 손그림이라 Point로 두면 계단이 보인다.</item>
    /// <item><b>피벗 = 발밑</b> — 지면에 서는 캐릭터는 발이 기준이어야 층·경사에서 안 뜬다.
    /// 무기가 한쪽으로 뻗어 있어 <b>가로 중앙과 발 중심이 다르다</b>(암흑 검사 0.37).</item>
    /// </list>
    /// </summary>
    public static class FormSpriteImporter
    {
        private const string FORMS_DIR = "Assets/Art/Sprites/Forms";
        private const float PPU = 128f;

        /// <summary>
        /// 폼별 발밑 피벗. 이미지에서 실측한 값이다 —
        /// <c>Tools/PixelArt/</c>의 준비 스크립트가 알파 최하단 행의 가로 중앙으로 계산한다.
        /// 그림을 다시 뽑으면 이 값도 다시 재야 한다.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, Vector2> Pivots = new()
        {
            ["dark_blade"] = new Vector2(0.369f, 0.047f),
            ["void_archer"] = new Vector2(0.361f, 0.074f),
            ["ancient_shield"] = new Vector2(0.331f, 0.066f),
            ["void_thrower"] = new Vector2(0.316f, 0.070f),
        };

        [MenuItem(AbyssMenu.ApplyFormSpriteImport)]
        public static void Apply()
        {
            if (!Directory.Exists(FORMS_DIR))
            {
                Debug.LogWarning($"[FormSpriteImporter] 폴더 없음: {FORMS_DIR}");
                return;
            }

            int applied = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FORMS_DIR }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;

                string id = Path.GetFileNameWithoutExtension(path);
                Vector2 pivot = Pivots.TryGetValue(id, out var p) ? p : new Vector2(0.5f, 0f);
                if (!Pivots.ContainsKey(id))
                {
                    Debug.LogWarning(
                        $"[FormSpriteImporter] \"{id}\"의 발밑 피벗이 등록돼 있지 않다 — " +
                        "가로 중앙(0.5, 0)으로 둔다. 무기가 한쪽으로 뻗은 그림이면 발이 중앙에서 벗어나 " +
                        "달릴 때 캐릭터가 좌우로 흔들린다. Pivots에 실측값을 추가할 것.");
                }

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = PPU;
                imp.spritePivot = pivot;
                imp.filterMode = FilterMode.Bilinear;   // 손그림 — 도트가 아니다
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;

                // spritePivot은 SpriteAlignment.Custom일 때만 반영된다 —
                // 이 한 줄을 빼면 위에서 넣은 피벗이 조용히 무시되고 Center로 남는다.
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                imp.SetTextureSettings(settings);

                imp.SaveAndReimport();
                applied += 1;
                Debug.Log($"[FormSpriteImporter] {id} — PPU {PPU}, 피벗 ({pivot.x:F3}, {pivot.y:F3})");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[FormSpriteImporter] 폼 스프라이트 {applied}장 설정 완료.");
        }
    }
}
#endif
