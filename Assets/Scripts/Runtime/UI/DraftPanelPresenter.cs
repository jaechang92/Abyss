using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using UnityEngine;
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

        private void Awake()
        {
            if (root != null) root.SetActive(false);

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    int captured = i;
                    cards[i].OnSelected += HandleCardSelected;
                }
            }

            if (rerollButton != null) rerollButton.onClick.AddListener(HandleReroll);
            if (skipButton != null) skipButton.onClick.AddListener(HandleSkip);
        }

        private void OnEnable()
        {
            GameEvents.OnDraftOptionsReady += HandleOptionsReady;
            GameEvents.OnDraftClosed += HandleDraftClosed;
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
        }

        private void OnDisable()
        {
            GameEvents.OnDraftOptionsReady -= HandleOptionsReady;
            GameEvents.OnDraftClosed -= HandleDraftClosed;
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            HandleKeyboardSelection();
        }

        private void HandleKeyboardSelection()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) TrySelect(0);
            else if (kb.digit2Key.wasPressedThisFrame) TrySelect(1);
            else if (kb.digit3Key.wasPressedThisFrame) TrySelect(2);
            else if (kb.enterKey.wasPressedThisFrame) TrySelect(0);
        }

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
            session?.TryReroll();
        }

        private void HandleSkip()
        {
            session?.TrySkip();
        }

        private void HandleOptionsReady(DraftOptions options)
        {
            if (options == null) return;

            if (root != null) root.SetActive(true);

            if (titleText != null)
            {
                string reasonLabel = ReasonLabel(options.Reason);
                titleText.text = $"{reasonLabel} — 스킬 1개 선택";
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                var skill = i < options.Cards.Count ? options.Cards[i] : null;
                cards[i].Bind(i, skill);
            }

            RefreshButtons();
            RefreshBuildContext();
        }

        private void HandleDraftClosed()
        {
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
                    rerollLabel.text = cost == int.MaxValue
                        ? "리롤 (소진)"
                        : $"리롤 ({cost} gold)";
                }
            }

            if (skipButton != null)
            {
                skipButton.interactable = true;
                if (skipLabel != null) skipLabel.text = $"스킵 (+{session.SkipReward} gold)";
            }
        }

        private void RefreshBuildContext()
        {
            if (buildContext != null && session != null) buildContext.Refresh(session.Owned);
        }

        private static string ReasonLabel(DraftTriggerReason reason) => reason switch
        {
            DraftTriggerReason.LevelUp => "레벨 업!",
            DraftTriggerReason.BossBonus => "보스 격파 보너스!",
            DraftTriggerReason.EliteBonus => "엘리트 격파 보너스!",
            DraftTriggerReason.RoomReward => "방 보상",
            _ => "드래프트"
        };
    }
}
