using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
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
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Abyss.Tests.PlayMode
{
    public sealed class EventRoomReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private EventRoomPanel panel;
        private EventData data;
        private RunManager run;
        private bool ownsPanel, ownsRun, runEnabled;
        private Canvas savedCanvas;
        private float oldCanvasScale;
        private bool oldScalerEnabled;
        private int oldGold, oldLevel, resolved, confirmed, oldDismissed;
        private LocalizationLanguage oldLanguage;
        private float oldTime;
        private Random.State oldRandom;
        private GameObject ownedEvents, behind, oldSelection;
        private bool oldNavigation;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private InputSettings oldInput, input;
        private readonly List<string> sequence = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            oldLanguage = Loc.CurrentLanguage; oldTime = Time.timeScale; oldRandom = Random.state;
            Assert.False(EventRoomPanel.IsOpen, "An existing open event must be preserved.");
            ownsPanel = Object.FindAnyObjectByType<EventRoomPanel>() == null;
            ownsRun = !RunManager.HasInstance; run = RunManager.Instance;
            oldGold = run.GoldShards; oldLevel = run.CurrentLevel; runEnabled = run.enabled; run.enabled = false;
            run.GainGoldShards(1000 - run.GoldShards);
            oldDismissed = (int)typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            SetDismissed(-1);
            oldInput = InputSystem.settings; input = Object.Instantiate(oldInput);
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = input;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("EventReadingEvents", typeof(EventSystem));
            oldSelection = EventSystem.current.currentSelectedGameObject; oldNavigation = EventSystem.current.sendNavigationEvents;
            behind = new GameObject("EventReadingPreviousFocus", typeof(RectTransform), typeof(Button));
            EventSystem.current.SetSelectedGameObject(behind);
            data = ScriptableObject.CreateInstance<EventData>(); data.eventId = "broken_altar";
            FillLongData();
            resolved = confirmed = 0; sequence.Clear();
            GameEvents.OnDraftClosed += OnClosed; GameEvents.OnEventResolved += OnResolved;
            GameEvents.OnPlayerLevelUp += OnLevel;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (panel != null && EventRoomPanel.IsOpen) CallBase("HideBody");
                GameEvents.OnDraftClosed -= OnClosed; GameEvents.OnEventResolved -= OnResolved;
                GameEvents.OnPlayerLevelUp -= OnLevel;
                if (savedCanvas != null && !ownsPanel) { savedCanvas.scaleFactor = oldCanvasScale; savedCanvas.GetComponent<CanvasScaler>().enabled = oldScalerEnabled; }
                if (ownsPanel && panel != null) Object.Destroy(panel.gameObject);
                if (run != null)
                {
                    Set(run, "currentLevel", oldLevel); run.GainGoldShards(oldGold - run.GoldShards); run.enabled = runEnabled;
                    if (ownsRun) Object.Destroy(run.gameObject);
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = oldNavigation;
                    EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : null);
                }
                if (behind != null) Object.Destroy(behind); if (ownedEvents != null) Object.Destroy(ownedEvents);
                if (data != null) Object.Destroy(data);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                if (pad != null) InputSystem.RemoveDevice(pad);
                InputSystem.settings = oldInput; if (input != null) Object.Destroy(input);
                SetDismissed(oldDismissed); Loc.SetLanguage(oldLanguage); Time.timeScale = oldTime; Random.state = oldRandom;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator ActualOpenShowsFullTextAcrossThreeScalesAndLanguages()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (var language in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
                {
                    Loc.SetLanguage(language); Open(); yield return Settle();
                    AssertLayout(scale); Assert.AreEqual(data.title, Field<Text>("titleText").text);
                    Assert.AreEqual(data.description, Field<Text>("descriptionText").text);
                    for (int i = 0; i < 4; i++) Assert.AreEqual(data.choices[i].label, Labels[i].text);
                    Assert.AreEqual(1000, run.GoldShards); Assert.Zero(resolved);
                }
            }
        }

        [UnityTest]
        public IEnumerator NarrowPausedInputReadsEndWithoutChoosingOrMovingFixedChrome()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); Narrow(); Time.timeScale = 0;
            yield return Settle(); AssertLayout(1.5f); var scroll = Scroll;
            Assert.True(scroll.gameObject.activeInHierarchy); Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height + 1);
            Vector3 titlePosition = Field<Text>("titleText").transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return new WaitForSecondsRealtime(0.12f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f);
            scroll.verticalNormalizedPosition = 1;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.down });
            yield return new WaitForSecondsRealtime(0.12f); InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); scroll.verticalNormalizedPosition = 1;
            var canvas = scroll.GetComponentInParent<Canvas>(); var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, camera));
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) });
            yield return null; Assert.AreSame(mouse, Mouse.current); yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f);
            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases(); Inside(Buttons[3].transform as RectTransform, scroll.viewport);
            Assert.AreEqual(titlePosition, Field<Text>("titleText").transform.position);
            Assert.IsNull(Field<EventChoice>("chosen")); Assert.AreEqual(1000, run.GoldShards); Assert.Zero(resolved);
        }

        [UnityTest]
        public IEnumerator ClearedPointerFocusDoesNotUndoManualReadingOrChooseAReward()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); Narrow(); yield return Settle();
            EventSystem.current.SetSelectedGameObject(Buttons[3].gameObject); yield return Settle();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Settle();
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f, "Home reading must survive a stationary last-choice focus.");
            panel.GetComponent<Canvas>().scaleFactor *= 1.04f; yield return Settle();
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f, "Resizing must preserve deliberate Home reading instead of revealing a hidden selection.");
            Scroll.verticalNormalizedPosition = 0.45f; yield return Settle();
            Assert.AreEqual(0.45f, Scroll.verticalNormalizedPosition, 0.01f);
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
            EventSystem.current.SetSelectedGameObject(null);
            // Model EventSystem pointer deselection; actual wheel polling is exercised separately.
            Scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -1) });
            float position = Scroll.content.anchoredPosition.y; Call("Update");
            Scroll.GetComponentInParent<EventRoomReadability>().SendMessage("LateUpdate");
            yield return Settle(); InputSystem.QueueStateEvent(mouse, new MouseState());
            Assert.AreEqual(position, Scroll.content.anchoredPosition.y, 0.5f);
            Assert.NotNull(EventSystem.current.currentSelectedGameObject);
            Assert.IsNull(Field<EventChoice>("chosen")); Assert.AreEqual(1000, run.GoldShards); Assert.Zero(resolved);
        }
        [UnityTest]
        public IEnumerator SameFirstChoiceFocusRepairDoesNotSuppressNextKeyboardReveal()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); Narrow(); yield return Settle();
            EventSystem.current.SetSelectedGameObject(Buttons[0].gameObject); yield return Settle();
            var helper = Scroll.GetComponentInParent<EventRoomReadability>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Settle();
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f);
            EventSystem.current.SetSelectedGameObject(null); helper.SendMessage("LateUpdate");
            Call("Update"); Assert.AreSame(Buttons[0].gameObject, EventSystem.current.currentSelectedGameObject);
            helper.SendMessage("LateUpdate");
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f, "Restoring the same first choice must preserve reading.");
            yield return null; yield return null;
            // EventSystem navigation selects another actual button; this fixture has no input module.
            EventSystem.current.SetSelectedGameObject(Buttons[3].gameObject); helper.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases(); Inside(Buttons[3].transform as RectTransform, Scroll.viewport);
            Assert.IsNull(Field<EventChoice>("chosen")); Assert.AreEqual(1000, run.GoldShards); Assert.Zero(resolved);
        }
        [UnityTest]
        public IEnumerator SelectionRevealsLastChoiceAndResultResetsReadingToTop()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); Narrow(); yield return Settle();
            EventSystem.current.SetSelectedGameObject(Buttons[3].gameObject); yield return Settle();
            Inside(Buttons[3].transform as RectTransform, Scroll.viewport);
            var selected = EventSystem.current.currentSelectedGameObject;
            panel.GetComponent<Canvas>().scaleFactor *= 0.97f; yield return Settle();
            Assert.AreSame(selected, EventSystem.current.currentSelectedGameObject);
            Inside(Buttons[3].transform as RectTransform, Scroll.viewport);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.25f)); yield return Settle();
            Inside(Buttons[3].transform as RectTransform, Scroll.viewport);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Inside(Buttons[3].transform as RectTransform, Scroll.viewport);
            Assert.AreSame(Buttons[2], Buttons[3].navigation.selectOnUp);
            Buttons[3].onClick.Invoke(); yield return Settle();
            Assert.AreEqual(data.choices[3].resultText, Field<Text>("descriptionText").text);
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f);
            Assert.AreSame(Field<Button>("continueButton").gameObject, EventSystem.current.currentSelectedGameObject);
            foreach (var button in Buttons) Assert.False(button.gameObject.activeSelf);
            AssertLayout(1.5f); Assert.True(EventRoomPanel.IsOpen); Assert.Zero(resolved);
        }

        [UnityTest]
        public IEnumerator DynamicGoldCostLabelsAndEffectsRemainSingleUse()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            data.choices[0].effects = new List<EventEffect> { new() { type = EventEffectType.GoldSpend, amount = 1200 }, new() { type = EventEffectType.GoldGain, amount = 37 } };
            Open(); yield return Settle(); Assert.False(Buttons[0].interactable);
            StringAssert.Contains("1200", Labels[0].text); AssertLayout(1.5f); Buttons[0].onClick.Invoke(); Assert.IsNull(Field<EventChoice>("chosen"));
            run.GainGoldShards(300); yield return Settle(); Assert.True(Buttons[0].interactable); Assert.AreEqual(data.choices[0].label, Labels[0].text);
            Buttons[0].onClick.Invoke(); Buttons[0].onClick.Invoke(); Assert.AreEqual(137, run.GoldShards);
            Field<Button>("continueButton").onClick.Invoke(); Assert.True(EventRoomPanel.IsOpen, "The result frame cannot confirm immediately.");
            yield return null; Field<Button>("continueButton").onClick.Invoke(); Field<Button>("continueButton").onClick.Invoke();
            Assert.False(EventRoomPanel.IsOpen); Assert.AreEqual(1, resolved); Assert.AreEqual(137, run.GoldShards);
            Assert.AreSame(behind, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator WorldPendingDraftClosesBeforeGrantAndConfirmsWithoutLegacySignal()
        {
            Assert.IsNull(Object.FindAnyObjectByType<DraftSessionController>(), "This isolated event-order test cannot alter a live draft session.");
            data.choices[0].effects.Add(new EventEffect { type = EventEffectType.SkillDraft });
            Assert.True(EventRoomPanel.OpenForWorld(data, () => { confirmed++; sequence.Add("confirmed"); })); FindPanel();
            yield return null; Buttons[0].onClick.Invoke(); Assert.AreEqual(1, Field<int>("pendingDrafts")); Assert.AreEqual(oldLevel, run.CurrentLevel);
            yield return null; Field<Button>("continueButton").onClick.Invoke();
            CollectionAssert.AreEqual(new[] { "closed", "level", "confirmed" }, sequence);
            Assert.AreEqual(oldLevel + 1, run.CurrentLevel); Assert.AreEqual(1, confirmed); Assert.Zero(resolved);
            Open(); yield return null; Buttons[0].onClick.Invoke(); yield return null; Field<Button>("continueButton").onClick.Invoke();
            Assert.AreEqual(1, confirmed); Assert.AreEqual(1, resolved);
        }

        [UnityTest]
        public IEnumerator StaleWorldOwnerCancelsPendingDraftEvenWhileUpperModalCapturesInput()
        {
            bool valid = true; data.choices[0].effects.Add(new EventEffect { type = EventEffectType.SkillDraft });
            Assert.True(EventRoomPanel.OpenForWorld(data, () => confirmed++, isOwnerValid: () => valid)); FindPanel();
            yield return null; Buttons[0].onClick.Invoke(); yield return null; valid = false; SetDismissed(Time.frameCount);
            Call("Update"); Assert.False(EventRoomPanel.IsOpen); Assert.Zero(Field<int>("pendingDrafts"));
            Assert.AreEqual(oldLevel, run.CurrentLevel); Assert.Zero(confirmed); Assert.Zero(resolved);
        }

        [UnityTest]
        public IEnumerator CancelOnlyConfirmsResultAndUpperModalCannotChoose()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); Narrow(); yield return Settle();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return null;
            Scroll.verticalNormalizedPosition = 1; float before = Scroll.content.anchoredPosition.y;
            SetDismissed(Time.frameCount);
            Scroll.GetComponentInParent<EventRoomReadability>().SendMessage("LateUpdate");
            Assert.AreEqual(before, Scroll.content.anchoredPosition.y, 0.01f, "The upper modal also owns reading input.");
            Buttons[0].onClick.Invoke();
            Assert.IsNull(Field<EventChoice>("chosen")); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Assert.True(EventRoomPanel.IsOpen); Assert.IsNull(Field<EventChoice>("chosen"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Buttons[0].onClick.Invoke(); yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null;
            Assert.False(EventRoomPanel.IsOpen); Assert.AreEqual(1, resolved);
        }

        [UnityTest]
        public IEnumerator ShortScaleRoundtripAndReopenRemoveOldChoicesWithoutDuplicatingListeners()
        {
            data.title = "Title"; data.description = "Short";
            foreach (var choice in data.choices) choice.label = "Choose";
            Open(); yield return Settle();
            var art = PanelRect.Find("EventArtwork").GetComponent<Image>(); var sprite = art.sprite; Vector2 artSize = art.rectTransform.rect.size;
            float readingScale = savedCanvas.scaleFactor;
            bool readingScalerEnabled = savedCanvas.GetComponent<CanvasScaler>().enabled;
            Vector2 readingCanvasSize = ((RectTransform)savedCanvas.transform).rect.size;
            Assert.True(art.enabled); Assert.True(art.preserveAspect); Assert.False(art.raycastTarget);
            Narrow(); yield return Settle(); Assert.False(art.enabled);
            panel.GetComponent<CanvasScaler>().enabled = readingScalerEnabled; savedCanvas.scaleFactor = readingScale; yield return Settle();
            Assert.AreEqual(readingCanvasSize.x, ((RectTransform)savedCanvas.transform).rect.width, 0.01f);
            Assert.AreEqual(readingCanvasSize.y, ((RectTransform)savedCanvas.transform).rect.height, 0.01f);
            Assert.True(art.enabled); Assert.AreSame(sprite, art.sprite); Assert.AreEqual(artSize.x, art.rectTransform.rect.width, 0.01f); Assert.AreEqual(artSize.y, art.rectTransform.rect.height, 0.01f);
            Vector2 size = PanelRect.rect.size; var callbacks = Buttons.Select(button => button.onClick).ToArray();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Assert.AreEqual(size.x, PanelRect.rect.width, 0.01f); Assert.AreEqual(size.y, PanelRect.rect.height, 0.01f);
            data.choices.RemoveRange(1, 3); Open(); yield return Settle();
            for (int i = 0; i < 4; i++) { Assert.AreSame(callbacks[i], Buttons[i].onClick); Assert.AreEqual(i == 0, Buttons[i].gameObject.activeSelf); }
            Assert.IsNull(Buttons[0].navigation.selectOnDown); AssertLayout(1);
            Buttons[0].onClick.Invoke(); yield return null; Field<Button>("continueButton").onClick.Invoke(); Assert.AreEqual(1, resolved);
        }

        private void FillLongData()
        {
            data.title = "오래된 계약과 선택의 대가 Long choices and consequences 選択と代償の記録";
            data.description = string.Join("\n", Enumerable.Repeat("설명을 끝까지 읽고 선택하세요. Read every consequence before choosing. 最後まで説明を読んでください。", 8));
            for (int i = 0; i < 4; i++) data.choices.Add(new EventChoice
            {
                label = "긴 선택지의 조건과 대가 Long choice terms and consequences 条件と代償を確認する " + i,
                resultText = string.Join("\n", Enumerable.Repeat("선택 결과를 마지막까지 읽습니다. Read the entire result. 結果を最後まで確認します。", 12))
            });
        }
        private void Open() { EventRoomPanel.Open(data); FindPanel(); }
        private void FindPanel()
        {
            panel = Object.FindAnyObjectByType<EventRoomPanel>();
            if (savedCanvas == null)
            {
                savedCanvas = panel.GetComponent<Canvas>(); oldCanvasScale = savedCanvas.scaleFactor;
                oldScalerEnabled = savedCanvas.GetComponent<CanvasScaler>().enabled;
            }
        }
        private List<Button> Buttons => Field<List<Button>>("choiceButtons");
        private List<Text> Labels => Field<List<Text>>("choiceLabels");
        private RectTransform PanelRect => panel.transform.Find("Body/Panel") as RectTransform;
        private ScrollRect Scroll => panel.GetComponentsInChildren<ScrollRect>(true).Single();
        private void Narrow()
        {
            panel.GetComponent<CanvasScaler>().enabled = false;
            panel.GetComponent<Canvas>().scaleFactor = Mathf.Max(Screen.width / 640f, Screen.height / 520f);
            Canvas.ForceUpdateCanvases();
        }
        private void AssertLayout(float scale)
        {
            AssertText(Field<Text>("titleText"), Mathf.RoundToInt(26 * scale));
            AssertText(Field<Text>("descriptionText"), Mathf.RoundToInt(19 * scale));
            Inside(PanelRect, panel.transform as RectTransform); Inside(Field<Text>("titleText").rectTransform, PanelRect);
            foreach (var label in Labels.Where(label => label.gameObject.activeInHierarchy))
            {
                AssertText(label, Mathf.RoundToInt(19 * scale)); Inside(label.rectTransform, label.transform.parent as RectTransform);
            }
            if (Field<GameObject>("continueRoot").activeSelf)
            {
                AssertText(Field<Text>("continueLabel"), Mathf.RoundToInt(20 * scale));
                Inside(Field<Button>("continueButton").transform as RectTransform, PanelRect);
            }
            var viewport = Scroll.viewport;
            Assert.LessOrEqual(Bounds(viewport, PanelRect).yMax, Bounds(Field<Text>("titleText").rectTransform, PanelRect).yMin + 1);
            if (Field<GameObject>("continueRoot").activeSelf)
                Assert.GreaterOrEqual(Bounds(viewport, PanelRect).yMin, Bounds(Field<Button>("continueButton").transform as RectTransform, PanelRect).yMax - 1);
            var active = Buttons.Where(button => button.gameObject.activeSelf).ToArray();
            for (int i = 1; i < active.Length; i++)
                Assert.LessOrEqual(Bounds(active[i].transform as RectTransform, PanelRect).yMax, Bounds(active[i - 1].transform as RectTransform, PanelRect).yMin + 1);
        }
        private static void AssertText(Text text, int size)
        {
            Assert.AreEqual(size, text.fontSize); Assert.False(text.resizeTextForBestFit);
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1, text.name);
        }
        private static Rect Bounds(RectTransform rect, Transform parent)
        {
            var points = new Vector3[4]; rect.GetWorldCorners(points); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var point in points) { Vector2 p = parent.InverseTransformPoint(point); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void Inside(RectTransform child, RectTransform parent)
        {
            var rect = Bounds(child, parent); Assert.GreaterOrEqual(rect.xMin, parent.rect.xMin - 1); Assert.LessOrEqual(rect.xMax, parent.rect.xMax + 1);
            Assert.GreaterOrEqual(rect.yMin, parent.rect.yMin - 1); Assert.LessOrEqual(rect.yMax, parent.rect.yMax + 1);
        }
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
        private void OnClosed() => sequence.Add("closed");
        private void OnResolved() { resolved++; sequence.Add("resolved"); }
        private void OnLevel(int level, DraftTriggerReason reason) { Assert.False(EventRoomPanel.IsOpen); sequence.Add("level"); }
        private static void SetDismissed(int frame) => typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, frame);
        private T Field<T>(string name) => (T)typeof(EventRoomPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private void Call(string name) => typeof(EventRoomPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
        private void CallBase(string name) => typeof(RunModalPanel<EventRoomPanel>).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
    }
}
