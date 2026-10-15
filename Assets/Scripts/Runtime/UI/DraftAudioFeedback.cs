using System;
using System.Collections.Generic;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Draft;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>드래프트 전용 SFX. 기존 버튼 릴레이만 관찰하며 선택/포커스를 변경하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class DraftAudioFeedback : MonoBehaviour
    {
        private const float HOVER_INTERVAL = 0.06f;
        private readonly List<TargetBinding> bindings = new();
        private GameObject panelRoot;
        private DraftPanelPresenter presenter;
        private DraftSessionController session;
        private GameObject lastTarget;
        private AudioClip hoverClip;
        private AudioClip rerollClip;
        private AudioClip skipClip;
        private float lastHoverTime = float.NegativeInfinity;
        private int suppressedFrame = -1;
        private bool isInitialized;

        public void Initialize(DraftPanelPresenter owner, GameObject root, DraftSessionController controller,
            SkillCardView[] cards, Button reroll, Button skip)
        {
            if (isInitialized) return;
            isInitialized = true;
            presenter = owner;
            panelRoot = root;
            session = controller;
            hoverClip = Resources.Load<AudioClip>("Audio/UI/draft_hover");
            rerollClip = Resources.Load<AudioClip>("Audio/UI/draft_reroll");
            skipClip = Resources.Load<AudioClip>("Audio/UI/draft_skip");

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card != null) Attach(card.FocusTarget, card);
                }
            }
            if (reroll != null) Attach(reroll.gameObject, null);
            if (skip != null) Attach(skip.gameObject, null);
        }

        /// <summary>재바인딩·최초 포커스와 같은 프레임의 알림은 상태만 갱신한다.</summary>
        public void SuppressAutomaticFocus() => suppressedFrame = Time.frameCount;

        public void ResetTargets()
        {
            lastTarget = null;
            lastHoverTime = float.NegativeInfinity;
            SuppressAutomaticFocus();
            foreach (var binding in bindings)
            {
                binding.IsHovered = false;
                binding.IsFocused = false;
            }
        }

        // TrySkip은 먼저 패널을 닫는다. 성공음은 root 활성 상태로 제한하지 않는다.
        public void PlayRerollSuccess() => PlaySuccess(rerollClip);
        public void PlaySkipSuccess() => PlaySuccess(skipClip);

        private void PlaySuccess(AudioClip clip)
        {
            SuppressAutomaticFocus();
            lastHoverTime = Time.unscaledTime;
            Play(clip, 0.6f);
        }

        private void Attach(GameObject target, SkillCardView card)
        {
            if (target == null || !target.TryGetComponent<Selectable>(out var selectable)) return;
            foreach (var existing in bindings)
            {
                if (existing.Target == target) return;
            }

            var relay = target.GetComponent<SkillCardFocusRelay>();
            if (relay == null) relay = target.AddComponent<SkillCardFocusRelay>();
            var binding = new TargetBinding(target, selectable, card, relay);
            binding.FocusEntered = () => Enter(binding, false);
            binding.FocusExited = () => Exit(binding, false);
            binding.PointerEntered = () => Enter(binding, true);
            binding.PointerExited = () => Exit(binding, true);
            relay.OnFocusSelected += binding.FocusEntered;
            relay.OnFocusDeselected += binding.FocusExited;
            relay.OnPointerEntered += binding.PointerEntered;
            relay.OnPointerExited += binding.PointerExited;
            bindings.Add(binding);
        }

        private void Enter(TargetBinding binding, bool isPointer)
        {
            bool wasPresent = binding.IsHovered || binding.IsFocused;
            if (isPointer) binding.IsHovered = true;
            else binding.IsFocused = true;

            if (!CanPlayHover(binding)) return;
            if (wasPresent && lastTarget == binding.Target) return;
            lastTarget = binding.Target;
            if (Time.frameCount == suppressedFrame || Time.unscaledTime - lastHoverTime < HOVER_INTERVAL) return;

            lastHoverTime = Time.unscaledTime;
            Play(hoverClip, 0.35f);
        }

        private bool CanPlayHover(TargetBinding binding) =>
            isActiveAndEnabled && presenter != null && presenter.isActiveAndEnabled &&
            panelRoot != null && panelRoot.activeInHierarchy && session != null && session.IsSessionActive &&
            !SaveStatusOverlay.IsCapturingInput && binding.Target != null && binding.Target.activeInHierarchy &&
            binding.Selectable != null && binding.Selectable.IsInteractable() &&
            (binding.Card == null || binding.Card.CanReceiveFocus);

        private void Exit(TargetBinding binding, bool isPointer)
        {
            if (isPointer) binding.IsHovered = false;
            else binding.IsFocused = false;
            if (!binding.IsHovered && !binding.IsFocused && lastTarget == binding.Target) lastTarget = null;
        }

        private static void Play(AudioClip clip, float volume)
        {
            if (clip != null) AudioManager.GetInstanceSafe()?.PlaySfx(clip, volume);
        }

        private void OnDisable() => ResetTargets();

        private void OnDestroy()
        {
            foreach (var binding in bindings)
            {
                if (binding.Relay == null) continue;
                binding.Relay.OnFocusSelected -= binding.FocusEntered;
                binding.Relay.OnFocusDeselected -= binding.FocusExited;
                binding.Relay.OnPointerEntered -= binding.PointerEntered;
                binding.Relay.OnPointerExited -= binding.PointerExited;
            }
            bindings.Clear();
        }

        private sealed class TargetBinding
        {
            public readonly GameObject Target;
            public readonly Selectable Selectable;
            public readonly SkillCardView Card;
            public readonly SkillCardFocusRelay Relay;
            public bool IsHovered;
            public bool IsFocused;
            public Action FocusEntered;
            public Action FocusExited;
            public Action PointerEntered;
            public Action PointerExited;

            public TargetBinding(GameObject target, Selectable selectable, SkillCardView card, SkillCardFocusRelay relay)
            {
                Target = target;
                Selectable = selectable;
                Card = card;
                Relay = relay;
            }
        }
    }
}
