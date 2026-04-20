#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 프로토 SO 에셋 일괄 생성 에디터 툴.
    /// 생성 대상: FormData 2 / SkillData 9 / EnemyData 4 / RunConfig 1 = 총 16개.
    /// 기본값은 stage-d-analyst.md 확정 스펙 + 03-skill-draft-system.md 9스킬 목록.
    /// 이미 존재하는 에셋은 건너뜀(덮어쓰지 않음).
    /// </summary>
    public static class ContentBuilder
    {
        private const string MenuPath = "Tools/Abyss/Generate Prototype Content";
        private const string FormDir = "Assets/Data/Forms";
        private const string SkillDir = "Assets/Data/Skills";
        private const string EnemyDir = "Assets/Data/Enemies";
        private const string RunDir = "Assets/Data/Run";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "ContentBuilder",
                "프로토 SO 에셋 17개 생성:\n" +
                "  · FormData 2 (dark_blade, void_archer)\n" +
                "  · SkillData 9 (불꽃 5 + 심연 4)\n" +
                "  · EnemyData 5 (근접 2 / 원거리 1 / 엘리트 1 / 보스 1)\n" +
                "  · RunConfig 1\n\n" +
                "이미 존재하는 에셋은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(FormDir);
            EnsureDir(SkillDir);
            EnsureDir(EnemyDir);
            EnsureDir(RunDir);

            CreateForms();
            CreateSkills();
            CreateEnemies();
            CreateRunConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ContentBuilder] 프로토 SO 생성 완료 — Assets/Data/ 하위 확인");
        }

        private static void CreateForms()
        {
            CreateOrSkip<FormData>($"{FormDir}/DarkBlade.asset", so =>
            {
                so.formId = "dark_blade";
                so.displayName = "암흑 검사";
                so.description = "근접·패링·흡수 HP. 불꽃 축 시너지. HP 보정 +20%, 이동속도 기본.";
                so.hpMultiplier = 1.2f;
                so.moveSpeedMultiplier = 1f;
            });

            CreateOrSkip<FormData>($"{FormDir}/VoidArcher.asset", so =>
            {
                so.formId = "void_archer";
                so.displayName = "공허 궁수";
                so.description = "원거리·기동성·심연. 빠른 연사·백스텝. HP -10%, 이동속도 +15%.";
                so.hpMultiplier = 0.9f;
                so.moveSpeedMultiplier = 1.15f;
            });
        }

        private static void CreateSkills()
        {
            AddSkill(1, "skill_fireball", "화염구", SkillCategory.Active, SkillRarity.Common, "fire", "", "전방 화염 발사, 연소 부여, CD 4초");
            AddSkill(2, "skill_burn_enhancement", "연소 강화", SkillCategory.Passive, SkillRarity.Common, "fire", "", "모든 연소 데미지 +50%");
            AddSkill(3, "skill_explosive_theology", "폭발 신학", SkillCategory.Synergy, SkillRarity.Rare, "fire", "", "[불꽃] 2개+ 시 적 사망 폭발");
            AddSkill(4, "skill_flame_armor", "불꽃 갑옷", SkillCategory.Passive, SkillRarity.Rare, "fire", "", "받는 피해 10% → 연소 스택 변환");
            AddSkill(5, "skill_flame_roar", "화염의 포효", SkillCategory.Active, SkillRarity.Epic, "fire", "dark_blade", "암흑검사 특화: 근처 적 모두 연소 3 부여");
            AddSkill(6, "skill_afterimage", "잔상", SkillCategory.Passive, SkillRarity.Common, "abyss", "", "대시 시 잔상 생성, 0.5초 후 폭발 (피해 기본 공격×0.5)");
            AddSkill(7, "skill_abyss_charge", "심연 충전", SkillCategory.Passive, SkillRarity.Rare, "abyss", "", "폼 교체 시 다음 공격 피해 2배");
            AddSkill(8, "skill_swift_slash", "순간 절단", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_archer", "공허궁수 특화: 순간이동 후 3연 검기 (CD 3초)");
            AddSkill(9, "skill_abyss_ally", "심연 동료", SkillCategory.Synergy, SkillRarity.Rare, "abyss", "", "[심연] 2개+ 시 대시 CD -0.5초");
        }

        private static void AddSkill(int index, string skillId, string displayName, SkillCategory category, SkillRarity rarity, string synergyTag, string formBound, string description)
        {
            var filename = $"Skill_{index:D2}_{ToPascalCase(skillId.Replace("skill_", string.Empty))}.asset";
            CreateOrSkip<SkillData>($"{SkillDir}/{filename}", so =>
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

        private static void CreateEnemies()
        {
            CreateOrSkip<EnemyData>($"{EnemyDir}/MeleeGrunt.asset", so =>
            {
                so.enemyId = "melee_grunt";
                so.displayName = "근접 병사";
                so.baseHp = 30;
                so.baseDamage = 10;
                so.moveSpeed = 3f;
                so.detectionRange = 6f;
                so.attackRange = 1.2f;
                so.attackCooldown = 1.5f;
                so.expReward = 15;
                so.goldReward = 3;
            });

            CreateOrSkip<EnemyData>($"{EnemyDir}/MeleeBrute.asset", so =>
            {
                so.enemyId = "melee_brute";
                so.displayName = "중장 강적";
                so.baseHp = 60;
                so.baseDamage = 18;
                so.moveSpeed = 2.5f;
                so.detectionRange = 6f;
                so.attackRange = 1.5f;
                so.attackCooldown = 2f;
                so.expReward = 25;
                so.goldReward = 5;
            });

            CreateOrSkip<EnemyData>($"{EnemyDir}/RangedArcher.asset", so =>
            {
                so.enemyId = "ranged_archer";
                so.displayName = "원거리 사수";
                so.baseHp = 25;
                so.baseDamage = 12;
                so.moveSpeed = 2f;
                so.detectionRange = 8f;
                so.attackRange = 5f;
                so.attackCooldown = 1.8f;
                so.expReward = 20;
                so.goldReward = 4;
                so.isRanged = true;
            });

            CreateOrSkip<EnemyData>($"{EnemyDir}/EliteHunter.asset", so =>
            {
                so.enemyId = "elite_hunter";
                so.displayName = "엘리트 사냥꾼";
                so.baseHp = 120;
                so.baseDamage = 25;
                so.moveSpeed = 3.5f;
                so.detectionRange = 8f;
                so.attackRange = 1.8f;
                so.attackCooldown = 1.3f;
                so.expReward = 60;
                so.goldReward = 15;
                so.isElite = true;
            });

            CreateOrSkip<EnemyData>($"{EnemyDir}/BossAbyssKeeper.asset", so =>
            {
                so.enemyId = "boss_abyss_keeper";
                so.displayName = "심연의 수호자";
                so.baseHp = 400;
                so.baseDamage = 30;
                so.moveSpeed = 2.5f;
                so.detectionRange = 12f;
                so.attackRange = 2.5f;
                so.attackCooldown = 1.8f;
                so.expReward = 200;
                so.goldReward = 50;
                so.isBoss = true;
            });
        }

        private static void CreateRunConfig()
        {
            CreateOrSkip<RunConfig>($"{RunDir}/RunConfig.asset", _ => { });
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
