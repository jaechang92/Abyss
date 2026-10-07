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

        // 드래프트 시안: 카드 면은 일정한 짙은 청회색, 희귀도는 위 가장자리 작은 띠 색으로만 보인다.
        private static readonly Color DraftFaceColor = new(0.067f, 0.106f, 0.145f); // #111B25
        private static readonly Color EmptyFaceColor = new(0.1f, 0.1f, 0.13f);

        // 희귀도 띠 밝기 보정(드래프트 전용). RarityBackground는 어두운 면용 색이라 4px 띠로는 안 보인다.
        private static readonly Color AccentIvory = new(0.93f, 0.89f, 0.80f);
        private const float ACCENT_IVORY_BLEND = 0.35f;

        // DraftArtSkin이 붙이는 희귀도 띠. 없으면(스킨 미적용) 예전처럼 면 전체를 희귀도 색으로 칠한다.
        private Image rarityAccent;

        private int cardIndex;
        private SkillData currentSkill;
        private SkillCardFocusRelay focusRelay;
        private SkillCardDetailsPanel details;

        public SkillData CurrentSkill => currentSkill;
        public int CardIndex => cardIndex;

        /// <summary>EventSystem 포커스가 실제로 머무는 오브젝트(선택 버튼). 없으면 null.</summary>
        public GameObject FocusTarget => selectButton != null ? selectButton.gameObject : null;

        /// <summary>포커스를 줄 만한 카드인가 — 켜져 있고 스킬이 바인딩돼 버튼이 눌리는 상태.</summary>
        public bool CanReceiveFocus =>
            isActiveAndEnabled && currentSkill != null && selectButton != null && selectButton.IsInteractable();

        public event Action<int> OnSelected;

        private void Awake()
        {
            if (selectButton == null) return;
            selectButton.onClick.AddListener(HandleClick);

            // select/deselect는 선택된 버튼 GameObject에만 온다 — 카드 루트가 아니라 버튼에 릴레이를 단다.
            focusRelay = selectButton.GetComponent<SkillCardFocusRelay>();
            if (focusRelay == null) focusRelay = selectButton.gameObject.AddComponent<SkillCardFocusRelay>();
            focusRelay.OnFocusSelected += HandleFocusSelected;
            focusRelay.OnFocusDeselected += HandleFocusDeselected;
            focusRelay.OnPointerEntered += HandlePointerEntered;
            focusRelay.OnPointerExited += HandlePointerExited;
        }

        private void OnDisable()
        {
            if (details != null) details.ForgetCard(this);
        }

        private void OnDestroy()
        {
            if (focusRelay == null) return;
            focusRelay.OnFocusSelected -= HandleFocusSelected;
            focusRelay.OnFocusDeselected -= HandleFocusDeselected;
            focusRelay.OnPointerEntered -= HandlePointerEntered;
            focusRelay.OnPointerExited -= HandlePointerExited;
        }

        /// <summary>상세 패널 연결. <see cref="DraftPanelPresenter"/>가 런타임에 만든 패널을 넘긴다.</summary>
        public void AttachDetails(SkillCardDetailsPanel panel)
        {
            details = panel;
        }

        /// <summary>희귀도 띠 연결. Bind보다 늦게 붙어도 현재 스킬 기준으로 바로 칠한다.</summary>
        public void AttachRarityAccent(Image accent)
        {
            rarityAccent = accent;
            ApplyFaceStyle(currentSkill);
        }

        public void Bind(int index, SkillData skill)
        {
            BindView(index, skill);
            if (details != null) details.NotifyCardRebound(this);
        }

        private void BindView(int index, SkillData skill)
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
                ApplyFaceStyle(null);
                return;
            }

            if (nameText != null) nameText.text = skill.displayName;
            if (rarityCategoryText != null) rarityCategoryText.text = SkillDisplay.Headline(skill);
            if (descriptionText != null) descriptionText.text = skill.description;
            if (formulaText != null) formulaText.text = string.IsNullOrEmpty(skill.formulaDescription) ? string.Empty : $"공식: {skill.formulaDescription}";

            if (iconImage != null)
            {
                // 빌더가 남긴 회색 tint를 지워 아이콘 원색을 보인다. 아이콘 없는 스킬은 숨김(대체 그림 없음).
                iconImage.sprite = skill.icon;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.enabled = skill.icon != null;
            }

            ApplyFaceStyle(skill);
        }

        /// <summary>
        /// 카드 면·희귀도 띠. 빈 카드는 기존 빈 면 색 + 띠 숨김.
        /// 띠가 있으면 면은 일정 색, 띠 색만 <see cref="SkillDisplay.RarityBackground"/>(도감과 같은 SoT)를 따른다.
        /// </summary>
        private void ApplyFaceStyle(SkillData skill)
        {
            bool hasSkill = skill != null;
            if (rarityAccent != null)
            {
                rarityAccent.enabled = hasSkill;
                if (hasSkill)
                {
                    rarityAccent.color = Color.Lerp(SkillDisplay.RarityBackground(skill.rarity),
                        AccentIvory, ACCENT_IVORY_BLEND);
                }
            }

            if (background == null) return;
            background.color = !hasSkill ? EmptyFaceColor
                : rarityAccent != null ? DraftFaceColor
                : SkillDisplay.RarityBackground(skill.rarity);
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

        // 포커스·호버는 상세 표시만 바꾼다. 스킬 선택(OnSelected)은 클릭/키 입력에서만 일어난다.
        private void HandleFocusSelected()
        {
            if (details != null) details.NotifyFocus(this, true);
        }

        private void HandleFocusDeselected()
        {
            if (details != null) details.NotifyFocus(this, false);
        }

        private void HandlePointerEntered()
        {
            if (details != null) details.NotifyHover(this, true);
        }

        private void HandlePointerExited()
        {
            if (details != null) details.NotifyHover(this, false);
        }
    }
}
