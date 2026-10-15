using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
    public sealed class BossIntroReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private BossIntroPanel panel, previousPanel;
        private bool previousActive, oldNavigation, scalerEnabled;
        private int oldDismissed, finished, opened, closed;
        private float oldTime, canvasScale;
        private float completedAt;
        private bool wasOpenInCallback;
        private LocalizationLanguage oldLanguage;
        private Random.State oldRandom;
        private Canvas canvas;
        private GameObject ownedEvents, oldSelection;
        private Keyboard keyboard;
        private Gamepad pad;
        private InputSettings oldInput, input;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); oldTime = Time.timeScale; oldLanguage = Loc.CurrentLanguage; oldRandom = Random.state;
            Assert.False(BossIntroPanel.IsOpen, "Do not replace an existing boss introduction.");
            previousPanel = (BossIntroPanel)InstanceField.GetValue(null);
            if (previousPanel != null) { previousActive = previousPanel.gameObject.activeSelf; previousPanel.gameObject.SetActive(false); }
            InstanceField.SetValue(null, null);
            oldDismissed = (int)typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            SetDismissed(-1); oldInput = InputSystem.settings; input = Object.Instantiate(oldInput);
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = input; keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("BossIntroReadingEvents", typeof(EventSystem));
            oldSelection = EventSystem.current.currentSelectedGameObject; oldNavigation = EventSystem.current.sendNavigationEvents;
            finished = opened = closed = 0; completedAt = -1; wasOpenInCallback = true;
            GameEvents.OnDraftOpened += OnOpened; GameEvents.OnDraftClosed += OnClosed;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (panel != null)
                {
                    if (BossIntroPanel.IsOpen) typeof(RunModalPanel<BossIntroPanel>).GetMethod("HideBody", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
                    Object.Destroy(panel.gameObject);
                }
                yield return null; yield return null; // Let destroyCancellationToken unwind the actual RunAsync.
                GameEvents.OnDraftOpened -= OnOpened; GameEvents.OnDraftClosed -= OnClosed;
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (pad != null) InputSystem.RemoveDevice(pad);
                InputSystem.settings = oldInput; if (input != null) Object.Destroy(input);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = oldNavigation;
                    EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                Loc.SetLanguage(oldLanguage); Time.timeScale = oldTime; Random.state = oldRandom; SetDismissed(oldDismissed);
                InstanceField.SetValue(null, previousPanel);
                if (previousPanel != null) previousPanel.gameObject.SetActive(previousActive);
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator ActualPlayShowsFullTitleEpithetAndHintAcrossScalesAndLanguages()
        {
            Play("깊은 왕국의 마지막 수호자 Last keeper 深淵の最後の守護者", "잊힌 맹약 Forgotten oath 忘れられた誓い");
            yield return Settle(); CaptureCanvas();
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (var language in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
                {
                    Loc.SetLanguage(language); yield return Settle(); AssertLayout(scale);
                    Assert.AreEqual("깊은 왕국의 마지막 수호자 Last keeper 深淵の最後の守護者", Title.text);
                    Assert.AreEqual(Loc.Get(StringKey.Dialogue_NextHint), Hint.text);
                    Assert.Zero(finished); Assert.AreEqual(1, opened);
                }
            }
        }

        [UnityTest]
        public IEnumerator RealLineChangesKeepNarrativeScaleAndContrastIndependentOfMenuScale()
        {
            string first = "이 문장은 별도의 서사 크기를 따릅니다. Narrative text owns its scale. 物語の字幕は独立した倍率です。";
            string second = "다음 대사 Next line 次の台詞";
            Play("Keeper", "Oath", new[] { first, second }); yield return Pulse(Key.Space); yield return WaitForLine(first);
            CaptureCanvas(); canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Max(Screen.width / 1100f, Screen.height / 800f);
            var narrative = Line.GetComponent<NarrativeReadability>(); var background = Field<Image>(narrative, "background");
            Color lineColor = Line.color; lineColor.a = 0.42f; Line.color = lineColor;
            foreach (var setting in new[] { (1.5f, 1.25f, true), (1f, 1.5f, false), (1.25f, 1.25f, true) })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(setting.Item1));
                Assert.True(MetaSaveService.Instance.TrySaveReadability(setting.Item2, setting.Item3));
                yield return Settle(); AssertLayout(setting.Item1);
                Assert.AreEqual(Mathf.RoundToInt(30 * setting.Item2), Line.fontSize); Assert.AreEqual(first, Line.text);
                Assert.AreEqual(setting.Item2, NarrativeReadability.Scale); Assert.AreEqual(setting.Item3, background.gameObject.activeSelf);
                Assert.LessOrEqual(Line.preferredHeight, Line.rectTransform.rect.height + 1);
                if (setting.Item3) { Assert.AreEqual(1f, Line.color.r); Assert.AreEqual(1f, Line.color.g); Assert.AreEqual(1f, Line.color.b); }
                Assert.AreEqual(0.42f, Line.color.a, 0.001f);
                Assert.False(background.raycastTarget); Assert.AreSame(Line.transform.parent, background.transform.parent);
                Inside(background.rectTransform, Body);
                if (setting.Item3)
                {
                    Assert.GreaterOrEqual(Bounds(background.rectTransform).xMin, Body.rect.xMin + 23);
                    Assert.LessOrEqual(Bounds(background.rectTransform).xMax, Body.rect.xMax - 23);
                    Assert.LessOrEqual(Bounds(background.rectTransform).yMax, Bounds(Divider).yMin + 1);
                    Assert.LessOrEqual(Bounds(Hint.rectTransform).yMax, Bounds(background.rectTransform).yMin + 1);
                }
            }
            yield return Pulse(Key.Enter); yield return WaitForLine(second); yield return Settle();
            Assert.AreEqual(38, Line.fontSize); AssertLayout(1.25f);
            yield return Pulse(Key.Z); yield return WaitForFinished();
            Assert.AreEqual(1, finished); Assert.False(wasOpenInCallback); Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator InsufficientVerticalRoomHidesSourcePortraitAndRestoresFittedArtWhenWidened()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Play("Boss", "Keeper", enemyId: "boss_abyss_keeper"); yield return Settle(); CaptureCanvas();
            var image = Body.Find("ArtBossPortrait").GetComponent<Image>(); Assert.True(image.enabled); Assert.NotNull(image.sprite);
            var sprite = image.sprite; Vector2 size = image.rectTransform.rect.size; Color color = image.color; bool raycast = image.raycastTarget;
            canvas.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = Mathf.Max(Screen.width / 800f, Screen.height / 500f);
            yield return Settle(); AssertLayout(1.5f);
            Assert.LessOrEqual(Body.rect.height, 501f); Assert.LessOrEqual(Body.rect.width, 801f);
            float safeHeight = Body.rect.height - 48f;
            float textHeight = Bounds(Title.rectTransform).yMax - Bounds(Hint.rectTransform).yMin;
            Assert.LessOrEqual(textHeight, safeHeight + 1f, "The full text stack must fit before testing decorative art removal.");
            Assert.Greater(textHeight + size.y + 16f, safeHeight + 1f, "This fixture must leave insufficient height for the original portrait and its gap.");
            Assert.LessOrEqual(Bounds(Title.rectTransform).yMax, Body.rect.yMax - 23f);
            Assert.GreaterOrEqual(Bounds(Hint.rectTransform).yMin, Body.rect.yMin + 23f);
            Assert.False(panel.GetComponentInChildren<BossIntroReadability>(true).IsConstrained); Assert.False(image.enabled);
            canvas.GetComponent<CanvasScaler>().enabled = scalerEnabled; canvas.scaleFactor = canvasScale;
            yield return Settle(); Assert.True(image.enabled); Assert.AreSame(sprite, image.sprite); AssertVector(size, image.rectTransform.rect.size);
            Assert.AreEqual(color, image.color); Assert.AreEqual(raycast, image.raycastTarget); Inside(image.rectTransform, Body);
            Assert.LessOrEqual(Bounds(Title.rectTransform).yMax, Bounds(image.rectTransform).yMin + 1);
        }

        [UnityTest]
        public IEnumerator ShortRoundtripAndRepeatedAttachKeepReferencesAndBaselineGeometry()
        {
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f, true));
            Play("Boss", "Keeper"); yield return Settle(); CaptureCanvas();
            Assert.AreEqual(45, Line.fontSize);
            Vector2 titleSize = Title.rectTransform.rect.size, titlePosition = Title.rectTransform.anchoredPosition;
            Vector2 epithetSize = Epithet.rectTransform.rect.size, hintPosition = Hint.rectTransform.anchoredPosition;
            var helper = panel.GetComponentInChildren<BossIntroReadability>(true);
            Assert.AreSame(helper, BossIntroReadability.Attach(Body, Title, Epithet, Line, Hint, Divider, Line.transform.parent as RectTransform));
            var title = Title; var epithet = Epithet; var line = Line; var hint = Hint;
            foreach (float scale in new[] { 1.5f, 1.25f, 1f, 1.5f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); yield return Settle(); AssertLayout(scale);
            }
            Assert.AreSame(title, Title); Assert.AreSame(epithet, Epithet); Assert.AreSame(line, Line); Assert.AreSame(hint, Hint);
            AssertVector(titleSize, Title.rectTransform.rect.size); AssertVector(titlePosition, Title.rectTransform.anchoredPosition);
            AssertVector(epithetSize, Epithet.rectTransform.rect.size); AssertVector(hintPosition, Hint.rectTransform.anchoredPosition);
            foreach (float narrativeScale in new[] { 1f, 1.5f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveReadability(narrativeScale, narrativeScale > 1)); yield return Settle();
                Assert.AreEqual(Mathf.RoundToInt(30 * narrativeScale), Line.fontSize);
                Assert.AreEqual(90f, Line.rectTransform.rect.height, 0.01f);
                if (narrativeScale == 1)
                {
                    AssertVector(Vector2.one * 0.5f, Line.rectTransform.pivot); AssertVector(Vector2.zero, Line.rectTransform.anchoredPosition);
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualTitleWaitUsesOnePointOneUnscaledSecondsAndHidesBeforeCallback()
        {
            Time.timeScale = 0; float started = Time.unscaledTime;
            Play("Title", "Oath", Array.Empty<string>()); yield return WaitForFinished();
            Assert.GreaterOrEqual(completedAt - started, 1.08f); Assert.AreEqual(1, finished);
            Assert.False(BossIntroPanel.IsOpen); Assert.False(wasOpenInCallback); Assert.AreEqual(1, closed); Assert.AreEqual(0, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator ActualLineWaitKeepsFourSecondDeadlineWhilePaused()
        {
            Time.timeScale = 0; float started = Time.unscaledTime;
            Play("Title", "Oath", new[] { "Timed line" }); yield return WaitForLine("Timed line", 3f);
            float seenAt = Time.unscaledTime; yield return WaitForFinished(6f);
            Assert.GreaterOrEqual(completedAt - seenAt, 3.85f); Assert.GreaterOrEqual(completedAt - started, 5.05f);
            Assert.AreEqual(1, finished); Assert.False(wasOpenInCallback); Assert.AreEqual(0, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator GraceRejectsInitialSpaceThenActualEnterZAndPadAdvanceOnePhaseEach()
        {
            Play("Grace", "Oath", new[] { "First", "Second" });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return null; yield return null;
            Assert.IsEmpty(Line.text); Assert.Zero(finished);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Pulse(Key.Enter); yield return WaitForLine("First");
            yield return Pulse(Key.Z); yield return WaitForLine("Second");
            yield return Pulse(Key.None); yield return WaitForFinished();
            Assert.AreEqual(1, finished); Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator ActualAdvancePredicateRespectsSaveCaptureForAllSupportedInputs()
        {
            Play("Input", "Oath");
            foreach (var key in new[] { Key.Space, Key.Enter, Key.Z, Key.None })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                if (key == Key.None) InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
                else InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null; SetDismissed(Time.frameCount);
                Assert.False((bool)typeof(BossIntroPanel).GetMethod("WasAdvancePressed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null));
                SetDismissed(-1);
                Assert.True((bool)typeof(BossIntroPanel).GetMethod("WasAdvancePressed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null));
            }
            Assert.Zero(finished);
        }

        [UnityTest]
        public IEnumerator AlreadyPlayingRequestCompletesSeparatelyWithoutReplacingOriginalSequence()
        {
            Play("Original", "Original oath", Array.Empty<string>()); int second = 0;
            BossIntroPanel.Play("Wrong", "Wrong oath", new[] { "Wrong line" }, () => second++);
            Assert.AreEqual(1, second); Assert.Zero(finished); Assert.AreEqual("Original", Title.text); Assert.AreEqual("Original oath", Epithet.text);
            Assert.AreEqual(1, opened); yield return Pulse(Key.Space); yield return WaitForFinished();
            Assert.AreEqual(1, finished); Assert.AreEqual(1, second); Assert.False(wasOpenInCallback);
        }

        [UnityTest]
        public IEnumerator DestroyDuringRealLineWaitCancelsWithoutInvokingFinishedCallback()
        {
            Play("Cancel", "Oath", new[] { "Line before destruction" }); yield return Pulse(Key.Space); yield return WaitForLine("Line before destruction");
            Object.Destroy(panel.gameObject); yield return null; yield return null; yield return null;
            Assert.Zero(finished); Assert.False(BossIntroPanel.IsOpen);
        }

        private void Play(string title, string epithet, string[] lines = null, string enemyId = null)
        {
            BossIntroPanel.Play(title, epithet, lines ?? Enumerable.Repeat("대사 Dialogue 台詞", 20).ToArray(), () =>
            {
                finished++; completedAt = Time.unscaledTime; wasOpenInCallback = BossIntroPanel.IsOpen;
            }, enemyId);
            panel = Object.FindAnyObjectByType<BossIntroPanel>();
        }
        private static IEnumerator Pulse(Key key)
        {
            yield return new WaitForSecondsRealtime(0.28f);
            if (key == Key.None) InputSystem.QueueStateEvent(Gamepad.current, new GamepadState().WithButton(GamepadButton.South));
            else InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.QueueStateEvent(Gamepad.current, new GamepadState()); yield return null;
        }
        private IEnumerator WaitForLine(string expected, float timeout = 2f)
        {
            float deadline = Time.unscaledTime + timeout;
            while (panel != null && Line.text != expected && Time.unscaledTime < deadline) yield return null;
            Assert.NotNull(panel); Assert.AreEqual(expected, Line.text);
        }
        private IEnumerator WaitForFinished(float timeout = 3f)
        {
            float deadline = Time.unscaledTime + timeout;
            while (finished == 0 && Time.unscaledTime < deadline) yield return null;
            Assert.AreEqual(1, finished, "The actual RunAsync sequence did not finish within its original deadline.");
        }
        private void CaptureCanvas()
        {
            if (canvas != null) return;
            canvas = panel.GetComponent<Canvas>(); canvasScale = canvas.scaleFactor; scalerEnabled = canvas.GetComponent<CanvasScaler>().enabled;
        }
        private RectTransform Body => panel.transform.Find("Body") as RectTransform;
        private Text Title => Field<Text>(panel, "titleLabel");
        private Text Epithet => Field<Text>(panel, "epithetLabel");
        private Text Line => Field<Text>(panel, "lineLabel");
        private Text Hint => Body.Find("Hint").GetComponent<Text>();
        private RectTransform Divider => Body.Find("Divider") as RectTransform;
        private void AssertLayout(float scale)
        {
            AssertText(Title, Mathf.RoundToInt(64 * scale)); AssertText(Epithet, Mathf.RoundToInt(26 * scale)); AssertText(Hint, Mathf.RoundToInt(17 * scale));
            Inside(Title.rectTransform, Body); Inside(Epithet.rectTransform, Body); Inside(Hint.rectTransform, Body); Inside(Divider, Body); Inside(Line.rectTransform, Body);
            Assert.LessOrEqual(Bounds(Epithet.rectTransform).yMax, Bounds(Title.rectTransform).yMin + 1);
            Assert.LessOrEqual(Bounds(Divider).yMax, Bounds(Epithet.rectTransform).yMin + 1);
            Assert.LessOrEqual(Bounds(Line.rectTransform).yMax, Bounds(Divider).yMin + 1);
            Assert.LessOrEqual(Bounds(Hint.rectTransform).yMax, Bounds(Line.rectTransform).yMin + 1);
            Assert.False(Title.raycastTarget); Assert.False(Epithet.raycastTarget); Assert.False(Hint.raycastTarget);
        }
        private static void AssertText(Text text, int size)
        {
            Assert.AreEqual(size, text.fontSize); Assert.False(text.resizeTextForBestFit);
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1, text.name);
        }
        private Rect Bounds(RectTransform rect)
        {
            var points = new Vector3[4]; rect.GetWorldCorners(points); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var point in points) { Vector2 p = Body.InverseTransformPoint(point); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private void Inside(RectTransform child, RectTransform parent)
        {
            var points = new Vector3[4]; child.GetWorldCorners(points);
            foreach (var point in points)
            {
                Vector2 p = parent.InverseTransformPoint(point);
                Assert.GreaterOrEqual(p.x, parent.rect.xMin - 1); Assert.LessOrEqual(p.x, parent.rect.xMax + 1);
                Assert.GreaterOrEqual(p.y, parent.rect.yMin - 1); Assert.LessOrEqual(p.y, parent.rect.yMax + 1);
            }
        }
        private static FieldInfo InstanceField => typeof(RunModalPanel<BossIntroPanel>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void SetDismissed(int frame) => typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, frame);
        private static void AssertVector(Vector2 expected, Vector2 actual) { Assert.AreEqual(expected.x, actual.x, 0.01f); Assert.AreEqual(expected.y, actual.y, 0.01f); }
        private void OnOpened() => opened++;
        private void OnClosed() => closed++;
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
    }
}
