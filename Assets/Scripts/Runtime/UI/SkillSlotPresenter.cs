using Abyss.Runtime.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// HUD의 Active 스킬 한 슬롯. 상한 2 (Analyst MF-3), 2개 인스턴스를 HUD에 배치.
    /// 쿨다운 게이지는 후속 태스크(스킬 어빌리티 연결) 시 Ability 인스턴스 참조로 연동.
    /// </summary>
    public sealed class SkillSlotPresenter : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Text labelText;

        private SkillData currentSkill;

        public SkillData CurrentSkill => currentSkill;

        public void SetSkill(SkillData skill)
        {
            currentSkill = skill;

            if (iconImage != null)
            {
                iconImage.sprite = skill != null ? skill.icon : null;
                iconImage.enabled = skill != null && skill.icon != null;
            }
            if (labelText != null)
            {
                labelText.text = skill != null ? skill.displayName : "—";
            }
            if (cooldownFill != null)
            {
                cooldownFill.fillAmount = 0f;
            }
        }
    }
}
