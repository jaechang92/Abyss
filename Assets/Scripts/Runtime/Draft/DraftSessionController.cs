using System.Collections.Generic;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 드래프트 세션 흐름 제어. OnPlayerLevelUp 구독 → 3장 뽑기 → 선택/리롤/스킵 처리.
    /// DraftPoolManager(풀)·DraftWeightCalculator(가중치)와 역할 분리 (500줄 규칙).
    /// Active 상한 2 초과 시 OnDraftSlotReplaceRequested 발행 — 실제 모달 UI는 P-17.
    /// </summary>
    [RequireComponent(typeof(DraftPoolManager))]
    public sealed class DraftSessionController : MonoBehaviour
    {
        [Header("Analyst 확정 (03-skill-draft-system.md §2)")]
        [SerializeField] private int optionCount = 3;
        [SerializeField] private int[] rerollCostLadder = { 15, 30 };
        [SerializeField] private int skipReward = 10;
        [SerializeField] private int activeSlotLimit = 2;

        [Header("선택 참조 (런타임 연결)")]
        [SerializeField] private FormController formController;

        private DraftPoolManager pool;
        private readonly List<SkillData> owned = new();
        private readonly HashSet<string> ownedSynergyTags = new();
        private readonly HashSet<string> ownedSkillIds = new();

        private DraftOptions currentOptions;
        private DraftTriggerReason currentReason;
        private int rerollsUsed;
        private bool isSessionActive;

        public bool IsSessionActive => isSessionActive;
        public IReadOnlyList<SkillData> Owned => owned;
        public DraftOptions CurrentOptions => currentOptions;
        public int RerollsUsed => rerollsUsed;
        public int SkipReward => skipReward;

        private void Awake()
        {
            pool = GetComponent<DraftPoolManager>();
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;
        }

        public int GetRerollCost()
        {
            if (rerollsUsed >= rerollCostLadder.Length) return int.MaxValue;
            return rerollCostLadder[rerollsUsed];
        }

        public bool CanReroll()
        {
            if (!isSessionActive || rerollsUsed >= rerollCostLadder.Length) return false;
            return RunManager.Instance != null && RunManager.Instance.GoldShards >= GetRerollCost();
        }

        public bool TryReroll()
        {
            if (!CanReroll()) return false;

            int cost = GetRerollCost();
            if (!RunManager.Instance.SpendGoldShards(cost)) return false;

            rerollsUsed += 1;
            DrawAndAnnounce();
            return true;
        }

        public bool TrySkip()
        {
            if (!isSessionActive) return false;

            RunManager.Instance?.GainGoldShards(skipReward);
            CloseSession();
            return true;
        }

        public bool TrySelect(int cardIndex)
        {
            if (!isSessionActive || currentOptions == null) return false;
            if (cardIndex < 0 || cardIndex >= currentOptions.Cards.Count) return false;

            var chosen = currentOptions.Cards[cardIndex];
            if (chosen == null) return false;

            if (chosen.category == SkillCategory.Active && CountActiveOwned() >= activeSlotLimit)
            {
                GameEvents.RaiseDraftSlotReplaceRequested(chosen, SnapshotActiveOwned());
                return true;
            }

            AcquireSkill(chosen);
            GameEvents.RaiseSkillDrafted(chosen, currentReason);
            CloseSession();
            return true;
        }

        /// <summary>교체 모달에서 기존 슬롯을 버리기로 결정한 뒤 호출.</summary>
        public bool ConfirmReplacement(string droppedSkillId, SkillData incoming)
        {
            if (!isSessionActive || incoming == null) return false;

            RemoveOwned(droppedSkillId);
            AcquireSkill(incoming);
            GameEvents.RaiseSkillDrafted(incoming, currentReason);
            CloseSession();
            return true;
        }

        private void HandleLevelUp(int newLevel, DraftTriggerReason reason)
        {
            if (isSessionActive) return;

            currentReason = reason;
            rerollsUsed = 0;
            isSessionActive = true;

            DrawAndAnnounce();
            GameEvents.RaiseDraftOpened();
        }

        private void DrawAndAnnounce()
        {
            string currentFormId = formController != null && formController.CurrentForm != null
                ? formController.CurrentForm.formId
                : string.Empty;

            var cards = pool.DrawOptions(optionCount, currentFormId, ownedSynergyTags, ownedSkillIds);
            currentOptions = new DraftOptions(cards, currentReason, rerollsUsed);
            GameEvents.RaiseDraftOptionsReady(currentOptions);
        }

        private void CloseSession()
        {
            isSessionActive = false;
            currentOptions = null;
            GameEvents.RaiseDraftClosed();
        }

        private void AcquireSkill(SkillData skill)
        {
            owned.Add(skill);
            ownedSkillIds.Add(skill.skillId);
            if (!string.IsNullOrEmpty(skill.synergyTag)) ownedSynergyTags.Add(skill.synergyTag);
        }

        private void RemoveOwned(string skillId)
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] != null && owned[i].skillId == skillId)
                {
                    var removed = owned[i];
                    owned.RemoveAt(i);
                    ownedSkillIds.Remove(skillId);
                    RecomputeSynergyTags();
                    Debug.Log($"[Draft] 교체: {removed.displayName} 제거");
                    break;
                }
            }
        }

        private void RecomputeSynergyTags()
        {
            ownedSynergyTags.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null && !string.IsNullOrEmpty(owned[i].synergyTag))
                {
                    ownedSynergyTags.Add(owned[i].synergyTag);
                }
            }
        }

        private int CountActiveOwned()
        {
            int count = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null && owned[i].category == SkillCategory.Active) count += 1;
            }
            return count;
        }

        private IReadOnlyList<SkillData> SnapshotActiveOwned()
        {
            var result = new List<SkillData>();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null && owned[i].category == SkillCategory.Active) result.Add(owned[i]);
            }
            return result;
        }
    }
}
