using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

namespace Abyss.Tests.PlayMode
{
    public sealed class ShopReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private ShopData data;
        private Keyboard keyboard;
        private InputSettings previousInputSettings;
        private InputSettings testInputSettings;
        private ShopRoomPanel panel;
        private bool ownsPanel;
        private bool ownsRun;
        private GameObject ownedEvents;
        private GameObject previousSelection;
        private bool previousNavigation;
        private LocalizationLanguage previousLanguage;
        private float previousTimeScale;
        private Random.State previousRandom;
        private int previousGold;
        private int openedCount;
        private int closedCount;
        private int resolvedCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire();
            openedCount = closedCount = resolvedCount = 0;
            previousLanguage = Loc.CurrentLanguage;
            previousTimeScale = Time.timeScale;
            previousRandom = Random.state;
            previousInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            Assert.False(ShopRoomPanel.IsOpen, "This fixture requires no existing open shop.");
            ownsPanel = Object.FindAnyObjectByType<ShopRoomPanel>() == null;
            ownsRun = !RunManager.HasInstance;
            previousGold = RunManager.Instance.GoldShards;
            // Purchasing gold-only effects does not require starting or replacing an existing run.
            RunManager.Instance.GainGoldShards(1000 - previousGold);
            if (EventSystem.current == null)
                ownedEvents = new GameObject("ShopReadabilityTestEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            previousNavigation = EventSystem.current.sendNavigationEvents;
            data = ScriptableObject.CreateInstance<ShopData>();
            data.shopId = "readability_test";
            data.title = "길어진 상점 안내 제목 Long shop title 長い店舗の案内";
            data.description = "가격과 재고를 읽고 구매할 품목을 선택하세요. Read the price and remaining stock before buying. 購入前に価格と在庫を確認してください。";
            for (int i = 0; i < 6; i++)
                data.items.Add(new ShopItem
                {
                    label = "긴 품목 이름 Long item name 長い商品名 " + i,
                    description = "자세한 상품 설명을 끝까지 읽을 수 있어야 합니다. A long item description remains readable. 詳しい商品の説明を最後まで読めます。",
                    stock = 1,
                    effects = new List<EventEffect> { new() { type = EventEffectType.GoldSpend, amount = 25 + i } }
                });
            GameEvents.OnDraftOpened += HandleOpened;
            GameEvents.OnDraftClosed += HandleClosed;
            GameEvents.OnEventResolved += HandleResolved;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                GameEvents.OnDraftOpened -= HandleOpened;
                GameEvents.OnDraftClosed -= HandleClosed;
                GameEvents.OnEventResolved -= HandleResolved;
                if (panel != null && ShopRoomPanel.IsOpen) Field<Button>("leaveButton").onClick.Invoke();
                if (ownsPanel && panel != null) Object.Destroy(panel.gameObject);
                if (RunManager.HasInstance)
                {
                    RunManager.Instance.GainGoldShards(previousGold - RunManager.Instance.GoldShards);
                    if (ownsRun) Object.Destroy(RunManager.Instance.gameObject);
                }
                if (EventSystem.current != null)
                {
                    EventSystem.current.sendNavigationEvents = previousNavigation;
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                }
                if (ownedEvents != null) Object.Destroy(ownedEvents);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = previousInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
                if (data != null) Object.Destroy(data);
                Loc.SetLanguage(previousLanguage);
                Time.timeScale = previousTimeScale;
                Random.state = previousRandom;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator LargeTextFitsAllRowsAndSelectedLastItemIsVisibleAcrossLanguages()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            ShopRoomPanel.Open(data);
            panel = Object.FindAnyObjectByType<ShopRoomPanel>();
            yield return SettleLayout();
            float baselineHeight = ((RectTransform)Field<List<GameObject>>("itemRows")[0].transform).rect.height;
            int baselineFont = Field<List<Text>>("nameLabels")[0].fontSize;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return null;
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                ShopRoomPanel.Open(data);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var rows = Field<List<GameObject>>("itemRows");
                var names = Field<List<Text>>("nameLabels");
                var descriptions = Field<List<Text>>("descLabels");
                var costs = Field<List<Text>>("costLabels");
                var stocks = Field<List<Text>>("stockLabels");
                var buys = Field<List<Button>>("buyButtons");
                for (int i = 0; i < 6; i++)
                {
                    Assert.Greater(((RectTransform)rows[i].transform).rect.height, 76f);
                    Assert.AreEqual(data.items[i].CostText, costs[i].text);
                    Assert.AreEqual("남은 재고 1", stocks[i].text);
                    Assert.True(buys[i].interactable);
                    AssertFits(names[i], language);
                    AssertFits(descriptions[i], language);
                    AssertFits(costs[i], language);
                    AssertFits(stocks[i], language);
                    Assert.AreEqual(25 + i, data.items[i].GoldCost);
                    Assert.AreEqual(1, data.items[i].stock);
                }
                var scroll = Field<ScrollRect>("shopReadingScroll");
                Assert.NotNull(scroll);
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                EventSystem.current.SetSelectedGameObject(buys[5].gameObject);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, buys[5].transform);
                Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f, language.ToString());
                Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f, language.ToString());
                Assert.AreEqual(1000, RunManager.Instance.GoldShards);
            }
            Assert.AreEqual(1, openedCount, "Changing scale/language must not re-enter the shop pause state.");
            Assert.Zero(resolvedCount);
            Loc.SetLanguage(previousLanguage);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return SettleLayout();
            Assert.AreEqual(baselineHeight, ((RectTransform)Field<List<GameObject>>("itemRows")[0].transform).rect.height, 0.5f);
            Assert.AreEqual(baselineFont, Field<List<Text>>("nameLabels")[0].fontSize);
            float expandedContent = Field<ScrollRect>("shopReadingScroll").content.rect.height;
            data.items.RemoveRange(1, 5);
            ShopRoomPanel.Open(data);
            yield return null;
            for (int i = 1; i < 6; i++) Assert.False(Field<List<GameObject>>("itemRows")[i].activeSelf);
            Assert.Less(Field<ScrollRect>("shopReadingScroll").content.rect.height, expandedContent);
        }

        [UnityTest]
        public IEnumerator EnlargedShopKeepsPurchaseCostStockAndSingleLeaveEvents()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            ShopRoomPanel.Open(data);
            panel = Object.FindAnyObjectByType<ShopRoomPanel>();
            yield return null;
            Field<List<Button>>("buyButtons")[0].onClick.Invoke();
            Assert.AreEqual(975, RunManager.Instance.GoldShards);
            Assert.AreEqual("품절", Field<List<Text>>("stockLabels")[0].text);
            Assert.False(Field<List<Button>>("buyButtons")[0].interactable);
            Assert.True(ShopRoomPanel.IsOpen);
            Assert.Zero(resolvedCount);
            Field<List<Button>>("buyButtons")[0].onClick.Invoke();
            Assert.AreEqual(975, RunManager.Instance.GoldShards, "A sold-out item must not charge twice.");
            var leave = Field<Button>("leaveButton");
            leave.onClick.Invoke();
            leave.onClick.Invoke();
            Assert.False(ShopRoomPanel.IsOpen);
            Assert.AreEqual(1, openedCount);
            Assert.AreEqual(1, closedCount);
            Assert.AreEqual(1, resolvedCount);
            Assert.AreEqual(25, data.items[0].GoldCost);
            Assert.AreEqual(1, data.items[0].stock);
        }

        [UnityTest]
        public IEnumerator SelectingOversizedRowStillShowsItsBuyButton()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            data.items[5].description = string.Join("\n", new string[24]).Replace("\n", "A long description line.\n");
            ShopRoomPanel.Open(data);
            panel = Object.FindAnyObjectByType<ShopRoomPanel>();
            yield return null;
            var scroll = Field<ScrollRect>("shopReadingScroll");
            var rows = Field<List<GameObject>>("itemRows");
            Assert.Greater(((RectTransform)rows[5].transform).rect.height, scroll.viewport.rect.height);
            var last = Field<List<Button>>("buyButtons")[5];
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, last.transform);
            Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 1f);
            Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 1f);
        }

        [UnityTest]
        public IEnumerator PageDownReadsUnavailableItemsWithoutBuyingOrLeaving()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            foreach (var item in data.items)
                item.effects[0] = new EventEffect { type = EventEffectType.GoldSpend, amount = 2000 };
            keyboard = InputSystem.AddDevice<Keyboard>();
            ShopRoomPanel.Open(data);
            panel = Object.FindAnyObjectByType<ShopRoomPanel>();
            yield return null;
            var scroll = Field<ScrollRect>("shopReadingScroll");
            foreach (var button in Field<List<Button>>("buyButtons")) Assert.False(button.interactable);
            Assert.AreSame(Field<Button>("leaveButton").gameObject, EventSystem.current.currentSelectedGameObject);
            float before = scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return null;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, before);
            Assert.AreEqual(1000, RunManager.Instance.GoldShards);
            Assert.Zero(resolvedCount);
            Assert.True(ShopRoomPanel.IsOpen);
        }
        [UnityTest]
        public IEnumerator ClearedPointerSelectionRestoresLastBuyWithoutUndoingWheelScroll()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            ShopRoomPanel.Open(data);
            panel = Object.FindAnyObjectByType<ShopRoomPanel>();
            yield return SettleLayout();
            var last = Field<List<Button>>("buyButtons")[5];
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;
            yield return null;
            var scroll = Field<ScrollRect>("shopReadingScroll");
            // Model the EventSystem's deselection when a pointer targets the scroll area.
            // OnScroll is the actual UI scroll-handler; this does not claim a physical pointer click.
            scroll.verticalNormalizedPosition = 0.5f;
            EventSystem.current.SetSelectedGameObject(null);
            scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
            float expectedPosition = scroll.content.anchoredPosition.y;
            yield return null;
            yield return null;
            Assert.AreSame(last.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(expectedPosition, scroll.content.anchoredPosition.y, 0.5f,
                "Restoring null selection must not auto-reveal the first or previously selected item again.");
            Assert.AreEqual(1000, RunManager.Instance.GoldShards);
            Assert.Zero(resolvedCount);
        }

        private static IEnumerator SettleLayout()
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null; // Let LateUpdate remeasure fonts against the settled Canvas scale.
            Canvas.ForceUpdateCanvases();
        }
        private static void AssertFits(Text text, LocalizationLanguage language)
        {
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, text.name + " " + language);
        }

        private T Field<T>(string name) => (T)typeof(ShopRoomPanel)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
        private void HandleOpened() => openedCount++;
        private void HandleClosed() => closedCount++;
        private void HandleResolved() => resolvedCount++;
    }
}