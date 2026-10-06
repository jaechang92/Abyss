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
    /// 생성 대상: FormData 4 / SkillData 30 / EnemyData 10 / RunConfig 1.
    /// 기본값은 stage-d-analyst.md 확정 스펙 + 03-skill-draft-system.md 스킬 목록.
    /// 이미 존재하는 에셋은 건너뜀(덮어쓰지 않음).
    /// </summary>
    public static partial class ContentBuilder
    {
        public static void Generate()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "ContentBuilder",
                "프로토 SO 에셋 생성:\n" +
                "  · FormData 4 (dark_blade, void_archer, ancient_shield, void_thrower)\n" +
                "  · SkillData 30 (불꽃 6 + 심연 8 + 수호 3 + 피의 서약 7 + 무축 6)\n" +
                "  · EnemyData 10 (근접 2 / 원거리 4 / 엘리트 1 / 보스 1 + Stage2 중간보스 1 / 보스 1)\n" +
                "  · WeaponData 4 (폼마다 기본 무기 하나)\n" +
                "  · RunConfig 1\n\n" +
                "이미 존재하는 에셋은 건너뜁니다.",
                "생성", "취소");
            if (!proceed) return;

            EnsureDir(AbyssPaths.Forms);
            EnsureDir(AbyssPaths.Skills);
            EnsureDir(AbyssPaths.Enemies);
            EnsureDir(AbyssPaths.Weapons);
            EnsureDir(AbyssPaths.RunConfigDir);
            EnsureDir(AbyssPaths.Abilities);

            CreateForms();
            CreateSkills();
            CreateSkillsBloodPactAndNeutral();   // 19~30 (2026-10-06)
            CreateEnemies();
            LinkEnemySfx();   // 기존 에셋에도 붙여야 하므로 생성과 분리된 패스다
            CreateWeapons();
            CreateRunConfig();
            CreateAbilities();
            CreateAbilitiesBloodPactAndNeutral();
            WireActiveAbilities();
            WireActiveAbilitiesBloodPactAndNeutral();
            WireSkillIcons();
            WireFormBodySprites();     // 기존 에셋에도 붙여야 하므로 생성과 분리된 패스다
            WireWeaponSprites();       // 〃 — 무기 그림 + 각도 스트립
            WireFormDefaultWeapons();  // 〃 — 폼이 들고 시작할 무기
            WireFormRangedAttacks();   // 〃 — 궁수·투척사 원거리 기본 공격(발사체 프리팹은 PrefabBuilder)
            WireFormGuards();          // 〃 — 방패병 가드(강공격 키 = 가드 · 자동 반격)
            WireAbilitySfx();
            WireFormSwapSfx();         // 기존 에셋에도 붙여야 하므로 생성과 분리된 패스다 — 비어 있는 교체음만 채운다

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ContentBuilder] 프로토 SO 생성 완료 — Assets/Data/ 하위 확인");
        }

        // 폼 역할(P03 · 02-form-change-system.md 「역할표」). 설명은 도감에 그대로 나온다 — 구현 안 된 기능을 적지 않는다.
        private const float DARK_BLADE_LIFE_STEAL = 0.08f;
        private const string DARK_BLADE_DESCRIPTION =
            "붙어서 베는 근접. 적중 피해의 8%를 HP로 흡수. 불꽃 축 시너지. HP +20%, 이동속도 기본.";
        private const string VOID_ARCHER_DESCRIPTION =
            "멀리서 견제하는 원거리. 긴 사거리의 가벼운 화살, 강공격은 여러 적 관통. HP -10%, 이동속도 +15%, 3단 점프.";
        private const string VOID_THROWER_DESCRIPTION =
            "붙어서 던지는 유리대포. 짧은 사거리의 무거운 투창으로 한 방이 크다. 심연 축 시너지. HP -10%, 이동속도 +15%, 2단 점프.";

        private static void CreateForms()
        {
            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/DarkBlade.asset", so =>
            {
                so.formId = "dark_blade";
                so.displayName = "암흑 검사";
                so.description = DARK_BLADE_DESCRIPTION;
                so.hpMultiplier = 1.2f;
                so.moveSpeedMultiplier = 1f;
                so.meleeLifeSteal = DARK_BLADE_LIFE_STEAL;
            });

            CreateOrSkip<FormData>($"{AbyssPaths.Forms}/VoidArcher.asset", so =>
            {
                so.formId = "void_archer";
                so.displayName = "공허 궁수";
                so.description = VOID_ARCHER_DESCRIPTION;
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
                so.description = VOID_THROWER_DESCRIPTION;
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
            AddSkill(7, "skill_abyss_charge", "심연 충전", SkillCategory.Passive, SkillRarity.Rare, "abyss", "", "폼 교체 시 다음 기본 공격 피해 2배");
            AddSkill(8, "skill_swift_slash", "순간 절단", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_archer", "공허궁수 특화: 순간이동 후 3연 검기 (CD 3초)");
            AddSkill(9, "skill_abyss_ally", "심연 동료", SkillCategory.Synergy, SkillRarity.Rare, "abyss", "", "[심연] 2개+ 시 대시 CD -0.5초 (최소 0.3초)");
            AddSkill(10, "skill_battle_cry", "전장의 함성", SkillCategory.Active, SkillRarity.Rare, "", "", "발동 시 6초간 공격력 1.5배 (CD 12초)");
            AddSkill(11, "skill_void_volley", "공허 연사", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_archer", "공허궁수 특화: 전방 부채꼴로 화살 3발 동시 발사 (CD 5초)");
            AddSkill(12, "skill_shield_bash", "방패 강타", SkillCategory.Active, SkillRarity.Rare, "guard", "ancient_shield", "고대방패병 특화: 전방 방패로 광역 강타 (CD 4초)");
            AddSkill(13, "skill_iron_guard", "철벽 방어", SkillCategory.Active, SkillRarity.Epic, "guard", "ancient_shield", "고대방패병 특화: 5초간 받는 피해 50% 감소 (CD 10초)");
            AddSkill(14, "skill_void_javelin", "심연 투창", SkillCategory.Active, SkillRarity.Rare, "abyss", "void_thrower", "심연투척사 특화: 전방으로 고위력 투창을 던진다 (CD 4.5초)");
            AddSkill(15, "skill_phantom_step", "잔상 질주", SkillCategory.Active, SkillRarity.Epic, "abyss", "void_thrower", "심연투척사 특화: 5초간 이동속도·공격력을 강화한다 (CD 12초)");

            // ── 16~18 (2026-08-12) ──
            // 등급을 Common에 몰아준 이유는 취향이 아니라 분포다. 추첨은 등급을 먼저 고르는 2단계가
            // 아니라 스킬마다 등급 가중치(Common 60 / Rare 28 / Epic 10)를 붙인 단일 가중 추첨인데,
            // 풀의 Common이 3장뿐이라(화염구·연소 강화·잔상) 그 세 장이 실효 점유의 절반 이상을
            // 가져갔다(암흑검사 기준 180/330 ≈ 55%) — 3지선다마다 같은 카드가 반복됐다.
            // 또 폼 전용이 15장 중 7장(47%)이라 로드맵 하한(30%+)을 이미 넘겨, 신규 Common 2종은 전역(any).
            AddSkill(16, "skill_flame_burst", "화염 폭발", SkillCategory.Active, SkillRarity.Common, "fire", "", "주변을 화염으로 터뜨린다. 연소 2 부여 (CD 5초)");
            AddSkill(17, "skill_soul_reclaim", "영혼 회수", SkillCategory.Passive, SkillRarity.Common, "abyss", "", "적 처치 시 HP 2 회복");
            // 수호 축은 시너지 스킬이 없어 08 §3 체크리스트 3번이 미충족이었다. ancient_shield 귀속인
            // 이유는 그 축의 다른 스킬 2종이 모두 이 폼 전용이라, 전역으로 두면 다른 폼 런에서
            // 발동 조건을 영영 못 채우는 '무용 카드'가 되기 때문이다.
            AddSkill(18, "skill_counter_stance", "반격 태세", SkillCategory.Synergy, SkillRarity.Rare, "guard", "ancient_shield", "[수호] 2개+ 시 피격당하면 받은 피해의 40%를 주변 적에게 되돌린다");
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

        /// <summary>
        /// 폼 몸통 스프라이트를 <c>Assets/Art/Sprites/Forms/{formId}.png</c>에서 연결한다(4-1 아트).
        ///
        /// <see cref="CreateOrSkip"/>는 기존 에셋을 건너뛰므로 생성 시점 값만으로는 이미 있는 4종에
        /// 안 붙는다 — 적 효과음 연결과 같은 이유로 <b>매번 도는 별도 패스</b>다.
        /// 그림이 아직 없는 폼은 <b>조용히 건너뛴다</b>: 비어 있으면 흰 사각형 + castColor 폴백이
        /// 그대로 돌아가므로, 없다고 경고를 쌓을 이유가 없다(4종을 한꺼번에 만들지 않는다).
        /// </summary>
        private static void WireFormBodySprites()
        {
            int linked = 0, missing = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:FormData", new[] { AbyssPaths.Forms }))
            {
                var form = AssetDatabase.LoadAssetAtPath<FormData>(AssetDatabase.GUIDToAssetPath(guid));
                if (form == null || string.IsNullOrEmpty(form.formId)) continue;

                string path = $"Assets/Art/Sprites/Forms/{form.formId}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { missing += 1; continue; }
                if (form.bodySprite == sprite) continue;

                form.bodySprite = sprite;
                EditorUtility.SetDirty(form);
                linked += 1;
            }
            if (linked > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[ContentBuilder] 폼 몸통 스프라이트 연결: {linked}종 갱신, {missing}종은 그림 없음(폴백 유지).");
        }

        /// <summary>
        /// 폼 교체음(<see cref="FormData.swapInSfx"/>) 임시 연결. 정식 교체음이 없어 폼 대표 스킬 발동음을 재사용한다.
        ///
        /// <see cref="CreateOrSkip"/>가 건너뛰는 기존 4폼에도 붙여야 하므로 별도 패스다.
        /// 🔴 이미 지정된 값은 덮어쓰지 않는다 — 나중에 정식 교체음을 넣었을 때 빌더가 임시 음원으로 되돌리면 안 된다.
        /// </summary>
        private static void WireFormSwapSfx()
        {
            WireFormSwapSfx($"{AbyssPaths.Forms}/DarkBlade.asset", "skill_flame_roar");
            WireFormSwapSfx($"{AbyssPaths.Forms}/VoidArcher.asset", "skill_void_volley");
            WireFormSwapSfx($"{AbyssPaths.Forms}/AncientShield.asset", "skill_shield_bash");
            WireFormSwapSfx($"{AbyssPaths.Forms}/VoidThrower.asset", "skill_void_javelin");
        }

        private static void WireFormSwapSfx(string formPath, string sfxKey)
        {
            var form = AssetDatabase.LoadAssetAtPath<FormData>(formPath);
            if (form == null)
            {
                Debug.LogWarning($"[ContentBuilder] 교체음 연결 건너뜀 — 폼 없음: {formPath}");
                return;
            }
            if (form.swapInSfx != null) return;

            string sfxPath = $"{AbyssPaths.Sfx}/{sfxKey}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxPath);
            if (clip == null)
            {
                Debug.LogWarning($"[ContentBuilder] 교체음 연결 건너뜀 — 음원 없음: {sfxPath}");
                return;
            }

            form.swapInSfx = clip;
            EditorUtility.SetDirty(form);
            Debug.Log($"[ContentBuilder] 교체음 연결: {form.name}.swapInSfx → {clip.name}");
        }
    }
}
#endif
