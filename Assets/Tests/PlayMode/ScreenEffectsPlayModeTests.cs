using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Abyss.Tests.PlayMode
{
    public sealed class ScreenEffectsPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new PlayModeSaveGuard();
        private GameObject cameraObject;
        private BossPresenter presenter;
        private Random.State randomState;
        private float previousTimeScale;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            guard.Acquire();
            randomState = Random.state;
            previousTimeScale = Time.timeScale;
            _ = MetaSaveService.Instance.Current;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.SetFileOperations(null);
                SettingsPanel.Close();
                if (cameraObject != null) Object.Destroy(cameraObject);
                if (presenter != null) Object.Destroy(presenter.gameObject);
                Time.timeScale = previousTimeScale;
                yield return null;
            }
            finally { Random.state = randomState; guard.Release(); }
        }

        private CameraShake CreateCamera()
        {
            cameraObject = new GameObject("AccessibilityTestCamera");
            return cameraObject.AddComponent<CameraShake>();
        }

        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [UnityTest]
        public IEnumerator HalfStrengthScalesOutputWithoutChangingTimerOrDecay()
        {
            var camera = CreateCamera();
            Assert.True(ScreenEffectPreferences.TrySet(1f, false));
            camera.Shake(2f, 2f);
            var seed = Random.state;
            Call(camera, "LateUpdate");
            var full = camera.CurrentShakeOffset;
            var timer = Field<float>(camera, "timer");
            var magnitude = Field<float>(camera, "currentMagnitude");
            camera.ClearShake();
            Assert.True(ScreenEffectPreferences.TrySet(0.5f, false));
            camera.Shake(2f, 2f);
            Random.state = seed;
            Call(camera, "LateUpdate");
            Assert.That((camera.CurrentShakeOffset - full * 0.5f).sqrMagnitude, Is.LessThan(0.0000001f));
            Assert.AreEqual(timer, Field<float>(camera, "timer"));
            Assert.AreEqual(magnitude, Field<float>(camera, "currentMagnitude"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingImmediatelyClearsAndDoesNotResumeQueuedShake()
        {
            var camera = CreateCamera();
            camera.Shake(2f, 2f);
            Call(camera, "LateUpdate");
            Assert.Greater(camera.CurrentShakeOffset.sqrMagnitude, 0f);
            Assert.True(ScreenEffectPreferences.TrySet(0f, false));
            Assert.AreEqual(Vector3.zero, camera.CurrentShakeOffset);
            camera.Shake(10f, 10f);
            Assert.True(ScreenEffectPreferences.TrySet(1f, false));
            Call(camera, "LateUpdate");
            Assert.AreEqual(Vector3.zero, camera.CurrentShakeOffset);
            camera.Shake(2f, 2f);
            Time.timeScale = 0f;
            yield return null;
            Assert.Greater(camera.CurrentShakeOffset.sqrMagnitude, 0f);
        }

        [UnityTest]
        public IEnumerator BossFlashStopsImmediatelyAndFutureFlashesFollowPreference()
        {
            presenter = (BossPresenter)typeof(BossPresenter).GetMethod("EnsureInstance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var flash = Field<Image>(presenter, "flash");
            Call(presenter, "Flash");
            Call(presenter, "Update");
            Assert.True(flash.gameObject.activeSelf);
            Assert.Greater(flash.color.a, 0f);
            Assert.True(ScreenEffectPreferences.TrySet(1f, true));
            Assert.False(flash.gameObject.activeSelf);
            Assert.AreEqual(0f, flash.color.a);
            Call(presenter, "Flash");
            Assert.False(flash.gameObject.activeSelf);
            Assert.True(ScreenEffectPreferences.TrySet(1f, false));
            Assert.False(flash.gameObject.activeSelf, "Old flash must not resume");
            Call(presenter, "Flash");
            Call(presenter, "Update");
            Assert.True(flash.gameObject.activeSelf);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreferencesRoundTripWithoutOverwritingOtherSettings()
        {
            var meta = MetaSaveService.Instance.Current;
            meta.settings.keyboardBindingOverrides = "other-setting";
            meta.settings.masterVolume = 0.3f;
            Assert.True(ScreenEffectPreferences.TrySet(0.5f, true));
            MetaSaveService.Instance.UnloadWithoutSaving();
            Assert.AreEqual(0.5f, MetaSaveService.Instance.Current.settings.cameraShakeScale);
            Assert.True(ScreenEffectPreferences.ReduceBossFlashes);
            Assert.AreEqual("other-setting", MetaSaveService.Instance.Current.settings.keyboardBindingOverrides);
            Assert.AreEqual(0.3f, MetaSaveService.Instance.Current.settings.masterVolume);
            yield return null;
        }

        private sealed class FailingWriteOperations : SaveFileOperations
        {
            public override void WriteAllText(string path, string contents) => throw new IOException("screen effect test failure");
        }

        [UnityTest]
        public IEnumerator FailedSaveRestoresMemoryFileAndDoesNotPublishChangedEvent()
        {
            Assert.True(ScreenEffectPreferences.TrySet(0.5f, false));
            string path = Path.Combine(guard.TestDirectory, MetaSave.FileName);
            string disk = File.ReadAllText(path);
            string timestamp = MetaSaveService.Instance.Current.saveTimeUtc;
            int changes = 0;
            System.Action listener = () => changes++;
            ScreenEffectPreferences.OnChanged += listener;
            try
            {
                SaveSystem.Instance.SetFileOperations(new FailingWriteOperations());
                LogAssert.Expect(LogType.Error, new Regex("저장 실패: screen effect test failure"));
                Assert.False(ScreenEffectPreferences.TrySet(0f, true));
                Assert.AreEqual(0.5f, ScreenEffectPreferences.ShakeScale);
                Assert.False(ScreenEffectPreferences.ReduceBossFlashes);
                Assert.AreEqual(timestamp, MetaSaveService.Instance.Current.saveTimeUtc);
                Assert.AreEqual(disk, File.ReadAllText(path));
                Assert.AreEqual(0, changes);
            }
            finally { ScreenEffectPreferences.OnChanged -= listener; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsSelectorsAndToggleApplyAndRetainExplicitNavigation()
        {
            SettingsPanel.Open();
            var panel = Object.FindAnyObjectByType<SettingsPanel>();
            var previous = Field<Button>(panel, "shakePrev");
            var next = Field<Button>(panel, "shakeNext");
            var toggle = Field<Toggle>(panel, "bossFlashToggle");
            Assert.False(next.interactable);
            previous.onClick.Invoke();
            Assert.AreEqual(0.5f, ScreenEffectPreferences.ShakeScale);
            previous.onClick.Invoke();
            Assert.AreEqual(0f, ScreenEffectPreferences.ShakeScale);
            Assert.False(previous.interactable);
            Assert.AreSame(toggle, next.navigation.selectOnDown);
            toggle.isOn = true;
            Assert.True(ScreenEffectPreferences.ReduceBossFlashes);
            next.onClick.Invoke();
            Assert.AreEqual(0.5f, ScreenEffectPreferences.ShakeScale);
            SettingsPanel.Close();
            SettingsPanel.Open();
            Assert.AreEqual("50%", Field<Text>(panel, "shakeValue").text);
            Assert.True(toggle.isOn);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedSettingsChangeRestoresVisibleValueAndToggle()
        {
            SettingsPanel.Open();
            var panel = Object.FindAnyObjectByType<SettingsPanel>();
            var previous = Field<Button>(panel, "shakePrev");
            var toggle = Field<Toggle>(panel, "bossFlashToggle");
            SaveSystem.Instance.SetFileOperations(new FailingWriteOperations());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: screen effect test failure"));
            previous.onClick.Invoke();
            Assert.AreEqual("100%", Field<Text>(panel, "shakeValue").text);
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: screen effect test failure"));
            toggle.isOn = true;
            Assert.False(toggle.isOn);
            Assert.AreEqual(1f, ScreenEffectPreferences.ShakeScale);
            Assert.False(ScreenEffectPreferences.ReduceBossFlashes);
            SaveSystem.Instance.SetFileOperations(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReloadingReducedPreferenceClearsActiveFlashOnNextFrame()
        {
            Assert.True(ScreenEffectPreferences.TrySet(1f, true));
            MetaSaveService.Instance.Current.settings.reduceBossScreenFlashes = false;
            presenter = (BossPresenter)typeof(BossPresenter).GetMethod("EnsureInstance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Call(presenter, "Flash");
            var flash = Field<Image>(presenter, "flash");
            Assert.True(flash.gameObject.activeSelf);
            MetaSaveService.Instance.UnloadWithoutSaving();
            Assert.True(MetaSaveService.Instance.Current.settings.reduceBossScreenFlashes);
            yield return null;
            Assert.False(flash.gameObject.activeSelf);
            Assert.AreEqual(0f, flash.color.a);
        }

        [UnityTest]
        public IEnumerator InvalidRequestedValuesNeverSave()
        {
            Assert.False(ScreenEffectPreferences.TrySet(float.NaN, true));
            Assert.False(ScreenEffectPreferences.TrySet(float.PositiveInfinity, true));
            Assert.False(ScreenEffectPreferences.TrySet(-1f, true));
            Assert.False(ScreenEffectPreferences.TrySet(2f, true));
            Assert.AreEqual(1f, ScreenEffectPreferences.ShakeScale);
            Assert.False(ScreenEffectPreferences.ReduceBossFlashes);
            yield return null;
        }
    }
}
