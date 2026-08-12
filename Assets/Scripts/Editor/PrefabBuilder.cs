#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 프로토타입 프리팹 일괄 생성 툴.
    /// 적 5종 + 플레이어 1종 생성 → EnemyData.spawnPrefab 자동 연결.
    /// 사전 조건: ContentBuilder로 EnemyData/FormData 에셋이 생성되어 있을 것.
    /// </summary>
    public static partial class PrefabBuilder
    {
        [MenuItem(AbyssMenu.GeneratePrefabs)]
        public static void Build()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder",
                "프로토 프리팹 생성:\n" +
                "  · 적 프리팹 5종 (EnemyData별)\n" +
                "  · 플레이어 프리팹 1종\n" +
                "  · WhiteSquare.png 임시 스프라이트\n\n" +
                "EnemyData.spawnPrefab 필드에 역참조를 자동 연결합니다.\n" +
                "이미 존재하는 프리팹은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            RunBuild(forceRebuildEnemies: false, forceRebuildPlayer: false);
        }

        [MenuItem(AbyssMenu.GenerateRebuildEnemies)]
        public static void RebuildEnemies()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder — Force Rebuild",
                "기존 적 프리팹을 전부 삭제하고 재생성합니다.\n" +
                "(EnemyVisuals/Nametag 자식 등 최신 구조가 필요할 때 사용)\n\n" +
                "EnemyData.spawnPrefab 역참조는 자동으로 재연결됩니다.\n" +
                "플레이어 프리팹은 영향을 받지 않습니다.",
                "재생성", "취소");
            if (!proceed) return;

            RunBuild(forceRebuildEnemies: true, forceRebuildPlayer: false);
        }

        [MenuItem(AbyssMenu.GenerateRebuildPlayer)]
        public static void RebuildPlayer()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "PrefabBuilder — Force Rebuild Player",
                "기존 Player 프리팹을 삭제하고 재생성합니다.\n" +
                "(AttackPoint/AttackEffect 자식 등 최신 구조가 필요할 때 사용)\n\n" +
                "씬에 배치된 Player 인스턴스는 변경되지 않으므로,\n" +
                "필요 시 씬에서 Player 제거 후 프리팹 드롭하여 교체하세요.",
                "재생성", "취소");
            if (!proceed) return;

            RunBuild(forceRebuildEnemies: false, forceRebuildPlayer: true);
        }

        private static void RunBuild(bool forceRebuildEnemies, bool forceRebuildPlayer)
        {
            EnsureDir(AbyssPaths.EnemyPrefabs);
            EnsureDir(AbyssPaths.PlayerPrefabs);
            EnsureDir(AbyssPaths.CombatPrefabs);
            EnsureDir(AbyssPaths.Sprites);

            // 적 스프라이트 임포트 설정 자동 적용 (PPU·FilterMode 일괄)
            SetupEnemySpriteImportSettings();

            var sprite = GetOrCreateWhiteSprite();
            int enemyCount = BuildAllEnemyPrefabs(sprite, forceRebuildEnemies);
            bool playerBuilt = BuildPlayerPrefab(sprite, forceRebuildPlayer);

            // 발사체 프리팹 생성 후 isRanged 적 EnemyData에 자동 연결.
            var projectile = BuildProjectilePrefab(sprite, forceRebuildEnemies);
            int rangedLinked = LinkProjectileToRangedEnemies(projectile);

            var arcShell = BuildArcProjectilePrefab(sprite, forceRebuildEnemies);
            int mortarLinked = LinkArcProjectileToMortars(arcShell);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PrefabBuilder] 완료 — 적 {enemyCount}종{(forceRebuildEnemies ? " (force)" : "")}, " +
                      $"플레이어 {(playerBuilt ? 1 : 0)}종{(forceRebuildPlayer ? " (force)" : "")}, " +
                      $"발사체 직진 1종(원거리 적 {rangedLinked}종 연결) · 곡사 1종(곡사병 {mortarLinked}종 연결).");
        }

        /// <summary>Assets/Audio/SFX/{name}.wav 로드. 미import/부재 시 null(보스 측 PlaySfx가 무음 가드).</summary>
        private static AudioClip LoadSfx(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{AbyssPaths.Sfx}/{name}.wav");
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        // ==================== White Sprite ====================
        private static Sprite GetOrCreateWhiteSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(AbyssPaths.WhiteSquare);
            if (existing != null) return existing;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(AbyssPaths.WhiteSquare, png);
            AssetDatabase.ImportAsset(AbyssPaths.WhiteSquare);

            var importer = (TextureImporter)AssetImporter.GetAtPath(AbyssPaths.WhiteSquare);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 4;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }

            Debug.Log($"[PrefabBuilder] 생성: {AbyssPaths.WhiteSquare}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(AbyssPaths.WhiteSquare);
        }

        // ==================== Helpers ====================
        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static string ToPascalCase(string snake)
        {
            if (string.IsNullOrEmpty(snake)) return string.Empty;
            var parts = snake.Split('_');
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                sb.Append(char.ToUpper(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
        }
    }
}
#endif
