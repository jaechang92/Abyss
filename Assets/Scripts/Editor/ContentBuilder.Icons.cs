#if UNITY_EDITOR
using System;
using System.IO;
using Abyss.Runtime.Draft;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class ContentBuilder
    {
        /// <summary>
        /// Active 스킬 3종 도트 아이콘(Tools/PixelArt/generate_skill_icons.py 산출물)을 SkillData.icon에 연결.
        /// skillId → SkillIcons/{key}.png 매핑. 임포트 설정(Sprite/Point)을 먼저 보장한 뒤 로드·할당한다.
        /// 아이콘 PNG가 없으면(생성기 미실행) 해당 항목만 건너뛴다.
        /// </summary>
        private static void WireSkillIcons()
        {
            SetupSkillIconImportSettings();
            WireIcon("skill_fireball", "fireball");
            WireIcon("skill_flame_roar", "flame_roar");
            WireIcon("skill_swift_slash", "swift_slash");
            WireIcon("skill_battle_cry", "battle_cry");
            WireIcon("skill_void_volley", "void_volley");
            WireIcon("skill_shield_bash", "shield_bash");
            WireIcon("skill_iron_guard", "iron_guard");
            WireIcon("skill_void_javelin", "void_javelin");
            WireIcon("skill_phantom_step", "phantom_step");
            WireIcon("skill_flame_burst", "flame_burst");
        }

        private static void WireIcon(string skillId, string iconKey)
        {
            string iconPath = $"{AbyssPaths.SkillIcons}/{iconKey}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ContentBuilder] 아이콘 없음: {iconPath} — generate_skill_icons.py 먼저 실행 필요.");
                return;
            }

            var skill = FindSkillById(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[ContentBuilder] SkillData 없음: {skillId}");
                return;
            }

            if (skill.icon == sprite)
            {
                Debug.Log($"[ContentBuilder] 아이콘 이미 연결됨: {skillId} → {sprite.name}");
                return;
            }

            skill.icon = sprite;
            EditorUtility.SetDirty(skill);
            Debug.Log($"[ContentBuilder] 아이콘 연결: {skillId}.icon → {sprite.name}");
        }

        /// <summary>
        /// SkillIcons/*.png를 UI 아이콘용으로 임포트(TextureType=Sprite, Single, FilterMode=Point, 압축 없음, PPU=32).
        /// PrefabBuilder.SetupEnemySpriteImportSettings와 동일 패턴.
        /// </summary>
        private static void SetupSkillIconImportSettings()
        {
            if (!Directory.Exists(AbyssPaths.SkillIcons)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AbyssPaths.SkillIcons });
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                importer.textureType        = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 32;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
