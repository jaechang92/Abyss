using System.Collections;
using System.Linq;
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
    public sealed class EndingStatsReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private EndingSequencePanel panel;
        private EndingSequencePanel previousInstance;
        private RunManager run;
        private bool ownsRun;
        private RunSummary previousSummary;
        private bool previousRunEnabled;
        private bool previousInstanceActive;
        private GameObject body;
        private CanvasGroup group;
        private Text title;
        private Text stats;
        private Text hint;
        private Text credits;
        private EndingStatsReadability helper;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private GameObject ownedEvents;
        private GameObject previousSelection;
        private bool previousNavigation;
        private GameObject behindButton;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private int callbacks;
        private static readonly FieldInfo InstanceField = typeof(EndingSequencePanel).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale;
            Time.timeScale = 0f; callbacks = 0;
            ownsRun = !RunManager.HasInstance; run = RunManager.Instance;
            previousSummary = run.LastRunSummary;
            previousRunEnabled = run.enabled; run.enabled = false; // Preserve an existing run clock while reading its summary.
            Set(run, "lastRunSummary", ShortSummary());
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            previousInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("EndingStatsTestEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject; previousNavigation = EventSystem.current.sendNavigationEvents;
            behindButton = new GameObject("EndingStatsPreviousFocus", typeof(RectTransform), typeof(Button));
            EventSystem.current.SetSelectedGameObject(behindButton);
            previousInstance = (EndingSequencePanel)InstanceField.GetValue(null);
            previousInstanceActive = previousInstance != null && previousInstance.gameObject.activeSelf;
            if (previousInstance != null) previousInstance.gameObject.SetActive(false);
            InstanceField.SetValue(null, null);
            EndingSequencePanel.Play(CountFinished);
            panel = (EndingSequencePanel)InstanceField.GetValue(null);
            panel.enabled = false; // Drive real phases explicitly; no autonomous cover timeout or scene callback.
            body = Field<GameObject>(panel, "body"); group = Field<CanvasGroup>(panel, "statsGroup");
            stats = Field<Text>(panel, "statsBody"); hint = Field<Text>(panel, "skipHint"); credits = Field<Text>(panel, "creditsText");
            title = group.transform.Find("StatsTitle").GetComponent<Text>();
            helper = group.GetComponent<EndingStatsReadability>(); Assert.NotNull(helper);
            Call(panel, "BeginStats"); yield return Settle();
            Assert.AreEqual(string.Join("\n", RunSummaryText.AllLines(run.LastRunSummary)), stats.text);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (panel != null) { Call(panel, "HideCover"); Object.Destroy(panel.gameObject); }
                if (behindButton != null) Object.Destroy(behindButton);
                if (run != null) { Set(run, "lastRunSummary", previousSummary); if (ownsRun) Object.Destroy(run.gameObject); }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse); if (gamepad != null) InputSystem.RemoveDevice(gamepad);
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
        public IEnumerator ActualBeginStatsPreservesSummaryAndFullTextAcrossThreeScalesAndLocales()
        {
            var summary = LongSummary(); Set(run, "lastRunSummary", summary); string unchanged = JsonUtility.ToJson(summary);
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); Call(panel, "BeginStats");
                    title.text = "길어진 엔딩 통계 제목 Ending statistics at the journey's end 長いエンディング統計見出し";
                    yield return Settle(); AssertLayout(scale);
                    Assert.AreEqual(string.Join("\n", RunSummaryText.AllLines(summary)), stats.text);
                    Assert.AreEqual(unchanged, JsonUtility.ToJson(summary)); Assert.AreEqual(0, callbacks);
                }
            }
            var canvas = panel.GetComponent<Canvas>(); canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = 0.5f; yield return Settle(); AssertLayout(1.5f);
        }

        [UnityTest]
        public IEnumerator ShortStatisticsFadeThenAutoFinishAtOriginalTwelveSecondsExactlyOnce()
        {
            Assert.False(helper.IsOverflow);
            Set(panel, "phaseTimer", 0f); Call(panel, "TickStats"); Assert.AreEqual(0f, group.alpha);
            Set(panel, "phaseTimer", 0.4f); Call(panel, "TickStats"); Assert.AreEqual(0.5f, group.alpha, 0.001f);
            Set(panel, "phaseTimer", 0.8f); Call(panel, "TickStats"); Assert.AreEqual(1f, group.alpha);
            Set(panel, "phaseTimer", 11.9f); Call(panel, "TickStats"); Assert.AreEqual(0, callbacks);
            Set(panel, "phaseTimer", 12f); Call(panel, "TickStats"); Assert.AreEqual(1, callbacks);
            Assert.False(group.gameObject.activeSelf); Assert.True(body.activeSelf);
            Call(panel, "Finish"); Assert.AreEqual(1, callbacks);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverflowStatisticsWaitBeyondTwelveSecondsThenConsumeEnterAndPadOnce()
        {
            Assert.False(helper.IsOverflow, "Fresh short statistics initially fit.");
            FixedBody(); ((RectTransform)body.transform).sizeDelta = new Vector2(640, 400);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            // No Apply or yielded frame: TickStats must refresh current geometry before deciding automatic completion.
            Set(panel, "phaseTimer", 12f); Call(panel, "TickStats");
            Assert.True(helper.IsOverflow); Assert.AreEqual(0, callbacks); Assert.True(group.gameObject.activeSelf);
            Set(run, "lastRunSummary", LongSummary()); FixedBody();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Call(panel, "BeginStats"); yield return Settle();
            Assert.True(helper.IsOverflow);
            Set(panel, "phaseTimer", 20f); Call(panel, "TickStats");
            Assert.AreEqual(0, callbacks); Assert.True(group.gameObject.activeSelf); Assert.AreEqual(1f, group.alpha);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.True((bool)CallResult(panel, "ConsumeSkipInput")); Assert.AreEqual(1, callbacks);
            Assert.False((bool)CallResult(panel, "ConsumeSkipInput")); Assert.AreEqual(1, callbacks);
            Assert.True(body.activeSelf); Assert.False(group.gameObject.activeSelf);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(gamepad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator PausedPageRightStickAndActualOverlayWheelMoveOnlyLongStatistics()
        {
            FixedBody(); Set(run, "lastRunSummary", LongSummary());
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Call(panel, "BeginStats"); yield return Settle();
            var scroll = group.GetComponentInChildren<ScrollRect>(true); Assert.NotNull(scroll); Assert.True(helper.IsOverflow);
            AssertReadingChrome(scroll);
            Vector3 heading = title.transform.position, hintPosition = hint.transform.position;
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down }); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            var corners = new Vector3[4]; scroll.viewport.GetWorldCorners(corners);
            Vector2 pointer = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, null)); mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, scroll = new Vector2(0, -120) }); yield return null;
            Assert.AreSame(mouse, Mouse.current); AssertVector(pointer, mouse.position.ReadValue());
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, mouse.position.ReadValue(), null)); yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }); yield return null;
            AssertVector3(heading, title.transform.position); AssertVector3(hintPosition, hint.transform.position);
            Assert.AreEqual(0, callbacks); Assert.AreEqual(0f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator ShortBaselineAndCreditsRemainIndependentOfMenuStatisticsScale()
        {
            Vector2 groupSize = ((RectTransform)group.transform).rect.size;
            Vector2 titleSize = title.rectTransform.rect.size, titlePosition = title.rectTransform.anchoredPosition;
            Vector2 statsSize = stats.rectTransform.rect.size, statsPosition = stats.rectTransform.anchoredPosition;
            float spacing = stats.lineSpacing; int creditFont = credits.fontSize; string creditText = credits.text;
            Call(panel, "BeginCredits");
            Vector2 creditPosition = credits.rectTransform.anchoredPosition;
            Call(panel, "TickCredits"); Assert.GreaterOrEqual(credits.rectTransform.anchoredPosition.y, creditPosition.y);
            foreach (float scale in new[] { 1.5f, 1.25f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); Call(panel, "BeginStats"); yield return Settle();
                Assert.AreEqual(creditFont, credits.fontSize); Assert.AreEqual(creditText, credits.text); Assert.AreEqual(spacing, stats.lineSpacing);
            }
            Assert.False(helper.IsOverflow); AssertVector(groupSize, ((RectTransform)group.transform).rect.size);
            AssertVector(titleSize, title.rectTransform.rect.size); AssertVector(titlePosition, title.rectTransform.anchoredPosition);
            AssertVector(statsSize, stats.rectTransform.rect.size); AssertVector(statsPosition, stats.rectTransform.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator ReplayingActualPlayResetsReadingAndFocusCoverRestoresLivingPreviousSelection()
        {
            FixedBody(); Set(run, "lastRunSummary", LongSummary());
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Call(panel, "BeginStats"); yield return Settle();
            var scroll = group.GetComponentInChildren<ScrollRect>(true); scroll.verticalNormalizedPosition = 0f;
            EndingSequencePanel.Play(CountFinished); panel.enabled = false; Call(panel, "BeginStats"); yield return Settle();
            Assert.AreEqual(1f, scroll.verticalNormalizedPosition, 0.001f); Assert.AreEqual(0, callbacks);
            var sink = Field<GameObject>(panel, "focusSink"); Assert.AreSame(sink, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(Navigation.Mode.None, sink.GetComponent<Selectable>().navigation.mode);
            Assert.IsNull(sink.GetComponent<ISubmitHandler>()); Assert.IsNull(sink.GetComponent<Button>());
            Call(panel, "Finish"); Assert.AreEqual(1, callbacks); Assert.True(body.activeSelf);
            Assert.AreSame(sink, EventSystem.current.currentSelectedGameObject);
            Call(panel, "HideCover"); Assert.False(body.activeSelf);
            Assert.AreSame(behindButton, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator CreditsAdvanceAndStatsEscapeUseActualInputWithoutLoadingAScene()
        {
            Call(panel, "BeginCredits"); Set(panel, "startFrame", Time.frameCount - 1);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.True((bool)CallResult(panel, "ConsumeSkipInput")); Assert.True(group.gameObject.activeSelf); Assert.AreEqual(0, callbacks);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Assert.True((bool)CallResult(panel, "ConsumeSkipInput")); Assert.AreEqual(1, callbacks);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        private void FixedBody()
        {
            var rect = (RectTransform)body.transform; rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(640, 520);
        }
        private void AssertLayout(float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(34 * scale), title.fontSize); Assert.False(title.resizeTextForBestFit);
            Assert.AreEqual(Mathf.RoundToInt(21 * scale), stats.fontSize); Assert.False(stats.resizeTextForBestFit);
            Assert.AreEqual(Mathf.RoundToInt(15 * scale), hint.fontSize); Assert.False(hint.resizeTextForBestFit);
            Assert.GreaterOrEqual(title.rectTransform.rect.height + 1f, title.preferredHeight);
            Assert.GreaterOrEqual(stats.rectTransform.rect.height + 1f, stats.preferredHeight);
            Assert.GreaterOrEqual(hint.rectTransform.rect.height + 1f, hint.preferredHeight);
            var scroll = group.GetComponentInChildren<ScrollRect>(true);
            Rect mainText = Bounds(scroll != null && scroll.gameObject.activeInHierarchy ? scroll.viewport : stats.rectTransform, body.transform);
            Assert.False(mainText.Overlaps(Bounds(title.rectTransform, body.transform)));
            Assert.False(mainText.Overlaps(Bounds(hint.rectTransform, body.transform)));
            Inside(title.rectTransform, (RectTransform)body.transform); Inside(hint.rectTransform, (RectTransform)body.transform);
            Inside((RectTransform)group.transform, (RectTransform)body.transform);
            if (scroll != null && scroll.gameObject.activeInHierarchy) AssertReadingChrome(scroll);
        }
        private void AssertReadingChrome(ScrollRect scroll)
        {
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
            Inside(scroll.viewport, (RectTransform)body.transform);
            Inside(title.rectTransform, (RectTransform)body.transform); Inside(hint.rectTransform, (RectTransform)body.transform);
            var readingHint = group.transform.Find("StatsReadingHint") as RectTransform;
            Assert.NotNull(readingHint); Assert.True(readingHint.gameObject.activeInHierarchy);
            Inside(readingHint, (RectTransform)body.transform);
            Rect view = Bounds(scroll.viewport, body.transform);
            Assert.False(view.Overlaps(Bounds(title.rectTransform, body.transform)));
            Assert.False(view.Overlaps(Bounds(hint.rectTransform, body.transform)));
            Assert.False(view.Overlaps(Bounds(readingHint, body.transform)));
            Assert.GreaterOrEqual(readingHint.rect.height + 1f, readingHint.GetComponent<Text>().preferredHeight);
            Assert.AreEqual(Navigation.Mode.None, scroll.verticalScrollbar.navigation.mode);
        }        private static RunSummary ShortSummary() => new() { hasRecord = true, enemiesKilled = 1, maxCombo = 1, elapsedSeconds = 60, abyssEarned = 1, abyssTotal = 10 };
        private static RunSummary LongSummary()
        {
            var summary = ShortSummary(); summary.dominantFormId = "긴 폼 이름 Long form name 長いフォーム名"; summary.dominantFormRatio = 0.9f;
            summary.formsUsed = Enumerable.Range(0, 8).Select(i => "긴 폼 Long form 長いフォーム " + i).ToList();
            summary.draftedSkillIds = Enumerable.Range(0, 30).Select(i => "긴 스킬 Long skill 長いスキル " + i).ToList(); return summary;
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
        private static void AssertVector(Vector2 expected, Vector2 actual) { Assert.AreEqual(expected.x, actual.x, 0.01f); Assert.AreEqual(expected.y, actual.y, 0.01f); }
        private static void AssertVector3(Vector3 expected, Vector3 actual) { Assert.AreEqual(expected.x, actual.x, 0.01f); Assert.AreEqual(expected.y, actual.y, 0.01f); Assert.AreEqual(expected.z, actual.z, 0.01f); }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static object CallResult(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}