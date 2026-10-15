using System.Collections;
using System.Reflection;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
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
    public sealed class CodexReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private CodexPanel codex;
        private GameObject events;
        private GameObject oldSelection;
        private LocalizationLanguage oldLanguage;
        private float oldTimeScale;
        private InputSettings oldInput;
        private InputSettings testInput;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private bool ownsCodex;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            oldLanguage = Loc.CurrentLanguage;
            oldTimeScale = Time.timeScale;
            oldInput = InputSystem.settings;
            testInput = Object.Instantiate(oldInput);
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInput;
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            SettingsPanel.Close();
            if (EventSystem.current == null) events = new GameObject("CodexReadingEvents", typeof(EventSystem));
            oldSelection = EventSystem.current.currentSelectedGameObject;
            ownsCodex = Object.FindAnyObjectByType<CodexPanel>() == null;
            CodexPanel.Open();
            codex = Object.FindAnyObjectByType<CodexPanel>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SettingsPanel.Close();
                CodexPanel.Close();
                if (ownsCodex && codex != null) Object.Destroy(codex.gameObject);
                Loc.SetLanguage(oldLanguage);
                Time.timeScale = oldTimeScale;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
                InputSystem.settings = oldInput;
                Object.Destroy(testInput);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(oldSelection);
                if (events != null) Object.Destroy(events);
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator MaximumFontFitsHeaderTabsAndRecordsRemainInsidePanelInThreeLanguages()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            var tabs = Field<Button[]>(codex, "tabButtons");
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                tabs[5].onClick.Invoke();
                yield return null;
                yield return null;
                foreach (var tab in tabs)
                {
                    var text = tab.GetComponentInChildren<Text>();
                    Assert.AreEqual(Mathf.RoundToInt(19f * 1.5f), text.fontSize);
                    Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.text);
                    Assert.AreEqual(Navigation.Mode.Explicit, tab.navigation.mode);
                }
                var scroll = Field<ScrollRect>(codex, "recordsScroll");
                float right = float.MinValue;
                float left = float.MaxValue;
                foreach (var text in scroll.content.GetComponentsInChildren<Text>())
                {
                    Assert.AreEqual(Mathf.RoundToInt(19f * 1.5f), text.fontSize);
                    Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f);
                    left = Mathf.Min(left, text.rectTransform.anchoredPosition.x - text.rectTransform.rect.width * 0.5f);
                    right = Mathf.Max(right, text.rectTransform.anchoredPosition.x + text.rectTransform.rect.width * 0.5f);
                }
                Assert.GreaterOrEqual(left, -scroll.viewport.rect.width * 0.5f);
                Assert.LessOrEqual(right, scroll.viewport.rect.width * 0.5f);
                Assert.GreaterOrEqual(scroll.content.rect.height, scroll.content.GetChild(1).GetComponent<Text>().preferredHeight);
                Assert.False(Field<GameObject>(codex, "gridRoot").activeInHierarchy);
                Assert.IsNull(tabs[5].navigation.selectOnDown);
            }
        }

        [UnityTest]
        public IEnumerator LongDescriptionAndStatsHaveSeparateScrollableBoundsAndResetOnSelection()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            var description = Field<Text>(codex, "detailDescription");
            var stats = Field<Text>(codex, "detailStats");
            description.text = string.Join("\n", new string[25]).Replace("\n", "A long description line that wraps into additional detail.\n");
            stats.text = "Damage 100\nCooldown 1.0\nAdditional stat detail";
            yield return null;
            yield return null;
            var scroll = Field<ScrollRect>(codex, "detailScroll");
            Assert.AreEqual(26, description.fontSize);
            Assert.Less(stats.rectTransform.anchoredPosition.y,
                description.rectTransform.anchoredPosition.y - description.rectTransform.rect.height);
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, 0f);
            float bottom = -stats.rectTransform.anchoredPosition.y + stats.rectTransform.rect.height;
            Assert.LessOrEqual(bottom - scroll.content.anchoredPosition.y, scroll.viewport.rect.height + 1f);
            Field<Button[]>(codex, "tabButtons")[1].onClick.Invoke();
            yield return null;
            Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.1f);
        }

        [UnityTest]
        public IEnumerator EnlargedLongTileNamesStayInsideRowsAndFocusedBottomRowIsVisible()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Field<Button[]>(codex, "tabButtons")[1].onClick.Invoke();
            yield return null;
            var grid = Field<GameObject>(codex, "gridRoot");
            var scroll = Field<ScrollRect>(codex, "gridScroll");
            var first = grid.transform.GetChild(0);
            var name = first.Find("Name").GetComponent<Text>();
            name.text = "An exceptionally lengthy discovered skill name that needs multiple lines";
            yield return null;
            Assert.AreEqual(20, name.fontSize);
            var rect = (RectTransform)first;
            Assert.Greater(rect.rect.height, 150f);
            var frame = first.Find("ArtEntryFrame").GetComponent<Image>();
            Assert.AreEqual(frame.sprite.rect.width / frame.sprite.rect.height,
                frame.rectTransform.rect.width / frame.rectTransform.rect.height, 0.02f);
            Assert.GreaterOrEqual(name.rectTransform.anchoredPosition.y - name.rectTransform.rect.height * 0.5f, -rect.rect.height * 0.5f);
            var last = grid.transform.GetChild(14).Find("Background").GetComponent<Button>();
            Assert.True(last.gameObject.activeInHierarchy);
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;
            yield return null;
            var lastRect = (RectTransform)last.transform.parent;
            float bottom = -lastRect.anchoredPosition.y + lastRect.rect.height * 0.5f + 4f;
            Assert.LessOrEqual(bottom - scroll.content.anchoredPosition.y, scroll.viewport.rect.height + 1f);
            Assert.AreEqual(Navigation.Mode.Explicit, last.navigation.mode);
        }

        [UnityTest]
        public IEnumerator PageChangesPreserveFifteenSlotsAndDefaultFontRestoresWithoutGrowth()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Field<Button[]>(codex, "tabButtons")[1].onClick.Invoke();
            yield return null;
            var next = Field<Button>(codex, "nextButton");
            Assert.True(next.interactable);
            next.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, Field<int>(codex, "currentPage"));
            Assert.AreEqual(15, Field<int>(codex, "selectedIndex"));
            Assert.AreEqual(15, Field<GameObject>(codex, "gridRoot").transform.childCount);
            Field<Button>(codex, "prevButton").onClick.Invoke();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return null;
            yield return null;
            Assert.AreEqual(26, Field<Text>(codex, "detailName").fontSize);
            Assert.AreEqual(13, Field<GameObject>(codex, "gridRoot").transform.GetChild(0).Find("Name").GetComponent<Text>().fontSize);
            var scroll = Field<ScrollRect>(codex, "detailScroll");
            float height = scroll.content.rect.height;
            yield return null;
            Assert.AreEqual(height, scroll.content.rect.height, 0.1f);
        }

        [UnityTest]
        public IEnumerator OpenCodexRefreshesBodyLanguageAndScrollingWorksWhilePausedButNotUnderSettings()
        {
            Field<Button[]>(codex, "tabButtons")[5].onClick.Invoke();
            Loc.SetLanguage(LocalizationLanguage.English);
            yield return null;
            Assert.That(Field<Text>(codex, "recordsText").text, Does.Contain(Loc.Get(StringKey.Codex_Records_LastRunHeader)));
            Loc.SetLanguage(LocalizationLanguage.Korean);
            yield return null;
            Assert.That(Field<Text>(codex, "recordsText").text, Does.Contain(Loc.Get(StringKey.Codex_Records_LastRunHeader)));
            var text = Field<Text>(codex, "recordsSecondaryText");
            text.text = string.Join("\n", new string[40]).Replace("\n", "Record line\n");
            yield return null;
            Time.timeScale = 0f;
            var scroll = Field<ScrollRect>(codex, "recordsScroll");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return null;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, 0f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            // PageDown has already reached the bottom; reset before testing further downward input.
            scroll.content.anchoredPosition = Vector2.zero;
            float before = scroll.content.anchoredPosition.y;
            SettingsPanel.Open();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down });
            yield return null;
            yield return null;
            Assert.AreEqual(before, scroll.content.anchoredPosition.y, 0.1f);
            Assert.True(CodexPanel.IsOpen);
            SettingsPanel.Close();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.down });
            yield return null;
            yield return null;
            Assert.AreSame(gamepad, Gamepad.current);
            Assert.Less(gamepad.rightStick.ReadValue().y, -0.5f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, before);
        }
    }
}
