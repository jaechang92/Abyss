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
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class BuildContextReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> created = new();
        private SkillData[] owned;
        private GameObject canvas;
        private GameObject root;
        private BuildContextPanel context;
        private Text title;
        private Text synergy;
        private Text list;
        private SkillCardView card;
        private SkillCardDetailsPanel details;
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
        private InputRouter previousRouter;
        private InputRouter.InputMode previousMode;
        private int grants;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            created.Clear();
            grants = 0;
            previousLanguage = Loc.CurrentLanguage;
            previousTime = Time.timeScale;
            previousRandom = Random.state;
            previousInput = InputSystem.settings;
            previousRouter = InputRouter.GetInstanceSafe();
            if (previousRouter != null) previousMode = previousRouter.CurrentMode;
            testInput = Object.Instantiate(previousInput);
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInput;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            if (EventSystem.current == null) ownedEvents = new GameObject("BuildReadingEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            previousNavigation = EventSystem.current.sendNavigationEvents;
            EventSystem.current.SetSelectedGameObject(null);
            owned = new SkillData[30];
            string[] axes = { SynergyAxis.AXIS_FIRE, SynergyAxis.AXIS_ABYSS, SynergyAxis.AXIS_GUARD,
                SynergyAxis.AXIS_BLOOD_PACT, SynergyAxis.AXIS_FROST, SynergyAxis.AXIS_SOUL };
            for (int i = 0; i < owned.Length; i++)
            {
                owned[i] = ScriptableObject.CreateInstance<SkillData>();
                owned[i].skillId = "build_reading_" + i;
                owned[i].displayName = "긴 보유 스킬 Long owned skill 長い所持スキル END_BUILD_" + i;
                owned[i].synergyTag = axes[i % axes.Length];
                owned[i].category = i < 2 ? SkillCategory.Active : SkillCategory.Passive;
                owned[i].description = string.Join("\n", Enumerable.Repeat("Long detail description 자세한 설명", 30));
                created.Add(owned[i]);
            }
            canvas = UiFactory.CreateOverlayCanvas("BuildReadingCanvas", 500);
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            root = UiFactory.CreateRect(canvas.transform, "DraftRoot", Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            UiFactory.Stretch((RectTransform)root.transform);
            root.SetActive(false);
            var main = UiFactory.CreateRect(root.transform, "MainPanel", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(1460f, 960f));
            main.AddComponent<Image>();
            var box = UiFactory.CreateRect(root.transform, "BuildContext", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(260f, 900f));
            box.AddComponent<Image>();
            title = UiFactory.CreateLabel(box.transform, "Title", Vector2.zero, new Vector2(228f, 40f), "", 24, Color.white, TextAnchor.MiddleCenter);
            synergy = UiFactory.CreateLabel(box.transform, "SynergyCounts", Vector2.zero, new Vector2(228f, 100f), "", 24, Color.white, TextAnchor.UpperCenter);
            list = UiFactory.CreateLabel(box.transform, "SkillList", Vector2.zero, new Vector2(228f, 650f), "", 24, Color.white, TextAnchor.UpperLeft);
            context = box.AddComponent<BuildContextPanel>();
            Set(context, "titleText", title);
            Set(context, "synergyCountsText", synergy);
            Set(context, "skillListText", list);
            card = BuildCard(main.transform);
            details = SkillCardDetailsPanel.Create(root.transform);
            card.AttachDetails(details);
            ModalArtSkin.ApplyDraft(root.transform, null, new[] { card }, null, null, null, null, context, details);
            context.AttachReadability();
            context.Refresh(owned);
            root.SetActive(true);
            card.Bind(0, owned[29]);
            EventSystem.current.SetSelectedGameObject(card.FocusTarget);
            GameEvents.OnSkillDrafted += OnGranted;
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                GameEvents.OnSkillDrafted -= OnGranted;
                if (canvas != null) Object.Destroy(canvas);
                foreach (var value in created) if (value != null) Object.Destroy(value);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (previousRouter != null) previousRouter.SwitchMode(previousMode);
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
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
        public IEnumerator FullOwnedAndSynergyTextFitsEveryScaleLanguageAndCanReachLastLine()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
                {
                    Loc.SetLanguage(language);
                    yield return Settle();
                    foreach (var text in new[] { title, synergy, list })
                    {
                        Assert.AreEqual(Mathf.RoundToInt(24f * scale), text.fontSize);
                        Assert.False(text.resizeTextForBestFit);
                        Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.name);
                    }
                    StringAssert.Contains("END_BUILD_29", list.text);
                    StringAssert.Contains("Active 2/2", list.text);
                    foreach (var axis in owned.Select(skill => skill.synergyTag).Distinct())
                        StringAssert.Contains("[" + SynergyAxis.GetDisplayName(axis) + "] 5", synergy.text);
                    Assert.Greater(Scroll.content.rect.height, Scroll.viewport.rect.height);
                    Scroll.verticalNormalizedPosition = 0f;
                    Canvas.ForceUpdateCanvases();
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Scroll.viewport, list.transform);
                    Assert.GreaterOrEqual(bounds.min.y, Scroll.viewport.rect.yMin - 1f);
                    Assert.LessOrEqual(bounds.min.y, Scroll.viewport.rect.yMax + 1f);
                    AssertNoAcquisition();
                }
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return Settle();
            Assert.AreEqual(24, list.fontSize);
        }

        [UnityTest]
        public IEnumerator NullEmptyAndShorterRefreshRemoveOldNamesAndShrinkContent()
        {
            float fullHeight = Scroll.content.rect.height;
            context.Refresh(new[] { owned[0] });
            yield return Settle();
            StringAssert.Contains("END_BUILD_0", list.text);
            StringAssert.DoesNotContain("END_BUILD_29", list.text);
            Assert.Less(Scroll.content.rect.height, fullHeight);
            foreach (var value in new IReadOnlyList<SkillData>[] { null, new SkillData[0], new SkillData[] { null } })
            {
                context.Refresh(value);
                yield return Settle();
                StringAssert.DoesNotContain("END_BUILD_", list.text);
                Assert.LessOrEqual(Scroll.content.rect.height, Scroll.viewport.rect.height + 1f);
                AssertNoAcquisition();
            }
        }

        [UnityTest]
        public IEnumerator ManualScrollPositionSurvivesScaleAndCanvasChanges()
        {
            Scroll.verticalNormalizedPosition = 0.35f;
            foreach (float scale in new[] { 1.5f, 1.25f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                yield return Settle();
                Assert.AreEqual(0.35f, Scroll.verticalNormalizedPosition, 0.015f);
            }
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.GetComponent<Canvas>().scaleFactor = 0.5f;
            yield return Settle();
            Assert.AreEqual(0.35f, Scroll.verticalNormalizedPosition, 0.015f);
            AssertNoAcquisition();
        }

        [UnityTest]
        public IEnumerator PageDownAndEndReadWhilePausedWithoutAcquiringOrChangingSelection()
        {
            Time.timeScale = 0f;
            EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
            var selection = EventSystem.current.currentSelectedGameObject;
            float before = Scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return null;
            yield return null;
            Assert.Greater(Scroll.content.anchoredPosition.y, before);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.End));
            yield return null;
            yield return null;
            Assert.LessOrEqual(Scroll.verticalNormalizedPosition, 0.01f);
            Assert.AreSame(selection, EventSystem.current.currentSelectedGameObject);
            AssertNoAcquisition();
        }

        [UnityTest]
        public IEnumerator RightStickUsesUnscaledTimeAndNeutralStopsBuildScroll()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Time.timeScale = 0f;
                EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
                float before = Scroll.content.anchoredPosition.y;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0f, -1f) });
                yield return null;
                yield return null;
                Assert.Greater(Scroll.content.anchoredPosition.y, before);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                yield return null;
                float stopped = Scroll.content.anchoredPosition.y;
                yield return null;
                yield return null;
                Assert.AreEqual(stopped, Scroll.content.anchoredPosition.y, 0.5f);
                AssertNoAcquisition();
            }
            finally { InputSystem.RemoveDevice(gamepad); }
        }

        [UnityTest]
        public IEnumerator WheelOnlyReadsBuildWhenPointerIsInsideItsReadingArea()
        {
            Time.timeScale = 0f;
            var inside = RectTransformUtility.WorldToScreenPoint(null, Scroll.viewport.TransformPoint(Scroll.viewport.rect.center));
            float before = Scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside, scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.Greater(Scroll.content.anchoredPosition.y, before);
            float after = Scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-10000f, -10000f), scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.AreEqual(after, Scroll.content.anchoredPosition.y, 0.5f);
            AssertNoAcquisition();
        }

        [UnityTest]
        public IEnumerator PointerBuildOwnershipPreventsFocusedSkillDetailFromScrollingTwice()
        {
            Time.timeScale = 0f;
            EventSystem.current.SetSelectedGameObject(card.FocusTarget);
            yield return Settle();
            var detailScroll = Field<ScrollRect>(details, "readingScroll");
            Assert.True(Field<GameObject>(details, "body").activeSelf);
            float detailBefore = detailScroll.content.anchoredPosition.y;
            float buildBefore = Scroll.content.anchoredPosition.y;
            var inside = RectTransformUtility.WorldToScreenPoint(null, Scroll.viewport.TransformPoint(Scroll.viewport.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return null;
            yield return null;
            Assert.Greater(Scroll.content.anchoredPosition.y, buildBefore);
            Assert.AreEqual(detailBefore, detailScroll.content.anchoredPosition.y, 0.5f);
            Assert.AreSame(card.FocusTarget, EventSystem.current.currentSelectedGameObject);
            AssertNoAcquisition();
        }

        [UnityTest]
        public IEnumerator DraftPresenterRefreshesBuildFromActualOwnedSkillEvent()
        {
            var session = CreateSessionPresenter();
            yield return Settle();
            Assert.AreEqual(1, session.Owned.Count);
            Assert.AreEqual(1, grants);
            Assert.AreEqual(1, list.text.Split(new[] { "END_BUILD_0" }, System.StringSplitOptions.None).Length - 1);
            StringAssert.DoesNotContain("END_BUILD_29", list.text);
            Assert.True(root.activeSelf);
        }

        [UnityTest]
        public IEnumerator ReadingFocusEnterAndSubmitNeverConfirmTheFirstDraftCard()
        {
            var session = CreateSessionPresenter();
            yield return Settle();
            EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
            Assert.IsEmpty(context.ReadingTarget.GetComponents<ISubmitHandler>());
            Assert.False(ExecuteEvents.Execute(context.ReadingTarget.gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler));
            int before = session.Owned.Count;
            int beforeGrants = grants;
            int? beforeGold = RunManager.HasInstance ? RunManager.Instance.GoldShards : null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            yield return null;
            Assert.AreEqual(before, session.Owned.Count);
            Assert.AreEqual(beforeGrants, grants);
            Assert.AreEqual(beforeGold, RunManager.HasInstance ? (int?)RunManager.Instance.GoldShards : null);
            Assert.True(session.IsSessionActive);
            Assert.True(root.activeSelf);
            Assert.AreSame(context.ReadingTarget.gameObject, EventSystem.current.currentSelectedGameObject);
        }
        [UnityTest]
        public IEnumerator ReadingTargetCanNavigateToCardWithoutAcquiringIt()
        {
            EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
            yield return Settle();
            Assert.AreSame(card.FocusTarget.GetComponent<Button>(), context.ReadingTarget.FindSelectableOnRight());
            Assert.AreSame(context.ReadingTarget, card.FocusTarget.GetComponent<Button>().FindSelectableOnLeft());
            ExecuteEvents.Execute(context.ReadingTarget.gameObject,
                new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right }, ExecuteEvents.moveHandler);
            Assert.AreSame(card.FocusTarget, EventSystem.current.currentSelectedGameObject);
            AssertNoAcquisition();
        }
        [UnityTest]
        public IEnumerator HiddenDraftReleasesReadingOwnershipAndReactivationPreservesItsSelectedNode()
        {
            EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
            var selection = EventSystem.current.currentSelectedGameObject;
            Assert.True(BuildContextPanel.IsReadingInputOwned);
            root.SetActive(false);
            yield return null;
            Assert.False(BuildContextPanel.IsReadingInputOwned);
            root.SetActive(true);
            yield return Settle();
            Assert.AreSame(selection, EventSystem.current.currentSelectedGameObject);
            Assert.True(BuildContextPanel.IsReadingInputOwned);
            AssertNoAcquisition();
        }
        [UnityTest]
        public IEnumerator PointerDetailWheelStillWorksWhileBuildKeepsReadingFocus()
        {
            Time.timeScale = 0f;
            EventSystem.current.SetSelectedGameObject(context.ReadingTarget.gameObject);
            details.NotifyHover(card, true);
            yield return Settle();
            var detailScroll = Field<ScrollRect>(details, "readingScroll");
            float detailBefore = detailScroll.content.anchoredPosition.y;
            float buildBefore = Scroll.content.anchoredPosition.y;
            var inside = RectTransformUtility.WorldToScreenPoint(null,
                detailScroll.viewport.TransformPoint(detailScroll.viewport.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside, scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.Greater(detailScroll.content.anchoredPosition.y, detailBefore);
            Assert.AreEqual(buildBefore, Scroll.content.anchoredPosition.y, 0.5f);
            Assert.AreSame(context.ReadingTarget.gameObject, EventSystem.current.currentSelectedGameObject);
            AssertNoAcquisition();
        }
        private DraftSessionController CreateSessionPresenter()
        {
            var formObject = new GameObject("BuildReadingSessionForms");
            created.Add(formObject);
            var forms = formObject.AddComponent<FormController>();
            forms.SetRunContext(false);
            var sessionObject = new GameObject("BuildReadingSession");
            created.Add(sessionObject);
            var session = sessionObject.AddComponent<DraftSessionController>();
            Set(session, "formController", forms);
            session.GetComponent<DraftPoolManager>().SetPool(owned.Take(3));
            var presenterObject = new GameObject("BuildReadingPresenter");
            created.Add(presenterObject);
            presenterObject.SetActive(false);
            var presenter = presenterObject.AddComponent<DraftPanelPresenter>();
            Set(presenter, "root", root);
            Set(presenter, "cards", new[] { card });
            Set(presenter, "buildContext", context);
            Set(presenter, "session", session);
            presenterObject.SetActive(true);
            session.DebugGrantSkill(owned[0]);
            GameEvents.RaisePlayerLevelUp(2, DraftTriggerReason.LevelUp);
            return session;
        }

        private SkillCardView BuildCard(Transform parent)
        {
            var go = UiFactory.CreateRect(parent, "Card", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(432f, 440f));
            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var icon = UiFactory.CreateRect(go.transform, "Icon", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(112f, 112f)).AddComponent<Image>();
            var name = UiFactory.CreateLabel(go.transform, "Name", Vector2.zero, new Vector2(384f, 88f), "", 32, Color.white, TextAnchor.MiddleCenter);
            var headline = UiFactory.CreateLabel(go.transform, "RarityCategory", Vector2.zero, new Vector2(384f, 64f), "", 24, Color.white, TextAnchor.MiddleCenter);
            var description = UiFactory.CreateLabel(go.transform, "Description", Vector2.zero, new Vector2(384f, 96f), "", 26, Color.white, TextAnchor.UpperCenter);
            var formula = UiFactory.CreateLabel(go.transform, "Formula", Vector2.zero, new Vector2(384f, 24f), "", 17, Color.white, TextAnchor.MiddleLeft);
            var view = go.AddComponent<SkillCardView>();
            Set(view, "background", image); Set(view, "iconImage", icon); Set(view, "nameText", name);
            Set(view, "rarityCategoryText", headline); Set(view, "descriptionText", description);
            Set(view, "formulaText", formula); Set(view, "selectButton", button);
            return view;
        }
        private ScrollRect Scroll => Field<ScrollRect>(context, "readingScroll");
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private void OnGranted(SkillData skill, DraftTriggerReason reason) => grants++;
        private void AssertNoAcquisition()
        {
            Assert.Zero(grants);
            Assert.AreEqual(30, owned.Length);
            Assert.AreEqual("build_reading_29", owned[29].skillId);
            Assert.AreSame(owned[29], card.CurrentSkill);
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