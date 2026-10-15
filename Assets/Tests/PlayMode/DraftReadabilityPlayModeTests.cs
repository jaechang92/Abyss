using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed partial class DraftReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> created = new();
        private GameObject canvas;
        private GameObject root;
        private RectTransform main;
        private GameObject ownedEvents;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage previousLanguage;
        private float previousTime;
        private Random.State previousRandom;
        private InputSettings previousInput;
        private InputSettings testInput;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSystemUIInputModule ownedUiModule;
        private bool ownsRun;
        private int previousGold;
        private InputRouter previousRouter;
        private InputRouter.InputMode previousRouterMode;
        private SkillData[] skills;
        private DraftSessionController session;
        private DraftPanelPresenter presenter;
        private SkillCardView[] cards;
        private Text title;
        private Button reroll;
        private Button skip;
        private ReplacementModal replacement;
        private Button replacementCancel;
        private int grants;
        private int closed;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            created.Clear();
            grants = closed = 0;
            previousLanguage = Loc.CurrentLanguage;
            previousTime = Time.timeScale;
            previousRandom = Random.state;
            previousInput = InputSystem.settings;
            testInput = Object.Instantiate(previousInput);
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInput;
            keyboard = InputSystem.AddDevice<Keyboard>();
            ownsRun = !RunManager.HasInstance;
            previousGold = RunManager.Instance.GoldShards;
            previousRouter = InputRouter.GetInstanceSafe();
            if (previousRouter != null) previousRouterMode = previousRouter.CurrentMode;
            RunManager.Instance.GainGoldShards(500 - previousGold);
            if (EventSystem.current == null) ownedEvents = new GameObject("DraftReadingEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            previousNavigation = EventSystem.current.sendNavigationEvents;
            EventSystem.current.SetSelectedGameObject(null); // Isolate first-card focus; restore the prior UI selection in TearDown.
            var formObject = new GameObject("DraftReadingForms");
            created.Add(formObject);
            var forms = formObject.AddComponent<FormController>();
            forms.SetRunContext(false);
            skills = new SkillData[8];
            for (int i = 0; i < skills.Length; i++)
            {
                skills[i] = ScriptableObject.CreateInstance<SkillData>();
                skills[i].skillId = "draft_reading_" + i;
                skills[i].displayName = "긴 스킬 이름 Long skill name 長いスキル名 " + i;
                skills[i].description = string.Join("\n", Enumerable.Repeat("긴 설명 A long description 詳しい説明", 24)) + "\nEND_DESCRIPTION_" + i;
                skills[i].formulaDescription = "(base + 12) * 1.5 — END_FORMULA_" + i;
                created.Add(skills[i]);
            }
            var sessionObject = new GameObject("DraftReadingSession");
            created.Add(sessionObject);
            session = sessionObject.AddComponent<DraftSessionController>();
            Set(session, "formController", forms);
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.draftOptionCount = 3;
            config.activeSlotLimit = 2;
            config.rerollCostLadder = new[] { 15, 30 };
            config.skipReward = 10;
            created.Add(config);
            Set(session, "config", config);
            session.GetComponent<DraftPoolManager>().SetPool(skills.Take(6));
            canvas = UiFactory.CreateOverlayCanvas("DraftReadingCanvas", 500);
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            root = UiFactory.CreateRect(canvas.transform, "DraftRoot", Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            UiFactory.Stretch((RectTransform)root.transform);
            root.SetActive(false);
            main = (RectTransform)UiFactory.CreateRect(root.transform, "MainPanel", Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(1460f, 960f)).transform;
            main.gameObject.AddComponent<Image>();
            title = UiFactory.CreateLabel(main, "Title", Vector2.zero, new Vector2(880f, 56f), "", 40, Color.white, TextAnchor.MiddleCenter);
            cards = Enumerable.Range(0, 3).Select(BuildCard).ToArray();
            reroll = UiFactory.CreateButton(main, "Reroll", Vector2.zero, new Vector2(320f, 60f), "", 26);
            skip = UiFactory.CreateButton(main, "Skip", Vector2.zero, new Vector2(320f, 60f), "", 26);
            var presenterObject = new GameObject("DraftReadingPresenter");
            created.Add(presenterObject);
            presenterObject.SetActive(false);
            presenter = presenterObject.AddComponent<DraftPanelPresenter>();
            Set(presenter, "root", root);
            Set(presenter, "titleText", title);
            Set(presenter, "cards", cards);
            Set(presenter, "rerollButton", reroll);
            Set(presenter, "rerollLabel", reroll.GetComponentInChildren<Text>());
            Set(presenter, "skipButton", skip);
            Set(presenter, "skipLabel", skip.GetComponentInChildren<Text>());
            Set(presenter, "session", session);
            presenterObject.SetActive(true);
            GameEvents.OnSkillDrafted += OnGranted;
            GameEvents.OnDraftClosed += OnClosed;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                GameEvents.OnSkillDrafted -= OnGranted;
                GameEvents.OnDraftClosed -= OnClosed;
                GameEvents.OnDraftSlotReplaceRequested -= OnReplaceRequested;
                if (canvas != null) Object.Destroy(canvas);
                foreach (var value in created) if (value != null) Object.Destroy(value);
                if (RunManager.HasInstance)
                {
                    RunManager.Instance.GainGoldShards(previousGold - RunManager.Instance.GoldShards);
                    if (ownsRun) Object.Destroy(RunManager.Instance.gameObject);
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (previousRouter != null) previousRouter.SwitchMode(previousRouterMode);
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (ownedUiModule != null) Object.Destroy(ownedUiModule);
                InputSystem.settings = previousInput;
                if (testInput != null) Object.Destroy(testInput);
                Loc.SetLanguage(previousLanguage);
                Time.timeScale = previousTime;
                Random.state = previousRandom;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator AllScalesKeepReadableDetailAndFooterWithoutGrantingOrShrinking()
        {
            yield return OpenDraft();
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
                {
                    Loc.SetLanguage(language);
                    yield return Settle();
                    Assert.AreEqual(Mathf.RoundToInt(40f * scale), title.fontSize);
                    AssertTextFits(title);
                    foreach (var button in new[] { reroll, skip })
                    {
                        var text = button.GetComponentInChildren<Text>();
                        Assert.AreEqual(Mathf.RoundToInt(26f * scale), text.fontSize);
                        AssertTextFits(text);
                    }
                    AssertSeparate(reroll.transform, skip.transform);
                    AssertInsideMain(title.transform);
                    AssertInsideMain(reroll.transform);
                    AssertInsideMain(skip.transform);
                    foreach (var card in cards)
                    {
                        var name = card.transform.Find("Name").GetComponent<Text>();
                        var headline = card.transform.Find("RarityCategory").GetComponent<Text>();
                        var description = card.transform.Find("Description").GetComponent<Text>();
                        Assert.AreEqual(Mathf.RoundToInt(32f * scale), name.fontSize);
                        Assert.AreEqual(Mathf.RoundToInt(24f * scale), headline.fontSize);
                        Assert.AreEqual(Mathf.RoundToInt(26f * scale), description.fontSize);
                        Assert.False(name.resizeTextForBestFit);
                        Assert.False(description.resizeTextForBestFit);
                        AssertSeparate(name.transform, headline.transform);
                        AssertSeparate(headline.transform, description.transform);
                        Assert.AreEqual(card.CurrentSkill.description, description.text); // Preview clipping is allowed; details carry the full original.
                    }
                    var detail = Detail;
                    var body = Field<Text>(detail, "bodyText");
                    var header = Field<Text>(detail, "headerText");
                    Assert.AreEqual(Mathf.RoundToInt(24f * scale), body.fontSize);
                    Assert.AreEqual(Mathf.RoundToInt(30f * scale), header.fontSize);
                    AssertTextFits(body);
                    AssertTextFits(header);
                    foreach (var image in detail.GetComponentsInChildren<Image>(true)) Assert.False(image.raycastTarget);
                    Assert.False(body.raycastTarget);
                    Assert.False(header.raycastTarget);
                    var scroll = Field<ScrollRect>(detail, "readingScroll");
                    Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                    StringAssert.Contains("END_DESCRIPTION_", body.text);
                    StringAssert.Contains("END_FORMULA_", body.text);
                    Assert.Zero(grants);
                    Assert.Zero(session.Owned.Count);
                    Assert.AreEqual(500, RunManager.Instance.GoldShards);
                }
            }
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.GetComponent<Canvas>().scaleFactor = 0.5f;
            yield return Settle();
            AssertTextFits(Field<Text>(Detail, "bodyText"));
            AssertTextFits(skip.GetComponentInChildren<Text>());
            AssertSeparate(reroll.transform, skip.transform);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return Settle();
            Assert.AreEqual(40, title.fontSize);
            Assert.AreEqual(26, skip.GetComponentInChildren<Text>().fontSize);
            Assert.AreEqual(24, Field<Text>(Detail, "bodyText").fontSize);
        }

        [UnityTest]
        public IEnumerator EndKeyReadsLastLineWhilePausedWithoutSelectingAndIdlePreservesPosition()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            Time.timeScale = 0f;
            var scroll = Field<ScrollRect>(Detail, "readingScroll");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.End));
            yield return null;
            yield return null;
            Assert.LessOrEqual(scroll.verticalNormalizedPosition, 0.01f);
            Canvas.ForceUpdateCanvases();
            var body = Field<Text>(Detail, "bodyText");
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, body.transform);
            Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f);
            Assert.LessOrEqual(bounds.min.y, scroll.viewport.rect.yMax + 1f);
            float position = scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.AreEqual(position, scroll.content.anchoredPosition.y, 0.5f);
            Assert.True(session.IsSessionActive);
            Assert.Zero(grants);
            Assert.Zero(session.Owned.Count);
        }

        [UnityTest]
        public IEnumerator CardButtonConfirmGrantsExactlyOneSkillAndCloses()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            var chosen = cards[1].CurrentSkill;
            cards[1].FocusTarget.GetComponent<Button>().onClick.Invoke();
            cards[1].FocusTarget.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, session.Owned.Count);
            Assert.AreSame(chosen, session.Owned[0]);
            Assert.AreEqual(1, grants);
            Assert.AreEqual(1, closed);
            Assert.False(root.activeSelf);
            Assert.False(session.IsSessionActive);
        }

        [UnityTest]
        public IEnumerator DigitShortcutStillGrantsTheMatchingOptionAtMaximumScale()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            var chosen = cards[2].CurrentSkill;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
            yield return null;
            yield return null;
            Assert.AreEqual(1, session.Owned.Count);
            Assert.AreSame(chosen, session.Owned[0]);
            Assert.AreEqual(1, grants);
            Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator ReplacementCancelRestoresSameCardsAndDetailsWithoutGranting()
        {
            skills[0].category = skills[6].category = skills[7].category = SkillCategory.Active;
            session.DebugGrantSkill(skills[6]);
            session.DebugGrantSkill(skills[7]);
            grants = 0;
            session.GetComponent<DraftPoolManager>().SetPool(skills.Take(3));
            BuildReplacement();
            GameEvents.OnDraftSlotReplaceRequested += OnReplaceRequested;
            yield return OpenDraft();
            var original = session.CurrentOptions;
            var chosenCard = cards.Single(card => card.CurrentSkill == skills[0]);
            chosenCard.FocusTarget.GetComponent<Button>().onClick.Invoke();
            Assert.False(root.activeSelf);
            yield return Settle();
            replacementCancel.onClick.Invoke();
            yield return Settle();
            Assert.True(root.activeSelf);
            Assert.True(session.IsSessionActive);
            Assert.AreSame(original, session.CurrentOptions);
            CollectionAssert.AreEqual(new[] { skills[6], skills[7] }, session.Owned);
            Assert.Zero(grants);
            Assert.Zero(closed);
            Assert.True(Field<GameObject>(Detail, "body").activeSelf);
            StringAssert.Contains("END_DESCRIPTION_", Field<Text>(Detail, "bodyText").text);
        }

        [UnityTest]
        public IEnumerator RerollChargesOnlyItsDisplayedCostAndKeepsSessionUnacquired()
        {
            yield return OpenDraft();
            var before = session.CurrentOptions;
            int cost = session.GetRerollCost();
            Assert.True(reroll.interactable);
            reroll.onClick.Invoke();
            yield return Settle();
            Assert.AreEqual(500 - cost, RunManager.Instance.GoldShards);
            Assert.AreEqual(1, session.RerollsUsed);
            Assert.AreNotSame(before, session.CurrentOptions);
            Assert.True(root.activeSelf);
            Assert.Zero(grants);
            Assert.Zero(session.Owned.Count);
            Assert.AreEqual(session.CurrentOptions.Cards[0], cards[0].CurrentSkill);
        }

        [UnityTest]
        public IEnumerator SkipAwardsItsConfiguredGoldOnceAndHidesDetail()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            skip.onClick.Invoke();
            skip.onClick.Invoke();
            Assert.AreEqual(500 + session.SkipReward, RunManager.Instance.GoldShards);
            Assert.AreEqual(1, closed);
            Assert.Zero(grants);
            Assert.Zero(session.Owned.Count);
            Assert.False(root.activeSelf);
            Assert.False(Field<GameObject>(Detail, "body").activeSelf);
        }

        [UnityTest]
        public IEnumerator HoveredCardSurvivesPointerCrossingGapIntoScrollableDetail()
        {
            mouse = InputSystem.AddDevice<Mouse>();
            if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
            {
                ownedUiModule = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                ownedUiModule.AssignDefaultActions();
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            var hovered = cards[2];
            var cardRect = (RectTransform)hovered.transform;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = RectTransformUtility.WorldToScreenPoint(null, cardRect.TransformPoint(cardRect.rect.center)) });
            yield return null;
            yield return null;
            Assert.AreSame(hovered.CurrentSkill, Field<SkillData>(Detail, "shownSkill"));
            var cardCorners = new Vector3[4];
            cardRect.GetWorldCorners(cardCorners);
            var detailRect = (RectTransform)Field<GameObject>(Detail, "body").transform;
            var detailCorners = new Vector3[4];
            detailRect.GetWorldCorners(detailCorners);
            Vector2 cardBottom = RectTransformUtility.WorldToScreenPoint(null, (cardCorners[0] + cardCorners[3]) * 0.5f);
            Vector2 detailTop = RectTransformUtility.WorldToScreenPoint(null, (detailCorners[1] + detailCorners[2]) * 0.5f);
            Assert.Greater(cardBottom.y, detailTop.y);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(cardBottom.x, (cardBottom.y + detailTop.y) * 0.5f) });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = RectTransformUtility.WorldToScreenPoint(null, detailRect.TransformPoint(detailRect.rect.center)) });
            float until = Time.realtimeSinceStartup + 0.35f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.AreSame(hovered.CurrentSkill, Field<SkillData>(Detail, "shownSkill"));
            Assert.True(root.activeSelf);
            Assert.Zero(grants);
            Assert.Zero(session.Owned.Count);
        }
        [UnityTest]
        public IEnumerator LongTitleAndFooterKeepTheirBandsInsideMainPanel()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            title.text = "아주 긴 보상 제목 Long reward title\n추가로 길어진 두 번째 제목줄 Longer second title";
            reroll.GetComponentInChildren<Text>().text = "긴 리롤 안내\nReroll long description\n追加の説明";
            yield return Settle();
            AssertTextFits(title); AssertTextFits(reroll.GetComponentInChildren<Text>());
            AssertInsideMain(title.transform); AssertInsideMain(reroll.transform); AssertInsideMain(skip.transform);
            AssertSeparate(title.transform, cards[0].transform); AssertSeparate(reroll.transform, skip.transform);
        }

        private SkillCardView BuildCard(int index)
        {
            var card = UiFactory.CreateRect(main, "Card" + index, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(432f, 440f));
            var image = card.AddComponent<Image>();
            var button = card.AddComponent<Button>();
            button.targetGraphic = image;
            var iconObject = UiFactory.CreateRect(card.transform, "Icon", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(112f, 112f));
            var icon = iconObject.AddComponent<Image>();
            var name = UiFactory.CreateLabel(card.transform, "Name", Vector2.zero, new Vector2(384f, 88f), "", 32, Color.white, TextAnchor.MiddleCenter);
            var headline = UiFactory.CreateLabel(card.transform, "RarityCategory", Vector2.zero, new Vector2(384f, 64f), "", 24, Color.white, TextAnchor.MiddleCenter);
            var description = UiFactory.CreateLabel(card.transform, "Description", Vector2.zero, new Vector2(384f, 96f), "", 26, Color.white, TextAnchor.UpperCenter);
            var formula = UiFactory.CreateLabel(card.transform, "Formula", Vector2.zero, new Vector2(384f, 24f), "", 17, Color.white, TextAnchor.MiddleLeft);
            var view = card.AddComponent<SkillCardView>();
            Set(view, "background", image);
            Set(view, "iconImage", icon);
            Set(view, "nameText", name);
            Set(view, "rarityCategoryText", headline);
            Set(view, "descriptionText", description);
            Set(view, "formulaText", formula);
            Set(view, "selectButton", button);
            return view;
        }

        private void BuildReplacement()
        {
            var modalRoot = UiFactory.CreateRect(canvas.transform, "ReplacementRoot", Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            modalRoot.SetActive(false);
            var box = UiFactory.CreateRect(modalRoot.transform, "Panel", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(520f, 320f));
            var incoming = UiFactory.CreateLabel(box.transform, "IncomingText", new Vector2(0f, 100f), new Vector2(480f, 40f), "", 20, Color.white, TextAnchor.MiddleCenter);
            var buttons = new[] { UiFactory.CreateButton(box.transform, "Slot0", new Vector2(-120f, 20f), new Vector2(200f, 72f), "", 14),
                UiFactory.CreateButton(box.transform, "Slot1", new Vector2(120f, 20f), new Vector2(200f, 72f), "", 14) };
            replacementCancel = UiFactory.CreateButton(box.transform, "Cancel", new Vector2(0f, -110f), new Vector2(200f, 72f), "Cancel", 14);
            replacement = modalRoot.AddComponent<ReplacementModal>();
            Set(replacement, "root", modalRoot);
            Set(replacement, "incomingText", incoming);
            Set(replacement, "currentSlotButtons", buttons);
            Set(replacement, "currentSlotLabels", buttons.Select(button => button.GetComponentInChildren<Text>()).ToArray());
            Set(replacement, "cancelButton", replacementCancel);
        }

        private IEnumerator OpenDraft()
        {
            GameEvents.RaisePlayerLevelUp(2, DraftTriggerReason.LevelUp);
            yield return Settle();
            Assert.True(root.activeSelf);
            Assert.True(session.IsSessionActive);
            Assert.AreEqual(3, session.CurrentOptions.Cards.Count);
        }
        private SkillCardDetailsPanel Detail => Field<SkillCardDetailsPanel>(presenter, "cardDetails");
        private void OnReplaceRequested(SkillData incoming, IReadOnlyList<SkillData> owned) => replacement.Open(incoming, owned, session);
        private void OnGranted(SkillData skill, DraftTriggerReason reason) => grants++;
        private void OnClosed() => closed++;
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void AssertTextFits(Text text)
        {
            Assert.False(text.resizeTextForBestFit);
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.name);
        }
        private void AssertSeparate(Transform first, Transform second)
        {
            var a = RectTransformUtility.CalculateRelativeRectTransformBounds(main, first);
            var b = RectTransformUtility.CalculateRelativeRectTransformBounds(main, second);
            Assert.True(a.max.x <= b.min.x || b.max.x <= a.min.x || a.max.y <= b.min.y || b.max.y <= a.min.y);
        }
        private void AssertInsideMain(Transform target)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(main, target);
            Assert.GreaterOrEqual(bounds.min.x, main.rect.xMin - 1f, target.name);
            Assert.LessOrEqual(bounds.max.x, main.rect.xMax + 1f, target.name);
            Assert.GreaterOrEqual(bounds.min.y, main.rect.yMin - 1f, target.name);
            Assert.LessOrEqual(bounds.max.y, main.rect.yMax + 1f, target.name);
        }

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
    }
}