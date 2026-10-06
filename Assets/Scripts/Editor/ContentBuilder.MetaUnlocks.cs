#if UNITY_EDITOR
using System.IO;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Meta;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 메타 해금 1단계(2026-10-06, 08-content-roadmap §7-2 「M2 1단계」) — 신규 스킬 3종을 잠그고 제단에서 해금한다.
    ///
    /// 🔵 결정 근거: 2026-08-19 「기존 콘텐츠는 안 뺏고 신규 콘텐츠만 잠근다」 + 2026-09-02 「19번째 스킬이 생기는 작업에 묶는다」.
    /// 기존 18종·폼 4종은 잠그지 않는다(MetaUnlockTests 가 지킨다). 시작 특전은 이미 2종(시작 골드·무료 리롤)이 있고,
    /// 로드맵의 「FormC」는 새 폼이 없어 이번 범위 밖이다.
    ///
    /// 해금 업그레이드 에셋은 로비 씬 빌더가 아니라 여기서 만든다 — 그 빌더는 로비 씬 전체를 다시 짓는다.
    /// 둘 다 <b>Upsert</b>라 재실행 시 값이 이 표로 보정된다.
    /// </summary>
    public static partial class ContentBuilder
    {
        // (skillId, 비용) — 런을 바꾸는 효과가 큰 순으로 비싸다. 로드맵 1단계 범위 10~50 안.
        private static readonly (string SkillId, int Cost)[] SkillUnlocks =
        {
            (SkillIds.DEATHS_PROMISE, 30),
            (SkillIds.CRIMSON_RADIANCE, 45),
            (SkillIds.FATES_FAVOR, 50),
        };

        private static void ApplyMetaUnlocks()
        {
            if (!Directory.Exists(AbyssPaths.MetaUpgrades)) Directory.CreateDirectory(AbyssPaths.MetaUpgrades);

            foreach (var (skillId, cost) in SkillUnlocks)
            {
                var skill = FindSkillById(skillId);
                if (skill == null)
                {
                    Debug.LogWarning($"[ContentBuilder] 해금 대상 스킬 없음: {skillId} — 잠그지 않고 해금 항목도 만들지 않는다");
                    continue;
                }

                // 잠금이 먼저 존재하고 해금 항목이 없으면 그 스킬은 영영 안 나온다 — 둘을 같은 자리에서 함께 만든다.
                UpsertSkillUnlock(skillId, cost);
                if (!skill.requiresMetaUnlock)
                {
                    skill.requiresMetaUnlock = true;
                    EditorUtility.SetDirty(skill);
                    Debug.Log($"[ContentBuilder] 메타 해금 대상으로 잠금: {skillId} (제단 {cost} 조각)");
                }
            }

            AssetDatabase.SaveAssets();
            MetaUpgrades.Reload();
        }

        /// <summary>파일명 <c>Unlock_{PascalId}</c> — 제단 목록에서 스탯·특전 뒤에 모인다(카탈로그는 이름순 로드).</summary>
        private static void UpsertSkillUnlock(string skillId, int cost)
        {
            string fileName = "Unlock_" + ToPascalCase(skillId.Replace("skill_", string.Empty));
            string path = $"{AbyssPaths.MetaUpgrades}/{fileName}.asset";

            var data = AssetDatabase.LoadAssetAtPath<MetaUpgradeData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MetaUpgradeData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.upgradeId = "unlock_" + skillId;
            data.type = MetaUpgradeType.UnlockSkill;
            data.unlockTargetId = skillId;
            data.valuePerLevel = 0f;
            data.costLadder = new[] { cost };   // 길이 1 = 1회 구매로 해금
            // 이름·설명은 제단 패널이 대상 스킬에서 읽는다(MetaUpgradePanel) — 스킬 문구와 두 벌이 되지 않게 비운다.
            data.nameKey = string.Empty;
            data.descKey = string.Empty;
            EditorUtility.SetDirty(data);
        }
    }
}
#endif
