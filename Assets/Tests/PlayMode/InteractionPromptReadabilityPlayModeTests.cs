using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Input;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class InteractionPromptReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private readonly Dictionary<InputActionAsset, string> previousOverrides = new();
        private Canvas canvas;
        private Text label;
        private PlayerInteractor interactor;
        private PromptBlocker blocker;
        private PromptTarget target;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private string sourceState;
        private InputRouter router;
        private InputActionAsset routerActions;
        private InputRouter.InputMode routerMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale;
            Time.timeScale = 1f;
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            router = InputRouter.GetInstanceSafe();
            if (router != null) { routerActions = router.Actions; routerMode = router.CurrentMode; }
            foreach (var player in Object.FindObjectsByType<PlayerInput>(FindObjectsInactive.Include))
                if (player.actions != null && !previousOverrides.ContainsKey(player.actions))
                    previousOverrides.Add(player.actions, player.actions.SaveBindingOverridesAsJson());
            var source = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions;
            sourceState = source.SaveBindingOverridesAsJson();
            GameplayKeyBindings.ReloadFromSettings();
            Assert.True(GameplayKeyBindings.ResetKeyboard(out var error)); Assert.AreEqual(KeyBindingError.None, error);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            var canvasGo = Own(new GameObject("InteractionPromptTestCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
            var labelGo = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(canvas.transform, false);
            label = labelGo.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter; label.text = "";
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(700f, 48f);
            var playerGo = Own(new GameObject("InteractionPromptTestPlayer")); playerGo.SetActive(false);
            playerGo.transform.position = new Vector3(10000f, 10000f, 0f);
            var body = playerGo.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var trigger = playerGo.AddComponent<CircleCollider2D>(); trigger.radius = 2f; trigger.isTrigger = true;
            blocker = playerGo.AddComponent<PromptBlocker>();
            interactor = playerGo.AddComponent<PlayerInteractor>(); Set(interactor, "promptLabel", label);
            playerGo.SetActive(true);
            target = NewTarget(1f);
            yield return EnterPhysics();
            Assert.AreSame(target, interactor.CurrentTarget, "Real trigger contact must register the candidate.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                owned.Clear();
                yield return null;
                foreach (var entry in previousOverrides)
                    if (entry.Key != null) entry.Key.LoadBindingOverridesFromJson(entry.Value);
                previousOverrides.Clear();
                if (router != null)
                {
                    if (routerActions != null) router.UsePlayerActions(routerActions); else router.DetachPlayerActions();
                    router.SwitchMode(routerMode);
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previousInputSettings; Object.Destroy(testInputSettings);
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
                Assert.AreEqual(sourceState, Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions.SaveBindingOverridesAsJson());
            }
            finally { guard.Release(); GameplayKeyBindings.InvalidateCache(); }
        }

        [UnityTest]
        public IEnumerator LongPromptFitsAllScalesLanguagesAndNarrowVirtualCanvasThenRestores()
        {
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                target.Prompt = lang switch
                {
                    LocalizationLanguage.English => "Read the entire lengthy interaction instruction before entering this mysterious destination (G) [G]",
                    LocalizationLanguage.Japanese => "この不思議な目的地に入る前に長い操作の説明を最後まで読んでください (G) [G]",
                    _ => "이 신비로운 목적지에 들어가기 전에 길어진 상호작용 안내 문구를 끝까지 읽어 주세요 (G) [G]"
                };
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); yield return Settle();
                    AssertLabelFits(scale);
                    Assert.AreEqual(target.Prompt, label.text, "Default G substitutions preserve the complete prompt.");
                    Assert.AreEqual(0, target.Interactions);
                }
            }
            canvas.scaleFactor = 0.5f; yield return Settle(); AssertLabelFits(1.5f);
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(260f, 1080f);
            yield return Settle(); AssertLabelFits(1.5f);
            Assert.LessOrEqual(label.rectTransform.rect.width, 208.1f, "Narrow canvas keeps the configured 10-percent margin on each side.");
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f); canvas.scaleFactor = 1f;
            target.Prompt = "Use (G)";
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Assert.AreEqual(700f, label.rectTransform.rect.width, 0.01f);
            Assert.AreEqual(48f, label.rectTransform.rect.height, 0.01f);
            Assert.AreEqual(120f, label.rectTransform.anchoredPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator ActualTriggerExitNearestCanInteractAndDestroyedTargetsControlPrompt()
        {
            var nearer = NewTarget(0.5f); yield return EnterPhysics();
            Assert.AreSame(nearer, interactor.CurrentTarget);
            nearer.Allowed = false; yield return null;
            Assert.AreSame(target, interactor.CurrentTarget);
            target.Allowed = false; yield return null;
            Assert.IsNull(interactor.CurrentTarget); Assert.False(label.gameObject.activeSelf);
            target.Allowed = true; yield return null; Assert.True(label.gameObject.activeSelf);
            target.transform.position += Vector3.right * 20f; yield return EnterPhysics();
            Assert.IsNull(interactor.CurrentTarget); Assert.False(label.gameObject.activeSelf);
            nearer.Allowed = true; yield return null; Assert.True(label.gameObject.activeSelf);
            Object.Destroy(nearer.gameObject); yield return null; yield return null;
            Assert.IsNull(interactor.CurrentTarget); Assert.False(label.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator BlockedAndDisabledInteractorHidePromptAndMarkerThenRecover()
        {
            Assert.True(label.gameObject.activeSelf);
            blocker.Blocked = true; yield return null;
            Assert.False(label.gameObject.activeSelf); AssertMarkerHidden();
            blocker.Blocked = false; yield return null; Assert.True(label.gameObject.activeSelf);
            interactor.enabled = false;
            Assert.False(label.gameObject.activeSelf); Assert.IsNull(interactor.CurrentTarget); AssertMarkerHidden();
            interactor.enabled = true; yield return null;
            Assert.AreSame(target, interactor.CurrentTarget); Assert.True(label.gameObject.activeSelf);
            Assert.AreEqual(0, target.Interactions);
        }

        [UnityTest]
        public IEnumerator ActualPlayerInputUsesReboundInteractKeyAndHonorsBlocker()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            var clone = Own(Object.Instantiate(Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions));
            var playerInput = interactor.gameObject.AddComponent<PlayerInput>();
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            playerInput.actions = clone; playerInput.defaultActionMap = "Player";
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            playerInput.SwitchCurrentActionMap("Player");
            foreach (var action in playerInput.currentActionMap) if (action.name != "Interact") action.Disable();
            string id = GameplayKeyBindings.Rows.Single(row => row.ActionName == "Interact").BindingId;
            Assert.True(GameplayKeyBindings.TrySetKey(id, "<Keyboard>/f", out var error)); Assert.AreEqual(KeyBindingError.None, error);
            GameplayKeyBindings.ApplyTo(playerInput.actions);
            target.Prompt = "Enter (G) [G]"; yield return Settle();
            Assert.AreEqual("Enter (F) [F]", label.text);
            yield return Press(Key.G); Assert.AreEqual(0, target.Interactions);
            yield return Press(Key.F); Assert.AreEqual(1, target.Interactions);
            Assert.AreSame(interactor.gameObject, target.LastInteractor);
            blocker.Blocked = true; yield return null;
            yield return Press(Key.F); Assert.AreEqual(1, target.Interactions); Assert.False(label.gameObject.activeSelf);
            blocker.Blocked = false; target.Allowed = false; yield return null;
            yield return Press(Key.F); Assert.AreEqual(1, target.Interactions);
            target.Allowed = true; yield return null;
            yield return Press(Key.F); Assert.AreEqual(2, target.Interactions);
        }

        [UnityTest]
        public IEnumerator SameLabelReattachmentAndInteractorReroutingKeepOriginalScaleBaseline()
        {
            target.Prompt = "Use (G)";
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Assert.AreEqual(36, label.fontSize);
            var firstHelper = InteractionPromptReadability.Attach(label);
            Assert.AreSame(firstHelper, InteractionPromptReadability.Attach(label));
            var otherGo = new GameObject("OtherPrompt", typeof(RectTransform), typeof(Text));
            otherGo.transform.SetParent(canvas.transform, false);
            var other = otherGo.GetComponent<Text>(); other.font = label.font; other.fontSize = 24;
            other.rectTransform.anchorMin = other.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            other.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            other.rectTransform.sizeDelta = new Vector2(700f, 48f);
            Set(interactor, "promptLabel", other); yield return Settle();
            Assert.AreEqual(36, other.fontSize);
            Set(interactor, "promptLabel", label); yield return Settle();
            Assert.AreEqual(36, label.fontSize, "Returning to an already enlarged label must not capture 36 as its baseline.");
            Assert.AreSame(firstHelper, InteractionPromptReadability.Attach(label));
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Assert.AreEqual(24, label.fontSize);
            Assert.AreEqual(700f, label.rectTransform.rect.width, 0.01f);
            Assert.AreEqual(48f, label.rectTransform.rect.height, 0.01f);
            Assert.AreEqual(0, target.Interactions);
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private static IEnumerator EnterPhysics()
        {
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
        }
        private static IEnumerator Settle()
        {
            yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
        }
        private PromptTarget NewTarget(float distance)
        {
            var go = Own(new GameObject("InteractionPromptTestTarget"));
            go.transform.position = interactor.transform.position + Vector3.right * distance;
            var target = go.AddComponent<PromptTarget>(); target.Prompt = "Use (G)";
            go.AddComponent<BoxCollider2D>(); return target;
        }
        private void AssertLabelFits(float scale)
        {
            Assert.True(label.gameObject.activeSelf); Assert.AreEqual(Mathf.RoundToInt(24f * scale), label.fontSize);
            Assert.False(label.resizeTextForBestFit);
            Assert.GreaterOrEqual(label.rectTransform.rect.height + 1f, label.preferredHeight);
            var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
            Rect bounds = ((RectTransform)canvas.transform).rect;
            foreach (var corner in corners)
            {
                Vector3 local = canvas.transform.InverseTransformPoint(corner);
                Assert.GreaterOrEqual(local.x, bounds.xMin + 3f); Assert.LessOrEqual(local.x, bounds.xMax - 3f);
                Assert.GreaterOrEqual(local.y, bounds.yMin + 3f); Assert.LessOrEqual(local.y, bounds.yMax - 3f);
            }
        }
        private void AssertMarkerHidden()
        {
            var marker = Field<SpriteRenderer>(interactor, "marker"); if (marker != null) Assert.False(marker.enabled);
        }
        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        public sealed class PromptTarget : MonoBehaviour, IInteractable
        {
            public string Prompt;
            public bool Allowed = true;
            public int Interactions;
            public GameObject LastInteractor;
            public string InteractionPrompt => Prompt;
            public bool CanInteract => Allowed;
            public void Interact(GameObject interactor) { Interactions++; LastInteractor = interactor; }
        }
        public sealed class PromptBlocker : MonoBehaviour, IInteractionBlocker
        {
            public bool Blocked;
            public bool BlocksInteraction => Blocked;
        }
    }
}