using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Input;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
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
    public sealed class RunInteractionLockPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private readonly Dictionary<InputActionAsset, string> previousOverrides = new();
        private PlayerCharacter actor;
        private PlayerInteractor interactor;
        private PlayerInput playerInput;
        private Target target;
        private ExtraBlocker extra;
        private Text prompt;
        private InputRouter router;
        private bool ownsRouter;
        private InputActionAsset previousRouterActions;
        private InputActionAsset previousOwnedActions;
        private InputRouter.InputMode previousRouterMode;
        private GameFlowController flow;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private float timeScale;
        private Random.State random;
        private LocalizationLanguage language;
        private string sourceState;
        private bool ownsRun;
        private ShopRoomPanel shop;
        private bool ownsShop;
        private GameObject previousSelection;
        private bool previousNavigation;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            timeScale = Time.timeScale; random = Random.state; language = Loc.CurrentLanguage;
            ownsRun = !RunManager.HasInstance;
            Time.timeScale = 1f;
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            foreach (var pi in Object.FindObjectsByType<PlayerInput>(FindObjectsInactive.Include))
                if (pi.actions != null && !previousOverrides.ContainsKey(pi.actions)) previousOverrides.Add(pi.actions, pi.actions.SaveBindingOverridesAsJson());
            var source = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions;
            sourceState = source.SaveBindingOverridesAsJson();
            router = InputRouter.GetInstanceSafe(); ownsRouter = router == null;
            if (!ownsRouter)
            {
                previousRouterActions = router.Actions; previousRouterMode = router.CurrentMode;
                previousOwnedActions = Field<InputActionAsset>(router, "ownedActions");
                // Preserve the pre-existing owned clone: UsePlayerActions normally disposes it.
                Set(router, "ownedActions", null);
            }
            GameplayKeyBindings.ReloadFromSettings(); Assert.True(GameplayKeyBindings.ResetKeyboard(out _));
            if (ownsRouter)
            {
                var go = Own(new GameObject("RunInteractionTestRouter")); go.SetActive(false);
                router = go.AddComponent<InputRouter>(); Set(router, "inputActions", source); go.SetActive(true);
            }
            flow = Own(new GameObject("RunInteractionTestFlow")).AddComponent<GameFlowController>();
            var canvas = Own(new GameObject("RunInteractionTestCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080);
            var labelGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text)); labelGo.transform.SetParent(canvas.transform, false);
            prompt = labelGo.GetComponent<Text>(); prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); prompt.fontSize = 24;
            prompt.rectTransform.sizeDelta = new Vector2(700, 48);
            var playerGo = Own(new GameObject("RunInteractionTestPlayer")); playerGo.SetActive(false);
            playerGo.transform.position = new Vector3(20000, 20000, 0);
            actor = playerGo.AddComponent<PlayerCharacter>(); Set(actor, "context", PlayerContext.Run);
            extra = playerGo.AddComponent<ExtraBlocker>();
            var body = playerGo.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var trigger = playerGo.AddComponent<CircleCollider2D>(); trigger.isTrigger = true; trigger.radius = 2;
            interactor = playerGo.AddComponent<PlayerInteractor>(); Set(interactor, "promptLabel", prompt);
            playerInput = playerGo.AddComponent<PlayerInput>(); playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            playerInput.actions = source; playerInput.defaultActionMap = "Player"; playerInput.defaultControlScheme = "Keyboard&Mouse";
            playerGo.SetActive(true); actor.enabled = false;
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            GameplayKeyBindings.ReloadFromSettings();
            router.UsePlayerActions(playerInput.actions); router.SwitchMode(InputRouter.InputMode.Gameplay);
            var targetGo = Own(new GameObject("RunInteractionTestTarget")); targetGo.transform.position = playerGo.transform.position + Vector3.right;
            target = targetGo.AddComponent<Target>(); targetGo.AddComponent<BoxCollider2D>();
            if (EventSystem.current == null) Own(new GameObject("RunInteractionTestEvents", typeof(EventSystem)));
            previousSelection = EventSystem.current.currentSelectedGameObject; previousNavigation = EventSystem.current.sendNavigationEvents;
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.AreSame(playerInput.actions, router.Actions);
            Assert.AreSame(target, interactor.CurrentTarget); Assert.True(prompt.gameObject.activeSelf);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (shop != null && ShopRoomPanel.IsOpen) Field<Button>(shop, "leaveButton").onClick.Invoke();
                if (ownsShop && shop != null) Object.Destroy(shop.gameObject);
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                owned.Clear(); yield return null;
                foreach (var entry in previousOverrides) if (entry.Key != null) entry.Key.LoadBindingOverridesFromJson(entry.Value);
                previousOverrides.Clear();
                if (!ownsRouter && router != null)
                {
                    if (previousRouterActions != null) router.UsePlayerActions(previousRouterActions); else router.DetachPlayerActions();
                    Set(router, "ownedActions", previousOwnedActions); router.SwitchMode(previousRouterMode);
                }
                if (ownsRun && RunManager.HasInstance) Object.Destroy(RunManager.Instance.gameObject);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previousInputSettings; Object.Destroy(testInputSettings);
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
                Assert.AreEqual(sourceState, Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions.SaveBindingOverridesAsJson());
            }
            finally { guard.Release(); GameplayKeyBindings.InvalidateCache(); }
        }

        [UnityTest]
        public IEnumerator DraftBlocksPromptMarkerAndActualInputEvenIfGameplayMapIsIncorrectlyEnabled()
        {
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            GameEvents.RaiseDraftOpened(); yield return null; AssertBlocked();
            Assert.AreEqual(InputRouter.InputMode.UI, router.CurrentMode); Assert.False(router.GameplayMap.enabled);
            // This deliberately bypasses routing to verify the actor blocker, rather than only map disabling.
            router.GameplayMap.Enable(); yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            AssertBlocked();
            GameEvents.RaiseDraftClosed(); yield return null;
            Assert.False(actor.BlocksInteraction); Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(2, target.Calls);
        }

        [UnityTest]
        public IEnumerator AcceptedPauseAndResumeProtectReboundActualInteractAction()
        {
            string id = GameplayKeyBindings.Rows.Single(row => row.ActionName == "Interact").BindingId;
            Assert.True(GameplayKeyBindings.TrySetKey(id, "<Keyboard>/f", out _));
            router.UsePlayerActions(playerInput.actions); router.SwitchMode(InputRouter.InputMode.Gameplay);
            yield return Press(Key.F); Assert.AreEqual(1, target.Calls);
            GameEvents.RaisePauseRequested(); yield return null;
            Assert.True(flow.IsPaused); Assert.AreEqual(0f, Time.timeScale); AssertBlocked();
            router.GameplayMap.Enable(); yield return Press(Key.F); Assert.AreEqual(1, target.Calls);
            GameEvents.RaiseResumeRequested(); yield return null;
            Assert.False(flow.IsPaused); Assert.False(actor.BlocksInteraction); Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            yield return Press(Key.F); Assert.AreEqual(2, target.Calls);
        }

        [UnityTest]
        public IEnumerator HeldInteractAcrossDraftAndDuplicateOpenDoesNotExecuteOnClose()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G)); yield return null;
            Assert.AreEqual(1, target.Calls);
            GameEvents.RaiseDraftOpened(); GameEvents.RaiseDraftOpened(); yield return null; AssertBlocked();
            GameEvents.RaiseDraftClosed(); yield return null; yield return null;
            Assert.AreEqual(1, target.Calls, "Re-enabling the map must not reuse the held press.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return Press(Key.G); Assert.AreEqual(2, target.Calls);
            Assert.True(prompt.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ExplicitActorLockSurvivesModalCloseAndAllBlockerComponentsAreCombined()
        {
            actor.SetInputLocked(true); yield return null; AssertBlocked();
            GameEvents.RaiseDraftOpened(); GameEvents.RaiseDraftClosed(); yield return null;
            Assert.True(actor.IsInputLocked); AssertBlocked();
            yield return Press(Key.G); Assert.AreEqual(0, target.Calls);
            actor.SetInputLocked(false); extra.Blocked = true; yield return null;
            Assert.False(actor.BlocksInteraction); Assert.False(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(0, target.Calls);
            extra.Blocked = false; yield return null; Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
        }

        [UnityTest]
        public IEnumerator LobbyActorIgnoresDetachedRunRouterUiButHonorsItsDirectLock()
        {
            Set(actor, "context", PlayerContext.Lobby);
            router.DetachPlayerActions(); router.SwitchMode(InputRouter.InputMode.UI);
            playerInput.actions.FindActionMap("Player").Enable(); yield return null;
            Assert.False(actor.BlocksInteraction); Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            actor.SetInputLocked(true); yield return null; AssertBlocked();
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            actor.SetInputLocked(false); yield return null;
            yield return Press(Key.G); Assert.AreEqual(2, target.Calls);
        }

        [UnityTest]
        public IEnumerator ActualShopModalOpenRepeatAndLeaveGateInteractionWithoutActorUnlockSideEffects()
        {
            Assert.False(ShopRoomPanel.IsOpen);
            ownsShop = Object.FindAnyObjectByType<ShopRoomPanel>() == null;
            var data = Own(ScriptableObject.CreateInstance<ShopData>());
            data.shopId = "interaction_lock_test"; data.title = "Test shop";
            data.items.Add(new ShopItem { label = "Test item", stock = 1,
                effects = new List<EventEffect> { new() { type = EventEffectType.GoldSpend, amount = 25 } } });
            ShopRoomPanel.Open(data); shop = Object.FindAnyObjectByType<ShopRoomPanel>();
            ShopRoomPanel.Open(data); yield return null;
            Assert.True(ShopRoomPanel.IsOpen); AssertBlocked();
            yield return Press(Key.G); Assert.AreEqual(0, target.Calls);
            Field<Button>(shop, "leaveButton").onClick.Invoke(); yield return null;
            Assert.False(ShopRoomPanel.IsOpen); Assert.False(actor.IsInputLocked); Assert.False(actor.BlocksInteraction);
            Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
        }

        [UnityTest]
        public IEnumerator RunEndAndNewRunFactsSwitchSharedInputOwnershipAndInteractionGate()
        {
            yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            // These are the same trusted facts emitted by RunManager; settlement and user progression are outside this input fixture.
            GameEvents.RaiseRunEnded(); yield return null;
            Assert.AreEqual(InputRouter.InputMode.UI, router.CurrentMode);
            Assert.AreSame(playerInput.actions, router.Actions);
            Assert.False(router.GameplayMap.enabled); Assert.True(router.UIMap.enabled);
            AssertBlocked();
            router.GameplayMap.Enable(); yield return Press(Key.G); Assert.AreEqual(1, target.Calls);
            GameEvents.RaiseRunStarted(); yield return null;
            Assert.AreEqual(InputRouter.InputMode.Gameplay, router.CurrentMode);
            Assert.True(router.GameplayMap.enabled); Assert.False(router.UIMap.enabled);
            Assert.False(actor.BlocksInteraction); Assert.True(prompt.gameObject.activeSelf);
            yield return Press(Key.G); Assert.AreEqual(2, target.Calls);
        }
        private void AssertBlocked()
        {
            Assert.True(actor.BlocksInteraction); Assert.False(prompt.gameObject.activeSelf);
            var marker = Field<SpriteRenderer>(interactor, "marker"); if (marker != null) Assert.False(marker.enabled);
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        public sealed class Target : MonoBehaviour, IInteractable
        {
            public int Calls;
            public string InteractionPrompt => "Use (G)";
            public bool CanInteract => true;
            public void Interact(GameObject interactor) => Calls++;
        }
        public sealed class ExtraBlocker : MonoBehaviour, IInteractionBlocker
        {
            public bool Blocked;
            public bool BlocksInteraction => Blocked;
        }
    }
}