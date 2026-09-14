using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 무기 후보 격자에서 고른 칸을 <b>그립 피벗과 방향까지 맞춰</b> 개별 스프라이트로 들여온다.
    ///
    /// 🔴 <b>손으로 하면 안 되는 일이라 도구로 만든다</b> — 2026-09-14 에 손으로 넣어 보고
    /// 두 군데가 어긋났다:
    /// <list type="number">
    /// <item>격자에서 한 칸을 그냥 자르면 피벗이 Center(0.5, 0.5) 다. 손에 오려면
    /// <c>weapon_grips.py</c> 가 낸 그립 피벗이어야 하고, <b>그 값은 후보 64개가 저마다 다르다</b>.</item>
    /// <item>원본은 아이템 아이콘 관례라 <b>칼끝이 왼쪽 위</b>인데 캐릭터는 오른쪽을 본다.</item>
    /// </list>
    ///
    /// 🔑 <b>좌우 반전은 여기서 한 번 굽는다.</b> 런타임 <c>flipX</c> 로 하면 캐릭터가 왼쪽을 볼 때
    /// 루트의 <c>localScale.x</c> 반전과 겹쳐 <b>두 번 뒤집혀</b> 도로 원래 방향이 된다.
    /// 구워 두면 오른쪽을 볼 때 맞고, 왼쪽을 볼 때는 부모가 한 번만 뒤집어 역시 맞는다.
    ///
    /// 📌 <b>각도 0 = 그려진 그대로</b>다. 45° 로 그려져 있으면 각도 0 이 그 45° 다.
    /// 나중에 각도별로 새로 그리면 각 그림이 자기 각도의 0 이 되므로 규약이 그대로 산다.
    /// </summary>
    public static class WeaponSpriteImporter
    {
        private const string GripJson = "Art_Source/anchors/weapon_grips.json";
        private const string SourceDir = "Art_Source/weapons";
        private const string OutputDir = "Assets/Art/Sprites/Weapons";
        private const int PixelsPerUnit = 32;

        /// <summary>
        /// 어떤 후보를 쓸 것인가. <b>큐레이션 결과가 여기 쌓인다</b> —
        /// 폼 전용이므로 한 종류 안에서만 고르면 되고, 같은 폼의 무기는
        /// 실루엣 역할이 같아야 한다(검=세로선 · 궁수=곡선, `08-silhouette` §4).
        /// </summary>
        private static readonly Dictionary<string, int[]> Picks = new()
        {
            ["sword"] = new[] { 0 },
            ["bow"] = Array.Empty<int>(),
            ["shield"] = Array.Empty<int>(),
            ["dagger"] = Array.Empty<int>(),
        };

        [MenuItem("Tools/Abyss/Generate/Weapon Sprites")]
        public static void ImportPicked()
        {
            string gripPath = Path.Combine(Directory.GetCurrentDirectory(), GripJson);
            if (!File.Exists(gripPath))
            {
                Debug.LogError($"[WeaponSpriteImporter] {GripJson} 이 없다. weapon_grips.py 를 먼저 돌릴 것.");
                return;
            }

            GripDocument doc = JsonUtility.FromJson<GripDocument>(File.ReadAllText(gripPath));
            if (doc?.sheets == null)
            {
                Debug.LogError("[WeaponSpriteImporter] grips JSON 을 못 읽었다.");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            int made = 0;

            foreach (KeyValuePair<string, int[]> pick in Picks)
            {
                if (pick.Value == null || pick.Value.Length == 0) continue;

                GripSheet sheet = FindSheet(doc, pick.Key);
                if (sheet == null)
                {
                    Debug.LogError($"[WeaponSpriteImporter] grips JSON 에 {pick.Key} 가 없다.");
                    return;
                }

                string sourcePath = Path.Combine(Directory.GetCurrentDirectory(),
                                                 SourceDir, $"{pick.Key}_64_grid.png");
                if (!File.Exists(sourcePath))
                {
                    Debug.LogError($"[WeaponSpriteImporter] 원본이 없다: {sourcePath}");
                    return;
                }

                var grid = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                grid.LoadImage(File.ReadAllBytes(sourcePath));

                foreach (int index in pick.Value)
                {
                    GripItem item = FindItem(sheet, index);
                    if (item == null)
                    {
                        Debug.LogError($"[WeaponSpriteImporter] {pick.Key} 에 {index}번 칸이 없다(빈 칸?).");
                        return;
                    }
                    WriteCell(grid, sheet.cell, index, pick.Key, item);
                    made++;
                }

                UnityEngine.Object.DestroyImmediate(grid);
            }

            AssetDatabase.Refresh();
            Debug.Log(made == 0
                ? "[WeaponSpriteImporter] 고른 후보가 없다 — Picks 에 칸 번호를 넣을 것."
                : $"[WeaponSpriteImporter] {made}장 들여왔다 → {OutputDir}\n" +
                  "좌우 반전을 구웠고 피벗은 그립에 맞췄다. PPU 32 · Point · 압축 없음.");
        }

        /// <summary>한 칸을 잘라 <b>좌우로 뒤집어</b> 저장하고, 피벗까지 맞춘다.</summary>
        private static void WriteCell(Texture2D grid, int cell, int index, string kind, GripItem item)
        {
            int columns = grid.width / cell;
            int col = index % columns;
            int row = index / columns;

            // Texture2D 의 y 는 아래에서부터다. 격자 JSON 의 row 는 위에서부터라 뒤집어 읽는다.
            int srcY = grid.height - (row + 1) * cell;

            Color[] src = grid.GetPixels(col * cell, srcY, cell, cell);
            var flipped = new Color[src.Length];
            for (int y = 0; y < cell; y++)
            {
                for (int x = 0; x < cell; x++)
                {
                    flipped[y * cell + (cell - 1 - x)] = src[y * cell + x];
                }
            }

            var tex = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
            tex.SetPixels(flipped);
            tex.Apply();

            string assetPath = $"{OutputDir}/{kind}_{index:D2}.png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ApplySettings(assetPath, item);
        }

        /// <summary>
        /// 🔴 임포트 설정은 몸 시트와 같은 규약이다 — PPU 32 · Point · 압축 없음.
        /// 하나라도 어긋나면 무기만 크기나 선명도가 달라지는데 <b>오류로는 안 잡힌다</b>.
        /// </summary>
        private static void ApplySettings(string assetPath, GripItem item)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;

            // 좌우를 뒤집었으니 피벗 x 도 뒤집는다. y 는 그대로.
            float pivotX = 1f - item.pivotUnity[0];
            importer.spritePivot = new Vector2(pivotX, item.pivotUnity[1]);

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(pivotX, item.pivotUnity[1]);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        private static GripSheet FindSheet(GripDocument doc, string kind)
        {
            foreach (GripSheet s in doc.sheets)
            {
                if (s.sheet != null && s.sheet.StartsWith(kind, StringComparison.Ordinal)) return s;
            }
            return null;
        }

        private static GripItem FindItem(GripSheet sheet, int index)
        {
            foreach (GripItem i in sheet.items)
            {
                if (i.index == index) return i;
            }
            return null;
        }

        // ── JSON 대응 ────────────────────────────────────────────────────────────────
        // CS0649 를 끈다 — JsonUtility 가 리플렉션으로 채우는 필드다.
#pragma warning disable 0649
        [Serializable] private sealed class GripDocument { public GripSheet[] sheets; }

        [Serializable]
        private sealed class GripSheet
        {
            public string sheet;
            public string kind;
            public int cell;
            public GripItem[] items;
        }

        [Serializable]
        private sealed class GripItem
        {
            public int index;
            public float[] pivotUnity;   // [x(왼쪽 기준), y(아래 기준)]
        }
#pragma warning restore 0649
    }
}
