#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Skill;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 프로토 SO 에셋 일괄 생성 에디터 툴.
    /// 생성 대상: FormData 2 / SkillData 9 / EnemyData 7 / RunConfig 1 = 총 19개.
    /// 기본값은 stage-d-analyst.md 확정 스펙 + 03-skill-draft-system.md 9스킬 목록.
    /// 이미 존재하는 에셋은 건너뜀(덮어쓰지 않음).
    /// </summary>
    public static class ContentBuilder
    {
        private const string MenuPath = "Tools/Abyss/Generate Prototype Content";
        private const string FormDir = "Assets/Data/Forms";
        private const string SkillDir = "Assets/Data/Skills";
        private const string EnemyDir = "Assets/Data/Enemies";
        private const string RunDir = "Assets/Resources/Data"; // P-14: RunConfig 정규 위치(런타임 Resources.Load 대상). 구 Assets/Data/Run 중복 생성 방지.
        private const string AbilityDir = "Assets/Data/Abilities";
        private const string PlayerProjectilePrefabPath = "Assets/Prefabs/Combat/EnemyProjectile.prefab";
        private const string SkillIconDir = "Assets/Art/Sprites/SkillIcons";
        private const string SfxDir = "Assets/Audio/SFX";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "ContentBuilder",
                "프로토 SO 에셋 19개 생성:\n" +
                "  · FormData 2 (dark_blade, void_archer)\n" +
                "  · SkillData 10 (불꽃 5 + 심연 4 + 버프 1)\n" +
                "  · EnemyData 7 (근접 2 / 원거리 1 / 엘리트 1 / 보스 1 + Stage2 중간보스 1 / 보스 1)\n" +
                "  · RunConfig 1\n\n" +
                "이미 존재하는 에셋은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(FormDir);
            EnsureDir(SkillDir);
            EnsureDir(EnemyDir);
            EnsureDir(RunDir);
            EnsureDir(AbilityDir);

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

        [MenuItem("Tools/Abyss/Wire Active Skill Abilities")]
        public static void WireAbilitiesOnly()
        {
            EnsureDir(AbilityDir);
            CreateAbilities();
            WireActiveAbilities();
            WireSkillIcons();
            WireAbilitySfx();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ContentBuilder] Active 스킬 어빌리티 생성·연결 + 아이콘·발동음 연결 완료 — Assets/Data/Abilities 확인");
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
            AddSkill(10, "skill_battle_cry", "전장의 함성", SkillCategory.Active, SkillRarity.Rare, "", "", "발동 시 6초간 공격력 1.5배 (CD 12초)");
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
                so.projectileSpeed = 9f;       // 발사체 속도(근접 즉발 대신 직진 탄)
                so.projectileLifetime = 3f;    // 미명중 시 소멸 시간
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
                so.patrolRadius = 0f; // 보스는 Patrol 정지 (수동 페이즈 스크립트로 제어)
            });

            // Stage 2 "불꽃의 회랑" 신규 적 2종. AI·페이즈 패턴은 기존 재활용(스탯만 차별화),
            // 정식 회전베기/화염브레스 패턴은 M2 본작업으로 연기(08-content-roadmap.md).
            CreateOrSkip<EnemyData>($"{EnemyDir}/MidBossSentinel.asset", so =>
            {
                so.enemyId = "midboss_sentinel";
                so.displayName = "감시자 거인";
                so.baseHp = 220;
                so.baseDamage = 28;
                so.moveSpeed = 2.8f;
                so.detectionRange = 8f;
                so.attackRange = 2f;
                so.attackCooldown = 1.4f;
                so.expReward = 90;
                so.goldReward = 20;
                so.isElite = true; // Stage2 중간보스 — EliteBonus 드래프트 트리거 재활용
            });

            CreateOrSkip<EnemyData>($"{EnemyDir}/BossFlameSerpent.asset", so =>
            {
                so.enemyId = "boss_flame_serpent";
                so.displayName = "화염 뱀";
                so.baseHp = 520;
                so.baseDamage = 38;
                so.moveSpeed = 3f;
                so.detectionRange = 12f;
                so.attackRange = 2.8f;
                so.attackCooldown = 1.6f;
                so.expReward = 260;
                so.goldReward = 65;
                so.isBoss = true;
                so.patrolRadius = 0f; // 보스는 Patrol 정지
            });
        }

        private static void CreateRunConfig()
        {
            CreateOrSkip<RunConfig>($"{RunDir}/RunConfig.asset", _ => { });
        }

        /// <summary>
        /// Active 스킬용 GenericAbilityData 3종 생성. 화염구=발사체, 화염의 포효=근접 광역, 순간 절단=근접.
        /// 화염구 발사체는 기존 EnemyProjectile 프리팹을 재사용(faction은 런타임 Launch에서 HitsEnemies 지정).
        /// </summary>
        private static void CreateAbilities()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<Projectile>(PlayerProjectilePrefabPath);
            if (projectile == null)
            {
                Debug.LogWarning($"[ContentBuilder] 발사체 프리팹 없음({PlayerProjectilePrefabPath}) — 화염구 projectilePrefab 미연결. PrefabBuilder 먼저 실행 필요.");
            }

            CreateOrSkip<GenericAbilityData>($"{AbilityDir}/Ability_Fireball.asset", so =>
            {
                so.abilityName = "fireball";
                so.description = "전방으로 화염구를 발사한다.";
                so.cooldownDuration = 4f;
                so.effectType = AbilityEffectType.Projectile;
                so.damage = 22;
                so.projectilePrefab = projectile;
                so.projectileSpeed = 12f;
                so.projectileLifetime = 2f;
                so.projectileSpawnOffset = 0.7f;
                so.showHitEffect = true;
                so.effectColor = new Color(1f, 0.55f, 0.15f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbilityDir}/Ability_FlameRoar.asset", so =>
            {
                so.abilityName = "flame_roar";
                so.description = "주변 적을 화염으로 일제히 강타한다.";
                so.cooldownDuration = 6f;
                so.effectType = AbilityEffectType.MeleeArea;
                so.damage = 30;
                so.meleeBoxSize = new Vector2(3.4f, 2.4f);
                so.meleeForwardOffset = 0f;
                so.showHitEffect = true;
                so.effectColor = new Color(1f, 0.3f, 0.1f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbilityDir}/Ability_SwiftSlash.asset", so =>
            {
                so.abilityName = "swift_slash";
                so.description = "전방을 빠르게 베어 넘긴다.";
                so.cooldownDuration = 3f;
                so.effectType = AbilityEffectType.MeleeArea;
                so.damage = 18;
                so.meleeBoxSize = new Vector2(2.8f, 1.6f);
                so.meleeForwardOffset = 1.3f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.4f, 0.85f, 1f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbilityDir}/Ability_BattleCry.asset", so =>
            {
                so.abilityName = "battle_cry";
                so.description = "6초간 공격력을 1.5배로 끌어올린다.";
                so.cooldownDuration = 12f;
                so.effectType = AbilityEffectType.Buff;
                so.healAmount = 0;
                so.buffMoveSpeedMultiplier = 1f;
                so.buffAttackMultiplier = 1.5f;
                so.buffDuration = 6f;
                so.showHitEffect = true;
                so.effectColor = new Color(1f, 0.82f, 0.25f, 1f);
            });

            // 이미 존재해 CreateOrSkip이 건너뛴 자산에도 연출 설정을 반영(재실행 시 색 갱신).
            ApplyEffectSettings($"{AbilityDir}/Ability_Fireball.asset", new Color(1f, 0.55f, 0.15f, 1f));
            ApplyEffectSettings($"{AbilityDir}/Ability_FlameRoar.asset", new Color(1f, 0.3f, 0.1f, 1f));
            ApplyEffectSettings($"{AbilityDir}/Ability_SwiftSlash.asset", new Color(0.4f, 0.85f, 1f, 1f));
            ApplyEffectSettings($"{AbilityDir}/Ability_BattleCry.asset", new Color(1f, 0.82f, 0.25f, 1f));
        }

        private static void ApplyEffectSettings(string abilityPath, Color color)
        {
            var ability = AssetDatabase.LoadAssetAtPath<GenericAbilityData>(abilityPath);
            if (ability == null) return;
            ability.showHitEffect = true;
            ability.effectColor = color;
            EditorUtility.SetDirty(ability);
        }

        /// <summary>
        /// 기존 Active SkillData(이미 생성되어 CreateSkills가 건너뜀)의 relatedAbility를 어빌리티 자산에 연결.
        /// skillId → 어빌리티 자산 매핑. 이미 연결돼 있으면 건너뛴다.
        /// </summary>
        private static void WireActiveAbilities()
        {
            WireOne("skill_fireball", $"{AbilityDir}/Ability_Fireball.asset");
            WireOne("skill_flame_roar", $"{AbilityDir}/Ability_FlameRoar.asset");
            WireOne("skill_swift_slash", $"{AbilityDir}/Ability_SwiftSlash.asset");
            WireOne("skill_battle_cry", $"{AbilityDir}/Ability_BattleCry.asset");
        }

        private static void WireOne(string skillId, string abilityPath)
        {
            var ability = AssetDatabase.LoadAssetAtPath<GenericAbilityData>(abilityPath);
            if (ability == null)
            {
                Debug.LogWarning($"[ContentBuilder] 어빌리티 자산 없음: {abilityPath}");
                return;
            }

            var skill = FindSkillById(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[ContentBuilder] SkillData 없음: {skillId}");
                return;
            }

            if (skill.relatedAbility == ability)
            {
                Debug.Log($"[ContentBuilder] 이미 연결됨: {skillId} → {ability.name}");
                return;
            }

            skill.relatedAbility = ability;
            EditorUtility.SetDirty(skill);
            Debug.Log($"[ContentBuilder] 연결: {skillId}.relatedAbility → {ability.name}");
        }

        private static SkillData FindSkillById(string skillId)
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (data != null && data.skillId == skillId) return data;
            }
            return null;
        }

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
        }

        private static void WireIcon(string skillId, string iconKey)
        {
            string iconPath = $"{SkillIconDir}/{iconKey}.png";
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
            if (!Directory.Exists(SkillIconDir)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SkillIconDir });
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

        /// <summary>
        /// 어빌리티 3종 발동음(Docs/game-design/_skill_sfx_generator.py 산출물)을 GenericAbilityData.castSfx에 연결.
        /// 자산 경로 → SFX/skill_{key}.wav 매핑. 신규·기존(CreateOrSkip 건너뛴) 자산 모두 반영한다.
        /// wav가 없으면(생성기 미실행) 해당 항목만 건너뛴다.
        /// </summary>
        private static void WireAbilitySfx()
        {
            WireSfx($"{AbilityDir}/Ability_Fireball.asset", "skill_fireball");
            WireSfx($"{AbilityDir}/Ability_FlameRoar.asset", "skill_flame_roar");
            WireSfx($"{AbilityDir}/Ability_SwiftSlash.asset", "skill_swift_slash");
            WireSfx($"{AbilityDir}/Ability_BattleCry.asset", "skill_battle_cry");
        }

        private static void WireSfx(string abilityPath, string sfxKey)
        {
            var ability = AssetDatabase.LoadAssetAtPath<GenericAbilityData>(abilityPath);
            if (ability == null)
            {
                Debug.LogWarning($"[ContentBuilder] 어빌리티 자산 없음: {abilityPath}");
                return;
            }

            string sfxPath = $"{SfxDir}/{sfxKey}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxPath);
            if (clip == null)
            {
                Debug.LogWarning($"[ContentBuilder] 발동음 없음: {sfxPath} — _skill_sfx_generator.py 먼저 실행 필요.");
                return;
            }

            if (ability.castSfx == clip) return;

            ability.castSfx = clip;
            EditorUtility.SetDirty(ability);
            Debug.Log($"[ContentBuilder] 발동음 연결: {ability.name}.castSfx → {clip.name}");
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
