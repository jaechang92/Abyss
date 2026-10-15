using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using Abyss.Runtime.Events;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Abyss.Tests.PlayMode
{
    public sealed class StageFallReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private StageFallPanel panel, previousPanel;
        private bool previousPanelActive, oldNavigation, oldScalerEnabled;
        private int oldDismissed, completed, opened, closed;
        private GameObject ownedEvents, oldSelection;
        private float oldTime, oldCanvasScale;
        private LocalizationLanguage oldLanguage;
        private Random.State oldRandom;
        private Canvas canvas;
        private Texture2D texture;
        private Sprite sprite;
        private Keyboard keyboard;
        private Gamepad pad;
        private InputSettings oldInput, input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); oldTime = Time.timeScale; oldLanguage = Loc.CurrentLanguage; oldRandom = Random.state;
            Assert.False(StageFallPanel.IsOpen, "Do not replace an existing playing transition.");
            previousPanel = (StageFallPanel)InstanceField.GetValue(null);
            if (previousPanel != null) { previousPanelActive = previousPanel.gameObject.activeSelf; previousPanel.gameObject.SetActive(false); }
            InstanceField.SetValue(null, null);
            oldDismissed = (int)typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            SetDismissed(-1);
            oldInput = InputSystem.settings; input = Object.Instantiate(oldInput);
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = input; keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("FallReadingEvents", typeof(EventSystem));
            oldSelection = EventSystem.current.currentSelectedGameObject; oldNavigation = EventSystem.current.sendNavigationEvents;
            texture = new Texture2D(4, 8); sprite = Sprite.Create(texture, new Rect(0, 0, 4, 8), Vector2.one * 0.5f);
            completed = opened = closed = 0;
            GameEvents.OnDraftOpened += OnOpened; GameEvents.OnDraftClosed += OnClosed;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (panel != null) { StageFallPanel.Close(); Object.Destroy(panel.gameObject); }
                GameEvents.OnDraftOpened -= OnOpened; GameEvents.OnDraftClosed -= OnClosed;
                if (sprite != null) Object.Destroy(sprite); if (texture != null) Object.Destroy(texture);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (pad != null) InputSystem.RemoveDevice(pad);
                InputSystem.settings = oldInput; if (input != null) Object.Destroy(input);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = oldNavigation;
                    EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                Loc.SetLanguage(oldLanguage); Time.timeScale = oldTime; Random.state = oldRandom; SetDismissed(oldDismissed);
                yield return null;
                InstanceField.SetValue(null, previousPanel);
                if (previousPanel != null) previousPanel.gameObject.SetActive(previousPanelActive);
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator ActualPlayKeepsFullStageNumberAndLongNameAtEveryScaleAndLanguage()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (var language in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
                {
                    Loc.SetLanguage(language); string name = "잊힌 왕국의 가장 깊은 층 Forgotten kingdom 深淵の王国";
                    Start(int.MaxValue, name); yield return Settle(); CaptureCanvas(); AssertLayout(scale);
                    Assert.AreEqual(Loc.GetFormat(StringKey.Run_StageNumberFormat, int.MaxValue), Stage.text);
                    Assert.AreEqual(name, Name.text); Assert.True(StageFallPanel.IsOpen);
                    StageFallPanel.Close(); Assert.AreEqual(opened, completed); Assert.AreEqual(opened, closed);
                }
            }
        }

        [UnityTest]
        public IEnumerator NarrowResizeAndMenuChangesReadFullTextAndShortReplayRestoresBaseline()
        {
            Start(12345, "Short"); yield return Settle(); CaptureCanvas(); Vector2 baseline = TitleRect.rect.size;
            Vector2 stageSize = Stage.rectTransform.rect.size, nameSize = Name.rectTransform.rect.size;
            StageFallPanel.Close();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Start(int.MaxValue, "어둠 Deep realm 闇");
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Max(Screen.width / 640f, Screen.height / 520f);
            yield return Settle(); AssertLayout(1.5f);
            canvas.scaleFactor *= 1.08f; yield return Settle(); AssertLayout(1.5f);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.25f)); yield return Settle(); AssertLayout(1.25f);
            StageFallPanel.Close();
            canvas.GetComponent<CanvasScaler>().enabled = oldScalerEnabled; canvas.scaleFactor = oldCanvasScale;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1)); Start(12345, "Short"); yield return Settle();
            AssertVector(baseline, TitleRect.rect.size); AssertVector(stageSize, Stage.rectTransform.rect.size); AssertVector(nameSize, Name.rectTransform.rect.size);
            AssertLayout(1); Assert.AreEqual("Short", Name.text);
        }

        [UnityTest]
        public IEnumerator ShortCanvasFitsFullTextAndReportsUnavoidableDecorativePlayerOverlap()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Start(12345, "Depth 深層"); yield return Settle(); CaptureCanvas();
            var player = Field<RectTransform>("playerRect"); Vector2 originalSize = player.rect.size; Vector3 originalScale = player.localScale;
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Max(Screen.width / 640f, Screen.height / 360f);
            yield return Settle(); AssertLayout(1.5f);
            var helper = panel.GetComponentInChildren<StageFallReadability>(true);
            Assert.NotNull(helper); Assert.True(helper.IsConstrained, "Text fits, but the unchanged player envelope leaves insufficient room below it.");
            AssertVector(originalSize, player.rect.size); Assert.AreEqual(originalScale, player.localScale);
            Assert.AreEqual("Depth 深層", Name.text); Assert.Zero(completed);
            int warnings = 0;
            Application.LogCallback countWarning = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.Contains("[StageFallReadability]")) warnings++;
            };
            Application.logMessageReceived += countWarning;
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("\\[StageFallReadability\\] 스테이지 이름 묶음\\(.*px\\).*화면 안전 높이"));
                Name.text = string.Join("\n", Enumerable.Repeat("아주 긴 층 이름 Extremely long stage name とても長い階層名", 24));
                helper.Apply(); yield return Settle();
                AssertText(Name, 78); Assert.True(helper.IsConstrained);
                Assert.Greater(TitleRect.rect.height, (panel.transform.Find("Body") as RectTransform).rect.height);
                Assert.AreEqual(1, warnings);
                helper.Apply(); helper.RefreshIfNeeded(); yield return Settle(); Assert.AreEqual(1, warnings);
                // Even impossible geometry keeps all text and retains the original completion deadline.
                SetElapsed(2.59f); Call("Update"); Assert.Zero(completed);
                SetElapsed(2.61f); Call("Update"); Assert.AreEqual(1, completed); Assert.True(StageFallPanel.IsOpen);
            }
            finally { Application.logMessageReceived -= countWarning; }
        }
        [UnityTest]
        public IEnumerator ReadabilityPreservesActualPlayerSpriteFlipAndTwentyTwoStreaks()
        {
            Start(2, "Long stage name 긴 스테이지 이름 長い階層名", true); yield return Settle(); CaptureCanvas();
            var image = Field<Image>("playerImage"); var player = Field<RectTransform>("playerRect");
            var streaks = Field<List<RectTransform>>("streaks"); var speeds = Field<List<float>>("streakSpeeds");
            Assert.AreEqual(22, streaks.Count); Assert.AreEqual(22, speeds.Count);
            var sizes = new List<Vector2>(); var positions = new List<Vector2>(); var colors = new List<Color>();
            foreach (var streak in streaks) { sizes.Add(streak.rect.size); positions.Add(streak.anchoredPosition); colors.Add(streak.GetComponent<Image>().color); }
            var speedSnapshot = speeds.ToArray(); Vector2 playerSize = player.rect.size; Vector3 flip = player.localScale;
            Assert.AreSame(sprite, image.sprite); Assert.True(image.enabled); Assert.True(image.preserveAspect); Assert.False(image.raycastTarget); Assert.AreEqual(-1f, flip.x);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle(); AssertLayout(1.5f);
            Assert.AreSame(sprite, image.sprite); AssertVector(playerSize, player.rect.size); Assert.AreEqual(flip, player.localScale);
            CollectionAssert.AreEqual(speedSnapshot, speeds);
            for (int i = 0; i < streaks.Count; i++)
            {
                AssertVector(sizes[i], streaks[i].rect.size); AssertVector(positions[i], streaks[i].anchoredPosition);
                Assert.AreEqual(colors[i], streaks[i].GetComponent<Image>().color); Assert.False(streaks[i].GetComponent<Image>().raycastTarget);
            }
        }

        [UnityTest]
        public IEnumerator ActualAnimateKeepsOriginalHalfSecondDelayAndPointSixFade()
        {
            Start(2, "Fade"); yield return Settle();
            foreach (var sample in new[] { (0f, 0f), (0.5f, 0f), (0.8f, 0.5f), (1.1f, 1f) })
            {
                Call("Animate", sample.Item1); Assert.AreEqual(sample.Item2, Field<CanvasGroup>("titleGroup").alpha, 0.001f);
            }
            Assert.Zero(completed); Assert.True(StageFallPanel.IsOpen); Assert.False(Field<CanvasGroup>("titleGroup").blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator DurationReportsOnceKeepsBodyAndAnimationUntilExplicitClose()
        {
            Start(2, "Duration"); yield return Settle(); Time.timeScale = 0;
            SetElapsed(2.59f); Call("Update"); Assert.Zero(completed);
            SetElapsed(2.61f); Call("Update"); Assert.AreEqual(1, completed); Assert.True(StageFallPanel.IsOpen); Assert.True(Field<bool>("isPlaying"));
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Call("Update");
            AssertText(Stage, 33); AssertText(Name, 78);
            var player = Field<RectTransform>("playerRect"); SetElapsed(3.2f); Call("Update"); Vector2 before = player.anchoredPosition;
            SetElapsed(3.7f); Call("Update"); Assert.AreNotEqual(before, player.anchoredPosition);
            Call("Update"); Assert.AreEqual(1, completed); Assert.Zero(closed);
            StageFallPanel.Close(); StageFallPanel.Close(); Assert.AreEqual(1, completed); Assert.AreEqual(1, closed); Assert.False(StageFallPanel.IsOpen);
        }

        [UnityTest]
        public IEnumerator EarlyCloseReleasesSingleAwaiterAndPlayingReplayKeepsOriginalSource()
        {
            Start(7, "Original", true); yield return Settle(); object completionSource = Field<AwaitableCompletionSource>("finished");
            // Request the current play again, but never register a second waiter on its pooled Awaitable.
            _ = StageFallPanel.PlayAsync(99, "Must not replace", null, false);
            Assert.AreEqual("Original", Name.text); Assert.AreSame(completionSource, Field<AwaitableCompletionSource>("finished"));
            Assert.AreSame(sprite, Field<Image>("playerImage").sprite); Assert.AreEqual(-1, Field<RectTransform>("playerRect").localScale.x);
            Assert.AreEqual(1, opened); Assert.Zero(completed);
            StageFallPanel.Close(); Assert.AreEqual(1, completed); Assert.AreEqual(1, closed);
            Start(8, "Replay"); yield return Settle(); Assert.AreEqual("Replay", Name.text); Assert.AreEqual(2, opened); Assert.AreEqual(1, completed);
            StageFallPanel.Close(); Assert.AreEqual(2, completed); Assert.AreEqual(2, closed);
        }

        [UnityTest]
        public IEnumerator SpaceGraceRejectsBeforePointThreeAndAcceptsAfterwards()
        {
            Start(1, "Grace"); yield return Settle();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null;
            SetElapsed(0.29f); Call("Update"); Assert.Zero(completed);
            SetElapsed(0.31f); Call("Update"); Assert.AreEqual(1, completed); Assert.True(StageFallPanel.IsOpen);
        }

        [UnityTest]
        public IEnumerator AllSkipInputsRespectSaveCaptureAndUnscaledUpdateRunsWhilePaused()
        {
            foreach (var key in new[] { Key.Space, Key.Enter, Key.Z, Key.None })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                int before = completed; Start(2, "Skip"); yield return Settle();
                if (key == Key.None) InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                else InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null; SetElapsed(0.4f); SetDismissed(Time.frameCount); Call("Update"); Assert.AreEqual(before, completed);
                SetDismissed(-1); Call("Update"); Assert.AreEqual(before + 1, completed); StageFallPanel.Close();
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            Start(3, "Unscaled"); yield return Settle(); Time.timeScale = 0; SetElapsed(0.7f);
            var player = Field<RectTransform>("playerRect"); Vector2 position = player.anchoredPosition;
            panel.enabled = true; yield return new WaitForSecondsRealtime(0.05f); panel.enabled = false;
            Assert.AreNotEqual(position, player.anchoredPosition); Assert.Greater(Field<CanvasGroup>("titleGroup").alpha, 0);
            Assert.AreEqual(0, Time.timeScale); Assert.AreEqual(4, completed); Assert.True(StageFallPanel.IsOpen);
        }

        private void Start(int number, string name, bool flip = false)
        {
            var pending = StageFallPanel.PlayAsync(number, name, sprite, flip);
            panel = Object.FindAnyObjectByType<StageFallPanel>(); panel.enabled = false;
            var awaiter = pending.GetAwaiter();
            if (awaiter.IsCompleted) { awaiter.GetResult(); completed++; }
            else awaiter.OnCompleted(() => { awaiter.GetResult(); completed++; });
        }
        private void CaptureCanvas()
        {
            if (canvas != null) return;
            canvas = panel.GetComponent<Canvas>(); oldCanvasScale = canvas.scaleFactor; oldScalerEnabled = canvas.GetComponent<CanvasScaler>().enabled;
        }
        private Text Stage => Field<Text>("stageLabel");
        private Text Name => Field<Text>("titleLabel");
        private RectTransform TitleRect => Field<CanvasGroup>("titleGroup").transform as RectTransform;
        private void AssertLayout(float scale)
        {
            AssertText(Stage, Mathf.RoundToInt(22 * scale)); AssertText(Name, Mathf.RoundToInt(52 * scale));
            Inside(Stage.rectTransform, TitleRect); Inside(Name.rectTransform, TitleRect);
            Inside(TitleRect, panel.transform.Find("Body") as RectTransform);
            Assert.LessOrEqual(Bounds(Name.rectTransform, TitleRect).yMax, Bounds(Stage.rectTransform, TitleRect).yMin + 1);
            Assert.False(Stage.raycastTarget); Assert.False(Name.raycastTarget);
        }
        private static void AssertText(Text text, int size)
        {
            Assert.AreEqual(size, text.fontSize); Assert.False(text.resizeTextForBestFit);
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1, text.name);
        }
        private void SetElapsed(float elapsed) => Set(panel, "startedAt", Time.unscaledTime - elapsed);
        private static FieldInfo InstanceField => typeof(RunModalPanel<StageFallPanel>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static void SetDismissed(int frame) => typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, frame);
        private T Field<T>(string name) => (T)typeof(StageFallPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private void Call(string name, params object[] args) => typeof(StageFallPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, args);
        private void OnOpened() => opened++;
        private void OnClosed() => closed++;
        private static void AssertVector(Vector2 expected, Vector2 actual) { Assert.AreEqual(expected.x, actual.x, 0.01f); Assert.AreEqual(expected.y, actual.y, 0.01f); }
        private static Rect Bounds(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Inside(RectTransform child, RectTransform parent)
        {
            var actual = Bounds(child, parent); Assert.GreaterOrEqual(actual.xMin, parent.rect.xMin - 1); Assert.LessOrEqual(actual.xMax, parent.rect.xMax + 1);
            Assert.GreaterOrEqual(actual.yMin, parent.rect.yMin - 1); Assert.LessOrEqual(actual.yMax, parent.rect.yMax + 1);
        }
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
    }
}
