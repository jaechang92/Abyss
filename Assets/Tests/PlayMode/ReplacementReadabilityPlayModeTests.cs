using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class ReplacementReadabilityPlayModeTests
    {
        private sealed class ModalView
        {
            public GameObject Root;
            public RectTransform Box;
            public Text Incoming;
            public Button[] Slots;
            public Text[] SlotLabels;
            public Button Cancel;
        }
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> created = new();
        private GameObject canvas;
        private GameObject ownedEvents;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage previousLanguage;
        private float previousTimeScale;
        private Random.State previousRandom;
        private InputSettings previousInput;
        private InputSettings testInput;
        private FormController forms;
        private FormData[] formData;
        private SkillData[] skills;
        private DraftSessionController session;
        private ReplacementModal skillModal;
        private FormReplacementModal formModal;
        private ModalView skillView;
        private ModalView formView;
        private int skillGranted;
        private int draftClosed;
        private int optionsReady;
        private int formResolved;
        private int formSwapped;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            created.Clear();
            skillGranted = draftClosed = optionsReady = formResolved = formSwapped = 0;
            previousLanguage = Loc.CurrentLanguage;
            previousTimeScale = Time.timeScale;
            previousRandom = Random.state;
            previousInput = InputSystem.settings;
            testInput = Object.Instantiate(previousInput);
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInput;
            if (EventSystem.current == null) ownedEvents = new GameObject("ReplacementReadabilityEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            previousNavigation = EventSystem.current.sendNavigationEvents;
            canvas = UiFactory.CreateOverlayCanvas("ReplacementReadabilityTest", 500);
            formData = new FormData[3];
            for (int i = 0; i < 3; i++)
            {
                formData[i] = ScriptableObject.CreateInstance<FormData>();
                formData[i].formId = "replacement_form_" + i;
                formData[i].displayName = "길게 적힌 폼 이름 Long form name とても長いフォーム名 " + i;
                created.Add(formData[i]);
            }
            var formObject = new GameObject("ReplacementTestFormController");
            created.Add(formObject);
            forms = formObject.AddComponent<FormController>();
            forms.SetRunContext(false);
            forms.EquipForm(formData[0], 0, activate: true);
            forms.EquipForm(formData[1], 1, activate: false);
            skills = new SkillData[5];
            for (int i = 0; i < skills.Length; i++)
            {
                skills[i] = ScriptableObject.CreateInstance<SkillData>();
                skills[i].skillId = "replacement_skill_" + i;
                skills[i].displayName = "길게 적힌 스킬 이름 Long skill name とても長いスキル名 " + i;
                skills[i].category = SkillCategory.Active;
                created.Add(skills[i]);
            }
            skillView = BuildView("SkillReplacement");
            skillModal = skillView.Root.AddComponent<ReplacementModal>();
            Wire(skillModal, skillView);
            formView = BuildView("FormReplacement");
            formModal = formView.Root.AddComponent<FormReplacementModal>();
            Wire(formModal, formView);
            GameEvents.OnSkillDrafted += OnSkillGranted;
            GameEvents.OnDraftClosed += OnDraftClosed;
            GameEvents.OnDraftOptionsReady += OnOptionsReady;
            GameEvents.OnFormRewardResolved += OnFormResolved;
            GameEvents.OnFormSwapped += OnFormSwapped;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                GameEvents.OnSkillDrafted -= OnSkillGranted;
                GameEvents.OnDraftClosed -= OnDraftClosed;
                GameEvents.OnDraftOptionsReady -= OnOptionsReady;
                GameEvents.OnFormRewardResolved -= OnFormResolved;
                GameEvents.OnFormSwapped -= OnFormSwapped;
                GameEvents.OnDraftSlotReplaceRequested -= OnReplaceRequested;
                if (canvas != null) Object.Destroy(canvas);
                foreach (var item in created) if (item != null) Object.Destroy(item);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                InputSystem.settings = previousInput;
                if (testInput != null) Object.Destroy(testInput);
                Loc.SetLanguage(previousLanguage);
                Time.timeScale = previousTimeScale;
                Random.state = previousRandom;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator SkillModalLongTextFitsAtAllScalesAndRestoresWithoutGranting()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            skillModal.Open(skills[2], new[] { skills[0], skills[1] }, null);
            yield return SettleLayout();
            float baselineHeight = skillView.Box.rect.height;
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
                {
                    Loc.SetLanguage(language);
                    yield return SettleLayout();
                    AssertLayout(skillView, scale);
                    Assert.AreEqual(skills[0].displayName, skillView.SlotLabels[0].text);
                    Assert.True(skillView.Root.activeSelf);
                    Assert.Zero(skillGranted);
                }
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return SettleLayout();
            Assert.AreEqual(baselineHeight, skillView.Box.rect.height, 0.5f);
        }

        [UnityTest]
        public IEnumerator SkillCancelReturnsSameOptionsAndKeepsBothOwnedSkills()
        {
            yield return OpenActualSkillReplacement();
            var beforeOptions = session.CurrentOptions;
            var beforeOwned = session.Owned.ToArray();
            int beforeReady = optionsReady;
            skillView.Cancel.onClick.Invoke();
            skillView.Cancel.onClick.Invoke();
            Assert.False(skillView.Root.activeSelf);
            Assert.True(session.IsSessionActive);
            Assert.AreSame(beforeOptions, session.CurrentOptions);
            CollectionAssert.AreEqual(beforeOwned, session.Owned);
            Assert.AreEqual(beforeReady + 1, optionsReady);
            Assert.Zero(skillGranted);
            Assert.Zero(draftClosed);
        }

        [UnityTest]
        public IEnumerator SkillConfirmationReplacesExactSlotAndGrantsOnlyOnce()
        {
            yield return OpenActualSkillReplacement();
            skillView.Slots[1].onClick.Invoke();
            skillView.Slots[1].onClick.Invoke();
            CollectionAssert.AreEqual(new[] { skills[0], skills[2] }, session.Owned);
            Assert.False(session.IsSessionActive);
            Assert.False(skillView.Root.activeSelf);
            Assert.AreEqual(1, skillGranted);
            Assert.AreEqual(1, draftClosed);
        }

        [UnityTest]
        public IEnumerator FormLongTextAndTwoStepCancelRemainReadableAndNeverGrant()
        {
            formModal.Open(formData[2], forms);
            yield return SettleLayout();
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
                {
                    Loc.SetLanguage(language);
                    formModal.Open(formData[2], forms);
                    yield return SettleLayout();
                    AssertLayout(formView, scale);
                    Assert.AreSame(formData[0], forms.GetSlot(0));
                    Assert.AreSame(formData[1], forms.GetSlot(1));
                    Assert.Zero(formResolved);
                }
            }
            formView.Cancel.onClick.Invoke();
            formView.Cancel.onClick.Invoke(); // Duplicate input in one frame must only arm cancellation.
            yield return SettleLayout();
            Assert.True(formView.Root.activeSelf);
            Assert.AreEqual(Loc.Get(StringKey.Form_ReplaceCancelConfirm), formView.Cancel.GetComponentInChildren<Text>().text);
            AssertLayout(formView, 1.5f);
            Assert.Zero(formResolved);
            formView.Cancel.onClick.Invoke();
            Assert.False(formView.Root.activeSelf);
            Assert.AreSame(formData[0], forms.GetSlot(0));
            Assert.AreSame(formData[1], forms.GetSlot(1));
            Assert.AreEqual(1, formResolved);
            Assert.AreEqual(1, draftClosed);
            Assert.Zero(formSwapped);
        }

        [UnityTest]
        public IEnumerator FormSelectedSlotAcquiresAndActivatesOnlyOnce()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            formModal.Open(formData[2], forms);
            yield return SettleLayout();
            formView.Slots[1].onClick.Invoke();
            formView.Slots[1].onClick.Invoke();
            Assert.AreSame(formData[0], forms.GetSlot(0));
            Assert.AreSame(formData[2], forms.GetSlot(1));
            Assert.AreSame(formData[2], forms.CurrentForm);
            Assert.AreEqual(1, forms.ActiveSlot);
            Assert.AreEqual(1, formResolved);
            Assert.AreEqual(1, formSwapped);
            Assert.AreEqual(1, draftClosed);
            Assert.False(formView.Root.activeSelf);
        }

        [UnityTest]
        public IEnumerator UnavailableSkillSlotIsDisabledAndExcludedFromNavigation()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            skillModal.Open(skills[2], new SkillData[] { null, skills[1] }, null);
            yield return SettleLayout();
            Assert.False(skillView.Slots[0].interactable);
            Assert.True(skillView.Slots[1].interactable);
            Assert.AreSame(skillView.Slots[1].gameObject, EventSystem.current.currentSelectedGameObject);
            foreach (var button in new[] { skillView.Slots[1], skillView.Cancel })
                foreach (var next in new[] { button.navigation.selectOnUp, button.navigation.selectOnDown,
                    button.navigation.selectOnLeft, button.navigation.selectOnRight })
                    Assert.AreNotSame(skillView.Slots[0], next);
            skillView.Slots[0].onClick.Invoke();
            Assert.True(skillView.Root.activeSelf);
            Assert.Zero(skillGranted);
        }

        private IEnumerator OpenActualSkillReplacement()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            var sessionObject = new GameObject("ReplacementTestDraftSession");
            created.Add(sessionObject);
            session = sessionObject.AddComponent<DraftSessionController>();
            Set(session, "formController", forms);
            var config = ScriptableObject.CreateInstance<RunConfig>();
            config.draftOptionCount = 3;
            config.activeSlotLimit = 2;
            created.Add(config);
            Set(session, "config", config);
            session.GetComponent<DraftPoolManager>().SetPool(new[] { skills[2], skills[3], skills[4] });
            session.DebugGrantSkill(skills[0]);
            session.DebugGrantSkill(skills[1]);
            skillGranted = 0;
            GameEvents.OnDraftSlotReplaceRequested += OnReplaceRequested;
            GameEvents.RaisePlayerLevelUp(2, DraftTriggerReason.LevelUp);
            int index = session.CurrentOptions.Cards.ToList().IndexOf(skills[2]);
            Assert.GreaterOrEqual(index, 0);
            Assert.True(session.TrySelect(index)); // Accepted selection requests replacement; no skill was acquired yet.
            yield return SettleLayout();
            Assert.True(skillView.Root.activeSelf);
            AssertLayout(skillView, 1.5f);
        }

        private ModalView BuildView(string name)
        {
            var root = UiFactory.CreateRect(canvas.transform, name, Vector2.zero, Vector2.one,
                Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
            UiFactory.Stretch((RectTransform)root.transform);
            root.SetActive(false); // Set serialized references before the first Awake, as the saved HUD does.
            var box = UiFactory.CreateRect(root.transform, "Panel", Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(520f, 320f));
            box.AddComponent<Image>();
            var incoming = UiFactory.CreateLabel(box.transform, "IncomingText", new Vector2(0f, -48f),
                new Vector2(480f, 40f), "", 20, Color.white, TextAnchor.MiddleCenter);
            incoming.rectTransform.anchorMin = incoming.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            incoming.rectTransform.pivot = new Vector2(0.5f, 1f);
            var buttons = new Button[2];
            for (int i = 0; i < 2; i++)
                buttons[i] = UiFactory.CreateButton(box.transform, "Slot" + i + "Button",
                    new Vector2(i == 0 ? -120f : 120f, 20f), new Vector2(200f, 72f), "", 14);
            var cancel = UiFactory.CreateButton(box.transform, "CancelButton", new Vector2(0f, -110f),
                new Vector2(200f, 72f), Loc.Get(StringKey.Common_Cancel), 14);
            return new ModalView { Root = root, Box = (RectTransform)box.transform, Incoming = incoming,
                Slots = buttons, SlotLabels = buttons.Select(button => button.GetComponentInChildren<Text>()).ToArray(), Cancel = cancel };
        }

        private static void Wire(object modal, ModalView view)
        {
            Set(modal, "root", view.Root);
            Set(modal, "incomingText", view.Incoming);
            Set(modal, "currentSlotButtons", view.Slots);
            Set(modal, "currentSlotLabels", view.SlotLabels);
            Set(modal, "cancelButton", view.Cancel);
        }

        private static void AssertLayout(ModalView view, float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(20f * scale), view.Incoming.fontSize);
            foreach (var text in view.Root.GetComponentsInChildren<Text>())
            {
                Assert.False(text.resizeTextForBestFit);
                Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.text);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.Box, text.transform);
                Assert.GreaterOrEqual(bounds.min.y, view.Box.rect.yMin - 1f);
                Assert.LessOrEqual(bounds.max.y, view.Box.rect.yMax + 1f);
            }
            var all = new[] { view.Slots[0], view.Slots[1], view.Cancel };
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    var a = RectTransformUtility.CalculateRelativeRectTransformBounds(view.Box, all[i].transform);
                    var b = RectTransformUtility.CalculateRelativeRectTransformBounds(view.Box, all[j].transform);
                    Assert.True(a.max.x <= b.min.x || b.max.x <= a.min.x || a.max.y <= b.min.y || b.max.y <= a.min.y,
                        all[i].name + " overlaps " + all[j].name);
                }
        }

        private static IEnumerator SettleLayout()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private void OnReplaceRequested(SkillData incoming, IReadOnlyList<SkillData> owned) => skillModal.Open(incoming, owned, session);
        private void OnSkillGranted(SkillData _, DraftTriggerReason reason) => skillGranted++;
        private void OnDraftClosed() => draftClosed++;
        private void OnOptionsReady(DraftOptions _) => optionsReady++;
        private void OnFormResolved() => formResolved++;
        private void OnFormSwapped(FormData previous, FormData current) => formSwapped++;
    }
}