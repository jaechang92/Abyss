using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class ResultPanelReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private ResultPanelPresenter presenter;
        private GameObject body;
        private RectTransform panel;
        private Canvas canvas;
        private Text title;
        private readonly Text[] stats = new Text[8];
        private readonly Button[] buttons = new Button[2];
        private readonly Text[] buttonLabels = new Text[2];
        private readonly string[] statFields = { "killsText", "comboText", "dominantFormText", "formsUsedText", "skillsText", "stageText", "elapsedText", "abyssEarnedText" };
        private readonly List<object>[] originalListeners = new List<object>[2];
        private readonly Button.ButtonClickedEvent[] clickEvents = new Button.ButtonClickedEvent[2];
        private Navigation[] navigations;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private RunSummary summary;
        private int sentinelCalls;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            if (EventSystem.current == null) Own(new GameObject("ResultReadabilityEvents", typeof(EventSystem)));
            previousSelection = EventSystem.current.currentSelectedGameObject; previousNavigation = EventSystem.current.sendNavigationEvents;
            var canvasGo = Own(new GameObject("ResultReadabilityCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = Mathf.Max(0.01f, Screen.width / 1920f);
            body = Node(canvas.transform, "Body", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            body.AddComponent<Image>();
            panel = Node(body.transform, "Panel", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(720, 560));
            panel.gameObject.AddComponent<Image>();
            title = MakeText(panel, "Title", new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(700, 48), 28);
            for (int i = 0; i < stats.Length; i++)
                stats[i] = MakeText(panel, statFields[i], new Vector2(0.5f, 1), new Vector2(0, -110 - 40 * i), new Vector2(640, 34), 16);
            for (int i = 0; i < 2; i++)
            {
                var rect = Node(panel, "Button" + i, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(i == 0 ? -160 : 160, 50), new Vector2(300, 72));
                rect.pivot = new Vector2(0.5f, 0f);
                var image = rect.gameObject.AddComponent<Image>(); buttons[i] = rect.gameObject.AddComponent<Button>(); buttons[i].targetGraphic = image;
                buttonLabels[i] = MakeText(rect, "Label", Vector2.one * 0.5f, Vector2.zero, new Vector2(280, 64), 16);
                var labelRect = buttonLabels[i].rectTransform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.anchoredPosition = Vector2.zero; labelRect.sizeDelta = Vector2.zero;
                buttons[i].onClick.AddListener(CountSentinel);
            }
            buttons[0].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = buttons[1] };
            buttons[1].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = buttons[0] };
            var presenterGo = Own(new GameObject("ResultReadabilityPresenter")); presenterGo.SetActive(false);
            presenter = presenterGo.AddComponent<ResultPanelPresenter>(); Set(presenter, "root", body); Set(presenter, "titleText", title);
            for (int i = 0; i < stats.Length; i++) Set(presenter, statFields[i], stats[i]);
            Set(presenter, "restartButton", buttons[0]); Set(presenter, "restartLabel", buttonLabels[0]);
            Set(presenter, "lobbyButton", buttons[1]); Set(presenter, "lobbyLabel", buttonLabels[1]);
            presenterGo.SetActive(true);
            // Disable raw keyboard polling; all layout and actual Populate/Show behavior remains active.
            presenter.enabled = false;
            summary = ShortSummary();
            Call(presenter, "ShowPanel"); Call(presenter, "Populate", summary);
            navigations = buttons.Select(button => button.navigation).ToArray();
            for (int i = 0; i < 2; i++) { clickEvents[i] = buttons[i].onClick; originalListeners[i] = RuntimeListeners(clickEvents[i]); }
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                foreach (var item in owned) if (item != null) Object.Destroy(item); owned.Clear(); yield return null;
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (gamepad != null) InputSystem.RemoveDevice(gamepad);
                if (testInputSettings != null) { InputSystem.settings = previousInputSettings; Object.Destroy(testInputSettings); }
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random; sentinelCalls = 0;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator ActualPopulatePreservesSummaryStringsAndFullRowsAtEveryScaleAndLocale()
        {
            summary = LongSummary(); string original = JsonUtility.ToJson(summary);
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Call(presenter, "Populate", summary);
                    title.text = "길어진 결과 제목 A longer run result title 長いラン結果の見出し";
                    Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); yield return Settle();
                    CollectionAssert.AreEqual(RunSummaryText.AllLines(summary).ToArray(), stats.Select(text => text.text).ToArray());
                    AssertFontsAndRows(scale); AssertFixedChrome(); AssertBindingsPreserved();
                    Assert.AreEqual(original, JsonUtility.ToJson(summary));
                }
            }
            canvas.scaleFactor = 0.5f; yield return Settle(); AssertFontsAndRows(1.5f); AssertFixedChrome();
        }

        [UnityTest]
        public IEnumerator NarrowVirtualCanvasScrollsLongStatsWhileTitleButtonsAndFocusStayFixed()
        {
            canvas.renderMode = RenderMode.WorldSpace; ((RectTransform)canvas.transform).sizeDelta = new Vector2(440, 520);
            summary = LongSummary(); Call(presenter, "Populate", summary);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            var scroll = body.GetComponentInChildren<ScrollRect>(true); Assert.NotNull(scroll); Assert.True(scroll.vertical);
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
            Vector3 titlePosition = title.transform.position, restartPosition = buttons[0].transform.position, lobbyPosition = buttons[1].transform.position;
            EventSystem.current.SetSelectedGameObject(buttons[1].gameObject);
            scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases(); Vector3 firstPosition = stats[7].transform.position;
            scroll.verticalNormalizedPosition = 0f; yield return Settle();
            Assert.Greater((stats[7].transform.position - firstPosition).sqrMagnitude, 1f);
            AssertVector3(titlePosition, title.transform.position); AssertVector3(restartPosition, buttons[0].transform.position); AssertVector3(lobbyPosition, buttons[1].transform.position);
            Inside(stats[7].rectTransform, scroll.viewport); AssertFixedChrome();
            Assert.AreSame(buttons[1].gameObject, EventSystem.current.currentSelectedGameObject); AssertBindingsPreserved();
        }

        [UnityTest]
        public IEnumerator ShortSummaryRoundtripRestoresOriginalRectsAndRepeatedPopulateDoesNotAccumulateScale()
        {
            var baselinePanel = panel.rect.size;
            var positions = stats.Select(text => text.rectTransform.anchoredPosition).ToArray();
            var sizes = stats.Select(text => text.rectTransform.rect.size).ToArray();
            var parents = stats.Select(text => text.transform.parent).ToArray();
            var originalRefs = stats.ToArray();
            for (int repeat = 0; repeat < 3; repeat++)
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Call(presenter, "Populate", summary); yield return Settle(); AssertFontsAndRows(1.5f);
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); Call(presenter, "Populate", summary); yield return Settle();
                AssertVector(baselinePanel, panel.rect.size);
                for (int i = 0; i < 8; i++)
                {
                    Assert.AreSame(originalRefs[i], Field<Text>(presenter, statFields[i])); Assert.AreSame(parents[i], stats[i].transform.parent);
                    AssertVector(positions[i], stats[i].rectTransform.anchoredPosition); AssertVector(sizes[i], stats[i].rectTransform.rect.size);
                }
                AssertFontsAndRows(1f); AssertBindingsPreserved(); AssertFixedChrome();
                foreach (var button in buttons)
                {
                    Assert.AreEqual(50f, ((RectTransform)button.transform).anchoredPosition.y, 0.01f);
                    Assert.AreEqual(panel.rect.yMin + 122f, Bounds((RectTransform)button.transform, panel).yMax, 0.01f);
                }
            }
        }

        [UnityTest]
        public IEnumerator PausedScaleAndContentChangesApplyAfterDisabledBodyReopens()
        {
            Time.timeScale = 0f; body.SetActive(false);
            summary = LongSummary(); Call(presenter, "Populate", summary);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            body.SetActive(true); yield return Settle();
            Assert.AreEqual(0f, Time.timeScale); AssertFontsAndRows(1.5f);
            CollectionAssert.AreEqual(RunSummaryText.AllLines(summary).ToArray(), stats.Select(text => text.text).ToArray());
            stats[4].text += " Final visible end marker 最後の行 끝 표시"; yield return Settle();
            Assert.GreaterOrEqual(stats[4].rectTransform.rect.height + 1f, stats[4].preferredHeight);
            AssertBindingsPreserved();
        }

        [UnityTest]
        public IEnumerator ActualPopulateArtGlyphSizesAndButtonRuntimeListenersRemainUnchanged()
        {
            var icons = body.GetComponentsInChildren<Image>(true).Where(image => image.sprite != null).ToDictionary(image => image, image => image.rectTransform.rect.size);
            Assert.Greater(icons.Count, 0, "Populate must exercise actual result and currency art.");
            foreach (float scale in new[] { 1.5f, 1.25f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale)); Call(presenter, "Populate", summary); yield return Settle();
                foreach (var icon in icons) { Assert.NotNull(icon.Key); AssertVector(icon.Value, icon.Key.rectTransform.rect.size); }
                AssertBindingsPreserved();
            }
            Assert.AreEqual(0, sentinelCalls, "Layout must never invoke restart or lobby buttons.");
        }

        [UnityTest]
        public IEnumerator PausedSyntheticPagePadAndWheelScrollOnlyStatisticsWithoutActivatingButtons()
        {
            AddInputDevices(); Time.timeScale = 0f;
            var fixedBody = (RectTransform)body.transform;
            fixedBody.anchorMin = fixedBody.anchorMax = Vector2.one * 0.5f;
            fixedBody.anchoredPosition = Vector2.zero; fixedBody.sizeDelta = new Vector2(640, 520);
            Call(presenter, "Populate", LongSummary()); Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            var scroll = body.GetComponentInChildren<ScrollRect>(true); Assert.True(scroll.gameObject.activeInHierarchy);
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down }); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            var corners = new Vector3[4]; scroll.viewport.GetWorldCorners(corners);
            var helperCanvas = body.GetComponentInParent<Canvas>();
            UnityEngine.Camera pointerCamera = helperCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : helperCanvas.worldCamera;
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, helperCanvas.renderMode, "Input fixture uses the actual Run overlay coordinate mode.");
            Vector2 point = RectTransformUtility.WorldToScreenPoint(pointerCamera, (corners[0] + corners[2]) * 0.5f);
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, pointerCamera));
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) }); yield return null;
            Assert.AreSame(mouse, Mouse.current);
            AssertVector(point, mouse.position.ReadValue());
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, mouse.position.ReadValue(), pointerCamera));
            // The poller consumes wheel input in LateUpdate, after the coroutine resumes in this frame.
            yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            float stopped = scroll.verticalNormalizedPosition; body.SetActive(false);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(stopped, scroll.verticalNormalizedPosition, 0.001f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.AreEqual(0, sentinelCalls); Assert.AreEqual(0f, Time.timeScale); AssertBindingsPreserved();
        }

        [UnityTest]
        public IEnumerator UiSubmitOwnershipUsesPerformedActionAndSelectedResultButtonOnly()
        {
            AddInputDevices();
            var originalSystem = EventSystem.current; bool systemEnabled = originalSystem.enabled;
            var localGo = Own(new GameObject("ResultSubmitTestEvents")); localGo.SetActive(false);
            var system = localGo.AddComponent<EventSystem>(); var module = localGo.AddComponent<InputSystemUIInputModule>();
            var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var map = new InputActionMap("UI"); asset.AddActionMap(map);
            var submit = map.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter");
            module.actionsAsset = asset; module.submit = Own(InputActionReference.Create(submit));
            originalSystem.enabled = false;
            for (int i = 0; i < 2; i++) { buttons[i].onClick = new Button.ButtonClickedEvent(); buttons[i].onClick.AddListener(CountSentinel); }
            try
            {
                localGo.SetActive(true); asset.Enable(); yield return null; yield return null;
                Assert.AreSame(module, system.currentInputModule);
                foreach (var button in buttons)
                {
                    system.SetSelectedGameObject(button.gameObject); int before = sentinelCalls;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
                    Assert.True((bool)CallResult(presenter, "IsSubmitHandledByUi")); Assert.AreEqual(before + 1, sentinelCalls);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                    Assert.False((bool)CallResult(presenter, "IsSubmitHandledByUi"));
                }
                system.sendNavigationEvents = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
                Assert.False((bool)CallResult(presenter, "IsSubmitHandledByUi"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                system.sendNavigationEvents = true; system.SetSelectedGameObject(null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
                Assert.False((bool)CallResult(presenter, "IsSubmitHandledByUi"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                submit.Disable(); system.SetSelectedGameObject(buttons[1].gameObject);
                Assert.False((bool)CallResult(presenter, "IsSubmitHandledByUi"));
                module.enabled = false; Assert.False((bool)CallResult(presenter, "IsSubmitHandledByUi"));
            }
            finally
            {
                asset.Disable(); localGo.SetActive(false); originalSystem.enabled = systemEnabled;
                for (int i = 0; i < 2; i++) buttons[i].onClick = clickEvents[i];
            }
            AssertBindingsPreserved();
            // Presenter polling remains disabled: restart/lobby fallback destinations are statically inspected, never executed here.
        }

        private void AddInputDevices()
        {
            previousInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
        }
        private static object CallResult(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
        private void AssertFontsAndRows(float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(28 * scale), title.fontSize); Assert.False(title.resizeTextForBestFit);
            Assert.GreaterOrEqual(title.rectTransform.rect.height + 1f, title.preferredHeight);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(Mathf.RoundToInt(16 * scale), stats[i].fontSize); Assert.False(stats[i].resizeTextForBestFit);
                Assert.GreaterOrEqual(stats[i].rectTransform.rect.height + 1f, stats[i].preferredHeight);
                if (i > 0) Assert.LessOrEqual(Bounds(stats[i].rectTransform, panel).yMax + 1f, Bounds(stats[i - 1].rectTransform, panel).yMin);
            }
            foreach (var label in buttonLabels)
            { Assert.AreEqual(Mathf.RoundToInt(16 * scale), label.fontSize); Assert.False(label.resizeTextForBestFit); Assert.GreaterOrEqual(label.rectTransform.rect.height + 1f, label.preferredHeight); }
        }
        private void AssertFixedChrome()
        {
            Inside(title.rectTransform, panel); Inside((RectTransform)buttons[0].transform, panel); Inside((RectTransform)buttons[1].transform, panel);
            for (int i = 0; i < 2; i++)
            {
                Inside(buttonLabels[i].rectTransform, (RectTransform)buttons[i].transform);
                Assert.GreaterOrEqual(buttonLabels[i].rectTransform.rect.height + 1f, buttonLabels[i].preferredHeight);
            }
            Assert.False(Bounds((RectTransform)buttons[0].transform, panel).Overlaps(Bounds((RectTransform)buttons[1].transform, panel)));
            Inside(panel, (RectTransform)body.transform);
            var scroll = body.GetComponentInChildren<ScrollRect>(true);
            var hint = panel.Find("ResultReadingHint") as RectTransform;
            var fixedRows = new List<RectTransform> { title.rectTransform };
            if (scroll != null && scroll.gameObject.activeInHierarchy)
            {
                fixedRows.Add(scroll.viewport);
                if (hint != null && hint.gameObject.activeInHierarchy) fixedRows.Add(hint);
            }
            else fixedRows.AddRange(stats.Select(text => text.rectTransform));
            foreach (var fixedRow in fixedRows)
                foreach (var button in buttons)
                    Assert.False(Bounds(fixedRow, panel).Overlaps(Bounds((RectTransform)button.transform, panel)), fixedRow.name + " overlaps a result button.");
            if (scroll != null && scroll.gameObject.activeInHierarchy)
            {
                Assert.False(Bounds(title.rectTransform, panel).Overlaps(Bounds(scroll.viewport, panel)));
                if (hint != null && hint.gameObject.activeInHierarchy)
                {
                    Assert.False(Bounds(hint, panel).Overlaps(Bounds(scroll.viewport, panel)));
                    Inside(hint, panel);
                }
            }
        }
        private void AssertBindingsPreserved()
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.AreSame(clickEvents[i], buttons[i].onClick); CollectionAssert.AreEqual(originalListeners[i], RuntimeListeners(buttons[i].onClick));
                Assert.AreEqual(navigations[i].mode, buttons[i].navigation.mode);
                Assert.AreSame(navigations[i].selectOnLeft, buttons[i].navigation.selectOnLeft); Assert.AreSame(navigations[i].selectOnRight, buttons[i].navigation.selectOnRight);
            }
        }
        private static List<object> RuntimeListeners(UnityEventBase callback)
        {
            object calls = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(callback);
            var list = (IEnumerable)calls.GetType().GetField("m_RuntimeCalls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(calls);
            return list.Cast<object>().ToList();
        }
        private static RunSummary ShortSummary() => new() { hasRecord = true, enemiesKilled = 10, maxCombo = 3, elapsedSeconds = 120, abyssEarned = 4, abyssTotal = 20 };
        private static RunSummary LongSummary()
        {
            var summary = ShortSummary(); summary.enemiesKilled = int.MaxValue; summary.maxCombo = int.MaxValue;
            summary.dominantFormId = "긴 폼 이름 A long form name 長いフォームの名前"; summary.dominantFormRatio = 0.987f;
            summary.formsUsed = Enumerable.Range(0, 8).Select(i => "긴 보유 폼 Long owned form 長い所持フォーム " + i).ToList();
            summary.draftedSkillIds = Enumerable.Range(0, 18).Select(i => "긴 보유 스킬 Long owned skill 長い所持スキル " + i).ToList();
            summary.stageReached = "마지막 도달 장소 Last reached location 最後に到達した場所"; return summary;
        }
        private void CountSentinel() => sentinelCalls++;
        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
        private static RectTransform Node(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int font)
        {
            var rect = Node(parent, name, anchor, anchor, position, size);
            rect.pivot = anchor.y == 1f ? new Vector2(0.5f, 1f) : Vector2.one * 0.5f;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = font; text.text = "—"; return text;
        }
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
    }
}