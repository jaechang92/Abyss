using Abyss.Runtime.Draft;
using Abyss.Runtime.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// HUD의 Active 스킬 한 슬롯. 상한 2 (Analyst MF-3), 2개 인스턴스를 HUD에 배치.
    /// 쿨다운 게이지는 PlayerCharacter의 슬롯 어빌리티 잔여 쿨다운을 매 프레임 폴링해 표시.
    /// </summary>
    public sealed class SkillSlotPresenter : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private Text labelText;

        private SkillData currentSkill;
        private PlayerCharacter cooldownOwner;
        private int slotIndex = -1;

        public SkillData CurrentSkill => currentSkill;

        /// <summary>쿨다운 게이지 소스 연결. HUDPresenter가 슬롯 인덱스와 함께 호출.</summary>
        public void BindCooldownSource(PlayerCharacter owner, int index)
        {
            cooldownOwner = owner;
            slotIndex = index;
        }

        private void Update()
        {
            if (cooldownFill == null) return;

            if (currentSkill == null || cooldownOwner == null || slotIndex < 0)
            {
                cooldownFill.fillAmount = 0f;
                return;
            }

            cooldownFill.fillAmount = cooldownOwner.GetSlotCooldownFill(slotIndex);
        }

        public void SetSkill(SkillData skill)
        {
            currentSkill = skill;

            if (iconImage != null)
            {
                // 슬롯 표시 여부는 "스킬 보유" 기준. icon 스프라이트가 없어도(현재 스킬 에셋들)
                // sprite=null + enabled로 생성 시 지정한 회색 플레이스홀더 박스가 그려져, 획득한 슬롯이 보인다.
                iconImage.sprite = skill != null ? skill.icon : null;
                iconImage.enabled = skill != null;
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
