#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Meta;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 유물 SO를 <c>Resources/Data/Relics/</c>에 생성한다. 메뉴 경로는 <see cref="AbyssMenu.GenerateRelicContent"/>.
    ///
    /// 🔴 <b>생성 경로만 두지 않는다.</b> 이 프로젝트는 <c>CreateOrLoad</c>가 기존 에셋을 통째로
    /// 건너뛰는 바람에 <i>"코드에는 있는데 게임에는 없는"</i> 상태를 세 번 겪었다
    /// (EnemyTier · 적 SFX · 상점 리롤권). 건너뛰기 규약은 <b>손으로 맞춘 수치를 지키려는 것</b>이지
    /// 목록을 영원히 얼려 두려던 것이 아니다.
    /// → <see cref="Generate"/> 자체가 <b>추가 전용</b>이다. 기존 에셋은 읽지도 고치지도 않고
    ///   <b>relicId 에 해당하는 파일이 없는 것만</b> 새로 만든다. 그래서 정의에 유물을 더하고
    ///   메뉴를 다시 돌리면 그것만 들어온다 — 손으로 맞춘 수치는 그대로 남는다.
    ///
    /// 효과 어휘는 <see cref="MetaUpgradeType"/>를 그대로 쓴다(<see cref="RelicData"/> 주석 참조).
    /// 해금 2종은 유물이 쓸 수 없으므로 여기서도 만들지 않는다.
    /// </summary>
    public static class RelicContentBuilder
    {
        [MenuItem(AbyssMenu.GenerateRelicContent)]
        public static void Generate()
        {
            EnsureDir(AbyssPaths.Relics);

            int created = 0;
            foreach (var def in Definitions())
            {
                if (CreateOrSkip(def)) created += 1;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelicCatalog.Reload();

            Debug.Log($"[RelicContentBuilder] 유물 {created}개 신규 생성 (기존은 유지) — 총 {Definitions().Length}종 정의.");
        }

        /// <summary>
        /// 유물 정의. 등급을 나누는 기준은 <b>효과의 크기가 아니라 성격</b>이다 —
        /// Common 은 어느 빌드에나 들어가는 무난한 가산, Rare 는 한 축을 확실히 밀어 주는 것,
        /// Epic 은 플레이 방식을 바꾸는 것. 크기로만 나누면 "좋은 것/덜 좋은 것"이 되어
        /// 슬롯 3칸이 항상 같은 조합으로 굳는다.
        ///
        /// 초기 6종은 등급별로 고르게 둔다(Common 3 · Rare 2 · Epic 1) — 단일 가중 추첨이라
        /// <b>실효 확률이 등급별 개수에 비례</b>하기 때문에, 개수가 치우치면 가중치 의도가 어긋난다.
        /// </summary>
        private static RelicDef[] Definitions() => new[]
        {
            // ── Common: 무난한 가산. 슬롯이 비는 것보다 낫다는 정도. ──
            new RelicDef("relic_stone_heart", "Relic_StoneHeart_Name", "Relic_StoneHeart_Desc",
                RelicRarity.Common, MetaUpgradeType.MaxHp, 8f, 5),
            new RelicDef("relic_whetstone", "Relic_Whetstone_Name", "Relic_Whetstone_Desc",
                RelicRarity.Common, MetaUpgradeType.AttackMultiplier, 0.03f, 5),
            new RelicDef("relic_coin_pouch", "Relic_CoinPouch_Name", "Relic_CoinPouch_Desc",
                RelicRarity.Common, MetaUpgradeType.StartingGold, 15f, 5),

            // ── Rare: 한 축을 확실히 민다. Common 의 두 배 남짓. ──
            new RelicDef("relic_ember_core", "Relic_EmberCore_Name", "Relic_EmberCore_Desc",
                RelicRarity.Rare, MetaUpgradeType.AttackMultiplier, 0.07f, 4),
            new RelicDef("relic_deep_vein", "Relic_DeepVein_Name", "Relic_DeepVein_Desc",
                RelicRarity.Rare, MetaUpgradeType.MaxHp, 20f, 4),

            // ── Epic: 고르는 방식을 바꾼다. 리롤이 공짜면 드래프트를 대하는 태도가 달라진다. ──
            new RelicDef("relic_seers_eye", "Relic_SeersEye_Name", "Relic_SeersEye_Desc",
                RelicRarity.Epic, MetaUpgradeType.FreeReroll, 1f, 3),
        };

        /// <summary>
        /// 없는 유물만 만든다. 기존 에셋은 <b>읽지도 고치지도 않는다</b> —
        /// 인스펙터에서 손으로 맞춘 등급·수치를 빌더 재실행이 되돌리면 안 된다.
        /// </summary>
        private static bool CreateOrSkip(RelicDef def)
        {
            string path = $"{AbyssPaths.Relics}/{ToFileName(def.RelicId)}.asset";
            if (AssetDatabase.LoadAssetAtPath<RelicData>(path) != null) return false;

            var so = ScriptableObject.CreateInstance<RelicData>();
            so.relicId = def.RelicId;
            so.nameKey = def.NameKey;
            so.descKey = def.DescKey;
            so.rarity = def.Rarity;
            so.effectType = def.EffectType;
            so.valuePerLevel = def.ValuePerLevel;
            so.maxLevel = def.MaxLevel;

            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[RelicContentBuilder] 생성: {path}");
            return true;
        }

        /// <summary>relic_ember_core → EmberCore. 파일명은 PascalCase(다른 SO 폴더와 같은 규약).</summary>
        private static string ToFileName(string relicId)
        {
            string trimmed = relicId.StartsWith("relic_") ? relicId.Substring("relic_".Length) : relicId;
            var parts = trimmed.Split('_');
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1) sb.Append(part.Substring(1));
            }
            return sb.ToString();
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private readonly struct RelicDef
        {
            public readonly string RelicId;
            public readonly string NameKey;
            public readonly string DescKey;
            public readonly RelicRarity Rarity;
            public readonly MetaUpgradeType EffectType;
            public readonly float ValuePerLevel;
            public readonly int MaxLevel;

            public RelicDef(string relicId, string nameKey, string descKey, RelicRarity rarity,
                            MetaUpgradeType effectType, float valuePerLevel, int maxLevel)
            {
                RelicId = relicId;
                NameKey = nameKey;
                DescKey = descKey;
                Rarity = rarity;
                EffectType = effectType;
                ValuePerLevel = valuePerLevel;
                MaxLevel = maxLevel;
            }
        }
    }
}
#endif
