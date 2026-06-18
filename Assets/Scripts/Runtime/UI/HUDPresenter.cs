using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
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
            GameEvents.OnFormSwapped += HandleFormSwapped;
        }

        private void OnDisable()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnDraftSlotReplaceRequested -= HandleReplaceRequested;
            GameEvents.OnFormSwapped -= HandleFormSwapped;
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

        // 폼 교체 시 슬롯을 현재 폼 로드아웃으로 갱신(폼별 스킬 세트 전환).
        private void HandleFormSwapped(FormData previous, FormData next)
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

            // 플레이어 스폰 순서와 무관하게 매 갱신 시 재해석 — Start 시점 null이어도 첫 드래프트에서 바인딩 보장.
            if (player == null) player = FindAnyObjectByType<PlayerCharacter>();

            // 슬롯 순서 SoT는 DraftSessionController가 단일 관리(PlayerCharacter 어빌리티 등록과 동일 규칙).
            // 현재 폼 로드아웃만 노출 — any 스킬은 양 폼 공유, 전용 스킬은 해당 폼에서만.
            string currentFormId = player != null && player.Form != null && player.Form.CurrentForm != null
                ? player.Form.CurrentForm.formId
                : null;
            draftSession.CollectActiveOwned(activeBuffer, skillSlots.Length, currentFormId);

            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] == null) continue;
                // 쿨다운 게이지 소스(플레이어+슬롯 인덱스) 연결 후 스킬 표시.
                if (player != null) skillSlots[i].BindCooldownSource(player, i);
                skillSlots[i].SetSkill(i < activeBuffer.Count ? activeBuffer[i] : null);
            }
        }
    }
}
