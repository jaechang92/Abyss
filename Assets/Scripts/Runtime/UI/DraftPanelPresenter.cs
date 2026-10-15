using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 드래프트 창 루트. 3카드 뷰 + 리롤/스킵 버튼 + BuildContextPanel 조정.
    /// OnDraftOptionsReady/OnDraftClosed/OnSkillDrafted 구독.
    /// 키보드 1/2/3 선택, Enter = 첫번째 카드.
    /// </summary>
    public sealed class DraftPanelPresenter : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;

        [Header("카드 3장")]
        [SerializeField] private SkillCardView[] cards = new SkillCardView[3];

        [Header("버튼")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Text rerollLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private Text skipLabel;

        [Header("빌드 컨텍스트")]
        [SerializeField] private BuildContextPanel buildContext;

        [Header("참조")]
        [SerializeField] private DraftSessionController session;

        // 카드 호버·포커스 상세. 씬 배선 없이 root 밑에 런타임으로 만든다(root가 꺼지면 같이 정리된다).
        private SkillCardDetailsPanel cardDetails;
        private DraftTriggerReason displayedReason;
        private DraftAudioFeedback audioFeedback;

        private void Awake()
        {
            if (root != null)
            {
                root.SetActive(false);
                cardDetails = SkillCardDetailsPanel.Create(root.transform);

                // 채택 UI 스킨 — 저장된 씬의 기존 자식에 배치·프레임·선택 표식만 덧붙인다(빌더 재실행 불필요).
                ModalArtSkin.ApplyDraft(root.transform, titleText, cards,
                    rerollButton, rerollLabel, skipButton, skipLabel, buildContext, cardDetails);
                DraftPanelReadability.Attach(root.transform, titleText, rerollButton, rerollLabel, skipButton, skipLabel);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    int captured = i;
                    cards[i].OnSelected += HandleCardSelected;
                    cards[i].AttachDetails(cardDetails);
                }
            }

            if (rerollButton != null) rerollButton.onClick.AddListener(HandleReroll);
            if (skipButton != null) skipButton.onClick.AddListener(HandleSkip);
            audioFeedback = GetComponent<DraftAudioFeedback>();
            if (audioFeedback == null) audioFeedback = gameObject.AddComponent<DraftAudioFeedback>();
            audioFeedback.Initialize(this, root, session, cards, rerollButton, skipButton);
        }

        private void OnEnable()
        {
            GameEvents.OnDraftOptionsReady += HandleOptionsReady;
            GameEvents.OnDraftClosed += HandleDraftClosed;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnDraftSlotReplaceRequested += HandleReplaceRequested;
            GameEvents.OnRunEnded += HandleRunEnded;
            Loc.AddLanguageChangedListener(HandleLanguageChanged);
            RefreshVisibleLocalizedText();
        }

        private void OnDisable()
        {
            GameEvents.OnDraftOptionsReady -= HandleOptionsReady;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnDraftSlotReplaceRequested -= HandleReplaceRequested;
            GameEvents.OnRunEnded -= HandleRunEnded;
            Loc.RemoveLanguageChangedListener(HandleLanguageChanged);
            ClearCardDetails();
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            HandleKeyboardSelection();
        }

        private void HandleKeyboardSelection()
        {
            if (SaveStatusOverlay.IsCapturingInput) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) TrySelect(0);
            else if (kb.digit2Key.wasPressedThisFrame) TrySelect(1);
            else if (kb.digit3Key.wasPressedThisFrame) TrySelect(2);
            // 카드 버튼에 포커스가 있으면 Enter는 EventSystem Submit이 그 카드를 누른다 —
            // 여기서 0번을 또 고르면 한 번의 Enter로 두 카드가 동시에 선택 요청된다.
            else if (kb.enterKey.wasPressedThisFrame && !IsCardFocused() && !BuildContextPanel.IsReadingInputOwned) TrySelect(0);
        }

        private bool IsCardFocused()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null || !selected.activeInHierarchy) return false;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null && cards[i].FocusTarget == selected) return true;
            }
            return false;
        }

        /// <summary>
        /// 키보드·패드 최초 포커스. 포커스가 없거나 꺼진 오브젝트에 남아 있으면 첫 활성 카드 버튼을 고른다.
        /// 이미 드래프트 안(카드·리롤·스킵)에 있으면 바꾸지 않는다. 드래프트 밖의 다른 활성 UI가 쥔 포커스도 뺏지 않는다.
        /// 선택(SetSelectedGameObject)은 포커스만 옮기고 클릭하지 않는다 — 스킬 선택은 일어나지 않는다.
        /// </summary>
        private void EnsureInitialFocus(bool isReopened)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || root == null) return;

            var current = eventSystem.currentSelectedGameObject;
            bool isAlive = current != null && current.activeInHierarchy && IsInteractable(current);

            if (isAlive && current.transform.IsChildOf(root.transform))
            {
                // 닫혔다 다시 열린 경우 EventSystem은 옛 선택을 쥔 채 select를 다시 보내지 않는다.
                // 같은 대상을 다시 선택해 포커스 표시·상세를 되살린다(대상은 바꾸지 않는다).
                if (isReopened)
                {
                    eventSystem.SetSelectedGameObject(null);
                    eventSystem.SetSelectedGameObject(current);
                }
                return;
            }
            if (isAlive) return;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null || !cards[i].CanReceiveFocus) continue;
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(cards[i].FocusTarget);
                return;
            }
        }

        private static bool IsInteractable(GameObject target) =>
            !target.TryGetComponent<Selectable>(out var selectable) || selectable.IsInteractable();

        private void TrySelect(int index)
        {
            if (index < 0 || index >= cards.Length) return;
            cards[index]?.TriggerSelectFromKeyboard();
        }

        private void HandleCardSelected(int index)
        {
            if (session == null) return;
            session.TrySelect(index);
        }

        private void HandleReroll()
        {
            if (session != null && session.TryReroll()) audioFeedback?.PlayRerollSuccess();
        }

        private void HandleSkip()
        {
            if (session != null && session.TrySkip()) audioFeedback?.PlaySkipSuccess();
        }

        private void HandleOptionsReady(DraftOptions options)
        {
            if (options == null) return;

            audioFeedback?.SuppressAutomaticFocus();
            displayedReason = options.Reason;
            bool isReopened = root != null && !root.activeSelf;
            if (root != null) root.SetActive(true);

            RefreshTitle();

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                var skill = i < options.Cards.Count ? options.Cards[i] : null;
                cards[i].Bind(i, skill);
            }

            RefreshButtons();
            RefreshBuildContext();
            EnsureInitialFocus(isReopened);
        }

        private void HandleDraftClosed()
        {
            ClearCardDetails();
            if (root != null) root.SetActive(false);
        }

        // 런 종료 시 패널 표시 여부는 기존 흐름(DraftClosed)에 맡기고 상세만 치운다.
        private void HandleRunEnded()
        {
            ClearCardDetails();
        }

        private void ClearCardDetails()
        {
            audioFeedback?.ResetTargets();
            if (cardDetails != null) cardDetails.Clear();
        }

        // 카드 선택이 슬롯 교체로 이어지면 선택 단계는 끝났다 — 교체 모달에 화면을 넘긴다.
        // 세션은 살아 있으므로(CloseSession 아님) 패널만 숨긴다. 모달에서 취소하면
        // DraftSessionController.CancelReplacement가 OnDraftOptionsReady를 재발행해 되돌아온다.
        private void HandleReplaceRequested(SkillData _, IReadOnlyList<SkillData> __)
        {
            ClearCardDetails();
            if (root != null) root.SetActive(false);
        }

        private void HandleSkillDrafted(SkillData _, DraftTriggerReason __)
        {
            RefreshBuildContext();
        }

        private void RefreshButtons()
        {
            if (session == null) return;

            if (rerollButton != null)
            {
                bool canReroll = session.CanReroll();
                rerollButton.interactable = canReroll;
                if (rerollLabel != null)
                {
                    int cost = session.GetRerollCost();
                    // 티켓은 값이 0이지만 "0 gold"로 찍으면 안 된다 — 특전의 공짜와 달리
                    // 쓰면 없어지는 재고라, 남은 장수를 같이 보여줘야 아껴 쓸지 정할 수 있다.
                    rerollLabel.text =
                        cost == int.MaxValue ? Loc.Get(StringKey.Draft_RerollExhausted)
                        : session.NextRerollUsesTicket ? Loc.GetFormat(StringKey.Draft_RerollTicketFormat, session.ExtraRerollStock)
                        : Loc.GetFormat(StringKey.Draft_RerollCostFormat, cost);
                }
            }

            if (skipButton != null)
            {
                skipButton.interactable = true;
                if (skipLabel != null) skipLabel.text = Loc.GetFormat(StringKey.Draft_SkipRewardFormat, session.SkipReward);
            }
        }

        private void RefreshBuildContext()
        {
            if (buildContext != null && session != null) buildContext.Refresh(session.Owned);
        }

        private void HandleLanguageChanged(LocalizationLanguage _) => RefreshVisibleLocalizedText();

        private void RefreshVisibleLocalizedText()
        {
            if (root == null || !root.activeSelf) return;
            // 문구만 갱신한다. 카드 바인딩·포커스·선택/리롤/스킵 요청은 다시 실행하지 않는다.
            RefreshTitle();
            RefreshButtons();
        }

        private void RefreshTitle()
        {
            if (titleText != null)
                titleText.text = Loc.GetFormat(StringKey.Draft_TitleFormat, ReasonLabel(displayedReason));
        }

        private static string ReasonLabel(DraftTriggerReason reason) => Loc.Get(reason switch
        {
            DraftTriggerReason.LevelUp => StringKey.Draft_ReasonLevelUp,
            DraftTriggerReason.BossBonus => StringKey.Draft_ReasonBossBonus,
            DraftTriggerReason.EliteBonus => StringKey.Draft_ReasonEliteBonus,
            DraftTriggerReason.RoomReward => StringKey.Draft_ReasonRoomReward,
            _ => StringKey.Draft_ReasonDefault
        });
    }
}
