using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using SaveSystem_Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class RelicShopReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private RelicShopPanel panel;
        private GameObject canvas;
        private GameObject ownedEvents;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage previousLanguage;
        private float previousTimeScale;
        private Random.State previousRandom;
        private InputSettings previousInput;
        private InputSettings testInput;
        private RelicData[] previousCatalog;
        private RelicData[] catalog;
        private int closedCount;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static FieldInfo CatalogField => typeof(RelicCatalog).GetField("all", BindingFlags.NonPublic | BindingFlags.Static);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            closedCount = 0;
            previousLanguage = Loc.CurrentLanguage;
            previousTimeScale = Time.timeScale;
            previousRandom = Random.state;
            previousInput = InputSystem.settings;
            testInput = Object.Instantiate(previousInput);
            testInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInput;
            previousCatalog = (RelicData[])CatalogField.GetValue(null);
            catalog = new RelicData[20];
            var meta = MetaSaveService.Instance;
            meta.Current.relics.Clear();
            meta.Current.equippedRelicIds.Clear();
            meta.Current.abyssShardsTotal = 1000;
            for (int i = 0; i < catalog.Length; i++)
            {
                var relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicId = "readability_relic_" + i;
                relic.nameKey = StringKey.Menu_ToTitleConfirm;
                relic.descKey = StringKey.Menu_QuitConfirm;
                relic.maxLevel = 5;
                relic.effectType = MetaUpgradeType.MaxHp;
                catalog[i] = relic;
                meta.Current.relics.Add(new RelicEntry { relicId = relic.relicId, level = 1 });
            }
            CatalogField.SetValue(null, catalog);
            for (int i = 0; i < 3; i++) Assert.True(meta.TryEquipRelic(catalog[i].relicId, i, autoSave: false));
            Assert.True(meta.Save());
            if (EventSystem.current == null)
                ownedEvents = new GameObject("RelicShopReadabilityEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            previousNavigation = EventSystem.current.sendNavigationEvents;
            canvas = UiFactory.CreateOverlayCanvas("RelicShopReadabilityTest", 500);
            panel = canvas.AddComponent<RelicShopPanel>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.SetFileOperations(null);
                if (panel != null) panel.Close();
                if (canvas != null) Object.Destroy(canvas);
                CatalogField.SetValue(null, previousCatalog);
                if (catalog != null)
                    foreach (var relic in catalog) if (relic != null) Object.Destroy(relic);
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                InputSystem.settings = previousInput;
                if (testInput != null) Object.Destroy(testInput);
                Loc.SetLanguage(previousLanguage);
                Time.timeScale = previousTimeScale;
                Random.state = previousRandom;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator EnlargedThreeSlotsOwnedTilesAndDrawFitThreeLanguagesAndRestore()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            var firstName = Find("Slot_0").Find("Name").GetComponent<Text>();
            float baselineSlot = ((RectTransform)firstName.transform.parent).rect.height;
            int baselineFont = firstName.fontSize;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                panel.Open(() => closedCount++);
                yield return SettleLayout();
                Assert.AreEqual(Loc.Get(StringKey.Relic_Title), Find("Title").GetComponent<Text>().text);
                Assert.AreEqual(Loc.Get(StringKey.Relic_Equipped), Find("EquipHeader").GetComponent<Text>().text);
                Assert.AreEqual(Loc.Get(StringKey.Relic_OwnedHeader), Field<Text>("ownedHeaderLabel").text);
                for (int i = 0; i < 3; i++)
                {
                    var slot = Find("Slot_" + i);
                    var name = slot.Find("Name").GetComponent<Text>();
                    Assert.AreEqual(Loc.Get(catalog[i].nameKey), name.text);
                    Assert.AreEqual(Mathf.RoundToInt(baselineFont * 1.5f), name.fontSize);
                    AssertFits(name, language);
                    AssertFits(slot.Find("Level").GetComponent<Text>(), language);
                }
                foreach (var relic in catalog)
                {
                    var tile = Find("Tile_" + relic.relicId);
                    Assert.True(tile.gameObject.activeSelf);
                    Assert.AreEqual(Loc.Get(relic.nameKey), tile.Find("Name").GetComponent<Text>().text);
                    AssertFits(tile.Find("Name").GetComponent<Text>(), language);
                    AssertFits(tile.Find("Level").GetComponent<Text>(), language);
                }
                var draw = Field<Button>("drawButton");
                AssertFits(draw.GetComponentInChildren<Text>(), language);
                Assert.True(draw.interactable);
                var scroll = Field<ScrollRect>("readingScroll");
                Assert.NotNull(scroll);
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                var last = Find("Tile_" + catalog[^1].relicId).GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(last.gameObject);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, last.transform);
                Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f, language.ToString());
                Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f, language.ToString());
                Assert.AreEqual(1000, MetaSaveService.Instance.Current.abyssShardsTotal);
                Assert.AreEqual(20, MetaSaveService.Instance.Current.relics.Count);
                Assert.Zero(closedCount);
            }
            Loc.SetLanguage(previousLanguage);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return SettleLayout();
            Assert.AreEqual(baselineFont, firstName.fontSize);
            Assert.AreEqual(baselineSlot, ((RectTransform)firstName.transform.parent).rect.height, 0.5f);
            panel.Close();
            panel.Close();
            Assert.AreEqual(1, closedCount);
        }

        [UnityTest]
        public IEnumerator DrawWriteFailurePreservesShardsLevelsAndPersistedSave()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            string before = JsonUtility.ToJson(MetaSaveService.Instance.Current);
            string disk = File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName));
            SaveSystem.Instance.SetFileOperations(new FailingWrites());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: relic readability test failure"));
            Field<Button>("drawButton").onClick.Invoke();
            Assert.AreEqual(before, JsonUtility.ToJson(MetaSaveService.Instance.Current));
            Assert.AreEqual(disk, File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName)));
            Assert.True(panel.IsOpen);
            Assert.Zero(closedCount);
        }

        [UnityTest]
        public IEnumerator EquipAndUnequipWriteFailuresKeepExistingSlotsAndDisk()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            Assert.True(MetaSaveService.Instance.UnequipRelicSlot(2));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            string before = JsonUtility.ToJson(MetaSaveService.Instance.Current);
            string disk = File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName));
            SaveSystem.Instance.SetFileOperations(new FailingWrites());
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: relic readability test failure"));
            Find("Tile_" + catalog[3].relicId).GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(before, JsonUtility.ToJson(MetaSaveService.Instance.Current));
            Assert.AreEqual(disk, File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName)));
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: relic readability test failure"));
            Find("Slot_0").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(before, JsonUtility.ToJson(MetaSaveService.Instance.Current));
            Assert.AreEqual(disk, File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName)));
            Assert.True(panel.IsOpen);
            Assert.Zero(closedCount);
        }

        [UnityTest]
        public IEnumerator ClearedPointerSelectionPreservesLastTileAndWheelPosition()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            var last = Find("Tile_" + catalog[^1].relicId).GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;
            yield return null;
            var scroll = Field<ScrollRect>("readingScroll");
            scroll.verticalNormalizedPosition = 0.5f;
            // Model pointer deselection and invoke the actual wheel handler. No physical click is claimed.
            EventSystem.current.SetSelectedGameObject(null);
            scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
            float expected = scroll.content.anchoredPosition.y;
            yield return null;
            yield return null;
            Assert.AreSame(last.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(expected, scroll.content.anchoredPosition.y, 0.5f);
            Assert.AreEqual(1000, MetaSaveService.Instance.Current.abyssShardsTotal);
            Assert.Zero(closedCount);
        }

        [UnityTest]
        public IEnumerator EmptyInventoryWithoutFundsKeepsCloseReachable()
        {
            var meta = MetaSaveService.Instance;
            meta.Current.relics.Clear();
            meta.Current.equippedRelicIds.Clear();
            meta.Current.abyssShardsTotal = 0;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            foreach (var relic in catalog) Assert.False(Find("Tile_" + relic.relicId).gameObject.activeSelf);
            for (int i = 0; i < 3; i++) Assert.False(Find("Slot_" + i).GetComponent<Button>().interactable);
            Assert.False(Field<Button>("drawButton").interactable);
            var close = Field<Button>("closeButton");
            Assert.True(close.interactable);
            Assert.AreSame(close.gameObject, EventSystem.current.currentSelectedGameObject);
            foreach (var next in new[] { close.navigation.selectOnUp, close.navigation.selectOnDown,
                close.navigation.selectOnLeft, close.navigation.selectOnRight })
                Assert.True(next == null || (next.IsActive() && next.IsInteractable()));
            close.onClick.Invoke();
            Assert.False(panel.IsOpen);
            Assert.AreEqual(1, closedCount);
            Assert.Zero(meta.Current.abyssShardsTotal);
            Assert.Zero(meta.Current.relics.Count);
        }

        [UnityTest]
        public IEnumerator SuccessfulDrawResultRelocalizesNameAndLevelTemplate()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            Field<Button>("drawButton").onClick.Invoke();
            var meta = MetaSaveService.Instance;
            var drawn = catalog.Single(relic => meta.GetRelicLevel(relic.relicId) == 2);
            Assert.AreEqual(1000 - RelicGacha.DRAW_COST, meta.Current.abyssShardsTotal);
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                yield return SettleLayout();
                var message = Field<Text>("messageLabel");
                Assert.AreEqual(Loc.GetFormat(StringKey.Relic_LevelUpFormat, Loc.Get(drawn.nameKey), 2), message.text);
                AssertFits(message, language);
                Assert.AreEqual(2, meta.GetRelicLevel(drawn.relicId));
                Assert.Zero(closedCount);
            }
        }

        [UnityTest]
        public IEnumerator AutomaticEquipSecondWriteFailureKeepsSuccessfulDraw()
        {
            var meta = MetaSaveService.Instance;
            meta.Current.relics.Clear();
            meta.Current.equippedRelicIds.Clear();
            CatalogField.SetValue(null, new[] { catalog[0] });
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            Assert.True(meta.Save());
            var writes = new SecondWriteFails();
            SaveSystem.Instance.SetFileOperations(writes);
            LogAssert.Expect(LogType.Error, new Regex("저장 실패: relic automatic equip second write failure"));
            Field<Button>("drawButton").onClick.Invoke();
            Assert.AreEqual(2, writes.Count);
            Assert.AreEqual(1000 - RelicGacha.DRAW_COST, meta.Current.abyssShardsTotal);
            Assert.AreEqual(1, meta.GetRelicLevel(catalog[0].relicId));
            Assert.AreEqual(1, meta.Current.relics.Count);
            foreach (var id in meta.EquippedRelicIds) Assert.IsEmpty(id);
            var persisted = JsonUtility.FromJson<MetaSave>(File.ReadAllText(Path.Combine(guard.TestDirectory, MetaSave.FileName)));
            Assert.AreEqual(JsonUtility.ToJson(persisted), JsonUtility.ToJson(meta.Current));
            Assert.AreEqual(Loc.Get(StringKey.SaveStatus_WriteFailed), Field<Text>("messageLabel").text);
            Assert.True(panel.IsOpen);
            Assert.Zero(closedCount);
        }

        [UnityTest]
        public IEnumerator ReopeningAfterHiddenLanguageChangeRefreshesHeadersAndTileNames()
        {
            panel.Open(() => closedCount++);
            yield return SettleLayout();
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                panel.Close();
                Loc.SetLanguage(language);
                panel.Open(() => closedCount++);
                yield return SettleLayout();
                Assert.AreEqual(Loc.Get(StringKey.Relic_Title), Find("Title").GetComponent<Text>().text);
                Assert.AreEqual(Loc.Get(StringKey.Relic_Equipped), Find("EquipHeader").GetComponent<Text>().text);
                Assert.AreEqual(Loc.Get(catalog[0].nameKey), Find("Tile_" + catalog[0].relicId).Find("Name").GetComponent<Text>().text);
                Assert.AreEqual(Loc.GetFormat(StringKey.Relic_DrawFormat, RelicGacha.DRAW_COST), Field<Button>("drawButton").GetComponentInChildren<Text>().text);
                Assert.AreEqual(1000, MetaSaveService.Instance.Current.abyssShardsTotal);
            }
        }

        private sealed class SecondWriteFails : SaveFileOperations
        {
            public int Count { get; private set; }
            public override void WriteAllText(string path, string contents)
            {
                Count++;
                if (Count == 2) throw new IOException("relic automatic equip second write failure");
                base.WriteAllText(path, contents);
            }
        }
        private Transform Find(string name) => panel.GetComponentsInChildren<Transform>(true).Single(value => value.name == name);
        private T Field<T>(string name) => (T)typeof(RelicShopPanel).GetField(name, Private).GetValue(panel);
        private static void AssertFits(Text text, LocalizationLanguage language) =>
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.name + " " + language);
        private static IEnumerator SettleLayout()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        private sealed class FailingWrites : SaveFileOperations
        {
            public override void WriteAllText(string path, string contents) => throw new IOException("relic readability test failure");
        }
    }
}