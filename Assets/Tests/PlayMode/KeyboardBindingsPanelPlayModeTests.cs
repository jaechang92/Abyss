using System.Collections;
using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class KeyboardBindingsPanelPlayModeTests
    {
        private readonly PlayModeSaveGuard saveGuard = new();
        private Keyboard keyboard;
        private Gamepad gamepad;
        private GameObject ownedEventSystem;
        private GameObject initialSelection;
        private bool initialNavigation;
        private bool ownsSettings;
        private bool ownsKeys;
        private GameObject playerInputObject;
        private InputActionAsset playerActions;
        private PlayerInput playerInput;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private LocalizationLanguage oldLanguage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            oldLanguage = Loc.CurrentLanguage;
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            GameplayKeyBindings.ReloadFromSettings();
            ownsSettings = Object.FindAnyObjectByType<SettingsPanel>() == null;
            ownsKeys = Object.FindAnyObjectByType<KeyboardBindingsPanel>() == null;
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("KeyboardBindingsTestEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            initialSelection = EventSystem.current.currentSelectedGameObject;
            initialNavigation = EventSystem.current.sendNavigationEvents;
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            var config = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings");
            playerActions = Object.Instantiate(config.SourceActions);
            playerInputObject = new GameObject("KeyboardBindingsPlayerInput");
            playerInputObject.SetActive(false);
            playerInput = playerInputObject.AddComponent<PlayerInput>();
            playerInput.actions = playerActions;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            playerInputObject.SetActive(true);
            GameplayKeyBindings.ApplyTo(playerInput.actions);
            SettingsPanel.Open();
            yield return null;
            ClickButton(ButtonIn(Object.FindAnyObjectByType<SettingsPanel>().gameObject, "KeyboardButton"));
            yield return null;
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                KeyboardBindingsPanel.Close();
                SettingsPanel.Close();
                Loc.SetLanguage(oldLanguage);
                if (ownsKeys)
                {
                    var keys = Object.FindAnyObjectByType<KeyboardBindingsPanel>();
                    if (keys != null) Object.Destroy(keys.gameObject);
                }
                if (ownsSettings)
                {
                    var settings = Object.FindAnyObjectByType<SettingsPanel>();
                    if (settings != null) Object.Destroy(settings.gameObject);
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (gamepad != null) InputSystem.RemoveDevice(gamepad);
                if (playerInputObject != null) Object.DestroyImmediate(playerInputObject);
                if (playerActions != null) Object.DestroyImmediate(playerActions);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = initialNavigation;
                    EventSystem.current.SetSelectedGameObject(initialSelection != null && initialSelection.activeInHierarchy ? initialSelection : null);
                }
                if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
            }
            finally
            {
                try
                {
                    if (saveGuard.IsAcquired)
                    {
                        MetaSaveService.Instance.Current.settings.keyboardBindingOverrides = string.Empty;
                        GameplayKeyBindings.ReloadFromSettings();
                    }
                }
                finally
                {
                    saveGuard.Release();
                    GameplayKeyBindings.InvalidateCache();
                    if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
                    if (testInputSettings != null) Object.Destroy(testInputSettings);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeCancelsCaptureWithoutClosingEitherWindowOrChangingKey()
        {
            string before = GameplayKeyBindings.GetDisplayKey("Interact");
            ClickButton(InteractButton());
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.True);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
            yield return Press(Key.Escape);
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.True);
            Assert.That(SettingsPanel.IsOpen, Is.True);
            Assert.That(GameplayKeyBindings.GetDisplayKey("Interact"), Is.EqualTo(before));
            Assert.That(Status(), Is.EqualTo(Loc.Get(StringKey.Keys_Cancelled)));
            Assert.That(EventSystem.current.sendNavigationEvents, Is.EqualTo(initialNavigation));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(InteractButton().gameObject));
            yield return ReleaseKeys();
            yield return Press(Key.Escape);
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.False);
            Assert.That(SettingsPanel.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator PhysicalKeyCaptureSavesCanonicalPathAndResetRestoresDefaults()
        {
            var config = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings");
            string sourceBefore = config.SourceActions.ToJson();
            string original = GameplayKeyBindings.GetDisplayKey("Interact");
            ClickButton(InteractButton());
            yield return Press(Key.F);
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.True);
            string expected = InputControlPath.ToHumanReadableString("<Keyboard>/f", InputControlPath.HumanReadableStringOptions.OmitDevice);
            Assert.That(GameplayKeyBindings.GetDisplayKey("Interact"), Is.EqualTo(expected));
            var interact = playerInput.actions.FindAction("Player/Interact");
            bool runtimeChanged = false;
            foreach (var binding in interact.bindings)
                runtimeChanged |= binding.effectivePath == "<Keyboard>/f";
            Assert.That(runtimeChanged, Is.True, "The existing PlayerInput clone did not receive the saved key.");
            StringAssert.Contains("<Keyboard>/f", MetaSaveService.Instance.Current.settings.keyboardBindingOverrides);
            Assert.That(config.SourceActions.ToJson(), Is.EqualTo(sourceBefore), "Imported keyboard/gamepad/UI source changed.");
            Assert.That(Status(), Is.EqualTo(Loc.Get(StringKey.Keys_Saved)));
            yield return ReleaseKeys();
            ClickButton(ButtonIn(KeysObject(), "Reset"));
            Assert.That(GameplayKeyBindings.GetDisplayKey("Interact"), Is.EqualTo(original));
            Assert.That(Status(), Is.EqualTo(Loc.Get(StringKey.Keys_ResetDone)));
        }

        [UnityTest]
        public IEnumerator ConflictAndReservedKeyKeepOriginalKeyAndShowReason()
        {
            string before = GameplayKeyBindings.GetDisplayKey("Interact");
            ClickButton(InteractButton());
            yield return Press(Key.C);
            Assert.That(GameplayKeyBindings.GetDisplayKey("Interact"), Is.EqualTo(before));
            Assert.That(Status(), Is.EqualTo(Loc.Get(StringKey.Keys_Conflict)));
            yield return ReleaseKeys();
            ClickButton(InteractButton());
            yield return Press(Key.Tab);
            Assert.That(GameplayKeyBindings.GetDisplayKey("Interact"), Is.EqualTo(before));
            Assert.That(Status(), Is.EqualTo(Loc.Get(StringKey.Keys_Reserved)));
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator GamepadCancelOnlyCancelsCaptureAndRestoresNavigation()
        {
            ClickButton(InteractButton());
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return null;
            yield return null;
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.True);
            Assert.That(SettingsPanel.IsOpen, Is.True);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.EqualTo(initialNavigation));
        }

        [UnityTest]
        public IEnumerator ParentCloseClearsCaptureAndRestoresPreviousNavigationValue()
        {
            EventSystem.current.sendNavigationEvents = false;
            ClickButton(InteractButton());
            SettingsPanel.Close();
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.False);
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
            yield return null;
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingWindowClearsCaptureAndRestoresNavigation()
        {
            ClickButton(InteractButton());
            var window = KeysObject();
            window.SetActive(false);
            Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.False);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.EqualTo(initialNavigation));
            window.SetActive(true);
            yield return null;
            Assert.That(KeyboardBindingsPanel.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator LastRowFocusScrollsIntoViewport()
        {
            var scroll = KeysObject().GetComponentInChildren<ScrollRect>();
            var rows = GameplayKeyBindings.Rows;
            Assert.That(rows.Count, Is.GreaterThan(8));
            var last = ButtonIn(KeysObject(), "BindingRow" + (rows.Count - 1)).transform;
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0f));
            float rowBottom = rows.Count * 56f - scroll.content.anchoredPosition.y;
            Assert.That(rowBottom, Is.LessThanOrEqualTo(scroll.viewport.rect.height + 0.01f));
            Assert.That(KeysObject().GetComponent<Canvas>().sortingOrder, Is.LessThan(UiSortingOrder.SaveStatus));
        }

        [UnityTest]
        public IEnumerator SceneChangeCancelsCaptureAndClosesChild()
        {
            var originalScene = SceneManager.GetActiveScene();
            var temporaryScene = SceneManager.CreateScene("KeyboardBindingsCaptureCleanup");
            try
            {
                ClickButton(InteractButton());
                Assert.That(KeyboardBindingsPanel.IsCapturing, Is.True);
                SceneManager.SetActiveScene(temporaryScene);
                Assert.That(KeyboardBindingsPanel.IsCapturing, Is.False);
                Assert.That(KeyboardBindingsPanel.IsOpen, Is.False);
                Assert.That(EventSystem.current.sendNavigationEvents, Is.EqualTo(initialNavigation));
            }
            finally
            {
                SceneManager.SetActiveScene(originalScene);
                SceneManager.UnloadSceneAsync(temporaryScene);
            }
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MaximumTextKeepsLastRowVisibleAndCaptureCancellationSafeInThreeLanguages()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                yield return null;
                yield return null;
                var target = InteractButton();
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                yield return null;
                var scroll = KeysObject().GetComponentInChildren<ScrollRect>();
                var row = (RectTransform)target.transform.parent;
                float bottom = -row.anchoredPosition.y + row.rect.height * 0.5f - scroll.content.anchoredPosition.y;
                Assert.LessOrEqual(bottom, scroll.viewport.rect.height + 0.1f, language.ToString());
                foreach (var text in scroll.content.GetComponentsInChildren<Text>())
                {
                    Assert.AreEqual(33, text.fontSize);
                    Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.text);
                }
                ClickButton(target);
                yield return null;
                Assert.True(KeyboardBindingsPanel.IsCapturing);
                Assert.LessOrEqual(KeysObject().transform.Find("Body/Panel/Status").GetComponent<Text>().preferredHeight,
                    KeysObject().transform.Find("Body/Panel/Status").GetComponent<RectTransform>().rect.height + 1f);
                yield return Press(Key.Escape);
                yield return ReleaseKeys();
                Assert.False(KeyboardBindingsPanel.IsCapturing);
                Assert.True(KeyboardBindingsPanel.IsOpen);
                Assert.AreEqual(initialNavigation, EventSystem.current.sendNavigationEvents);
            }
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return null;
            Assert.AreEqual(GameplayKeyBindings.Rows.Count * 56f, KeysObject().GetComponentInChildren<ScrollRect>().content.rect.height);
        }

        [UnityTest]
        public IEnumerator ExpandedRowsReflowLongActionAndKeepFooterSeparated()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            var scroll = KeysObject().GetComponentInChildren<ScrollRect>();
            var label = scroll.content.GetChild(0).Find("Action").GetComponent<Text>();
            label.text = "A deliberately long action name that wraps across several lines";
            yield return null;
            var first = (RectTransform)label.transform.parent;
            var second = (RectTransform)scroll.content.GetChild(1);
            Assert.Greater(first.rect.height, 56f);
            Assert.LessOrEqual(second.anchoredPosition.y + second.rect.height * 0.5f,
                first.anchoredPosition.y - first.rect.height * 0.5f);
            var panel = scroll.transform.parent;
            var reset = panel.Find("Reset").GetComponent<RectTransform>();
            var guide = panel.Find("CancelGuide").GetComponent<RectTransform>();
            Assert.Less(guide.anchoredPosition.y + guide.rect.height * 0.5f, reset.anchoredPosition.y - reset.rect.height * 0.5f);
            Assert.Greater(scroll.viewport.anchoredPosition.y - scroll.viewport.rect.height * 0.5f,
                reset.anchoredPosition.y + reset.rect.height * 0.5f);
        }

        [UnityTest]
        public IEnumerator LongCapturePromptAndCanvasChangeDoNotStealFocusOrOverlapViewport()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.25f));
            ClickButton(InteractButton());
            yield return null;
            var canvas = KeysObject().GetComponent<Canvas>();
            KeysObject().GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = 0.5f;
            var status = KeysObject().transform.Find("Body/Panel/Status").GetComponent<Text>();
            status.text = "A lengthy keyboard capture instruction with additional detail that must wrap without obscuring the scrolling list or selecting a disabled binding button.";
            yield return null;
            yield return null;
            var scroll = KeysObject().GetComponentInChildren<ScrollRect>();
            Assert.AreEqual(28, status.fontSize);
            Assert.LessOrEqual(status.preferredHeight, status.rectTransform.rect.height + 1f);
            Assert.Greater(status.rectTransform.anchoredPosition.y - status.rectTransform.rect.height * 0.5f,
                scroll.viewport.anchoredPosition.y + scroll.viewport.rect.height * 0.5f);
            Assert.IsNull(EventSystem.current.currentSelectedGameObject);
            Assert.False(EventSystem.current.sendNavigationEvents);
            Assert.True(KeyboardBindingsPanel.IsCapturing);
            yield return Press(Key.Escape);
            yield return ReleaseKeys();
            Assert.True(KeyboardBindingsPanel.IsOpen);
            Assert.False(KeyboardBindingsPanel.IsCapturing);
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
        }

        private IEnumerator ReleaseKeys()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static GameObject KeysObject() => Object.FindAnyObjectByType<KeyboardBindingsPanel>().gameObject;

        private static void ClickButton(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }

        private static string Status() => KeysObject().transform.Find("Body/Panel/Status").GetComponent<Text>().text;

        private static Button InteractButton()
        {
            var rows = GameplayKeyBindings.Rows;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].ActionName == "Interact") return ButtonIn(KeysObject(), "BindingRow" + i);
            Assert.Fail("Interact keyboard row missing.");
            return null;
        }

        private static Button ButtonIn(GameObject root, string objectName)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == objectName) return transform.GetComponent<Button>() ?? transform.GetComponentInChildren<Button>();
            Assert.Fail("Missing button: " + objectName);
            return null;
        }
    }
}
