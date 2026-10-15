using System.Collections;
using System.Reflection;
using Abyss.Runtime.Events;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed class PauseMenuReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private GameObject canvas;
        private LocalizationLanguage oldLanguage;
        private float oldTimeScale;

        [SetUp]
        public void SetUp()
        {
            guard.Acquire();
            oldLanguage = Loc.CurrentLanguage;
            oldTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (canvas != null) Object.Destroy(canvas);
                Loc.SetLanguage(oldLanguage);
                Time.timeScale = oldTimeScale;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator PausedConfirmationFitsThreeLanguagesAndRestoresWithoutActivatingExit()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            canvas = UiFactory.CreateOverlayCanvas("PauseReadabilityTest", 500);
            canvas.SetActive(false);
            var body = UiFactory.CreateDimBody(canvas.transform);
            var panelObject = UiFactory.CreateRect(body.transform, "Panel", Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(420, 420));
            var panel = (RectTransform)panelObject.transform;
            var title = UiFactory.CreateLabel(panel, "TitleText", new Vector2(0, 146), new Vector2(380, 40), "", 24, Color.white, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -44f);
            string[] fields = { "resumeButton", "settingsButton", "toTitleButton", "quitButton" };
            var buttons = new Button[4];
            var pause = canvas.AddComponent<PausePanel>();
            Set(pause, "root", body);
            for (int i = 0; i < 4; i++)
            {
                buttons[i] = UiFactory.CreateButton(panel, fields[i], new Vector2(0, 78 - i * 62), new Vector2(320, 54), "", 20);
                Set(pause, fields[i], buttons[i]);
            }
            canvas.SetActive(true);
            Time.timeScale = 0f;
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                GameEvents.RaiseGameResumed();
                GameEvents.RaiseGamePaused();
                buttons[2].onClick.Invoke();
                yield return null;
                Assert.AreEqual(Loc.Get(StringKey.Menu_ToTitleConfirm), buttons[2].GetComponentInChildren<Text>().text);
                buttons[3].onClick.Invoke(); // Arms quit and disarms title; never confirms either exit.
                yield return null;
                Assert.AreEqual(Loc.Get(StringKey.Menu_ToTitle), buttons[2].GetComponentInChildren<Text>().text);
                Assert.AreEqual(Loc.Get(StringKey.Menu_QuitConfirm), buttons[3].GetComponentInChildren<Text>().text);
                Assert.AreEqual(0f, Time.timeScale);
                Assert.AreEqual(36, title.fontSize);
                for (int i = 0; i < 4; i++)
                {
                    var text = buttons[i].GetComponentInChildren<Text>();
                    Assert.AreEqual(30, text.fontSize);
                    Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f, language.ToString());
                    var rect = (RectTransform)buttons[i].transform;
                    Assert.Greater(rect.anchoredPosition.y - rect.rect.height * 0.5f, -panel.rect.height * 0.5f);
                    if (i > 0)
                    {
                        var previous = (RectTransform)buttons[i - 1].transform;
                        Assert.Less(rect.anchoredPosition.y + rect.rect.height * 0.5f,
                            previous.anchoredPosition.y - previous.rect.height * 0.5f);
                        Assert.AreSame(buttons[i], buttons[i - 1].navigation.selectOnDown);
                    }
                }
            }
            GameEvents.RaiseGameResumed();
            Assert.False(body.activeSelf);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            GameEvents.RaiseGamePaused();
            yield return null;
            Assert.AreEqual(420f, panel.rect.height);
            Assert.AreEqual(54f, ((RectTransform)buttons[3].transform).rect.height);
        }

        private static void Set(object obj, string name, object value) => obj.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    }
}
