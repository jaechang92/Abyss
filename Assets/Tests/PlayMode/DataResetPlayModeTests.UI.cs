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
    public sealed partial class DataResetPlayModeTests
    {
        // ───────────────────────── 설정 위 모달 ─────────────────────────

        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
        private static SettingsPanel Settings => Object.FindAnyObjectByType<SettingsPanel>();
        private static DataResetPanel Reset => Object.FindAnyObjectByType<DataResetPanel>();
        private static Button EntryButton => Get<Button>(Settings, "dataResetButton");

        private static void Click(Selectable target) =>
            ExecuteEvents.Execute(target.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

        private IEnumerator OpenReset()
        {
            SettingsPanel.Open();
            yield return null;
            Assert.True(EntryButton.interactable);
            EventSystem.current.SetSelectedGameObject(EntryButton.gameObject);
            Click(EntryButton);
            Assert.True(DataResetPanel.IsOpen);
            yield return null;
        }

        private IEnumerator ChooseAndConfirmStage(params int[] groupIndexes)
        {
            var toggles = Get<List<Toggle>>(Reset, "groupToggles");
            foreach (var toggle in toggles) Assert.False(toggle.isOn, "기본 선택은 없다.");
            Assert.False(Get<Button>(Reset, "nextButton").interactable);
            foreach (int index in groupIndexes) Click(toggles[index]);
            yield return null;
            Click(Get<Button>(Reset, "nextButton"));
            yield return null;
            Assert.True(Get<Button>(Reset, "confirmButton").gameObject.activeSelf);
            Assert.AreSame(Get<Button>(Reset, "backButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "되돌릴 수 없는 버튼을 기본 선택으로 두지 않는다.");
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator EntryIsDisabledOutsideTitleLobbyAndJoinsNavigationWhenAllowed()
        {
            isAllowed = false;
            SettingsPanel.Open();
            yield return null;
            Assert.False(EntryButton.interactable);
            Assert.False(DataResetPanel.Open());
            var close = Get<Button>(Settings, "closeButton");
            Assert.AreNotSame(EntryButton, close.navigation.selectOnUp, "꺼진 진입 버튼으로 가는 길을 남기지 않는다.");
            isAllowed = true;
            yield return null;
            Assert.True(EntryButton.interactable);
            Assert.AreSame(close, EntryButton.navigation.selectOnDown);
            Assert.AreSame(EntryButton, close.navigation.selectOnUp);
        }

        [UnityTest]
        public IEnumerator BackAndEscapeCancelWithZeroWritesAndRestoreSettingsSelection()
        {
            Populate();
            PlayerPrefs.SetString(LegacyKey, "Japanese");
            string disk = File.ReadAllText(MetaPath(guard));
            metaSaveCount = 0;
            yield return OpenReset();
            yield return ChooseAndConfirmStage(0, 6);
            string warning = Get<Text>(Reset, "warningLabel").text;
            StringAssert.Contains(DataResetPanelText("Group_Relics"), warning);
            StringAssert.Contains(DataResetPanelText("Group_Settings"), warning);
            StringAssert.DoesNotContain(DataResetPanelText("Group_Upgrades"), warning);
            Click(Get<Button>(Reset, "backButton"));
            yield return null;
            Assert.True(Get<List<Toggle>>(Reset, "groupToggles")[0].gameObject.activeSelf, "선택 단계로 돌아온다.");
            yield return Press(Key.Escape);
            Assert.False(DataResetPanel.IsOpen);
            Assert.True(SettingsPanel.IsOpen, "같은 ESC로 설정까지 닫지 않는다.");
            Assert.AreSame(EntryButton.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(0, metaSaveCount);
            Assert.AreEqual(disk, File.ReadAllText(MetaPath(guard)));
            Assert.AreEqual(77, Service.Current.abyssShardsTotal);
            Assert.AreEqual("Japanese", PlayerPrefs.GetString(LegacyKey), "취소는 옛 키를 건드리지 않는다.");
            Assert.IsEmpty(completions);
        }

        [UnityTest]
        public IEnumerator ProgressSuccessClosesBothWithoutSaveAllAndRequestsTitle()
        {
            Populate();
            yield return OpenReset();
            yield return ChooseAndConfirmStage(1, 4);
            string settingsBefore = JsonUtility.ToJson(Service.Current.settings);
            metaSaveCount = 0;
            Click(Get<Button>(Reset, "confirmButton"));
            Assert.False(DataResetPanel.IsOpen);
            Assert.False(SettingsPanel.IsOpen);
            Assert.AreEqual(1, metaSaveCount, "Settings.SaveAll이 추가로 쓰지 않는다.");
            CollectionAssert.AreEqual(new[] { MetaResetCategory.Currency | MetaResetCategory.CodexAndRunRecords }, completions);
            Assert.AreEqual(0, Service.Current.abyssShardsTotal);
            Assert.AreEqual(0, Service.Current.records.totalRunCount);
            Assert.True(Service.Current.records.hasSeenPrologue);
            Assert.True(Service.Current.records.hasSeenEnding);
            Assert.AreEqual(1, Service.Current.relics.Count);
            Assert.AreEqual(settingsBefore, JsonUtility.ToJson(Service.Current.settings));
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.True(selected == null || !selected.transform.IsChildOf(Reset.transform));
            yield return null;
            Assert.AreEqual(1, metaSaveCount);
        }

        [UnityTest]
        public IEnumerator SettingsSuccessAppliesFreshValuesWithoutExtraSave()
        {
            Populate();
            Assert.True(Service.TrySaveMenuTextScale(1.5f));
            var systemLanguage = SystemDefaultLanguage();
            var seeded = systemLanguage == LocalizationLanguage.Japanese ? LocalizationLanguage.English : LocalizationLanguage.Japanese;
            Loc.SetLanguage(seeded);
            PlayerPrefs.SetString(LegacyKey, seeded.ToString());   // 옛 언어 키가 남아 있는 기기
            yield return OpenReset();
            yield return ChooseAndConfirmStage(6);
            metaSaveCount = 0;
            Click(Get<Button>(Reset, "confirmButton"));
            Assert.False(SettingsPanel.IsOpen);
            Assert.AreEqual(1, metaSaveCount, "옛 키 재이관이 두 번째 저장을 만들지 않는다.");
            Assert.False(PlayerPrefs.HasKey(LegacyKey));
            Assert.AreEqual(string.Empty, Service.Current.settings.language);
            Assert.AreEqual(systemLanguage, Loc.CurrentLanguage);
            Assert.AreEqual(JsonUtility.ToJson(new MetaSettings()), JsonUtility.ToJson(Service.Current.settings));
            Assert.AreEqual(1f, MenuTextReadability.Scale);
            Assert.AreEqual(77, Service.Current.abyssShardsTotal, "설정만 지웠다.");
            CollectionAssert.AreEqual(new[] { MetaResetCategory.Settings }, completions);
            yield return null;
            Assert.AreEqual(1, metaSaveCount);
        }

        [UnityTest]
        public IEnumerator ConfirmRechecksContextAndKeepsPanel()
        {
            Populate();
            yield return OpenReset();
            yield return ChooseAndConfirmStage(1);
            isAllowed = false;
            metaSaveCount = 0;
            Click(Get<Button>(Reset, "confirmButton"));
            Assert.True(DataResetPanel.IsOpen);
            Assert.True(SettingsPanel.IsOpen);
            Assert.True(Get<Text>(Reset, "statusLabel").gameObject.activeSelf);
            Assert.True(Get<Button>(Reset, "confirmButton").gameObject.activeSelf);
            Assert.AreEqual(0, metaSaveCount);
            Assert.AreEqual(77, Service.Current.abyssShardsTotal);
            Assert.IsEmpty(completions);
        }

        [UnityTest]
        public IEnumerator FailedResetKeepsPanelSelectionAndData()
        {
            Populate();
            PlayerPrefs.SetString(LegacyKey, "Japanese");
            string disk = File.ReadAllText(MetaPath(guard));
            string settings = JsonUtility.ToJson(Service.Current.settings);
            yield return OpenReset();
            yield return ChooseAndConfirmStage(0, 6);
            SaveSystem.Instance.SetFileOperations(new FailingOperations());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: data reset test failure"));
            Click(Get<Button>(Reset, "confirmButton"));
            Assert.True(DataResetPanel.IsOpen);
            Assert.True(SettingsPanel.IsOpen);
            Assert.True(Get<Text>(Reset, "statusLabel").gameObject.activeSelf);
            Assert.AreEqual(MetaResetCategory.Relics | MetaResetCategory.Settings, Get<MetaResetCategory>(Reset, "pending"));
            Assert.AreEqual(1, Service.Current.relics.Count);
            Assert.AreEqual(settings, JsonUtility.ToJson(Service.Current.settings));
            Assert.AreEqual("Japanese", PlayerPrefs.GetString(LegacyKey), "실패하면 옛 키를 건드리지 않는다.");
            Assert.AreEqual(disk, File.ReadAllText(MetaPath(guard)));
            Assert.IsEmpty(completions);
            SaveSystem.Instance.SetFileOperations(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LargestMenuTextFitsAllLanguagesInsideCanvas()
        {
            Assert.True(Service.TrySaveMenuTextScale(1.5f));
            yield return OpenReset();
            foreach (bool isConfirm in new[] { false, true })
            {
                if (isConfirm)
                {
                    Click(Get<Button>(Reset, "fullButton"));
                    yield return null;
                }
                foreach (LocalizationLanguage language in Enum.GetValues(typeof(LocalizationLanguage)))
                {
                    Loc.SetLanguage(language);
                    yield return null;
                    yield return null;
                    var panel = (RectTransform)Get<GameObject>(Reset, "body").transform.Find("Panel");
                    var canvas = (RectTransform)Reset.transform;
                    Assert.LessOrEqual(panel.rect.height, canvas.rect.height, language.ToString());
                    Assert.LessOrEqual(panel.rect.width, canvas.rect.width, language.ToString());
                    foreach (var text in panel.GetComponentsInChildren<Text>(false))
                    {
                        // 줄바꿈 글자라 높이가 실제 칸을 넘지 않으면 잘리지 않은 것이다.
                        Assert.AreEqual(HorizontalWrapMode.Wrap, text.horizontalOverflow, text.name);
                        Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, $"{text.name} {language}");
                    }
                    var viewport = Get<ScrollRect>(Reset, "scroll").viewport;
                    var lowestUpper = (RectTransform)Get<Button>(Reset, isConfirm ? "confirmButton" : "selectAllButton").transform;
                    Assert.GreaterOrEqual(viewport.anchoredPosition.y - viewport.rect.height * 0.5f,
                        lowestUpper.anchoredPosition.y + lowestUpper.rect.height * 0.5f, language.ToString());
                }
            }
        }

        private static LocalizationLanguage SystemDefaultLanguage() => Application.systemLanguage switch
        {
            SystemLanguage.Korean => LocalizationLanguage.Korean,
            SystemLanguage.Japanese => LocalizationLanguage.Japanese,
            _ => LocalizationLanguage.English
        };

        private static string DataResetPanelText(string key) =>
            (string)typeof(DataResetPanel).GetMethod("Tr", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { key });
    }
}
