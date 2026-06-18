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

        /// <summary>
        /// 현재 폼에서 쓸 수 있는 Active 스킬만 등장 순서대로 buffer에 채운다(최대 limit개).
        /// 폼별 로드아웃의 단일 기준점(SoT) — HUD(SkillSlotPresenter 배치)와 PlayerCharacter(어빌리티 등록)가 공유.
        /// any(formBound 빈) 스킬은 모든 폼 공유, 전용 스킬은 해당 폼에서만 노출된다.
        /// </summary>
        public void CollectActiveOwned(List<SkillData> buffer, int limit, string currentFormId)
        {
            if (buffer == null) return;
            buffer.Clear();
            for (int i = 0; i < owned.Count; i++)
            {
                if (!IsSkillUsableInForm(owned[i], currentFormId)) continue;
                buffer.Add(owned[i]);
                if (buffer.Count >= limit) break;
            }
        }

        /// <summary>
        /// 스킬이 해당 폼 컨텍스트에서 슬롯에 오를 수 있는지. Active이고 폼 귀속이 없거나(any)
        /// 현재 폼과 일치해야 한다. 슬롯 표시·보유 상한·교체 모달의 공통 기준(SoT).
        /// </summary>
        private static bool IsSkillUsableInForm(SkillData skill, string currentFormId)
        {
            if (skill == null || skill.category != SkillCategory.Active) return false;
            if (string.IsNullOrEmpty(skill.formBound)) return true; // any — 모든 폼 공유
            return skill.formBound == currentFormId;
        }

        /// <summary>현재 폼 ID(폼 미연결 시 빈 문자열). 추첨·상한·교체 모달이 공유.</summary>
        private string CurrentFormId()
        {
            var fc = ResolveFormController();
            return fc != null && fc.CurrentForm != null ? fc.CurrentForm.formId : string.Empty;
        }
        public DraftOptions CurrentOptions => currentOptions;
        public int RerollsUsed => rerollsUsed;
        public int SkipReward => skipReward;

        private void Awake()
        {
            pool = GetComponent<DraftPoolManager>();
            ResolveFormController();
        }

        /// <summary>
        /// formController 폴백 해석. 씬 SerializeField 연결이 끊겨도(빌더 미실행·프리팹 재생성)
        /// 런타임에 FormController를 탐색해 연결한다. 미연결 시 currentFormId가 빈 문자열이 되어
        /// 폼 귀속 스킬이 드래프트에서 영구 필터링되는 문제를 방지한다(HUD 자동 와이어링과 동일 패턴).
        /// </summary>
        private FormController ResolveFormController()
        {
            if (formController == null)
            {
                formController = FindAnyObjectByType<FormController>(FindObjectsInactive.Include);
                if (formController == null)
                {
                    Debug.LogWarning("[Draft] FormController 미발견 — 폼 귀속 스킬이 드래프트에 노출되지 않습니다.");
                }
            }
            return formController;
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

            // 보유 상한은 현재 폼 컨텍스트 기준 — 폼별로 독립된 슬롯 2칸을 갖는다(폼별 로드아웃).
            string formId = CurrentFormId();
            if (chosen.category == SkillCategory.Active && CountActiveOwnedForForm(formId) >= activeSlotLimit)
            {
                GameEvents.RaiseDraftSlotReplaceRequested(chosen, SnapshotActiveOwnedForForm(formId));
                return true;
            }

            AcquireSkill(chosen);
            GameEvents.RaiseSkillDrafted(chosen, currentReason);
            CloseSession();
            return true;
        }

        /// <summary>
        /// 치트/디버그용 즉시 지급. 슬롯 상한·세션 상태 무시하고 보유에 추가 + OnSkillDrafted 발행
        /// (HUD·PlayerCharacter 슬롯 갱신 트리거).
        /// </summary>
        public void DebugGrantSkill(SkillData skill)
        {
            if (skill == null) return;
            AcquireSkill(skill);
            GameEvents.RaiseSkillDrafted(skill, DraftTriggerReason.LevelUp);
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
            string currentFormId = CurrentFormId();
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

        private int CountActiveOwnedForForm(string currentFormId)
        {
            int count = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (IsSkillUsableInForm(owned[i], currentFormId)) count += 1;
            }
            return count;
        }

        private IReadOnlyList<SkillData> SnapshotActiveOwnedForForm(string currentFormId)
        {
            var result = new List<SkillData>();
            for (int i = 0; i < owned.Count; i++)
            {
                if (IsSkillUsableInForm(owned[i], currentFormId)) result.Add(owned[i]);
            }
            return result;
        }
    }
}
