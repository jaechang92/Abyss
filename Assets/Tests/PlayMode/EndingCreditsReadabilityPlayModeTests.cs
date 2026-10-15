using System.Collections;
using System.Reflection;
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
    public sealed class EndingCreditsReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private EndingSequencePanel panel;
        private EndingSequencePanel previousInstance;
        private bool previousInstanceActive;
        private RunManager run;
        private bool ownsRun;
        private bool previousRunEnabled;
        private RunSummary previousSummary;
        private GameObject body;
        private CanvasGroup statsGroup;
        private Text credits;
        private Text hint;
        private RectTransform creditsRect;
        private EndingCreditsReadability helper;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private GameObject ownedEvents;
        private GameObject behindButton;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private int callbacks;
        private string originalCredits;
        private static readonly FieldInfo InstanceField = typeof(EndingSequencePanel).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale; callbacks = 0;
            Time.timeScale = 0f; ownsRun = !RunManager.HasInstance; run = RunManager.Instance;
            previousSummary = run.LastRunSummary; previousRunEnabled = run.enabled; run.enabled = false;
            Set(run, "lastRunSummary", new RunSummary { hasRecord = true, enemiesKilled = 1, maxCombo = 1, elapsedSeconds = 60 });
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            previousInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings; keyboard = InputSystem.AddDevice<Keyboard>(); gamepad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("CreditsReadabilityEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject; previousNavigation = EventSystem.current.sendNavigationEvents;
            behindButton = new GameObject("CreditsPreviousFocus", typeof(RectTransform), typeof(Button)); EventSystem.current.SetSelectedGameObject(behindButton);
            previousInstance = (EndingSequencePanel)InstanceField.GetValue(null);
            previousInstanceActive = previousInstance != null && previousInstance.gameObject.activeSelf;
            if (previousInstance != null) previousInstance.gameObject.SetActive(false);
            InstanceField.SetValue(null, null); EndingSequencePanel.Play(CountFinished);
            panel = (EndingSequencePanel)InstanceField.GetValue(null); panel.enabled = false;
            body = Field<GameObject>(panel, "body"); statsGroup = Field<CanvasGroup>(panel, "statsGroup");
            credits = Field<Text>(panel, "creditsText"); creditsRect = Field<RectTransform>(panel, "creditsRect"); hint = Field<Text>(panel, "skipHint");
            helper = body.GetComponent<EndingCreditsReadability>(); Assert.NotNull(helper);
            originalCredits = credits.text;
            Call(panel, "BeginCredits"); yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (panel != null) { Call(panel, "HideCover"); Object.Destroy(panel.gameObject); }
                if (behindButton != null) Object.Destroy(behindButton);
                if (run != null) { Set(run, "lastRunSummary", previousSummary); if (ownsRun) Object.Destroy(run.gameObject); }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (gamepad != null) InputSystem.RemoveDevice(gamepad);
                InputSystem.settings = previousInputSettings; if (testInputSettings != null) Object.Destroy(testInputSettings);
                yield return null;
                InstanceField.SetValue(null, previousInstance != null ? previousInstance : null);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
            }
            finally
            {
                guard.Release();
                if (!ownsRun && run != null) run.enabled = previousRunEnabled;
                if (previousInstance != null) previousInstance.gameObject.SetActive(previousInstanceActive);
            }
        }

        [UnityTest]
        public IEnumerator ActualCreditsTextFitsAllScalesAndLocalesWithoutChangingContentOrSpacing()
        {
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang); Call(panel, "RefreshHint", true);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); Call(panel, "BeginCredits"); yield return Settle();
                    AssertGeometry(scale); Assert.AreEqual(originalCredits, credits.text); Assert.AreSame(creditsRect, credits.rectTransform);
                    Assert.AreEqual(1.3f, credits.lineSpacing); Assert.AreEqual(0, callbacks);
                }
            }
            var canvas = panel.GetComponent<Canvas>(); canvas.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = 0.5f;
            yield return Settle(); AssertGeometry(1.5f);
        }

        [UnityTest]
        public IEnumerator NarrowOverlayViewportClipsFullCreditsAndActualTickKeepsUnscaledSpeed110()
        {
            FixedBody(); Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            string longHint = "ESC / Enter / Space / Pad A — 길어진 확인 안내 A longer confirmation instruction 長い確認の案内";
            Call(panel, "BeginCredits"); hint.text = longHint; yield return Settle(); AssertGeometry(1.5f);
            Assert.AreEqual(longHint, hint.text);
            var view = Viewport(); Assert.LessOrEqual(Bounds(creditsRect, view).yMax, view.rect.yMin + 0.1f);
            yield return null;
            float before = creditsRect.anchoredPosition.y, delta = Time.unscaledDeltaTime;
            Call(panel, "TickCredits");
            Assert.AreEqual(110f * delta, creditsRect.anchoredPosition.y - before, 0.05f);
            Assert.AreEqual(0f, Time.timeScale); Assert.AreEqual(0, callbacks); Assert.False(statsGroup.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator MidScrollScaleAndAreaChangesPreserveMeasuredProgressThenBeginResetsToBottom()
        {
            var view = Viewport(); PlaceProgress(view, 0.35f); Assert.AreEqual(0.35f, Progress(view), 0.001f);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); FixedBody(); yield return Settle();
            Assert.AreEqual(0.35f, Progress(Viewport()), 0.01f); AssertGeometry(1.5f);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Assert.AreEqual(0.35f, Progress(Viewport()), 0.01f);
            Call(panel, "BeginCredits"); yield return Settle();
            Assert.AreEqual(0f, Progress(Viewport()), 0.001f);
            EndingSequencePanel.Play(CountFinished); panel.enabled = false; Call(panel, "BeginCredits"); yield return Settle();
            Assert.AreEqual(0f, Progress(Viewport()), 0.001f); Assert.AreEqual(0, callbacks);
        }

        [UnityTest]
        public IEnumerator CrossingActualViewportTopTransitionsToStatisticsOnceWithoutFinishing()
        {
            var view = Viewport(); PlaceProgress(view, 0.999999f);
            Assert.AreEqual(view.rect.yMax, Bounds(creditsRect, view).yMin, 0.01f);
            Call(panel, "TickCredits");
            Assert.GreaterOrEqual(Bounds(creditsRect, view).yMin, view.rect.yMax - 0.01f);
            Assert.True(statsGroup.gameObject.activeSelf); Assert.False(credits.gameObject.activeSelf); Assert.AreEqual(0, callbacks);
            Assert.AreEqual(string.Join("\n", RunSummaryText.AllLines(run.LastRunSummary)), Field<Text>(panel, "statsBody").text);
            Set(panel, "phaseTimer", 0.4f); Call(panel, "TickStats"); Assert.AreEqual(0.5f, statsGroup.alpha, 0.001f);
            Assert.False((bool)CallResult(panel, "ConsumeSkipInput")); Assert.AreEqual(0, callbacks);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryActualAdvanceKeySkipsOnlyCreditsAndSimultaneousPadInputIsSinglePhase()
        {
            foreach (Key key in new[] { Key.Escape, Key.Enter, Key.Space })
            {
                EndingSequencePanel.Play(CountFinished); panel.enabled = false; Call(panel, "BeginCredits");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
                Assert.True((bool)CallResult(panel, "ConsumeSkipInput"));
                Assert.True(statsGroup.gameObject.activeSelf); Assert.AreEqual(0, callbacks);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            }
        }

        [UnityTest]
        public IEnumerator StatisticsOwnTheirFontsAfterCreditsAndFinishCoverReleasesLivingFocus()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle(); AssertGeometry(1.5f);
            Call(panel, "BeginStats"); yield return Settle();
            var statsBody = Field<Text>(panel, "statsBody"); var statsTitle = statsGroup.transform.Find("StatsTitle").GetComponent<Text>();
            Assert.AreEqual(Mathf.RoundToInt(21 * 1.5f), statsBody.fontSize); Assert.AreEqual(51, statsTitle.fontSize);
            Assert.AreEqual(Mathf.RoundToInt(15 * 1.5f), hint.fontSize); Assert.AreEqual(0f, statsGroup.alpha);
            var sink = Field<GameObject>(panel, "focusSink"); Assert.AreSame(sink, EventSystem.current.currentSelectedGameObject);
            Call(panel, "Finish"); Assert.AreEqual(1, callbacks); Assert.True(body.activeSelf);
            Assert.AreSame(sink, EventSystem.current.currentSelectedGameObject);
            Call(panel, "HideCover"); Assert.False(body.activeSelf); Assert.AreSame(behindButton, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator MissingCreditsReferenceFallsBackToActualStatisticsWithoutCallingCompletion()
        {
            Set(panel, "creditsText", null); Set(panel, "creditsRect", null);
            Call(panel, "BeginCredits");
            Assert.True(statsGroup.gameObject.activeSelf); Assert.AreEqual(0, callbacks);
            Assert.AreEqual(string.Join("\n", RunSummaryText.AllLines(run.LastRunSummary)), Field<Text>(panel, "statsBody").text);
            yield return null;
        }

        private RectTransform Viewport()
        {
            var mask = credits.GetComponentInParent<RectMask2D>(); Assert.NotNull(mask, "Credits need a real clipping viewport.");
            return (RectTransform)mask.transform;
        }
        private void AssertGeometry(float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(24 * scale), credits.fontSize); Assert.False(credits.resizeTextForBestFit);
            Assert.AreEqual(HorizontalWrapMode.Wrap, credits.horizontalOverflow);
            Assert.GreaterOrEqual(creditsRect.rect.height + 1f, credits.preferredHeight);
            Assert.AreEqual(Mathf.RoundToInt(15 * scale), hint.fontSize); Assert.False(hint.resizeTextForBestFit);
            Assert.GreaterOrEqual(hint.rectTransform.rect.height + 1f, hint.preferredHeight);
            var view = Viewport(); Inside(view, (RectTransform)body.transform); Inside(hint.rectTransform, (RectTransform)body.transform);
            Assert.False(Bounds(view, body.transform).Overlaps(Bounds(hint.rectTransform, body.transform)));
            Assert.False(statsGroup.gameObject.activeSelf);
        }
        private float Progress(RectTransform view)
        {
            Rect text = Bounds(creditsRect, view);
            float start = view.rect.yMin - text.height * 0.5f;
            return (text.center.y - start) / (view.rect.height + text.height);
        }
        private void PlaceProgress(RectTransform view, float progress)
        {
            Rect text = Bounds(creditsRect, view);
            float seconds = (view.rect.height + text.height) * (progress - helper.Progress) / 110f;
            Assert.False(helper.Advance(seconds), "Position the real scroller before completion, preserving its public progression contract.");
        }
        private void FixedBody()
        {
            var rect = (RectTransform)body.transform; rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(640, 520);
        }
        private void CountFinished() => callbacks++;
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
        private static Rect Bounds(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners) { Vector2 point = parent.InverseTransformPoint(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Inside(RectTransform child, RectTransform parent)
        {
            Rect actual = Bounds(child, parent); Assert.GreaterOrEqual(actual.xMin, parent.rect.xMin - 1); Assert.LessOrEqual(actual.xMax, parent.rect.xMax + 1);
            Assert.GreaterOrEqual(actual.yMin, parent.rect.yMin - 1); Assert.LessOrEqual(actual.yMax, parent.rect.yMax + 1);
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static object CallResult(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}