using Abyss.Runtime.Draft;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>제단 해금 항목의 이름과 설명을 원본 데이터에서 조회한다.</summary>
    public sealed partial class MetaUpgradePanel
    {
        private static string ResolveRowName(MetaUpgradeData data)
        {
            switch (data.type)
            {
                case MetaUpgradeType.UnlockSkill:
                {
                    var skill = FindSkill(data.unlockTargetId);
                    return Loc.GetFormat(StringKey.Altar_UnlockSkillNameFormat,
                        skill != null ? skill.displayName : data.unlockTargetId);
                }
                case MetaUpgradeType.UnlockForm:
                {
                    var form = FormCatalog.GetById(data.unlockTargetId);
                    return Loc.GetFormat(StringKey.Altar_UnlockFormNameFormat,
                        form != null ? form.LocalizedName : data.unlockTargetId);
                }
                default:
                    return Loc.Get(data.nameKey);
            }
        }

        private static string ResolveRowDescription(MetaUpgradeData data)
        {
            switch (data.type)
            {
                case MetaUpgradeType.UnlockSkill:
                {
                    var skill = FindSkill(data.unlockTargetId);
                    return skill != null ? skill.description : string.Empty;
                }
                case MetaUpgradeType.UnlockForm:
                {
                    var form = FormCatalog.GetById(data.unlockTargetId);
                    return form != null ? form.description : string.Empty;
                }
                default:
                    return Loc.Get(data.descKey);
            }
        }

        private static SkillData FindSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            var all = SkillCatalog.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].skillId == skillId) return all[i];
            }
            return null;
        }
    }
}
