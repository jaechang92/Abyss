#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Skill;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 프로토 SO 에셋 일괄 생성 에디터 툴.
    /// 생성 대상: FormData 4 / SkillData 15 / EnemyData 7 / RunConfig 1.
    /// 기본값은 stage-d-analyst.md 확정 스펙 + 03-skill-draft-system.md 스킬 목록.
    /// 이미 존재하는 에셋은 건너뜀(덮어쓰지 않음).
    /// </summary>
    public static partial class ContentBuilder
    {
        [MenuItem(AbyssMenu.GenerateContent)]
        public static void Generate()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "ContentBuilder",
                "프로토 SO 에셋 생성:\n" +
                "  · FormData 4 (dark_blade, void_archer, ancient_shield, void_thrower)\n" +
                "  · SkillData 15 (불꽃 5 + 심연 5 + 버프 1 + 방패 2 + 투척 2)\n" +
                "  · EnemyData 7 (근접 2 / 원거리 1 / 엘리트 1 / 보스 1 + Stage2 중간보스 1 / 보스 1)\n" +
                "  · RunConfig 1\n\n" +
                "이미 존재하는 에셋은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Forms);
            EnsureDir(AbyssPaths.Skills);
            EnsureDir(AbyssPaths.Enemies);
            EnsureDir(AbyssPaths.RunConfigDir);
            EnsureDir(AbyssPaths.Abilities);

            CreateForms();
            CreateSkills();
            CreateEnemies();
            CreateRunConfig();
            CreateAbilities();
            WireActiveAbilities();
            WireSkillIcons();
            WireAbilitySfx();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ContentBuilder] 프로토 SO 생성 완료 — Assets/Data/ 하위 확인");
        }

        private static void CreateForms()
        {
            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/DarkBlade.asset", so =>
            {
                so.formId = "dark_blade";
                so.displayName = "암흑 검사";
                so.description = "근접·패링·흡수 HP. 불꽃 축 시너지. HP 보정 +20%, 이동속도 기본.";
                so.hpMultiplier = 1.2f;
                so.moveSpeedMultiplier = 1f;
            });

            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/VoidArcher.asset", so =>
            {
                so.formId = "void_archer";
                so.displayName = "공허 궁수";
                so.description = "원거리·기동성·심연. 빠른 연사·백스텝. HP -10%, 이동속도 +15%.";
                so.hpMultiplier = 0.9f;
                so.moveSpeedMultiplier = 1.15f;
            });

            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/AncientShield.asset", so =>
            {
                so.formId = "ancient_shield";
                so.displayName = "고대 방패병";
                so.description = "방패로 버티는 철벽 근접. 최고 HP·저속·묵직한 단일 점프. 철벽 축 시너지. HP +50%, 이동속도 -15%.";
                so.hpMultiplier = 1.5f;
                so.moveSpeedMultiplier = 0.85f;
                so.jumpCount = 1;
            });

            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/VoidThrower.asset", so =>
            {
                so.formId = "void_thrower";
                so.displayName = "심연 투척사";
                so.description = "투창과 잔상 이동의 기동형 시프터. 유리대포 근·원거리 혼합. 심연 축 시너지. HP -10%, 이동속도 +15%, 2단 점프.";
                so.hpMultiplier = 0.9f;
                so.moveSpeedMultiplier = 1.15f;
                so.jumpCount = 2;
                so.castColor = new Color(0.75f, 0.35f, 0.95f, 1f);
            });
        }

        private static void CreateSkills()
        {
            AddSkill(1, "skill_fireball", "화염구", SkillCategory.Active, SkillRarity.Common, "fire", "", "전방 화염 발사, 연소 2 부여, CD 4초");
            AddSkill(2, "skill_burn_enhancement", "연소 강화", SkillCategory.Passive, SkillRarity.Common, "fire", "", "모든 연소 데미지 +50%");
            AddSkill(3, "skill_explosive_theology", "폭발 신학", SkillCategory.Synergy, SkillRarity.Rare, "fire", "", "[불꽃] 2개+ 시 적 사망 폭발 (적 최대 HP 30%)");
            AddSkill(4, "skill_flame_armor", "불꽃 갑옷", SkillCategory.Passive, SkillRarity.Rare, "fire", "", "받는 피해 10%를 주변 적 연소로 변환");
            AddSkill(5, "skill_flame_roar", "화염의 포효", SkillCategory.Active, SkillRarity.Epic, "fire", "dark_blade", "암흑검사 특화: 근처 적 모두 연소 3 부여");
            AddSkill(6, "skill_afterimage", "잔상", SkillCategory.Passive, SkillRarity.Common, "abyss", "", "대시 시 잔상 생성, 0.5초 후 폭발 (피해 기본 공격×0.5)");
            AddSkill(7, "skill_abyss_charge", "심연 충전", SkillCategory.Passive, SkillRarity.Rare, "abyss", "", "폼 교체 시 다음 공격 피해 2배");
            AddSkill(8, "skill_swift_slash", "순간 절단", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_archer", "공허궁수 특화: 순간이동 후 3연 검기 (CD 3초)");
            AddSkill(9, "skill_abyss_ally", "심연 동료", SkillCategory.Synergy, SkillRarity.Rare, "abyss", "", "[심연] 2개+ 시 대시 CD -0.5초 (최소 0.3초)");
            AddSkill(10, "skill_battle_cry", "전장의 함성", SkillCategory.Active, SkillRarity.Rare, "", "", "발동 시 6초간 공격력 1.5배 (CD 12초)");
            AddSkill(11, "skill_void_volley", "공허 연사", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_archer", "공허궁수 특화: 전방 부채꼴로 화살 3발 동시 발사 (CD 5초)");
            AddSkill(12, "skill_shield_bash", "방패 강타", SkillCategory.Active, SkillRarity.Rare, "guard", "ancient_shield", "고대방패병 특화: 전방 방패로 광역 강타 (CD 4초)");
            AddSkill(13, "skill_iron_guard", "철벽 방어", SkillCategory.Active, SkillRarity.Epic, "guard", "ancient_shield", "고대방패병 특화: 5초간 받는 피해 50% 감소 (CD 10초)");
            AddSkill(14, "skill_void_javelin", "심연 투창", SkillCategory.Active, SkillRarity.Rare, "abyss", "void_thrower", "심연투척사 특화: 전방으로 고위력 투창을 던진다 (CD 4.5초)");
            AddSkill(15, "skill_phantom_step", "잔상 질주", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_thrower", "심연투척사 특화: 5초간 이동속도·공격력을 강화한다 (CD 12초)");
        }

        private static void AddSkill(int index, string skillId, string displayName, SkillCategory category, SkillRarity rarity, string synergyTag, string formBound, string description)
        {
            var filename = $"Skill_{index:D2}_{ToPascalCase(skillId.Replace("skill_", string.Empty))}.asset";
            CreateOrSkip<SkillData>($"{AbyssPaths.Skills}/{filename}", so =>
            {
                so.skillId = skillId;
                so.displayName = displayName;
                so.description = description;
                so.category = category;
                so.rarity = rarity;
                so.synergyTag = synergyTag;
                so.formBound = formBound;
                so.formulaDescription = "(flatBonus + base) * multiplier";
                so.flatBonus = 0f;
                so.multiplier = 1f;
            });
        }

        private static void CreateRunConfig()
        {
            CreateOrSkip<RunConfig>($"{AbyssPaths.RunConfigDir}/RunConfig.asset", _ => { });
        }

        private static void CreateOrSkip<T>(string assetPath, Action<T> configure) where T : ScriptableObject
        {
            if (File.Exists(assetPath))
            {
                Debug.Log($"[ContentBuilder] 건너뜀 (존재): {assetPath}");
                return;
            }

            var so = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(so);
            AssetDatabase.CreateAsset(so, assetPath);
            Debug.Log($"[ContentBuilder] 생성: {assetPath}");
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static string ToPascalCase(string snake)
        {
            if (string.IsNullOrEmpty(snake)) return string.Empty;
            var parts = snake.Split('_');
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                sb.Append(char.ToUpper(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }
    }
}
#endif
