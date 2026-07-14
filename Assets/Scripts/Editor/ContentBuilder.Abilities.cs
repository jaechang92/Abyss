#if UNITY_EDITOR
using Abyss.Runtime.Combat;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Skill;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class ContentBuilder
    {
        [MenuItem(AbyssMenu.GenerateWireSkills)]
        public static void WireAbilitiesOnly()
        {
            EnsureDir(AbyssPaths.Abilities);
            CreateAbilities();
            WireActiveAbilities();
            WireSkillIcons();
            WireAbilitySfx();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ContentBuilder] Active 스킬 어빌리티 생성·연결 + 아이콘·발동음 연결 완료 — Assets/Data/Abilities 확인");
        }

        /// <summary>
        /// Active 스킬용 GenericAbilityData 3종 생성. 화염구=발사체, 화염의 포효=근접 광역, 순간 절단=근접.
        /// 화염구 발사체는 기존 EnemyProjectile 프리팹을 재사용(faction은 런타임 Launch에서 HitsEnemies 지정).
        /// </summary>
        private static void CreateAbilities()
        {
            var projectile = AssetDatabase.LoadAssetAtPath<Projectile>(AbyssPaths.EnemyProjectilePrefab);
            if (projectile == null)
            {
                Debug.LogWarning($"[ContentBuilder] 발사체 프리팹 없음({AbyssPaths.EnemyProjectilePrefab}) — 화염구 projectilePrefab 미연결. PrefabBuilder 먼저 실행 필요.");
            }

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_Fireball.asset", so =>
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

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_FlameRoar.asset", so =>
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

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_SwiftSlash.asset", so =>
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

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_BattleCry.asset", so =>
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

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_VoidVolley.asset", so =>
            {
                so.abilityName = "void_volley";
                so.description = "전방 부채꼴로 공허 화살 3발을 동시에 발사한다.";
                so.cooldownDuration = 5f;
                so.effectType = AbilityEffectType.Projectile;
                so.damage = 14;
                so.projectilePrefab = projectile;
                so.projectileSpeed = 13f;
                so.projectileLifetime = 2f;
                so.projectileSpawnOffset = 0.7f;
                so.projectileCount = 3;
                so.projectileSpreadAngle = 24f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.6f, 0.42f, 0.95f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_ShieldBash.asset", so =>
            {
                so.abilityName = "shield_bash";
                so.description = "전방을 방패로 광역 강타한다.";
                so.cooldownDuration = 4f;
                so.effectType = AbilityEffectType.MeleeArea;
                so.damage = 28;
                so.meleeBoxSize = new Vector2(3f, 2f);
                so.meleeForwardOffset = 1.1f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.7f, 0.75f, 0.85f, 1f);
            });

            CreateOrSkip<GenericAbilityData>($"{AbyssPaths.Abilities}/Ability_IronGuard.asset", so =>
            {
                so.abilityName = "iron_guard";
                so.description = "5초간 받는 피해를 50% 감소시킨다.";
                so.cooldownDuration = 10f;
                so.effectType = AbilityEffectType.Buff;
                so.healAmount = 0;
                so.buffMoveSpeedMultiplier = 1f;
                so.buffAttackMultiplier = 1f;
                so.buffDefenseMultiplier = 0.5f;
                so.buffDuration = 5f;
                so.showHitEffect = true;
                so.effectColor = new Color(0.55f, 0.7f, 0.95f, 1f);
            });

            // 이미 존재해 CreateOrSkip이 건너뛴 자산에도 연출 설정을 반영(재실행 시 색 갱신).
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_Fireball.asset", new Color(1f, 0.55f, 0.15f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_FlameRoar.asset", new Color(1f, 0.3f, 0.1f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_SwiftSlash.asset", new Color(0.4f, 0.85f, 1f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_BattleCry.asset", new Color(1f, 0.82f, 0.25f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_VoidVolley.asset", new Color(0.6f, 0.42f, 0.95f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_ShieldBash.asset", new Color(0.7f, 0.75f, 0.85f, 1f));
            ApplyEffectSettings($"{AbyssPaths.Abilities}/Ability_IronGuard.asset", new Color(0.55f, 0.7f, 0.95f, 1f));
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
            WireOne("skill_fireball", $"{AbyssPaths.Abilities}/Ability_Fireball.asset");
            WireOne("skill_flame_roar", $"{AbyssPaths.Abilities}/Ability_FlameRoar.asset");
            WireOne("skill_swift_slash", $"{AbyssPaths.Abilities}/Ability_SwiftSlash.asset");
            WireOne("skill_battle_cry", $"{AbyssPaths.Abilities}/Ability_BattleCry.asset");
            WireOne("skill_void_volley", $"{AbyssPaths.Abilities}/Ability_VoidVolley.asset");
            WireOne("skill_shield_bash", $"{AbyssPaths.Abilities}/Ability_ShieldBash.asset");
            WireOne("skill_iron_guard", $"{AbyssPaths.Abilities}/Ability_IronGuard.asset");
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
        /// 어빌리티 3종 발동음(Docs/game-design/_skill_sfx_generator.py 산출물)을 GenericAbilityData.castSfx에 연결.
        /// 자산 경로 → SFX/skill_{key}.wav 매핑. 신규·기존(CreateOrSkip 건너뛴) 자산 모두 반영한다.
        /// wav가 없으면(생성기 미실행) 해당 항목만 건너뛴다.
        /// </summary>
        private static void WireAbilitySfx()
        {
            WireSfx($"{AbyssPaths.Abilities}/Ability_Fireball.asset", "skill_fireball");
            WireSfx($"{AbyssPaths.Abilities}/Ability_FlameRoar.asset", "skill_flame_roar");
            WireSfx($"{AbyssPaths.Abilities}/Ability_SwiftSlash.asset", "skill_swift_slash");
            WireSfx($"{AbyssPaths.Abilities}/Ability_BattleCry.asset", "skill_battle_cry");
            WireSfx($"{AbyssPaths.Abilities}/Ability_VoidVolley.asset", "skill_void_volley");
            WireSfx($"{AbyssPaths.Abilities}/Ability_ShieldBash.asset", "skill_shield_bash");
            WireSfx($"{AbyssPaths.Abilities}/Ability_IronGuard.asset", "skill_iron_guard");
        }

        private static void WireSfx(string abilityPath, string sfxKey)
        {
            var ability = AssetDatabase.LoadAssetAtPath<GenericAbilityData>(abilityPath);
            if (ability == null)
            {
                Debug.LogWarning($"[ContentBuilder] 어빌리티 자산 없음: {abilityPath}");
                return;
            }

            string sfxPath = $"{AbyssPaths.Sfx}/{sfxKey}.wav";
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
    }
}
#endif
