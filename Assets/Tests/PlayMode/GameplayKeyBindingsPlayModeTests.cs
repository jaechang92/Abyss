using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Input;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>실제 입력 사본·라우터·메타 저장을 임시 세이브와 합성 키보드로 검증한다.</summary>
    public sealed class GameplayKeyBindingsPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new PlayModeSaveGuard();
        private Keyboard keyboard;
        private Mouse mouse;
        private GameObject playerObject;
        private GameObject flowObject;
        private InputRouter router;
        private bool ownsRouter;
        private InputActionAsset previousRouterActions;
        private InputRouter.InputMode previousMode;
        private Scene previousScene;
        private Scene testScene;
        private string sourceState;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            GameplayKeyBindings.ReloadFromSettings();
            sourceState = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions.SaveBindingOverridesAsJson();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            previousScene = SceneManager.GetActiveScene();
            testScene = SceneManager.CreateScene("KeyBindingsTestScene");
            SceneManager.SetActiveScene(testScene);
            router = InputRouter.GetInstanceSafe();
            ownsRouter = router == null;
            if (ownsRouter)
            {
                LogAssert.Expect(LogType.Warning, new Regex("InputActionAsset 미할당"));
                router = new GameObject("TestInputRouter").AddComponent<InputRouter>();
            }
            else
            {
                previousRouterActions = router.Actions;
                previousMode = router.CurrentMode;
            }
            flowObject = new GameObject("TestGameFlow");
            flowObject.AddComponent<GameFlowController>();
            playerObject = new GameObject("TestPlayerInput");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<PlayerInput>();
            player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            player.actions = Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions;
            player.defaultActionMap = "Player";
            player.defaultControlScheme = "Keyboard&Mouse";
            playerObject.SetActive(true);
            player.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            GameplayKeyBindings.ReloadFromSettings();
            router.SwitchMode(InputRouter.InputMode.Gameplay);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.SetFileOperations(null);
                if (playerObject != null) Object.Destroy(playerObject);
                if (flowObject != null) Object.Destroy(flowObject);
                if (ownsRouter && router != null) Object.Destroy(router.gameObject);
                else if (router != null)
                {
                    if (previousRouterActions != null) router.UsePlayerActions(previousRouterActions);
                    else router.DetachPlayerActions();
                    router.SwitchMode(previousMode);
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                Time.timeScale = 1;
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                yield return null;
                if (testScene.IsValid() && testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(testScene);
                Assert.AreEqual(sourceState, Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions.SaveBindingOverridesAsJson());
            }
            finally
            {
                try
                {
                    if (guard.IsAcquired)
                    {
                        MetaSaveService.Instance.Current.settings.keyboardBindingOverrides = string.Empty;
                        GameplayKeyBindings.ReloadFromSettings();
                    }
                }
                finally
                {
                    guard.Release();
                    GameplayKeyBindings.InvalidateCache();
                    if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
                    if (testInputSettings != null) Object.Destroy(testInputSettings);
                }
            }
        }

        private static string JumpId => GameplayKeyBindings.Rows.Single(row => row.ActionName == "Jump").BindingId;

        [UnityTest]
        public IEnumerator RemappedKeyReachesActualPlayerAndRouterOwnsSameMaps()
        {
            var player = playerObject.GetComponent<PlayerInput>();
            Assert.AreNotSame(Resources.Load<GameplayKeyBindingsConfig>("GameplayKeyBindings").SourceActions, player.actions);
            Assert.AreSame(player.actions, router.Actions, "Persistent router must own the actual player copy");
            Assert.True(GameplayKeyBindings.TrySetKey(JumpId, "<Keyboard>/space", out var error));
            Assert.AreEqual(KeyBindingError.None, error);
            var jump = player.actions.FindAction("Player/Jump");
            int performed = 0;
            jump.performed += _ => performed++;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
            yield return null;
            Assert.AreEqual(0, performed, "Old key must stop triggering Jump");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            Assert.AreEqual(1, performed);
            router.SwitchMode(InputRouter.InputMode.UI);
            Assert.False(player.actions.FindActionMap("Player").enabled);
            Assert.True(player.actions.FindActionMap("UI").enabled);
            router.SwitchMode(InputRouter.InputMode.Gameplay);
            Assert.True(GameplayKeyBindings.ResetKeyboard(out error));
            Assert.AreEqual("<Keyboard>/c", jump.bindings.First(binding => binding.id.ToString() == JumpId).effectivePath);
        }

        private sealed class FailingWriteOperations : SaveFileOperations
        {
            public override void WriteAllText(string path, string contents) => throw new IOException("keyboard binding test failure");
        }

        [UnityTest]
        public IEnumerator FailedSaveRollsBackMetadataRuntimeAndFileThenReloadRestores()
        {
            Assert.True(GameplayKeyBindings.TrySetKey(JumpId, "<Keyboard>/space", out _));
            var meta = MetaSaveService.Instance.Current;
            string before = meta.settings.keyboardBindingOverrides;
            string timestamp = meta.saveTimeUtc;
            string disk = File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName));
            string display = GameplayKeyBindings.GetDisplayKey("Jump");
            SaveSystem.Instance.SetFileOperations(new FailingWriteOperations());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: keyboard binding test failure"));
            Assert.False(GameplayKeyBindings.TrySetKey(JumpId, "<Keyboard>/j", out var error));
            Assert.AreEqual(KeyBindingError.SaveFailed, error);
            Assert.AreEqual(before, meta.settings.keyboardBindingOverrides);
            Assert.AreEqual(timestamp, meta.saveTimeUtc);
            Assert.AreEqual(display, GameplayKeyBindings.GetDisplayKey("Jump"));
            Assert.AreEqual(disk, File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName)));
            SaveSystem.Instance.SetFileOperations(null);
            MetaSaveService.Instance.UnloadWithoutSaving();
            GameplayKeyBindings.ReloadFromSettings();
            Assert.AreEqual(before, MetaSaveService.Instance.Current.settings.keyboardBindingOverrides);
            Assert.AreEqual(display, GameplayKeyBindings.GetDisplayKey("Jump"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyDetachesPersistentRunRouterAndKeepsPlayerDefaultMap()
        {
            var player = playerObject.GetComponent<PlayerInput>();
            router.SwitchMode(InputRouter.InputMode.UI);
            Object.Destroy(flowObject);
            flowObject = null;
            yield return null;
            player.actions.FindActionMap("UI").Disable();
            player.actions.FindActionMap("Player").Enable();
            GameplayKeyBindings.ReloadFromSettings();
            Assert.IsNull(router.Actions, "No Run flow: lobby player map is not controlled by persistent router");
            Assert.True(player.actions.FindActionMap("Player").enabled);
            Assert.False(player.actions.FindActionMap("UI").enabled);
            yield return null;
        }
    }
}
