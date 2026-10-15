using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    public sealed class NodeMapReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<RoomData> rooms = new();
        private NodeMapPanel panel;
        private RunManager run;
        private EventData eventData;
        private bool ownsPanel, ownsRun, runEnabled, runActive, oldScalerEnabled, oldNavigation;
        private int oldGold, pickedCount, oldDismissed;
        private RoomData pickedRoom;
        private Canvas savedCanvas;
        private float oldCanvasScale, oldTime;
        private LocalizationLanguage oldLanguage;
        private Random.State oldRandom;
        private GameObject ownedEvents, oldSelection;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private InputSettings oldInput, input;
        private Action closeHandler;
        private readonly Dictionary<Image, (Sprite sprite, Vector2 size, Color color, bool raycast)> icons = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); oldLanguage = Loc.CurrentLanguage; oldTime = Time.timeScale; oldRandom = Random.state;
            Assert.False(NodeMapPanel.IsOpen, "Do not replace an existing open route.");
            ownsPanel = Object.FindAnyObjectByType<NodeMapPanel>() == null;
            ownsRun = !RunManager.HasInstance; run = RunManager.Instance;
            oldGold = run.GoldShards; runEnabled = run.enabled; runActive = run.IsRunActive;
            run.enabled = false; Set(run, "isRunActive", true); run.GainGoldShards(1000 - run.GoldShards);
            oldDismissed = (int)typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            SetDismissed(-1);
            oldInput = InputSystem.settings; input = Object.Instantiate(oldInput);
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = input;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("NodeReadingEvents", typeof(EventSystem));
            oldSelection = EventSystem.current.currentSelectedGameObject; oldNavigation = EventSystem.current.sendNavigationEvents;
            for (int i = 0; i < 3; i++)
            {
                var room = ScriptableObject.CreateInstance<RoomData>(); room.roomId = "node_readability_" + i;
                room.roomType = i == 0 ? RoomType.Combat : i == 1 ? RoomType.Elite : RoomType.Rest;
                room.displayName = "긴 목적지 이름 Long destination title 長い行き先の名前 " + i;
                room.hint = string.Join("\n", Enumerable.Repeat("이 길의 보상과 위험을 끝까지 읽습니다. Read every consequence of this route. この道の報酬と危険を最後まで読みます。", 6));
                rooms.Add(room);
            }
            pickedCount = 0; pickedRoom = null; icons.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (closeHandler != null) GameEvents.OnDraftClosed -= closeHandler;
                if (panel != null && NodeMapPanel.IsOpen) CallBase("HideBody");
                if (savedCanvas != null && !ownsPanel)
                {
                    savedCanvas.GetComponent<CanvasScaler>().enabled = oldScalerEnabled; savedCanvas.scaleFactor = oldCanvasScale;
                }
                if (ownsPanel && panel != null) Object.Destroy(panel.gameObject);
                if (run != null)
                {
                    run.GainGoldShards(oldGold - run.GoldShards); Set(run, "isRunActive", runActive); run.enabled = runEnabled;
                    if (ownsRun) Object.Destroy(run.gameObject);
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = oldNavigation;
                    EventSystem.current.SetSelectedGameObject(oldSelection != null && oldSelection.activeInHierarchy ? oldSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                foreach (var room in rooms) if (room != null) Object.Destroy(room); rooms.Clear();
                if (eventData != null) Object.Destroy(eventData);
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
        public IEnumerator ActualTwoAndThreeNodeShowsKeepFullTextAtEveryScaleAndLocale()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (var language in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
                {
                    Loc.SetLanguage(language);
                    foreach (int count in new[] { 2, 3 })
                    {
                        Open(count); yield return Settle(); CaptureCanvas(); AssertLayout(scale); AssertIcons();
                        for (int i = 0; i < count; i++)
                        {
                            Assert.AreEqual(RoomTypeDisplay.Headline(rooms[i].roomType), Types[i].text); Assert.AreEqual(rooms[i].ChoiceTitle, Titles[i].text); Assert.AreEqual(rooms[i].hint, Hints[i].text);
                            Assert.AreSame(i > 0 ? Buttons[i - 1] : null, Buttons[i].navigation.selectOnLeft);
                            Assert.AreSame(i + 1 < count ? Buttons[i + 1] : null, Buttons[i].navigation.selectOnRight);
                            Assert.IsNull(Buttons[i].navigation.selectOnUp); Assert.IsNull(Buttons[i].navigation.selectOnDown);
                        }
                        Assert.Zero(pickedCount); Assert.AreEqual(1000, run.GoldShards);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualPreviewRebindsMoneyAndLanguageWithoutApplyingEffects()
        {
            eventData = ScriptableObject.CreateInstance<EventData>(); eventData.title = "Preview";
            eventData.choices.Add(new EventChoice { label = "Offer", effects = new List<EventEffect>
                { new() { type = EventEffectType.GoldSpend, amount = 1200 }, new() { type = EventEffectType.GoldGain, amount = 37 } } });
            rooms[0].roomId = "room3_event_brokenaltar"; rooms[0].roomType = RoomType.Event; rooms[0].eventData = eventData;
            Assert.True(rooms[0].IsEventRoom);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); yield return Settle(); CaptureCanvas();
            string low = Hints[0].text;
            foreach (var language in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(language); run.GainGoldShards(1000 - run.GoldShards); yield return Settle();
                Assert.AreEqual(ExpeditionRoomPreview.Detail(rooms[0]), Hints[0].text); AssertLayout(1.5f);
                string poor = Hints[0].text; run.GainGoldShards(300); yield return Settle();
                Assert.AreEqual(ExpeditionRoomPreview.Detail(rooms[0]), Hints[0].text);
                Assert.AreNotEqual(poor, Hints[0].text); Assert.AreEqual(1300, run.GoldShards); Assert.Zero(pickedCount);
            }
            Assert.IsNotEmpty(low);
        }

        [UnityTest]
        public IEnumerator NarrowPausedPagePadAndWheelReadFullHintsWithoutPicking()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); yield return Settle(); CaptureCanvas(); Narrow();
            Header.text = "다음 길 Long route 次の道"; Time.timeScale = 0; yield return Settle(); AssertLayout(1.5f);
            Assert.Greater(Scroll.content.rect.height, Scroll.viewport.rect.height + 1);
            Vector3 title = Header.transform.position;
            Scroll.verticalNormalizedPosition = 1;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Assert.Less(Scroll.verticalNormalizedPosition, 0.999f);
            Scroll.verticalNormalizedPosition = 1;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.down }); yield return new WaitForSecondsRealtime(0.12f);
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; Assert.Less(Scroll.verticalNormalizedPosition, 0.999f);
            Scroll.verticalNormalizedPosition = 1;
            var canvas = Scroll.GetComponentInParent<Canvas>(); var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, Scroll.viewport.TransformPoint(Scroll.viewport.rect.center));
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(Scroll.viewport, point, camera));
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) });
            yield return null; Assert.AreSame(mouse, Mouse.current); yield return null; Assert.Less(Scroll.verticalNormalizedPosition, 0.999f);
            Scroll.verticalNormalizedPosition = 0; Scroll.horizontalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
            Assert.LessOrEqual(Bounds(Hints[2].rectTransform, Scroll.viewport).yMin, Scroll.viewport.rect.yMax);
            Assert.GreaterOrEqual(Bounds(Hints[2].rectTransform, Scroll.viewport).yMin, Scroll.viewport.rect.yMin - 1);
            Assert.AreEqual(title, Header.transform.position); Assert.AreEqual(1000, run.GoldShards); Assert.Zero(pickedCount);
        }

        [UnityTest]
        public IEnumerator NarrowShiftPageAndRightStickReadOtherColumnsWithoutPicking()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); yield return Settle(); CaptureCanvas(); Narrow(); yield return Settle();
            Time.timeScale = 0; Assert.Greater(Scroll.content.rect.width, Scroll.viewport.rect.width + 1);
            Scroll.horizontalNormalizedPosition = 0; Scroll.verticalNormalizedPosition = 1;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.PageDown)); yield return null; yield return null;
            Assert.Greater(Scroll.horizontalNormalizedPosition, 0.001f); Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Scroll.horizontalNormalizedPosition = 0;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right }); yield return new WaitForSecondsRealtime(0.12f);
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; Assert.Greater(Scroll.horizontalNormalizedPosition, 0.001f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.End)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Settle();
            Assert.AreEqual(1f, Scroll.horizontalNormalizedPosition, 0.01f); Assert.AreEqual(0f, Scroll.verticalNormalizedPosition, 0.01f);
            Assert.Zero(pickedCount); Assert.AreEqual(1000, run.GoldShards);
        }
        [UnityTest]
        public IEnumerator VisibleSelectedNodeSurvivesReflowButHomeReadingDoesNotSnapBack()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); yield return Settle(); CaptureCanvas(); Narrow(); yield return Settle();
            EventSystem.current.SetSelectedGameObject(Buttons[2].gameObject); yield return Settle();
            AssertSelectedVisible(Buttons[2]);
            Scroll.verticalNormalizedPosition = 0.35f; yield return Settle();
            savedCanvas.scaleFactor *= 0.97f; yield return Settle(); AssertHorizontallyVisible(Buttons[2]);
            Assert.AreEqual(0.35f, Scroll.verticalNormalizedPosition, 0.01f, "Resizing must preserve the paragraph being read.");
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.25f)); yield return Settle(); AssertHorizontallyVisible(Buttons[2]);
            Assert.AreEqual(0.35f, Scroll.verticalNormalizedPosition, 0.01f, "Scaling must not pull reading back to the card top.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Settle();
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f);
            Assert.AreEqual(0f, Scroll.horizontalNormalizedPosition, 0.01f);
            savedCanvas.scaleFactor *= 1.04f; yield return Settle(); Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f);
            Assert.AreEqual(0f, Scroll.horizontalNormalizedPosition, 0.01f);
            Assert.Zero(pickedCount);
        }

        [UnityTest]
        public IEnumerator SameFirstNodeNullRepairPreservesReadingAndNextNavigationReveals()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); Open(); yield return Settle(); CaptureCanvas(); Narrow(); yield return Settle();
            EventSystem.current.SetSelectedGameObject(Buttons[0].gameObject); yield return Settle();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Settle();
            EventSystem.current.SetSelectedGameObject(null); HelperLate(); Call("Update"); HelperLate();
            Assert.AreSame(Buttons[0].gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(1f, Scroll.verticalNormalizedPosition, 0.01f); yield return null; yield return null;
            // Model EventSystem's explicit horizontal navigation without invoking a reward callback.
            EventSystem.current.SetSelectedGameObject(Buttons[2].gameObject); HelperLate(); Canvas.ForceUpdateCanvases();
            AssertSelectedVisible(Buttons[2]); Assert.Zero(pickedCount);
        }

        [UnityTest]
        public IEnumerator OpenFrameUpperModalAndCancelNeverPickButActualClickPicksOnce()
        {
            Open(); Buttons[0].onClick.Invoke(); Assert.Zero(pickedCount); yield return Settle(); CaptureCanvas();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null;
            Assert.True(NodeMapPanel.IsOpen); Assert.Zero(pickedCount);
            InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return null;
            Scroll.verticalNormalizedPosition = 1; float y = Scroll.content.anchoredPosition.y;
            SetDismissed(Time.frameCount); HelperLate(); Buttons[1].onClick.Invoke();
            Assert.AreEqual(y, Scroll.content.anchoredPosition.y, 0.01f); Assert.Zero(pickedCount);
            yield return null; Buttons[1].onClick.Invoke(); Buttons[1].onClick.Invoke();
            Assert.False(NodeMapPanel.IsOpen); Assert.AreEqual(1, pickedCount); Assert.AreSame(rooms[1], pickedRoom);
        }

        [UnityTest]
        public IEnumerator DraftClosedReentrantOpenKeepsNewOptionsAndCallback()
        {
            Open(); yield return Settle(); CaptureCanvas(); int second = 0; bool didReopen = false;
            closeHandler = () =>
            {
                if (didReopen) return; didReopen = true;
                NodeMapPanel.Open(new[] { rooms[1], rooms[2] }, room => { Assert.AreSame(rooms[2], room); second++; });
            };
            GameEvents.OnDraftClosed += closeHandler;
            Buttons[0].onClick.Invoke(); Assert.AreEqual(1, pickedCount); Assert.True(NodeMapPanel.IsOpen);
            Assert.AreEqual(rooms[1].ChoiceTitle, Titles[0].text); yield return null;
            Buttons[1].onClick.Invoke(); Assert.AreEqual(1, second); Assert.AreEqual(1, pickedCount); Assert.False(NodeMapPanel.IsOpen);
        }

        [UnityTest]
        public IEnumerator ZeroOneOptionsSkipUiAndReopeningTwoHidesThirdWithoutReplacingCallbacks()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[NodeMapPanel\\].*유효한 선택지가 없다"));
            NodeMapPanel.Open(null, room => { Assert.IsNull(room); pickedCount++; });
            NodeMapPanel.Open(new[] { null, rooms[0] }, room => { Assert.AreSame(rooms[0], room); pickedCount++; });
            Assert.AreEqual(2, pickedCount); Assert.False(NodeMapPanel.IsOpen); pickedCount = 0;
            foreach (var room in rooms) { room.displayName = "Short"; room.hint = "Short hint"; }
            Open(); yield return Settle(); CaptureCanvas(); var callbacks = Buttons.Select(button => button.onClick).ToArray(); Vector2 size = PanelRect.rect.size;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f)); yield return Settle();
            Assert.AreEqual(size.x, PanelRect.rect.width, 0.01f); Assert.AreEqual(size.y, PanelRect.rect.height, 0.01f);
            Open(2); yield return Settle(); Assert.False(Buttons[2].gameObject.activeSelf); Assert.IsNull(Buttons[1].navigation.selectOnRight);
            for (int i = 0; i < 3; i++) Assert.AreSame(callbacks[i], Buttons[i].onClick);
            AssertLayout(1);
        }

        private void Open(int count = 3) { NodeMapPanel.Open(rooms.Take(count).ToArray(), OnPicked); panel = Object.FindAnyObjectByType<NodeMapPanel>(); }
        private void OnPicked(RoomData room) { pickedCount++; pickedRoom = room; }
        private void CaptureCanvas()
        {
            if (savedCanvas != null) return;
            savedCanvas = panel.GetComponent<Canvas>(); oldCanvasScale = savedCanvas.scaleFactor;
            oldScalerEnabled = savedCanvas.GetComponent<CanvasScaler>().enabled;
        }
        private void Narrow()
        {
            savedCanvas.GetComponent<CanvasScaler>().enabled = false;
            savedCanvas.scaleFactor = Mathf.Max(Screen.width / 640f, Screen.height / 520f); Canvas.ForceUpdateCanvases();
        }
        private List<Button> Buttons => Field<List<Button>>("nodeButtons");
        private List<Text> Titles => Field<List<Text>>("nodeTitles");
        private List<Text> Types => Field<List<Text>>("nodeTypes");
        private List<Text> Hints => Field<List<Text>>("nodeHints");
        private RectTransform PanelRect => panel.transform.Find("Body/Panel") as RectTransform;
        private Text Header => PanelRect.Find("TitleText").GetComponent<Text>();
        private ScrollRect Scroll => panel.GetComponentsInChildren<ScrollRect>(true).Single();
        private void HelperLate() => Scroll.GetComponentInParent<NodeMapReadability>().SendMessage("LateUpdate");
        private void AssertLayout(float scale)
        {
            AssertText(Header, Mathf.RoundToInt(40 * scale)); Inside(Header.rectTransform, PanelRect); Inside(PanelRect, panel.transform as RectTransform);
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (!Buttons[i].gameObject.activeSelf) continue;
                AssertText(Types[i], Mathf.RoundToInt(28 * scale)); AssertText(Titles[i], Mathf.RoundToInt(28 * scale)); AssertText(Hints[i], Mathf.RoundToInt(24 * scale));
                var rect = Buttons[i].transform as RectTransform; Inside(rect, Scroll.content); Inside(Types[i].rectTransform, rect); Inside(Titles[i].rectTransform, rect); Inside(Hints[i].rectTransform, rect);
                Assert.LessOrEqual(Bounds(Titles[i].rectTransform, rect).yMax, Bounds(Types[i].rectTransform, rect).yMin + 1);
                Assert.LessOrEqual(Bounds(Hints[i].rectTransform, rect).yMax, Bounds(Titles[i].rectTransform, rect).yMin + 1);
            }
            Assert.LessOrEqual(Bounds(Scroll.viewport, PanelRect).yMax, Bounds(Header.rectTransform, PanelRect).yMin + 1);
            Assert.NotNull(Scroll.viewport.GetComponent<RectMask2D>());
            Assert.AreEqual(Navigation.Mode.None, Scroll.verticalScrollbar.navigation.mode);
            Assert.AreEqual(Navigation.Mode.None, Scroll.horizontalScrollbar.navigation.mode);
        }
        private void AssertSelectedVisible(Button button)
        {
            var bounds = Bounds(button.transform as RectTransform, Scroll.viewport);
            AssertHorizontallyVisible(button);
            if (bounds.height <= Scroll.viewport.rect.height + 1) Inside(button.transform as RectTransform, Scroll.viewport);
            else
            {
                Assert.LessOrEqual(bounds.yMax, Scroll.viewport.rect.yMax + 1);
                Assert.GreaterOrEqual(bounds.yMax, Scroll.viewport.rect.yMax - 64, "The readable top includes the original route-icon clearance.");
            }
        }
        private void AssertHorizontallyVisible(Button button)
        {
            var bounds = Bounds(button.transform as RectTransform, Scroll.viewport);
            Assert.GreaterOrEqual(bounds.xMin, Scroll.viewport.rect.xMin - 1);
            Assert.LessOrEqual(bounds.xMax, Scroll.viewport.rect.xMax + 1);
        }
        private void AssertIcons()
        {
            foreach (var icon in panel.GetComponentsInChildren<Image>(true).Where(image => image.name == "ArtRouteIcon" && image.sprite != null))
            {
                if (!icons.TryGetValue(icon, out var expected))
                {
                    icons.Add(icon, (icon.sprite, icon.rectTransform.rect.size, icon.color, icon.raycastTarget));
                    continue;
                }
                Assert.AreSame(expected.sprite, icon.sprite);
                Assert.AreEqual(expected.size.x, icon.rectTransform.rect.width, 0.01f);
                Assert.AreEqual(expected.size.y, icon.rectTransform.rect.height, 0.01f);
                Assert.AreEqual(expected.color, icon.color); Assert.AreEqual(expected.raycast, icon.raycastTarget);
            }
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
            var bounds = Bounds(child, parent); Assert.GreaterOrEqual(bounds.xMin, parent.rect.xMin - 1); Assert.LessOrEqual(bounds.xMax, parent.rect.xMax + 1);
            Assert.GreaterOrEqual(bounds.yMin, parent.rect.yMin - 1); Assert.LessOrEqual(bounds.yMax, parent.rect.yMax + 1);
        }
        private static IEnumerator Settle() { yield return null; yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
        private static void SetDismissed(int frame) => typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, frame);
        private T Field<T>(string name) => (T)typeof(NodeMapPanel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private void Call(string name) => typeof(NodeMapPanel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
        private void CallBase(string name) => typeof(RunModalPanel<NodeMapPanel>).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
    }
}
