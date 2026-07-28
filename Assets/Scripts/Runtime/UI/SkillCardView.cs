using System;
using Abyss.Runtime.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 카드 1장. 스킬 이름·카테고리·희귀도·수식·설명 표시.
    /// 선택 Button 클릭 시 OnSelected(index) 발행.
    /// </summary>
    public sealed class SkillCardView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text rarityCategoryText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text formulaText;
        [SerializeField] private Button selectButton;

        private int cardIndex;
        private SkillData currentSkill;

        public SkillData CurrentSkill => currentSkill;
        public int CardIndex => cardIndex;

        public event Action<int> OnSelected;

        private void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(HandleClick);
        }

        public void Bind(int index, SkillData skill)
        {
            cardIndex = index;
            currentSkill = skill;

            bool hasSkill = skill != null;
            if (selectButton != null) selectButton.interactable = hasSkill;

            if (!hasSkill)
            {
                if (nameText != null) nameText.text = "—";
                if (rarityCategoryText != null) rarityCategoryText.text = string.Empty;
                if (descriptionText != null) descriptionText.text = string.Empty;
                if (formulaText != null) formulaText.text = string.Empty;
                if (iconImage != null) iconImage.enabled = false;
                if (background != null) background.color = new Color(0.1f, 0.1f, 0.13f);
                return;
            }

            if (nameText != null) nameText.text = skill.displayName;
            if (rarityCategoryText != null) rarityCategoryText.text = $"{RarityLabel(skill.rarity)} · {skill.category} · [{SynergyAxis.GetDisplayName(skill.synergyTag)}]";
            if (descriptionText != null) descriptionText.text = skill.description;
            if (formulaText != null) formulaText.text = string.IsNullOrEmpty(skill.formulaDescription) ? string.Empty : $"공식: {skill.formulaDescription}";

            if (iconImage != null)
            {
                iconImage.sprite = skill.icon;
                iconImage.enabled = skill.icon != null;
            }

            if (background != null) background.color = RarityBackground(skill.rarity);
        }

        public void TriggerSelectFromKeyboard()
        {
            if (selectButton != null && selectButton.interactable)
            {
                HandleClick();
            }
        }

        private void HandleClick()
        {
            OnSelected?.Invoke(cardIndex);
        }

        private static string RarityLabel(SkillRarity rarity) => rarity switch
        {
            SkillRarity.Common => "★☆☆☆",
            SkillRarity.Rare => "★★☆☆",
            SkillRarity.Epic => "★★★☆",
            SkillRarity.Legendary => "★★★★",
            _ => string.Empty
        };

        private static Color RarityBackground(SkillRarity rarity) => rarity switch
        {
            SkillRarity.Common => new Color(0.18f, 0.2f, 0.22f),
            SkillRarity.Rare => new Color(0.15f, 0.22f, 0.32f),
            SkillRarity.Epic => new Color(0.28f, 0.18f, 0.32f),
            SkillRarity.Legendary => new Color(0.4f, 0.32f, 0.1f),
            _ => new Color(0.15f, 0.15f, 0.18f)
        };
    }
}
