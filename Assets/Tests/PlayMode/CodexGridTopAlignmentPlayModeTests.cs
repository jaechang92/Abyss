using System.Collections;
using System.Reflection;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class CodexGridTopAlignmentPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private CodexPanel codex;
        private GameObject events, previousSelection;
        private bool ownsCodex;
        private LocalizationLanguage language;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            guard.Acquire(); language = Loc.CurrentLanguage;
            if (EventSystem.current == null) events = new GameObject("GridTopEvents", typeof(EventSystem));
            previousSelection = EventSystem.current.currentSelectedGameObject;
            ownsCodex = Object.FindAnyObjectByType<CodexPanel>() == null;
            CodexPanel.Open(); codex = Object.FindAnyObjectByType<CodexPanel>();
            yield return Settle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                CodexPanel.Close();
                if (ownsCodex && codex != null) Object.Destroy(codex.gameObject);
                Loc.SetLanguage(language);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(previousSelection);
                if (events != null) Object.Destroy(events);
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator FirstVisibleRowStartsAtViewportTopAcrossScalesTabsPagesAndReopen()
        {
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, 1f })
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(scale));
                foreach (int tab in new[] { 0, 1, 2, 3, 4 })
                {
                    Field<Button[]>(codex, "tabButtons")[tab].onClick.Invoke(); yield return Settle();
                    AssertTop();
                }
            }
            Field<Button[]>(codex, "tabButtons")[1].onClick.Invoke(); yield return Settle();
            var next = Field<Button>(codex, "nextButton"); Assert.True(next.interactable);
            next.onClick.Invoke(); yield return Settle(); AssertTop();
            CodexPanel.Close(); CodexPanel.Open(); yield return Settle(); AssertTop();
        }

        [UnityTest]
        public IEnumerator FocusedLastRowAndScrollBottomAreActuallyVisibleInViewportWorldBounds()
        {
            Field<Button[]>(codex, "tabButtons")[1].onClick.Invoke();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f)); yield return Settle();
            var grid = Field<GameObject>(codex, "gridRoot");
            var firstName = grid.transform.GetChild(0).Find("Name").GetComponent<Text>();
            firstName.text = "A deliberately long discovered skill title that wraps into several lines";
            yield return Settle();
            var scroll = Field<ScrollRect>(codex, "gridScroll");
            scroll.verticalNormalizedPosition = 1f; yield return Settle(); AssertTop();
            var last = (RectTransform)grid.transform.GetChild(14);
            Assert.True(last.gameObject.activeInHierarchy);
            EventSystem.current.SetSelectedGameObject(last.Find("Background").gameObject);
            yield return Settle();
            Rect bounds = InViewport(last, scroll.viewport);
            Assert.GreaterOrEqual(bounds.yMin, scroll.viewport.rect.yMin - 1f);
            Assert.LessOrEqual(bounds.yMax, scroll.viewport.rect.yMax + 1f);
            scroll.verticalNormalizedPosition = 0f; yield return Settle();
            bounds = InViewport(last, scroll.viewport);
            Assert.GreaterOrEqual(bounds.yMin, scroll.viewport.rect.yMin - 1f);
            Assert.LessOrEqual(bounds.yMin, scroll.viewport.rect.yMin + 16f);
        }

        private void AssertTop()
        {
            var scroll = Field<ScrollRect>(codex, "gridScroll");
            scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases();
            var tile = (RectTransform)Field<GameObject>(codex, "gridRoot").transform.GetChild(0);
            Assert.AreEqual(1f, tile.anchorMin.y); Assert.AreEqual(1f, tile.anchorMax.y);
            Rect bounds = InViewport(tile, scroll.viewport);
            Assert.AreEqual(scroll.viewport.rect.yMax - 8f, bounds.yMax, 1.5f,
                "Compare rendered world corners, not the same anchoredPosition formula used by layout.");
        }

        private static Rect InViewport(RectTransform rect, RectTransform viewport)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var corner in corners)
            {
                Vector2 point = viewport.InverseTransformPoint(corner);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static IEnumerator Settle() { yield return null; Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases(); }
    }
}
