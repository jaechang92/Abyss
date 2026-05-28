using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Player;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// HUD 루트 조정자. HP/폼/스킬 2슬롯/교체 모달을 바인딩·구독.
    /// Analyst MF-3: Active 3번째 드래프트 시 OnDraftSlotReplaceRequested 수신 → Modal.Open.
    /// </summary>
    public sealed class HUDPresenter : MonoBehaviour
    {
        [Header("자식 Presenter")]
        [SerializeField] private HealthBarPresenter healthBar;
        [SerializeField] private FormSlotPresenter formSlot;
        [SerializeField] private SkillSlotPresenter[] skillSlots = new SkillSlotPresenter[2];
        [SerializeField] private ReplacementModal replacementModal;

        [Header("참조 (씬에서 연결)")]
        [SerializeField] private PlayerCharacter player;
        [SerializeField] private DraftSessionController draftSession;

        private readonly List<SkillData> activeBuffer = new();

        private void OnEnable()
        {
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnDraftSlotReplaceRequested += HandleReplaceRequested;
        }

        private void OnDisable()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnDraftSlotReplaceRequested -= HandleReplaceRequested;
        }

        private void Start()
        {
            // PrefabBuilder가 Player prefab을 재생성하면 씬 인스턴스가 갈리며 SerializeField 참조가 끊어질 수 있어 폴백 검색.
            if (player == null) player = FindAnyObjectByType<PlayerCharacter>();

            if (healthBar != null && player != null) healthBar.Bind(player);
            if (formSlot != null && player != null) formSlot.Bind(player.Form);
            RefreshSkillSlots();
        }

        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason reason)
        {
            RefreshSkillSlots();
        }

        private void HandleReplaceRequested(SkillData incoming, IReadOnlyList<SkillData> currentActives)
        {
            if (replacementModal != null)
            {
                replacementModal.Open(incoming, currentActives, draftSession);
            }
        }

        private void RefreshSkillSlots()
        {
            if (skillSlots == null || draftSession == null) return;

            activeBuffer.Clear();
            foreach (var skill in draftSession.Owned)
            {
                if (skill == null) continue;
                if (skill.category != SkillCategory.Active) continue;
                activeBuffer.Add(skill);
                if (activeBuffer.Count >= skillSlots.Length) break;
            }

            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] == null) continue;
                skillSlots[i].SetSkill(i < activeBuffer.Count ? activeBuffer[i] : null);
            }
        }
    }
}
