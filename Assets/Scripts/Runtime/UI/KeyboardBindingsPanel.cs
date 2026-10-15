using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.Runtime.UI
{
    /// <summary>Settings child window. Captures one keyboard key, leaving gamepad and UI bindings unchanged.</summary>
    public sealed partial class KeyboardBindingsPanel : MonoBehaviour
    {
        private static KeyboardBindingsPanel instance;
        private static int consumedFrame = -1;
        private GameObject body;
        private string captureBindingId;
        private int openedFrame = -1;
        private int captureFrame = -1;
        private EventSystem heldEventSystem;
        private bool previousNavigationEvents;
        private int navigationReleaseFrame = -1;
        private GameObject previousSelection;
        private GameObject captureSelection;

        public static bool IsOpen => instance != null && instance.body != null && instance.body.activeSelf;
        public static bool IsCapturing => IsOpen && !string.IsNullOrEmpty(instance.captureBindingId);
        public static bool WasInputConsumedThisFrame => consumedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            consumedFrame = -1;
        }

        public static void Open()
        {
            if (!SettingsPanel.IsOpen || SaveStatusOverlay.IsCapturingInput) return;
            if (instance == null)
            {
                var canvas = CreateOverlayCanvas("KeyboardBindingsPanel", UiSortingOrder.Settings + 1);
                // Height-based scale keeps the scroll window within small and narrow screens.
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1600f, 900f);
                scaler.matchWidthOrHeight = 1f;
                DontDestroyOnLoad(canvas);
                instance = canvas.AddComponent<KeyboardBindingsPanel>();
                instance.BuildUI(canvas.transform);
                instance.body.SetActive(false);
            }
            if (IsOpen) return;
            instance.previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            instance.openedFrame = Time.frameCount;
            instance.body.SetActive(true);
            instance.RefreshRows();
            instance.SetStatus(StringKey.Keys_Help);
            instance.SelectFirst();
        }

        public static void Close()
        {
            if (instance == null) return;
            instance.EndCapture(false);
            instance.RestoreNavigation();
            if (!IsOpen) return;
            consumedFrame = Time.frameCount;
            instance.body.SetActive(false);
            var events = EventSystem.current;
            if (events != null && !SaveStatusOverlay.IsCapturingInput &&
                (events.currentSelectedGameObject == null || instance.IsInside(events.currentSelectedGameObject)))
            {
                var restore = instance.previousSelection;
                events.SetSelectedGameObject(restore != null && restore.activeInHierarchy ? restore : null);
            }
            instance.previousSelection = null;
            instance.captureSelection = null;
        }

        private void OnEnable()
        {
            GameplayKeyBindings.OnBindingsChanged += RefreshRows;
            Loc.AddLanguageChangedListener(OnLanguageChanged);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDisable()
        {
            GameplayKeyBindings.OnBindingsChanged -= RefreshRows;
            Loc.RemoveLanguageChangedListener(OnLanguageChanged);
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Close();
        }

        private void OnSceneChanged(Scene previous, Scene next) => Close();

        private void OnLanguageChanged(LocalizationLanguage language)
        {
            RefreshRows();
            if (IsCapturing) ShowCapturePrompt();
            else if (!string.IsNullOrEmpty(statusKey)) SetStatus(statusKey);
        }

        private void Update()
        {
            if (navigationReleaseFrame >= 0 && Time.frameCount > navigationReleaseFrame) RestoreNavigation();
            if (!IsOpen) return;
            if (!SettingsPanel.IsOpen)
            {
                Close();
                return;
            }
            if (SaveStatusOverlay.IsCapturingInput)
            {
                if (IsCapturing) EndCapture(true);
                return;
            }
            if (Time.frameCount == openedFrame || WasInputConsumedThisFrame) return;
            if (WasCancelPressed())
            {
                RequestBack();
                return;
            }
            if (IsCapturing)
            {
                if (Time.frameCount == captureFrame) return;
                var keyboard = Keyboard.current;
                if (keyboard == null) return;
                foreach (var key in keyboard.allKeys)
                {
                    if (!key.wasPressedThisFrame) continue;
                    string bindingId = captureBindingId;
                    EndCapture(false);
                    bool saved = GameplayKeyBindings.TrySetKey(bindingId, "<Keyboard>/" + key.name, out var error);
                    RefreshRows();
                    SetStatus(saved ? StringKey.Keys_Saved : ErrorKey(error));
                    return;
                }
                return;
            }
            KeepFocusInside();
        }

        private static bool WasCancelPressed() =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        private bool CanAcceptCommand => IsOpen && !SaveStatusOverlay.IsCapturingInput &&
            !WasInputConsumedThisFrame && Time.frameCount != openedFrame;

        private void BeginCapture(string bindingId)
        {
            if (!CanAcceptCommand || IsCapturing) return;
            captureBindingId = bindingId;
            captureFrame = Time.frameCount;
            int rowIndex = bindingIds.IndexOf(bindingId);
            captureSelection = rowIndex >= 0 ? keyButtons[rowIndex].gameObject : null;
            heldEventSystem = EventSystem.current;
            if (heldEventSystem != null)
            {
                previousNavigationEvents = heldEventSystem.sendNavigationEvents;
                heldEventSystem.sendNavigationEvents = false;
                heldEventSystem.SetSelectedGameObject(null);
            }
            navigationReleaseFrame = -1;
            SetRowsInteractable(false);
            ShowCapturePrompt();
        }

        private void EndCapture(bool cancelled)
        {
            if (string.IsNullOrEmpty(captureBindingId)) return;
            captureBindingId = null;
            consumedFrame = Time.frameCount;
            // Keep navigation suppressed through this input frame; the new key must not also Submit/Move.
            navigationReleaseFrame = Time.frameCount;
            SetRowsInteractable(true);
            if (cancelled) SetStatus(StringKey.Keys_Cancelled);
        }

        private void RestoreNavigation()
        {
            if (heldEventSystem != null) heldEventSystem.sendNavigationEvents = previousNavigationEvents;
            heldEventSystem = null;
            navigationReleaseFrame = -1;
            if (IsOpen && !IsCapturing && !SaveStatusOverlay.IsCapturingInput && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null) SelectFirst();
        }

        private void RequestBack()
        {
            if (!CanAcceptCommand) return;
            if (IsCapturing) EndCapture(true);
            else Close();
        }

        private void ResetBindings()
        {
            if (!CanAcceptCommand || IsCapturing) return;
            consumedFrame = Time.frameCount;
            bool saved = GameplayKeyBindings.ResetKeyboard(out var error);
            RefreshRows();
            SetStatus(saved ? StringKey.Keys_ResetDone : ErrorKey(error));
        }

        private static string ErrorKey(KeyBindingError error) => error switch
        {
            KeyBindingError.Conflict => StringKey.Keys_Conflict,
            KeyBindingError.Reserved => StringKey.Keys_Reserved,
            KeyBindingError.Invalid => StringKey.Keys_Invalid,
            KeyBindingError.SaveFailed => StringKey.Keys_SaveFailed,
            _ => StringKey.Keys_Unavailable
        };
    }
}
