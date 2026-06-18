using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
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
            RebuildSkillSlots();
        }

        private void OnDestroy()
        {
            // 플레이어 파괴 시 등록 해제 — 영속 싱글톤에 파괴된 어빌리티가 잔류하지 않도록.
            if (!AbilitySystem.HasInstance) return;
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

            Debug.Log($"[PlayerCharacter] 슬롯 {slot} 스킬은 '{bound}' 폼 전용 — 현재 '{currentFormId ?? "?"}'에서 발동 불가.");
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
