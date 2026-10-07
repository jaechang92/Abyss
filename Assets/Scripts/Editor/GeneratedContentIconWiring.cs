#if UNITY_EDITOR
using System;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Weapon;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 제작한 콘텐츠 아이콘 32종(스킬 18 · 유물 6 · 무기 4 · 폼 4)을 기존 데이터의 <c>icon</c> 에 연결한다.
    ///
    /// 🔑 <b>비어 있는 icon 만 채운다.</b> 같은 그림이면 무변경, 다른 그림이 이미 있으면 손대지 않고 경고한다 —
    /// 사용자가 인스펙터에서 바꾼 아이콘을 메뉴 재실행이 되돌리면 안 된다. 그래서 몇 번을 돌려도 결과가 같다.
    ///
    /// 📌 매핑은 코드에 적어 둔다. 외부 매핑 파일·문서를 읽지 않는다(에디터 실행이 Docs 에 기대지 않도록).
    /// 그림은 <see cref="AssetDatabase.LoadAssetAtPath{T}"/> 로 실제 임포트된 Sprite 를 쓴다 —
    /// PNG 임포트 설정·YAML 은 건드리지 않는다.
    ///
    /// 콘텐츠 생성기(스킬 아이콘 · 무기 그림 · 유물 생성) 끝에서도 각각 불려서,
    /// 생성기를 다시 돌려도 연결이 유지된다. 저장은 호출한 쪽의 <c>SaveAssets</c> 가 맡는다.
    /// </summary>
    public static class GeneratedContentIconWiring
    {
        private const string LOG_TAG = "[GeneratedContentIconWiring]";
        private const string UNDO_NAME = "Apply Generated Content Icon";

        private const string SKILL_DATA_DIR = "Assets/Resources/Data/Skills";
        private const string RELIC_DATA_DIR = "Assets/Resources/Data/Relics";
        private const string WEAPON_DATA_DIR = "Assets/Resources/Data/Weapons";
        private const string SKILL_IMAGES = "Assets/Art/UI/GameImageInventory/skills";
        private const string RELIC_IMAGES = "Assets/Art/UI/GameImageInventory/relics";
        private const string WEAPON_IMAGES = "Assets/Art/UI/GameImageInventory/weapons";
        private const string FORM_DATA_DIR = "Assets/Resources/Data/Forms";
        private const string FORM_IMAGES = "Assets/Art/UI/IconReplacement/forms";

        /// <summary>데이터 ID · 데이터 에셋 경로 · 선택한 그림 경로 · 그림 GUID(경로가 엉뚱한 파일을 가리키는지 확인용).</summary>
        private readonly struct IconEntry
        {
            public readonly string Id;
            public readonly string AssetPath;
            public readonly string ImagePath;
            public readonly string ImageGuid;

            public IconEntry(string id, string assetPath, string imagePath, string imageGuid)
            {
                Id = id;
                AssetPath = assetPath;
                ImagePath = imagePath;
                ImageGuid = imageGuid;
            }
        }

        private static readonly IconEntry[] SkillIcons =
        {
            new IconEntry("skill_burn_enhancement", $"{SKILL_DATA_DIR}/Skill_02_BurnEnhancement.asset", $"{SKILL_IMAGES}/burn-enhancement-v1.png", "addb36077eb74a22b59adafab53415e2"),
            new IconEntry("skill_explosive_theology", $"{SKILL_DATA_DIR}/Skill_03_ExplosiveTheology.asset", $"{SKILL_IMAGES}/explosive-theology-v1.png", "497f60b2320b46588237450075c14002"),
            new IconEntry("skill_flame_armor", $"{SKILL_DATA_DIR}/Skill_04_FlameArmor.asset", $"{SKILL_IMAGES}/flame-armor-v1.png", "471598c0c928472e9d10e64194bf178e"),
            new IconEntry("skill_afterimage", $"{SKILL_DATA_DIR}/Skill_06_Afterimage.asset", $"{SKILL_IMAGES}/afterimage-v1.png", "da75a38f64ef4f81b284efc64f4265ed"),
            new IconEntry("skill_abyss_charge", $"{SKILL_DATA_DIR}/Skill_07_AbyssCharge.asset", $"{SKILL_IMAGES}/abyss-charge-v1.png", "95d265ad089a4d51aa0bee9268461275"),
            new IconEntry("skill_abyss_ally", $"{SKILL_DATA_DIR}/Skill_09_AbyssAlly.asset", $"{SKILL_IMAGES}/abyss-ally-v2.png", "dd47921a144d4a7b973b77911edb5fe6"),
            new IconEntry("skill_soul_reclaim", $"{SKILL_DATA_DIR}/Skill_17_SoulReclaim.asset", $"{SKILL_IMAGES}/soul-reclaim-v1.png", "0e9280f8d4274053836a241d5d3ded40"),
            new IconEntry("skill_counter_stance", $"{SKILL_DATA_DIR}/Skill_18_CounterStance.asset", $"{SKILL_IMAGES}/counter-stance-v1.png", "80cbf16bbe7f4e97a30960720890c97c"),
            new IconEntry("skill_blood_rage", $"{SKILL_DATA_DIR}/Skill_20_BloodRage.asset", $"{SKILL_IMAGES}/blood-rage-v1.png", "7f0d0d1f288f42bb8d54d2f28c9d26fd"),
            new IconEntry("skill_final_stand", $"{SKILL_DATA_DIR}/Skill_21_FinalStand.asset", $"{SKILL_IMAGES}/final-stand-v1.png", "4a8ee423faa54d64978c286e8edeb4be"),
            new IconEntry("skill_vampiric_seal", $"{SKILL_DATA_DIR}/Skill_22_VampiricSeal.asset", $"{SKILL_IMAGES}/vampiric-seal-v1.png", "245b47e804cf4c739aef7782a7af12e5"),
            new IconEntry("skill_crimson_radiance", $"{SKILL_DATA_DIR}/Skill_23_CrimsonRadiance.asset", $"{SKILL_IMAGES}/crimson-radiance-v1.png", "49dee4d1a4cf4d64bc1e3b6a42fc6c4c"),
            new IconEntry("skill_deaths_promise", $"{SKILL_DATA_DIR}/Skill_24_DeathsPromise.asset", $"{SKILL_IMAGES}/deaths-promise-v1.png", "2d7eb48c003e459885d798574faa876b"),
            new IconEntry("skill_blood_shade", $"{SKILL_DATA_DIR}/Skill_25_BloodShade.asset", $"{SKILL_IMAGES}/blood-shade-v1.png", "57b7f02a8f02478fb10747ee14cdcf47"),
            new IconEntry("skill_keen_eye", $"{SKILL_DATA_DIR}/Skill_26_KeenEye.asset", $"{SKILL_IMAGES}/keen-eye-v1.png", "7f92230634bd427a96491fa31dc783f1"),
            new IconEntry("skill_holy_ward", $"{SKILL_DATA_DIR}/Skill_27_HolyWard.asset", $"{SKILL_IMAGES}/holy-ward-v1.png", "65eb505b63f24ea487c60fe7a168cdca"),
            new IconEntry("skill_golden_touch", $"{SKILL_DATA_DIR}/Skill_28_GoldenTouch.asset", $"{SKILL_IMAGES}/golden-touch-v1.png", "465df34a00ba43b087137365d574b480"),
            new IconEntry("skill_fates_favor", $"{SKILL_DATA_DIR}/Skill_29_FatesFavor.asset", $"{SKILL_IMAGES}/fates-favor-v1.png", "f37585b3729b4bcc8391d26d69ad80f4"),
        };

        private static readonly IconEntry[] RelicIcons =
        {
            new IconEntry("relic_stone_heart", $"{RELIC_DATA_DIR}/StoneHeart.asset", $"{RELIC_IMAGES}/stone-heart-v1.png", "82788ae7671a440cb1af4c59ed048bc5"),
            new IconEntry("relic_whetstone", $"{RELIC_DATA_DIR}/Whetstone.asset", $"{RELIC_IMAGES}/whetstone-v1.png", "374ed51f3d2243b6971680fd6f44b790"),
            new IconEntry("relic_coin_pouch", $"{RELIC_DATA_DIR}/CoinPouch.asset", $"{RELIC_IMAGES}/coin-pouch-v1.png", "98d7b40bdf1d455a96bcdc16fa2a6b19"),
            new IconEntry("relic_ember_core", $"{RELIC_DATA_DIR}/EmberCore.asset", $"{RELIC_IMAGES}/ember-core-v1.png", "964fea791982469e85ac045ef4e9615c"),
            new IconEntry("relic_deep_vein", $"{RELIC_DATA_DIR}/DeepVein.asset", $"{RELIC_IMAGES}/deep-vein-v1.png", "258a7b33f8dd47c09bc0b09a21375d49"),
            new IconEntry("relic_seers_eye", $"{RELIC_DATA_DIR}/SeersEye.asset", $"{RELIC_IMAGES}/seers-eye-v1.png", "e9a7b41cb3c54e5e96e74ae445f84ee8"),
        };

        private static readonly IconEntry[] WeaponIcons =
        {
            new IconEntry("rusted_blade", $"{WEAPON_DATA_DIR}/RustedBlade.asset", $"{WEAPON_IMAGES}/rusted-blade-v1.png", "0b95654e8c2647b48d05f690e7826f8e"),
            new IconEntry("worn_bow", $"{WEAPON_DATA_DIR}/WornBow.asset", $"{WEAPON_IMAGES}/worn-bow-v1.png", "9d2dd43ca05e45a28545e4ccda26b9c2"),
            new IconEntry("dented_shield", $"{WEAPON_DATA_DIR}/DentedShield.asset", $"{WEAPON_IMAGES}/dented-shield-v1.png", "ea7e15d4d86748ea80ba50b60e74c10c"),
            new IconEntry("chipped_dagger", $"{WEAPON_DATA_DIR}/ChippedDagger.asset", $"{WEAPON_IMAGES}/chipped-dagger-v1.png", "54c9845bf682444aa5ce92f3a8196b98"),
        };

        private static readonly IconEntry[] FormIcons =
        {
            new IconEntry("dark_blade", $"{FORM_DATA_DIR}/DarkBlade.asset", $"{FORM_IMAGES}/dark_blade-v1.png", "5a8d34ad504e428381e0bf6c86b7df50"),
            new IconEntry("void_archer", $"{FORM_DATA_DIR}/VoidArcher.asset", $"{FORM_IMAGES}/void_archer-v1.png", "b219a1fae5014cd4b4a910862031a5cc"),
            new IconEntry("ancient_shield", $"{FORM_DATA_DIR}/AncientShield.asset", $"{FORM_IMAGES}/ancient_shield-v1.png", "8c5738c41f8b434d9ebe8991bbce66d5"),
            new IconEntry("void_thrower", $"{FORM_DATA_DIR}/VoidThrower.asset", $"{FORM_IMAGES}/void_thrower-v1.png", "e837c09147134cbb98c40e59db175fef"),
        };

        /// <summary>
        /// 연결 결과. 네 칸의 합이 항목 수와 같아야 한다 —
        /// 사용자가 실제 연결 수를 로그 한 줄로 읽을 수 있게 하려는 것이다.
        /// </summary>
        public sealed class Summary
        {
            public int Linked;        // 비어 있던 icon 을 이번에 채움
            public int AlreadyLinked; // 이미 같은 그림 — 무변경
            public int KeptExisting;  // 다른 그림이 있어 보존(경고)
            public int Missing;       // 데이터·그림 누락 또는 ID·GUID 불일치(경고, 보존)

            public int Total => Linked + AlreadyLinked + KeptExisting + Missing;

            public void Add(Summary other)
            {
                Linked += other.Linked;
                AlreadyLinked += other.AlreadyLinked;
                KeptExisting += other.KeptExisting;
                Missing += other.Missing;
            }

            public override string ToString() =>
                $"성공 {Linked} · 이미 연결 {AlreadyLinked} · 기존 유지 {KeptExisting} · 누락 {Missing} (합 {Total})";
        }

        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"{LOG_TAG} 플레이 중에는 실행하지 않는다 — 편집 모드에서 다시 실행할 것.");
                return;
            }

            var summary = new Summary();
            summary.Add(ApplySkills());
            summary.Add(ApplyRelics());
            summary.Add(ApplyWeapons());
            summary.Add(ApplyForms());

            if (summary.Linked > 0) AssetDatabase.SaveAssets();

            int expected = SkillIcons.Length + RelicIcons.Length + WeaponIcons.Length + FormIcons.Length;
            Debug.Log($"{LOG_TAG} 전체 {expected}종 — {summary}");
        }

        /// <summary>스킬 18종. 저장하지 않는다(호출한 쪽이 저장).</summary>
        public static Summary ApplySkills() =>
            Apply<SkillData>(SkillIcons, "스킬", so => so.skillId, so => so.icon, (so, s) => so.icon = s);

        /// <summary>유물 6종. 저장하지 않는다(호출한 쪽이 저장).</summary>
        public static Summary ApplyRelics() =>
            Apply<RelicData>(RelicIcons, "유물", so => so.relicId, so => so.icon, (so, s) => so.icon = s);

        /// <summary>무기 4종. 전투 그림(sprite · angleSprites)은 건드리지 않고 icon 만 본다.</summary>
        public static Summary ApplyWeapons() =>
            Apply<WeaponData>(WeaponIcons, "무기", so => so.weaponId, so => so.icon, (so, s) => so.icon = s);

        /// <summary>폼 4종의 UI 아이콘만 연결한다. 몸통·애니메이션·기본 무기는 보존한다.</summary>
        public static Summary ApplyForms() =>
            Apply<FormData>(FormIcons, "폼", so => so.formId, so => so.icon, (so, s) => so.icon = s);

        private static Summary Apply<T>(IconEntry[] entries, string label, Func<T, string> getId,
                                        Func<T, Sprite> getIcon, Action<T, Sprite> setIcon)
            where T : ScriptableObject
        {
            var summary = new Summary();
            foreach (var entry in entries)
            {
                var data = AssetDatabase.LoadAssetAtPath<T>(entry.AssetPath);
                if (data == null)
                {
                    Debug.LogWarning($"{LOG_TAG} {label} 데이터 없음: {entry.Id} ({entry.AssetPath}) — 건너뜀.");
                    summary.Missing += 1;
                    continue;
                }

                string dataId = getId(data);
                if (dataId != entry.Id)
                {
                    Debug.LogWarning($"{LOG_TAG} {label} ID 불일치: {entry.AssetPath} 의 ID 는 '{dataId}', 매핑은 '{entry.Id}' — 건너뜀.");
                    summary.Missing += 1;
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(entry.ImagePath);
                if (guid != entry.ImageGuid)
                {
                    Debug.LogWarning($"{LOG_TAG} {label} 그림 GUID 불일치: {entry.ImagePath} (실제 '{guid}', 기대 '{entry.ImageGuid}') — 건너뜀.");
                    summary.Missing += 1;
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(entry.ImagePath);
                if (sprite == null)
                {
                    Debug.LogWarning($"{LOG_TAG} {label} Sprite 로드 실패: {entry.ImagePath} — 임포트 설정(Sprite)을 확인할 것. 건너뜀.");
                    summary.Missing += 1;
                    continue;
                }

                var current = getIcon(data);
                if (current == sprite)
                {
                    summary.AlreadyLinked += 1;
                    continue;
                }
                if (current != null)
                {
                    Debug.LogWarning($"{LOG_TAG} {label} 기존 아이콘 유지: {entry.Id}.icon = {current.name} (생성 그림 {sprite.name} 은 붙이지 않음).");
                    summary.KeptExisting += 1;
                    continue;
                }

                Undo.RecordObject(data, UNDO_NAME);
                setIcon(data, sprite);
                EditorUtility.SetDirty(data);
                summary.Linked += 1;
                Debug.Log($"{LOG_TAG} {label} 아이콘 연결: {entry.Id}.icon → {sprite.name}");
            }

            Debug.Log($"{LOG_TAG} {label} {entries.Length}종 — {summary}");
            return summary;
        }
    }
}
#endif
