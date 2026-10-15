using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Abyss.Tests.PlayMode
{
    public sealed class SaveStatusReadabilityPlayModeTests
    {
        private sealed class ControlledFiles : SaveFileOperations
        {
            public string LockedPath;
            public bool FailWrite;
            public int Reads, Writes;
            public override string ReadAllText(string path)
            {
                Reads++; if (path == LockedPath) throw new IOException("save status readability injected lock");
                return base.ReadAllText(path);
            }
            public override void WriteAllText(string path, string content)
            {
                Writes++; if (FailWrite) throw new IOException("save status readability injected write");
                base.WriteAllText(path, content);
            }
        }
        private readonly PlayModeSaveGuard guard = new();
        private SaveStatusOverlay overlay, previousOverlay;
        private MetaSaveService service, previousService;
        private bool previousOverlayActive;
        private Action previousStatusListener, previousWriteListener;
        private int previousDismissed;
        private ControlledFiles files;
        private string savePath;
        private GameObject modal, badge, toast;
        private RectTransform panel;
        private Text title, mainText, pathText, retryText, badgeText, toastText;
        private Button retry, cont;
        private Button.ButtonClickedEvent retryEvent, continueEvent;
        private Navigation retryNavigation, continueNavigation;
        private List<object> retryListeners, continueListeners;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings previousInputSettings, testInputSettings;
        private GameObject ownedEvents, previousSelection, behindButton;
        private bool previousNavigation;
        private LocalizationLanguage language;
        private Random.State random;
        private float timeScale;
        private static readonly FieldInfo InstanceField = typeof(SaveStatusOverlay).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo DismissedField = typeof(SaveStatusOverlay).GetField("dismissedFrame", BindingFlags.Static | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            language = Loc.CurrentLanguage; random = Random.state; timeScale = Time.timeScale;
            if (EventSystem.current != null)
            { previousSelection = EventSystem.current.currentSelectedGameObject; previousNavigation = EventSystem.current.sendNavigationEvents; }
            previousOverlay = (SaveStatusOverlay)InstanceField.GetValue(null); previousDismissed = (int)DismissedField.GetValue(null);
            if (previousOverlay != null)
            {
                previousOverlayActive = previousOverlay.gameObject.activeSelf; previousService = Field<MetaSaveService>(previousOverlay, "service");
                previousStatusListener = (Action)Delegate.CreateDelegate(typeof(Action), previousOverlay, Method("HandleSaveStatusChanged"));
                previousWriteListener = (Action)Delegate.CreateDelegate(typeof(Action), previousOverlay, Method("HandleSaveWriteFailed"));
                if (previousService != null) { previousService.OnSaveStatusChanged -= previousStatusListener; previousService.OnSaveWriteFailed -= previousWriteListener; }
                previousOverlay.gameObject.SetActive(false);
            }
            InstanceField.SetValue(null, null); DismissedField.SetValue(null, -1); guard.Acquire();
            service = MetaSaveService.Instance; Assert.True(service.TrySaveMenuTextScale(1f));
            savePath = SaveSystem.Instance.GetFilePath(MetaSave.FileName);
            Assert.True(Path.GetFullPath(savePath).StartsWith(Path.GetFullPath(guard.TestDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            files = new ControlledFiles(); SaveSystem.Instance.SetFileOperations(files);
            previousInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings; keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            if (EventSystem.current == null) ownedEvents = new GameObject("SaveStatusReadabilityEvents", typeof(EventSystem));
            if (ownedEvents != null) previousNavigation = EventSystem.current.sendNavigationEvents;
            behindButton = new GameObject("SaveStatusPreviousFocus", typeof(RectTransform), typeof(Button)); EventSystem.current.SetSelectedGameObject(behindButton);
            SaveStatusOverlay.Ensure(); overlay = (SaveStatusOverlay)InstanceField.GetValue(null); overlay.enabled = false;
            modal = Field<GameObject>(overlay, "modalBody"); panel = modal.transform.Find("Panel") as RectTransform;
            title = panel.Find("TitleText").GetComponent<Text>(); mainText = panel.GetComponentsInChildren<Text>(true).Single(text => text.name == "BodyText");
            pathText = Field<Text>(overlay, "pathLabel"); retryText = Field<Text>(overlay, "retryResultLabel");
            retry = Field<Button>(overlay, "retryButton"); cont = panel.Find("ContinueButton").GetComponent<Button>();
            badge = Field<GameObject>(overlay, "badge"); badgeText = badge.transform.Find("Text").GetComponent<Text>();
            toast = Field<GameObject>(overlay, "toast"); toastText = Field<Text>(overlay, "toastLabel");
            retryEvent = retry.onClick; continueEvent = cont.onClick; retryNavigation = retry.navigation; continueNavigation = cont.navigation;
            retryListeners = RuntimeListeners(retryEvent); continueListeners = RuntimeListeners(continueEvent);
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (overlay != null) Object.Destroy(overlay.gameObject);
                if (behindButton != null) Object.Destroy(behindButton);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse); if (gamepad != null) InputSystem.RemoveDevice(gamepad);
                InputSystem.settings = previousInputSettings; if (testInputSettings != null) Object.Destroy(testInputSettings);
                yield return null;
                Loc.SetLanguage(language); Time.timeScale = timeScale; Random.state = random;
            }
            finally
            {
                guard.Release(); InstanceField.SetValue(null, previousOverlay != null ? previousOverlay : null); DismissedField.SetValue(null, previousDismissed);
                if (previousService != null) { previousService.OnSaveStatusChanged += previousStatusListener; previousService.OnSaveWriteFailed += previousWriteListener; }
                if (previousOverlay != null) previousOverlay.gameObject.SetActive(previousOverlayActive);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
            }
        }

        [UnityTest]
        public IEnumerator UnblockedLayoutFixtureShowsFullModalTextAtAllScalesAndLocalesWithoutIoOnRefresh()
        {
            var icons = IconSizes();
            foreach (var lang in new[] { LocalizationLanguage.Korean, LocalizationLanguage.English, LocalizationLanguage.Japanese })
            {
                Loc.SetLanguage(lang);
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    Assert.True(service.TrySaveMenuTextScale(scale)); ShowGeometryOnly(); LongText();
                    int reads = files.Reads, writes = files.Writes; yield return Settle();
                    AssertLayout(scale); AssertBindings(); AssertIcons(icons);
                    Assert.AreEqual(reads, files.Reads); Assert.AreEqual(writes, files.Writes);
                    Assert.False(service.IsSaveBlocked, "This is UI-only geometry; no retry/continue callbacks run against an unblocked service.");
                }
            }
        }

        [UnityTest]
        public IEnumerator NarrowOverlayPausedPagePadAndWheelScrollFullModalBodyWithoutMovingChrome()
        {
            Assert.True(service.TrySaveMenuTextScale(1.5f)); NarrowCanvas(); ShowGeometryOnly(); LongText(); Time.timeScale = 0f;
            yield return Settle(); AssertLayout(1.5f);
            var scroll = modal.GetComponentInChildren<ScrollRect>(true); Assert.NotNull(scroll); Assert.True(scroll.gameObject.activeInHierarchy);
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height); Assert.NotNull(scroll.viewport.GetComponent<RectMask2D>());
            Vector3 heading = title.transform.position, buttonPosition = cont.transform.position;
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown)); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down }); yield return new WaitForSecondsRealtime(0.12f);
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            scroll.verticalNormalizedPosition = 1f;
            var corners = new Vector3[4]; scroll.viewport.GetWorldCorners(corners);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
            Assert.True(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, null)); mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, -120) }); yield return null;
            Assert.AreSame(mouse, Mouse.current); AssertVector(point, mouse.position.ReadValue()); yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.999f); InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            scroll.verticalNormalizedPosition = 0f; yield return Settle(); Inside(retryText.rectTransform, scroll.viewport);
            AssertVector3(heading, title.transform.position); AssertVector3(buttonPosition, cont.transform.position); AssertBindings();
        }

        [UnityTest]
        public IEnumerator RepeatedAttachAndShortTextScaleRoundtripPreserveOriginalReferencesGeometryAndNavigation()
        {
            ShowGeometryOnly(); mainText.text = "Save access"; pathText.text = "Temp/save.json"; retryText.text = ""; yield return Settle();
            Vector2 size = panel.rect.size, position = panel.anchoredPosition;
            var helper = SaveStatusReadability.Attach(overlay.transform); Assert.NotNull(helper);
            for (int i = 0; i < 3; i++)
            {
                Assert.True(service.TrySaveMenuTextScale(1.5f)); Assert.AreSame(helper, SaveStatusReadability.Attach(overlay.transform)); yield return Settle(); AssertLayout(1.5f);
                Assert.True(service.TrySaveMenuTextScale(1f)); yield return Settle();
                AssertVector(size, panel.rect.size); AssertVector(position, panel.anchoredPosition); AssertLayout(1f); AssertBindings();
                Assert.AreSame(pathText, Field<Text>(overlay, "pathLabel")); Assert.AreSame(retryText, Field<Text>(overlay, "retryResultLabel"));
            }
        }

        [UnityTest]
        public IEnumerator ActualBlockedContinueKeepsBadgeCapturesDismissalFrameAndRestoresLivingFocus()
        {
            BlockActualSave(); Assert.True(SaveStatusOverlay.IsModalOpen); Assert.True(SaveStatusOverlay.IsCapturingInput);
            cont.onClick.Invoke();
            Assert.False(SaveStatusOverlay.IsModalOpen); Assert.True(SaveStatusOverlay.IsBadgeVisible); Assert.True(SaveStatusOverlay.IsCapturingInput);
            Assert.AreSame(behindButton, EventSystem.current.currentSelectedGameObject);
            int reads = files.Reads, writes = files.Writes;
            Assert.False(service.TrySaveMenuTextScale(1.5f)); Assert.AreEqual(reads, files.Reads); Assert.AreEqual(writes, files.Writes);
            yield return null; Assert.False(SaveStatusOverlay.IsCapturingInput);
            Assert.True(service.IsSaveBlocked); Assert.True(SaveStatusOverlay.IsBadgeVisible);
        }

        [UnityTest]
        public IEnumerator ActualEscapeAndPadBConsumeOneDismissalAndDoNotTriggerAutomaticRetry()
        {
            BlockActualSave();
            foreach (bool pad in new[] { false, true })
            {
                Set(overlay, "isModalDismissed", false); Call("ApplyBlockedState"); Call("FocusModal");
                int reads = files.Reads, writes = files.Writes;
                if (pad) InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
                else InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; Call("UpdateModalInput"); Call("Update");
                Assert.False(SaveStatusOverlay.IsModalOpen); Assert.True(SaveStatusOverlay.IsBadgeVisible); Assert.True(SaveStatusOverlay.IsCapturingInput);
                Assert.AreSame(behindButton, EventSystem.current.currentSelectedGameObject);
                Assert.AreEqual(reads, files.Reads); Assert.AreEqual(writes, files.Writes);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                Assert.False(SaveStatusOverlay.IsCapturingInput);
            }
        }

        [UnityTest]
        public IEnumerator ActualExplicitRetryFailureKeepsModalAndSuccessfulRetryReloadsOnlyGuardedTemporarySave()
        {
            service.AddAbyssShards(42, autoSave: false); Assert.True(service.Save());
            BlockActualSave(); ExpectBlockedErrors(); retry.onClick.Invoke();
            Assert.True(service.IsSaveBlocked); Assert.True(SaveStatusOverlay.IsModalOpen); Assert.AreEqual(Loc.Get(StringKey.SaveStatus_RetryFailed), retryText.text);
            int reads = files.Reads, writes = files.Writes; yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(reads, files.Reads); Assert.AreEqual(writes, files.Writes);
            files.LockedPath = null; retry.onClick.Invoke(); yield return null;
            Assert.False(service.IsSaveBlocked); Assert.False(SaveStatusOverlay.IsModalOpen); Assert.False(SaveStatusOverlay.IsBadgeVisible);
            Assert.AreEqual(42, service.Current.abyssShardsTotal); Assert.True(toast.activeSelf); Assert.True(File.Exists(savePath));
        }

        [UnityTest]
        public IEnumerator ActualWriteFailureToastScalesWithoutRaycastsAndUnscaledDeadlineHidesIt()
        {
            string good = File.ReadAllText(savePath); files.FailWrite = true;
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패:.*save status readability injected write"));
            Assert.False(service.Save()); Assert.True(toast.activeSelf); Assert.AreEqual(good, File.ReadAllText(savePath)); files.FailWrite = false;
            string message = "저장 상태의 긴 안내를 끝까지 읽을 수 있습니다. A long save status message remains readable. 長い保存状態の案内を最後まで読めます。";
            foreach (float scale in new[] { 1f, 1.25f, 1.5f })
            {
                Assert.True(service.TrySaveMenuTextScale(scale)); float shownAt = Time.unscaledTime; Call("ShowToast", message);
                Assert.AreEqual(shownAt + 3.5f, Field<float>(overlay, "toastHideTime"), 0.01f); yield return Settle();
                Assert.AreEqual(message, toastText.text); Assert.AreEqual(Mathf.RoundToInt(17 * scale), toastText.fontSize);
                Assert.GreaterOrEqual(toastText.rectTransform.rect.height + 1f, toastText.preferredHeight);
                Inside(toastText.rectTransform, (RectTransform)toast.transform);
                foreach (var graphic in toast.GetComponentsInChildren<Graphic>(true)) Assert.False(graphic.raycastTarget);
            }
            NarrowCanvas(); badge.SetActive(true);
            foreach (string caption in new[] { "Saved", message })
            {
                Call("ShowToast", caption); yield return Settle();
                Inside((RectTransform)toast.transform, (RectTransform)overlay.transform);
                Inside((RectTransform)badge.transform, (RectTransform)overlay.transform);
                Inside(badgeText.rectTransform, (RectTransform)badge.transform);
                Assert.GreaterOrEqual(toastText.rectTransform.rect.height + 1f, toastText.preferredHeight);
                foreach (var graphic in toast.GetComponentsInChildren<Graphic>(true))
                {
                    Inside(graphic.rectTransform, (RectTransform)toast.transform);
                    Assert.False(graphic.raycastTarget);
                }
                var badgeIcon = badge.transform.Find("ArtSaveIcon") as RectTransform;
                Assert.NotNull(badgeIcon); Inside(badgeIcon, (RectTransform)badge.transform);
            }
            Time.timeScale = 0f; Set(overlay, "toastHideTime", Time.unscaledTime - 1f); Call("Update");
            Assert.False(toast.activeSelf); Assert.AreEqual(0f, Time.timeScale);
        }

        private void BlockActualSave()
        {
            files.LockedPath = savePath; ExpectBlockedErrors(); service.Reload();
            Assert.True(service.IsSaveBlocked); Call("FocusModal");
        }
        private static void ExpectBlockedErrors()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] (본 파일을 열지 못했다|불러오기 실패)"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[MetaSaveService\] 세이브를 열지 못했다"));
        }
        private void ShowGeometryOnly() { modal.SetActive(true); badge.SetActive(true); }
        private void LongText()
        {
            mainText.text = Loc.Get(StringKey.SaveStatus_BlockedBody) + "\n" + string.Join(" ", Enumerable.Repeat("긴 저장 안내 Long save access explanation 長い保存の案内", 6));
            pathText.text = "Temporary directory: " + string.Join("/", Enumerable.Repeat("long_directory_긴_경로_長いパス", 12));
            retryText.text = "다시 시도할 수 있습니다. Retry manually without losing the latest save. 最新の保存を失わずに再試行できます。";
        }
        private void NarrowCanvas()
        {
            var canvas = overlay.GetComponent<Canvas>(); canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = Mathf.Max(Screen.width / 640f, Screen.height / 520f);
        }
        private void AssertLayout(float scale)
        {
            AssertFont(title, 26, scale); AssertFont(mainText, 17, scale); AssertFont(pathText, 13, scale); AssertFont(retryText, 15, scale);
            AssertFont(retry.GetComponentInChildren<Text>(), 19, scale); AssertFont(cont.GetComponentInChildren<Text>(), 19, scale); AssertFont(badgeText, 16, scale);
            Inside(panel, (RectTransform)overlay.transform); Inside(title.rectTransform, panel); Inside((RectTransform)retry.transform, panel); Inside((RectTransform)cont.transform, panel);
            Assert.False(Bounds((RectTransform)retry.transform, panel).Overlaps(Bounds((RectTransform)cont.transform, panel)));
            var scroll = modal.GetComponentInChildren<ScrollRect>(true);
            var content = scroll != null && scroll.gameObject.activeInHierarchy ? scroll.viewport : mainText.rectTransform;
            Assert.False(Bounds(content, panel).Overlaps(Bounds(title.rectTransform, panel)));
            foreach (var button in new[] { retry, cont }) Assert.False(Bounds(content, panel).Overlaps(Bounds((RectTransform)button.transform, panel)));
            for (int i = 1; i < 3; i++)
            {
                var rows = new[] { mainText, pathText, retryText };
                Assert.LessOrEqual(Bounds(rows[i].rectTransform, panel).yMax, Bounds(rows[i - 1].rectTransform, panel).yMin + 1f);
            }
            var icon = badge.transform.Find("ArtSaveIcon") as RectTransform; Assert.NotNull(icon);
            Inside((RectTransform)badge.transform, (RectTransform)overlay.transform);
            Inside(icon, (RectTransform)badge.transform); Inside(badgeText.rectTransform, (RectTransform)badge.transform);
            Assert.GreaterOrEqual(Bounds(badgeText.rectTransform, badge.transform).xMin, Bounds(icon, badge.transform).xMax);
        }
        private static void AssertFont(Text text, int font, float scale)
        {
            Assert.AreEqual(Mathf.RoundToInt(font * scale), text.fontSize); Assert.False(text.resizeTextForBestFit);
            Assert.GreaterOrEqual(text.rectTransform.rect.height + 1f, text.preferredHeight);
        }
        private void AssertBindings()
        {
            Assert.AreSame(retryEvent, retry.onClick); Assert.AreSame(continueEvent, cont.onClick);
            CollectionAssert.AreEqual(retryListeners, RuntimeListeners(retry.onClick)); CollectionAssert.AreEqual(continueListeners, RuntimeListeners(cont.onClick));
            Assert.AreEqual(retryNavigation.mode, retry.navigation.mode); Assert.AreEqual(continueNavigation.mode, cont.navigation.mode);
            Assert.AreSame(retryNavigation.selectOnRight, retry.navigation.selectOnRight); Assert.AreSame(continueNavigation.selectOnLeft, cont.navigation.selectOnLeft);
        }
        private static List<object> RuntimeListeners(UnityEventBase callback)
        {
            object calls = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(callback);
            var list = (IEnumerable)calls.GetType().GetField("m_RuntimeCalls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(calls);
            return list.Cast<object>().ToList();
        }
        private Dictionary<Image, Vector2> IconSizes() => overlay.GetComponentsInChildren<Image>(true).Where(image => image.sprite != null).ToDictionary(image => image, image => image.rectTransform.rect.size);
        private static void AssertIcons(Dictionary<Image, Vector2> icons) { foreach (var icon in icons) { Assert.NotNull(icon.Key); AssertVector(icon.Value, icon.Key.rectTransform.rect.size); } }
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
        private static MethodInfo Method(string name) => typeof(SaveStatusOverlay).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private void Call(string name, params object[] args) => Method(name).Invoke(overlay, args);
    }
}