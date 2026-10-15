using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Input;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.UI;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 데이터 초기화(서비스 거래 + 설정 위 모달) PlayMode 검증. 메타 쓰기는 <see cref="PlayModeSaveGuard"/>의 임시 폴더에서만 일어난다.
    ///
    /// 실제 씬 판정·성공 후 타이틀 전환은 DataResetPanel의 private 정적 대리자를 리플렉션으로 바꿔 대신한다 —
    /// 테스트는 씬을 바꾸지 않고, 끝나면 원래 대리자로 되돌린다. 옛 PlayerPrefs 언어 키·오디오·화면은 테스트 전 값으로 되돌린다.
    /// 기대 로그: 일부러 실패시키는 두 테스트가 SaveSystem Error 각 1건, 저장 알림 예외 테스트가 Warning 1건.
    /// </summary>
    public sealed partial class DataResetPlayModeTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string LegacyKey = LocalizationManager.LegacyLanguagePrefKey;

        private readonly PlayModeSaveGuard guard = new();
        private readonly List<MetaResetCategory> completions = new();
        private bool isAllowed = true;
        private int metaSaveCount;
        private object previousScenePredicate;
        private object previousCompletion;
        private Keyboard keyboard;
        private GameObject ownedEventSystem;
        private GameObject initialSelection;
        private bool ownsSettings;
        private bool ownsReset;
        private RunManager createdRun;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private LocalizationLanguage oldLanguage;
        private bool hadLegacyLanguageKey;
        private string legacyLanguageValue;
        private bool hasAudioSnapshot;
        private Vector3 audioVolumes;
        private int screenWidth, screenHeight;
        private FullScreenMode screenMode;

        private sealed class FailingOperations : SaveFileOperations
        {
            public override void WriteAllText(string path, string contents) => throw new IOException("data reset test failure");
        }

        private static string MetaPath(PlayModeSaveGuard saveGuard) => Path.Combine(saveGuard.TestDirectory, MetaSave.FileName);
        private static MetaSaveService Service => MetaSaveService.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            completions.Clear();
            isAllowed = true;
            metaSaveCount = 0;
            createdRun = null;
            // 언어 매니저 첫 초기화가 옛 키를 이관·삭제할 수 있으므로 어떤 설정·언어 접근보다 먼저 잡아 둔다.
            hadLegacyLanguageKey = PlayerPrefs.HasKey(LegacyKey);
            legacyLanguageValue = hadLegacyLanguageKey ? PlayerPrefs.GetString(LegacyKey) : null;
            (screenWidth, screenHeight, screenMode) = (Screen.width, Screen.height, Screen.fullScreenMode);
            guard.Acquire();
            _ = Service.Current;
            oldLanguage = Loc.CurrentLanguage;
            var audio = AudioManager.GetInstanceSafe();
            hasAudioSnapshot = audio != null;
            if (hasAudioSnapshot) audioVolumes = new Vector3(audio.GetMasterVolume(), audio.GetBgmVolume(), audio.GetSfxVolume());
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            ownsSettings = Object.FindAnyObjectByType<SettingsPanel>() == null;
            ownsReset = Object.FindAnyObjectByType<DataResetPanel>() == null;
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("DataResetTestEvents", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            initialSelection = EventSystem.current.currentSelectedGameObject;
            var sceneField = typeof(DataResetPanel).GetField("isSceneAllowed", PrivateStatic);
            var completionField = typeof(DataResetPanel).GetField("completeAfterReset", PrivateStatic);
            previousScenePredicate = sceneField.GetValue(null);
            previousCompletion = completionField.GetValue(null);
            sceneField.SetValue(null, new Func<string, bool>(_ => isAllowed));
            completionField.SetValue(null, new Action<MetaResetCategory>(c => completions.Add(c)));
            SaveSystem.Instance.OnSaved += CountSave;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.OnSaved -= CountSave;
                SaveSystem.Instance.SetFileOperations(null);
                DataResetPanel.Close();
                SettingsPanel.Close();
                typeof(DataResetPanel).GetField("isSceneAllowed", PrivateStatic).SetValue(null, previousScenePredicate);
                typeof(DataResetPanel).GetField("completeAfterReset", PrivateStatic).SetValue(null, previousCompletion);
                if (createdRun != null) Object.Destroy(createdRun.gameObject);
                if (ownsReset && Object.FindAnyObjectByType<DataResetPanel>() is { } reset) Object.Destroy(reset.gameObject);
                if (ownsSettings && Object.FindAnyObjectByType<SettingsPanel>() is { } settings) Object.Destroy(settings.gameObject);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(initialSelection != null && initialSelection.activeInHierarchy ? initialSelection : null);
                if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
                Loc.SetLanguage(oldLanguage);
                var audio = AudioManager.GetInstanceSafe();
                if (hasAudioSnapshot && audio != null)
                {
                    audio.SetMasterVolume(audioVolumes.x);
                    audio.SetBgmVolume(audioVolumes.y);
                    audio.SetSfxVolume(audioVolumes.z);
                }
                if (Screen.width != screenWidth || Screen.height != screenHeight || Screen.fullScreenMode != screenMode)
                    Screen.SetResolution(screenWidth, screenHeight, screenMode);
            }
            finally
            {
                RestoreLegacyLanguageKey();   // 언어 상태를 되돌린 뒤
                guard.Release();
                GameplayKeyBindings.InvalidateCache();
                if (previousInputSettings != null) InputSystem.settings = previousInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
            }
            yield return null;
        }

        /// <summary>테스트 전 있음/없음·값 그대로. 이미 같으면 PlayerPrefs에 쓰지 않는다.</summary>
        private void RestoreLegacyLanguageKey()
        {
            bool hasKeyNow = PlayerPrefs.HasKey(LegacyKey);
            if (hadLegacyLanguageKey && (!hasKeyNow || PlayerPrefs.GetString(LegacyKey) != legacyLanguageValue))
                PlayerPrefs.SetString(LegacyKey, legacyLanguageValue);
            else if (!hadLegacyLanguageKey && hasKeyNow) PlayerPrefs.DeleteKey(LegacyKey);
            else return;
            PlayerPrefs.Save();
        }

        private void CountSave(string path)
        {
            if (path != null && path.EndsWith(MetaSave.FileName)) metaSaveCount++;
        }

        private static void Populate()
        {
            var save = Service.Current;
            save.abyssShardsTotal = 77;
            save.relics.Add(new RelicEntry { relicId = "relic_test", level = 2 });
            save.equippedRelicIds.Clear();
            save.equippedRelicIds.AddRange(new[] { "relic_test", string.Empty, string.Empty });
            save.upgradeLevels.Add(new MetaUpgradeEntry { upgradeId = "upgrade_test", level = 1 });
            save.unlockedFormIds.Add("form_test");
            save.discoveredEnemyIds.Add("enemy_test");
            save.records.totalRunCount = 4;
            save.records.hasSeenPrologue = save.records.hasSeenEnding = save.hasCompletedFirstPlayTutorial = true;
            save.settings.masterVolume = 0.3f;
            save.settings.cameraShakeScale = 0.5f;
            Assert.True(Service.Save());
        }

        // ───────────────────────── 서비스 ─────────────────────────

        [UnityTest]
        public IEnumerator PartialResetSavesOnceAndDiskMatchesCandidate()
        {
            Populate();
            var old = Service.Current;
            string oldSettings = JsonUtility.ToJson(old.settings);
            metaSaveCount = 0;
            Assert.True(Service.TryResetData(MetaResetCategory.Currency | MetaResetCategory.Relics, out var error));
            Assert.AreEqual(MetaResetError.None, error);
            Assert.AreEqual(1, metaSaveCount);
            Assert.AreNotSame(old, Service.Current);
            Assert.AreEqual(0, Service.Current.abyssShardsTotal);
            Assert.IsEmpty(Service.Current.relics);
            Assert.AreEqual(1, Service.Current.upgradeLevels.Count);
            Assert.AreEqual(oldSettings, JsonUtility.ToJson(Service.Current.settings));
            Assert.AreEqual(77, old.abyssShardsTotal, "옛 객체는 변형되지 않는다.");
            Assert.AreEqual(JsonUtility.ToJson(Service.Current, true), File.ReadAllText(MetaPath(guard)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllResetIsFreshIncludingSettingsAndViewingFlags()
        {
            Populate();
            Assert.True(Service.TryResetData(MetaResetCategory.All, out _));
            var current = Service.Current;
            Assert.AreEqual(MetaSave.CurrentVersion, current.version);
            Assert.AreEqual(0.7f, current.settings.masterVolume);
            Assert.AreEqual(1f, current.settings.cameraShakeScale);
            Assert.False(current.records.hasSeenPrologue);
            Assert.False(current.records.hasSeenEnding);
            Assert.IsEmpty(current.relics);
            Assert.AreEqual(0, current.records.totalRunCount);
            Service.UnloadWithoutSaving();
            Assert.AreEqual(0.7f, Service.Current.settings.masterVolume, "디스크도 새 값이다.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedWriteRollsBackMemoryTimestampAndDisk()
        {
            Populate();
            var old = Service.Current;
            string json = JsonUtility.ToJson(old);
            string disk = File.ReadAllText(MetaPath(guard));
            SaveSystem.Instance.SetFileOperations(new FailingOperations());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: data reset test failure"));
            Assert.False(Service.TryResetData(MetaResetCategory.All, out var error));
            Assert.AreEqual(MetaResetError.SaveFailed, error);
            Assert.AreSame(old, Service.Current);
            Assert.AreEqual(json, JsonUtility.ToJson(Service.Current));
            Assert.AreEqual(disk, File.ReadAllText(MetaPath(guard)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SubscriberExceptionAfterCommitKeepsCandidateWithSingleSave()
        {
            Populate();
            var old = Service.Current;
            metaSaveCount = 0;
            Action<string> thrower = _ => throw new InvalidOperationException("data reset subscriber failure");
            SaveSystem.Instance.OnSaved += thrower;   // CountSave 다음에 붙어 저장 횟수는 그대로 센다
            LogAssert.Expect(LogType.Warning, new Regex("저장 알림 구독자가 예외"));
            bool isReset;
            MetaResetError error;
            try { isReset = Service.TryResetData(MetaResetCategory.Currency, out error); }
            finally { SaveSystem.Instance.OnSaved -= thrower; }
            Assert.True(isReset);
            Assert.AreEqual(MetaResetError.None, error);
            Assert.AreEqual(1, metaSaveCount);
            Assert.AreNotSame(old, Service.Current, "옛 객체를 다시 쓰지 않는다.");
            Assert.AreEqual(77, old.abyssShardsTotal);
            Assert.AreEqual(0, Service.Current.abyssShardsTotal);
            Assert.AreEqual(JsonUtility.ToJson(Service.Current, true), File.ReadAllText(MetaPath(guard)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockedSaveIsRefusedWithoutClearingBlockOrWriting()
        {
            Populate();
            string disk = File.ReadAllText(MetaPath(guard));
            var setter = typeof(MetaSaveService).GetProperty(nameof(MetaSaveService.IsSaveBlocked)).GetSetMethod(true);
            setter.Invoke(Service, new object[] { true });
            metaSaveCount = 0;
            Assert.False(Service.TryResetData(MetaResetCategory.Currency, out var error));
            Assert.AreEqual(MetaResetError.SaveBlocked, error);
            Assert.True(Service.IsSaveBlocked);
            Assert.AreEqual(77, Service.Current.abyssShardsTotal);
            Assert.AreEqual(0, metaSaveCount);
            Assert.AreEqual(disk, File.ReadAllText(MetaPath(guard)));
            setter.Invoke(Service, new object[] { false });
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidSelectionAndActiveRunAreRefused()
        {
            Populate();
            metaSaveCount = 0;
            Assert.False(Service.TryResetData(MetaResetCategory.None, out var error));
            Assert.AreEqual(MetaResetError.InvalidSelection, error);
            Assert.False(Service.TryResetData((MetaResetCategory)(1 << 12), out error));
            Assert.AreEqual(MetaResetError.InvalidSelection, error);
            var run = RunManager.GetInstanceSafe();
            if (run == null) run = createdRun = RunManager.Instance;
            var field = typeof(RunManager).GetField("isRunActive", PrivateInstance);
            bool wasActive = (bool)field.GetValue(run);
            field.SetValue(run, true);
            try
            {
                Assert.False(Service.TryResetData(MetaResetCategory.Currency, out error));
                Assert.AreEqual(MetaResetError.RunActive, error);
                Assert.False(DataResetPanel.IsResetContextAllowed);
            }
            finally { field.SetValue(run, wasActive); }
            Assert.AreEqual(77, Service.Current.abyssShardsTotal);
            Assert.AreEqual(0, metaSaveCount);
            yield return null;
        }

    }
}
