using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using Abyss.Runtime.Skill;
using GAS.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 스킬(GAS) 통합 파트. PlayerCharacter가 어빌리티 보유 주체이자 IGameplayContext를 직접 구현한다.
    /// 드래프트 Active 스킬(상한 2)을 슬롯0/1에 매핑해 GenericAbility로 AddComponent·등록하고,
    /// E=Skill1 / R=Skill2 입력(SendMessages)으로 실행한다. 쿨다운 tick·실행은 AbilitySystem이 담당.
    /// </summary>
    public sealed partial class PlayerCharacter : IGameplayContext
    {
        private const int SkillSlotCount = 2;

        [Header("폼 스킬 개시 연출")]
        [Tooltip("현재 폼에 castColor가 없을 때 쓰는 개시 플래시 폴백 색상")]
        [SerializeField] private Color castFlashColor = new(0.6f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f)] private float castFlashDuration = 0.15f;
        [Tooltip("개시 링 버스트 반경(월드 유닛). 0이면 링 미표시. 효과 종류(근접/발사체/버프)와 무관하게 " +
                 "폼 색상 링을 띄워 발동을 확실히 보이게 한다(BossAreaEffect 재사용).")]
        [SerializeField, Min(0f)] private float castRingRadius = 1.2f;
        [SerializeField, Min(0.05f)] private float castRingDuration = 0.3f;
        [Tooltip("개시 히트스탑(초). 공격 light(0.05)보다 약간 가볍게")]
        [SerializeField, Min(0f)] private float castHitstop = 0.04f;
        [Tooltip("개시 카메라 쉐이크 (magnitude, duration)")]
        [SerializeField] private Vector2 castShake = new(0.1f, 0.1f);

        private readonly GenericAbility[] slotAbilities = new GenericAbility[SkillSlotCount];
        private readonly string[] slotNames = new string[SkillSlotCount];
        // 슬롯 스킬의 폼 귀속(SkillData.formBound). 비어있으면 any. 발동 시 현재 폼과 대조한다.
        private readonly string[] slotFormBound = new string[SkillSlotCount];
        private readonly List<SkillData> activeSkillBuffer = new();

        private DraftSessionController draftSessionRef;

        // IGameplayContext 상태 저장소.
        private readonly Dictionary<string, bool> gameStates = new();
        private readonly Dictionary<string, object> customData = new();
        private Transform currentTarget;

        // ====== 생명주기 ======

        private void Start()
        {
            // AbilitySystem은 영속 싱글톤 — 현재 플레이어를 소유 컨텍스트로 (재)연결.
            AbilitySystem.Instance.Initialize(this);
            // 폼 전용 스킬 발동 개시 시 개시 연출을 띄우기 위해 개시 이벤트 구독(효과 완료 전 발화).
            AbilitySystem.Instance.OnAbilityStarted += HandleAbilityStartedForCastMotion;
            RebuildSkillSlots();
            // 런 도중 플레이어가 재생성되는 경로(폼 프리팹 교체 등)에서도 시너지 활성 상태가 살아나도록 1회 조회.
            RecomputeSynergyState();
        }

        private void OnDestroy()
        {
            // 플레이어 파괴 시 등록 해제 — 영속 싱글톤에 파괴된 어빌리티가 잔류하지 않도록.
            if (!AbilitySystem.HasInstance) return;
            AbilitySystem.Instance.OnAbilityStarted -= HandleAbilityStartedForCastMotion;
            for (int i = 0; i < SkillSlotCount; i++)
            {
                if (!string.IsNullOrEmpty(slotNames[i]))
                {
                    AbilitySystem.Instance.UnregisterAbility(slotNames[i]);
                    slotNames[i] = null;
                }
            }
        }

        // PlayerCharacter.Movement의 OnEnable/OnDisable에서 호출(partial 중복 정의 회피).
        private void SubscribeSkillEvents()
        {
            GameEvents.OnSkillDrafted += HandleSkillDraftedForSlots;
            GameEvents.OnFormSwapped += HandleFormSwappedForSlots;
        }

        private void UnsubscribeSkillEvents()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDraftedForSlots;
            GameEvents.OnFormSwapped -= HandleFormSwappedForSlots;
        }

        private void HandleSkillDraftedForSlots(SkillData skill, DraftTriggerReason reason)
        {
            RebuildSkillSlots();
        }

        // 폼 교체 시 슬롯을 현재 폼 로드아웃으로 재구성(폼별 스킬 세트 전환).
        private void HandleFormSwappedForSlots(FormData previous, FormData next)
        {
            RebuildSkillSlots();
        }

        // ====== 입력 핸들러 (SendMessages: 액션명 Skill1/Skill2 → OnSkill1/OnSkill2) ======

        private void OnSkill1(InputValue value)
        {
            if (value.isPressed) TryExecuteSlot(0);
        }

        private void OnSkill2(InputValue value)
        {
            if (value.isPressed) TryExecuteSlot(1);
        }

        private void TryExecuteSlot(int slot)
        {
            if (isDead) return;
            if (slot < 0 || slot >= SkillSlotCount) return;

            string abilityName = slotNames[slot];
            if (string.IsNullOrEmpty(abilityName) || !AbilitySystem.HasInstance) return;

            // 폼 귀속 검사 — 현재 폼과 다른 전용 스킬은 발동 차단.
            if (!IsSlotUsableInCurrentForm(slot)) return;

            // 비동기 실행은 fire-and-forget — 결과는 AbilitySystem 이벤트로 통지된다.
            _ = AbilitySystem.Instance.TryExecuteAbilityAsync(abilityName);
        }

        // ====== 폼 스킬 개시 연출 ======

        /// <summary>
        /// 어빌리티 실행 개시(OnAbilityStarted) 시 호출. 발동한 슬롯을 역매핑해
        /// formBound 전용 스킬이면 개시 연출(플래시 + 카메라 피드백)을 재생한다.
        /// 슬롯 키(slot{n}:...)로만 매칭하므로 적·비-슬롯 어빌리티는 자동 무시된다.
        /// OnAbilityStarted는 CanExecute 통과 후 효과 적용 전에 발화하므로, 효과 종류(즉발/발사체/버프)와
        /// 무관하게 발동 순간 연출이 뜬다(쿨다운·조건 미충족으로 막힌 발동엔 발화하지 않음).
        /// </summary>
        private void HandleAbilityStartedForCastMotion(string abilityName)
        {
            if (isDead || string.IsNullOrEmpty(abilityName)) return;

            for (int slot = 0; slot < SkillSlotCount; slot++)
            {
                if (slotNames[slot] != abilityName) continue;

                // 발동한 슬롯이 폼 전용(formBound 지정)일 때만 개시 연출.
                if (!string.IsNullOrEmpty(slotFormBound[slot])) PlayFormSkillCastMotion();
                return;
            }
        }

        /// <summary>
        /// 폼 전용 스킬 개시 연출: 폼 색상 링 버스트 + 본체 색 플래시 + 히트스탑 + 카메라 쉐이크.
        /// 링(BossAreaEffect)이 주 단서 — 근접/발사체/버프 등 스킬 자체 효과 유무와 무관하게 발동을 확실히 보이게 한다.
        /// 색은 현재 폼의 castColor로 폼을 구분한다(폼 미확보 시 공용 castFlashColor 폴백).
        /// formBound 스킬은 항상 해당 폼에서만 발동하므로 현재 폼 색 = 그 스킬의 폼 색.
        /// </summary>
        private void PlayFormSkillCastMotion()
        {
            var current = formController != null ? formController.CurrentForm : null;
            Color castColor = current != null ? current.castColor : castFlashColor;

            // 본체 tint 플래시(미묘) — 색 구분 보조.
            TriggerAttackFlash(castColor, castFlashDuration);

            // 폼 색상 링 버스트(주 단서) — 근접/발사체/버프 무관하게 발동 위치에 확실히 보인다.
            if (castRingRadius > 0f)
            {
                BossAreaEffect.Spawn(transform.position, castRingRadius, castColor, castRingDuration);
            }

            if (castHitstop > 0f && HitstopController.HasInstance)
            {
                HitstopController.Instance.Trigger(castHitstop);
            }

            if (castShake != Vector2.zero) TriggerShake(castShake);
        }

        /// <summary>
        /// 슬롯 스킬이 현재 폼에서 발동 가능한지. formBound이 비어있으면(any) 항상 가능,
        /// 설정돼 있으면 현재 폼 formId와 일치해야 한다. SkillSlotPresenter dim 표시도 이 기준 재사용.
        /// </summary>
        public bool IsSlotUsableInCurrentForm(int slot)
        {
            if (slot < 0 || slot >= SkillSlotCount) return false;

            string bound = slotFormBound[slot];
            if (string.IsNullOrEmpty(bound)) return true; // any — 모든 폼에서 사용 가능

            var current = formController != null ? formController.CurrentForm : null;
            string currentFormId = current != null ? current.formId : null;
            if (bound == currentFormId) return true;

            // 폼 전용 스킬을 다른 폼에서 발동 시도 — 조용히 차단(입력마다 로그 노이즈 방지). dim 표시는 SkillSlotPresenter가 담당.
            return false;
        }

        // ====== 슬롯 ↔ 어빌리티 매핑 ======

        private DraftSessionController ResolveDraftSession()
        {
            if (draftSessionRef == null)
            {
                draftSessionRef = FindAnyObjectByType<DraftSessionController>(FindObjectsInactive.Include);
            }
            return draftSessionRef;
        }

        /// <summary>
        /// 보유 Active 스킬을 슬롯0/1에 재매핑. 드래프트 획득·교체 시(OnSkillDrafted) 호출.
        /// 슬롯 순서 기준은 DraftSessionController.CollectActiveOwned(HUD와 동일 SoT).
        /// </summary>
        private void RebuildSkillSlots()
        {
            var draft = ResolveDraftSession();
            if (draft == null)
            {
                for (int i = 0; i < SkillSlotCount; i++) { ConfigureSlot(i, null); slotFormBound[i] = null; }
                return;
            }

            string currentFormId = formController != null && formController.CurrentForm != null
                ? formController.CurrentForm.formId
                : null;
            draft.CollectActiveOwned(activeSkillBuffer, SkillSlotCount, currentFormId);
            for (int i = 0; i < SkillSlotCount; i++)
            {
                SkillData skill = i < activeSkillBuffer.Count ? activeSkillBuffer[i] : null;
                slotFormBound[i] = skill != null ? skill.formBound : null;
                var abilityData = skill != null ? skill.relatedAbility as GenericAbilityData : null;
                ConfigureSlot(i, abilityData);
            }
        }

        private void ConfigureSlot(int slot, GenericAbilityData abilityData)
        {
            // 1) 이전 등록 해제(합성 키).
            if (!string.IsNullOrEmpty(slotNames[slot]))
            {
                if (AbilitySystem.HasInstance) AbilitySystem.Instance.UnregisterAbility(slotNames[slot]);
                slotNames[slot] = null;
            }

            // 2) 빈 슬롯이면 컴포넌트만 비활성으로 둔다(재사용).
            if (abilityData == null)
            {
                if (slotAbilities[slot] != null) slotAbilities[slot].enabled = false;
                return;
            }

            // 3) 컴포넌트 확보(슬롯당 1개 재사용) + 데이터 주입.
            if (slotAbilities[slot] == null)
            {
                slotAbilities[slot] = gameObject.AddComponent<GenericAbility>();
            }
            slotAbilities[slot].enabled = true;

            // 슬롯 단위 고유 키 — 같은 abilityName이 두 슬롯에 와도 충돌하지 않게.
            string registeredName = $"slot{slot}:{abilityData.abilityName}";
            slotAbilities[slot].Configure(abilityData, this, registeredName);
            slotNames[slot] = registeredName;

            if (AbilitySystem.HasInstance) AbilitySystem.Instance.RegisterAbility(slotAbilities[slot]);
        }

        /// <summary>
        /// 슬롯 쿨다운 잔여 비율(0 = 사용 가능, 1 = 방금 사용). SkillSlotPresenter가 게이지로 폴링.
        /// </summary>
        public float GetSlotCooldownFill(int slot)
        {
            if (slot < 0 || slot >= SkillSlotCount) return 0f;

            var ability = slotAbilities[slot];
            if (ability == null || string.IsNullOrEmpty(slotNames[slot])) return 0f;

            var cd = ability.Cooldown;
            if (cd == null || !cd.IsOnCooldown) return 0f;
            return Mathf.Clamp01(1f - cd.Progress);
        }

        // ====== IGameplayContext 구현 ======

        public GameObject Owner => gameObject;
        Transform IGameplayContext.Transform => transform;
        public bool IsAlive => !isDead;
        public bool CanAct => !isDead;
        public Vector3 Position => transform.position;
        public Vector3 Forward => new(facingSign, 0f, 0f);

        public bool IsInState(string stateName)
            => gameStates.TryGetValue(stateName, out var v) && v;

        public void SetState(string stateName, bool value)
            => gameStates[stateName] = value;

        public Transform GetTarget() => currentTarget;
        public void SetTarget(Transform target) => currentTarget = target;

        public T GetCustomData<T>(string key) where T : class
            => customData.TryGetValue(key, out var data) ? data as T : null;

        public void SetCustomData<T>(string key, T data) where T : class
            => customData[key] = data;
    }
}
