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
        [Header("선택 참조 (런타임 연결)")]
        [SerializeField] private FormController formController;

        // 드래프트 수치는 RunConfig(SoT)에서 읽는다(P1-B #9 후속 — 하드코딩 죽은 데이터 제거).
        // config 미로드 시 아래 폴백 기본값. 값은 Analyst 확정 스펙(03-skill-draft-system.md §2) 및 RunConfig 기본값과 일치.
        private const int DEFAULT_OPTION_COUNT = 3;
        private static readonly int[] DEFAULT_REROLL_LADDER = { 15, 30 };
        private const int DEFAULT_SKIP_REWARD = 10;
        private const int DEFAULT_ACTIVE_SLOT_LIMIT = 2;

        private RunConfig config;

        private int OptionCount => config != null ? Mathf.Max(1, config.draftOptionCount) : DEFAULT_OPTION_COUNT;
        private int[] RerollCostLadder => (config != null && config.rerollCostLadder != null) ? config.rerollCostLadder : DEFAULT_REROLL_LADDER;
        private int ActiveSlotLimit => config != null ? Mathf.Max(1, config.activeSlotLimit) : DEFAULT_ACTIVE_SLOT_LIMIT;

        private DraftPoolManager pool;
        private readonly List<SkillData> owned = new();
        private readonly HashSet<string> ownedSynergyTags = new();
        private readonly HashSet<string> ownedSkillIds = new();

        private DraftOptions currentOptions;
        private DraftTriggerReason currentReason;
        private int rerollsUsed;
        private bool isSessionActive;

        /// <summary>
        /// 상점·이벤트에서 산 리롤권 잔량. <b>런 단위</b>다 —
        /// <see cref="rerollsUsed"/>가 드래프트마다 0으로 돌아가는 것과 다르다.
        /// 사다리는 매 드래프트에 새로 주어지는 기본 권리이고, 티켓은 사서 쟁여 두는 물건이라
        /// 한 드래프트에서 다 써도 되고 아껴 뒀다 다음 드래프트에서 써도 된다.
        ///
        /// 런 시작 시 별도 초기화가 없는 이유는 Run 씬이 런마다 새로 로드되어
        /// 이 컴포넌트 자체가 새로 만들어지기 때문이다.
        /// </summary>
        private int extraRerollStock;

        // 세션 진행 중 도착한 레벨업(다중 레벨업·보스/엘리트 보너스)을 대기시켜 순차 처리한다.
        // 큐잉이 없으면 세션 중 발생한 레벨업 이벤트가 전부 폐기된다.
        private readonly Queue<DraftTriggerReason> pendingReasons = new();

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

        /// <summary>남은 리롤권 수. HUD·드래프트 패널 표시용.</summary>
        public int ExtraRerollStock => extraRerollStock;

        /// <summary>
        /// 지금 리롤을 누르면 리롤권이 소모되는가. 화면이 "0 gold"와 "리롤권"을 구분해야 하기 때문에 있다 —
        /// 둘 다 공짜지만 <b>특전은 매 드래프트 되살아나고 티켓은 쓰면 없어진다</b>.
        /// 같은 문구로 보이면 아껴 뒀어야 할 것을 모르고 태운다.
        /// </summary>
        public bool NextRerollUsesTicket =>
            isSessionActive && rerollsUsed >= RerollCostLadder.Length && extraRerollStock > 0;

        /// <summary>
        /// 리롤권 지급(상점·이벤트 보상). 음수·0은 무시한다.
        /// </summary>
        public void GrantExtraRerolls(int count)
        {
            if (count <= 0) return;
            extraRerollStock += count;
            Debug.Log($"[Draft] 리롤권 +{count} (잔량 {extraRerollStock})");
        }
        public int SkipReward => config != null ? config.skipReward : DEFAULT_SKIP_REWARD;

        private void Awake()
        {
            pool = GetComponent<DraftPoolManager>();
            config = RunConfigProvider.Current; // RunConfig SoT — 미로드 시 폴백 기본값.
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

        /// <summary>
        /// 이번 리롤의 비용. 상한(<c>ladder.Length</c>)을 넘으면 <see cref="int.MaxValue"/>.
        ///
        /// 무료 리롤 특전(3-2)은 <b>앞에서부터 그 횟수만큼을 0으로</b> 만든다.
        /// 리롤 <b>가능 횟수</b>는 안 늘린다 — 상한을 건드리면 드래프트 한 번에 볼 수 있는
        /// 카드 수가 바뀌어 밸런스가 흔들린다. 특전이 없으면 0회라 지금까지와 같다.
        /// </summary>
        public int GetRerollCost()
        {
            var ladder = RerollCostLadder;

            // 판정 순서가 곧 규칙이다. 리롤권이 0장이면 아래 세 줄은 예전 코드와 한 글자도 다르지 않게
            // 동작한다 — 사다리 밖은 불가, 특전 구간은 0, 나머지는 사다리 값.
            if (rerollsUsed >= ladder.Length + extraRerollStock) return int.MaxValue;
            if (rerollsUsed >= ladder.Length) return 0;   // 티켓 구간 — 값은 상점에서 이미 치렀다
            if (rerollsUsed < Meta.MetaUpgrades.FreeRerollCount()) return 0;
            return ladder[rerollsUsed];
        }

        public bool CanReroll()
        {
            if (!isSessionActive || rerollsUsed >= RerollCostLadder.Length + extraRerollStock) return false;
            return RunManager.Instance != null && RunManager.Instance.GoldShards >= GetRerollCost();
        }

        public bool TryReroll()
        {
            if (!CanReroll()) return false;

            // 티켓 소모 여부는 지불 <b>전에</b> 확정한다. rerollsUsed 를 올린 뒤에 물으면
            // 사다리 마지막 리롤이 티켓 구간에 들어가 있어, 골드로 산 리롤이 티켓까지 함께 먹는다.
            bool usesTicket = NextRerollUsesTicket;

            int cost = GetRerollCost();
            if (!RunManager.Instance.SpendGoldShards(cost)) return false;

            if (usesTicket) extraRerollStock -= 1;
            rerollsUsed += 1;
            DrawAndAnnounce();
            return true;
        }

        public bool TrySkip()
        {
            if (!isSessionActive) return false;

            RunManager.Instance?.GainGoldShards(SkipReward);
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
            if (chosen.category == SkillCategory.Active && CountActiveOwnedForForm(formId) >= ActiveSlotLimit)
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

            // 제거가 실패(잘못된 id)하면 신규만 추가돼 슬롯 상한을 초과한다 → 획득 중단.
            int droppedIndex = RemoveOwned(droppedSkillId);
            if (droppedIndex < 0)
            {
                Debug.LogWarning($"[Draft] 교체 대상 '{droppedSkillId}' 제거 실패 — 획득 취소");
                return false;
            }

            // 버린 스킬이 있던 자리에 넣는다. owned 순서가 곧 슬롯 순서(CollectActiveOwned)라,
            // 끝에 추가하면 뒷 슬롯 스킬이 앞으로 당겨지고 신규가 뒷칸에 배치된다(Bug-024).
            InsertSkill(incoming, droppedIndex);
            GameEvents.RaiseSkillDrafted(incoming, currentReason);
            CloseSession();
            return true;
        }

        /// <summary>
        /// 교체 모달에서 취소 — 드래프트 카드 선택으로 되돌린다.
        /// 세션·현재 옵션은 그대로 두고 OnDraftOptionsReady만 재발행한다. DraftPanelPresenter가
        /// 이를 받아 같은 카드로 패널을 다시 연다(교체 요청 시 숨겨졌던 그 패널).
        /// CloseSession을 쓰면 안 된다 — 게임이 재개되고 옵션이 사라져 돌아갈 화면이 없어진다.
        /// </summary>
        public bool CancelReplacement()
        {
            if (!isSessionActive || currentOptions == null) return false;

            GameEvents.RaiseDraftOptionsReady(currentOptions);
            return true;
        }

        private void HandleLevelUp(int newLevel, DraftTriggerReason reason)
        {
            // 세션 진행 중이면 폐기하지 않고 대기열에 넣어 세션 종료 후 순차로 연다.
            if (isSessionActive)
            {
                pendingReasons.Enqueue(reason);
                return;
            }

            OpenSession(reason);
        }

        private void OpenSession(DraftTriggerReason reason)
        {
            currentReason = reason;
            rerollsUsed = 0;
            isSessionActive = true;

            DrawAndAnnounce();
            GameEvents.RaiseDraftOpened();
        }

        private void DrawAndAnnounce()
        {
            string currentFormId = CurrentFormId();
            var cards = pool.DrawOptions(OptionCount, currentFormId, ownedSynergyTags, ownedSkillIds);
            currentOptions = new DraftOptions(cards, currentReason, rerollsUsed);
            GameEvents.RaiseDraftOptionsReady(currentOptions);
        }

        private void CloseSession()
        {
            isSessionActive = false;
            currentOptions = null;
            GameEvents.RaiseDraftClosed();

            // 대기 중인 레벨업이 있으면 다음 드래프트를 곧바로 이어 연다.
            if (pendingReasons.Count > 0)
            {
                OpenSession(pendingReasons.Dequeue());
            }
        }

        /// <summary>신규 획득 — 보유 목록 끝에 추가(뒤 슬롯부터 채워진다).</summary>
        private void AcquireSkill(SkillData skill) => InsertSkill(skill, owned.Count);

        /// <summary>보유 목록의 지정 위치에 넣는다. owned 순서가 곧 슬롯 순서라 위치가 슬롯을 결정한다.</summary>
        private void InsertSkill(SkillData skill, int index)
        {
            owned.Insert(Mathf.Clamp(index, 0, owned.Count), skill);
            ownedSkillIds.Add(skill.skillId);
            if (!string.IsNullOrEmpty(skill.synergyTag)) ownedSynergyTags.Add(skill.synergyTag);
        }

        /// <summary>제거한 보유 목록 위치를 반환(미발견 시 -1). 교체 시 그 자리에 신규를 넣기 위함.</summary>
        private int RemoveOwned(string skillId)
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                if (owned[i] != null && owned[i].skillId == skillId)
                {
                    var removed = owned[i];
                    owned.RemoveAt(i);
                    ownedSkillIds.Remove(skillId);
                    RecomputeSynergyTags();
                    Debug.Log($"[Draft] 교체: {removed.displayName} 제거 (보유 위치 {i})");
                    return i;
                }
            }
            return -1;
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
