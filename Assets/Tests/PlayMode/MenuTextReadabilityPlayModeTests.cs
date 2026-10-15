using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Localization;
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
    public sealed class MenuTextReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new PlayModeSaveGuard();
        private GameObject root;
        private LocalizationLanguage oldLanguage;
        private const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name) => (T)obj.GetType().GetField(name,Private).GetValue(obj);
        [SetUp]
        public void SetUp() { guard.Acquire(); _ = MetaSaveService.Instance.Current; oldLanguage = Loc.CurrentLanguage; }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.SetFileOperations(null);
                SettingsPanel.Close();
                LobbyMenuPanel.Close();
                if (root != null) Object.Destroy(root);
                Loc.SetLanguage(oldLanguage);
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator SettingsMaximumFontFitsThreeLanguagesAndNavigationStaysInside()
        {
            SettingsPanel.Open();
            var settings = Object.FindAnyObjectByType<SettingsPanel>();
            var next = Field<Button>(settings,"menuTextNext");
            var previous = Field<Button>(settings,"menuTextPrev");
            next.onClick.Invoke();
            next.onClick.Invoke();
            Assert.AreEqual(1.5f,MenuTextReadability.Scale);
            Assert.AreEqual(1f,NarrativeReadability.Scale);
            Assert.False(next.interactable);
            Assert.AreSame(Field<Button>(settings,"closeButton"),previous.navigation.selectOnDown);
            var body = Field<GameObject>(settings,"body");
            var panel = body.transform.Find("Panel").GetComponent<RectTransform>();
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                yield return null;
                foreach (var text in panel.GetComponentsInChildren<Text>(true))
                {
                    Assert.LessOrEqual(text.preferredWidth,text.rectTransform.rect.width+1f,text.name+" "+language);
                    Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+1f,text.name+" "+language);
                }
            }
            Assert.AreEqual(760f,panel.rect.width);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return null;
            Assert.AreEqual(560f,panel.rect.width);
        }

        [UnityTest]
        public IEnumerator ActualLobbyQuitConfirmationWrapsAndDoesNotOverlapFooter()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            int closed = 0;
            LobbyMenuPanel.Open(() => closed++);
            var lobby = Object.FindAnyObjectByType<LobbyMenuPanel>();
            var buttons = Field<Button[]>(lobby,"menuButtons");
            var quit = buttons[buttons.Length-1];
            quit.onClick.Invoke(); // 1회만: 실제 종료를 요청하지 않는다.
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                yield return null;
                var text = quit.GetComponentInChildren<Text>();
                Assert.AreEqual(30,text.fontSize);
                Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+1f,language.ToString());
                var rect = (RectTransform)quit.transform;
                var previous = (RectTransform)buttons[buttons.Length-2].transform;
                Assert.Less(rect.anchoredPosition.y+rect.rect.height*0.5f,previous.anchoredPosition.y-previous.rect.height*0.5f);
                var hint = quit.transform.parent.Find("HintText").GetComponent<RectTransform>();
                Assert.Less(hint.anchoredPosition.y+hint.rect.height*0.5f,rect.anchoredPosition.y-rect.rect.height*0.5f);
                Assert.AreSame(quit,buttons[buttons.Length-2].navigation.selectOnDown);
            }
            LobbyMenuPanel.Close();
            Assert.AreEqual(1,closed);
            LobbyMenuPanel.Open(null);
            yield return null;
            Assert.AreEqual(54f,((RectTransform)quit.transform).rect.height);
            Assert.AreEqual(464f,((RectTransform)quit.transform.parent).rect.height);
        }

        [UnityTest]
        public IEnumerator ActualTitleButtonsScaleWithoutScalingBrandAndKeepConfirmationSafe()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            root = UiFactory.CreateOverlayCanvas("TitleMenuReadabilityTest",500);
            root.SetActive(false);
            var buttons = new Button[4];
            for (int i=0;i<buttons.Length;i++) buttons[i] = UiFactory.CreateButton(root.transform,"Button"+i,new Vector2(0,70-i*70),new Vector2(360,58),"",20);
            var brand = UiFactory.CreateLabel(root.transform,"Brand",new Vector2(0,300),new Vector2(900,120),"ABYSS",96,Color.white,TextAnchor.MiddleCenter);
            var title = root.AddComponent<TitleMenuPanel>();
            string[] fields = {"startButton","codexButton","settingsButton","quitButton"};
            for (int i=0;i<fields.Length;i++) typeof(TitleMenuPanel).GetField(fields[i],Private).SetValue(title,buttons[i]);
            typeof(TitleMenuPanel).GetField("startLabel",Private).SetValue(title,buttons[0].GetComponentInChildren<Text>());
            root.SetActive(true);
            buttons[3].onClick.Invoke();
            foreach (LocalizationLanguage language in System.Enum.GetValues(typeof(LocalizationLanguage)))
            {
                Loc.SetLanguage(language);
                yield return null;
                var text = buttons[3].GetComponentInChildren<Text>();
                Assert.AreEqual(30,text.fontSize);
                Assert.LessOrEqual(text.preferredHeight,text.rectTransform.rect.height+1f);
                Assert.AreSame(buttons[1],buttons[0].navigation.selectOnDown);
            }
            Assert.AreEqual(96,brand.fontSize);
        }

        [UnityTest]
        public IEnumerator LongRecordWrapsUpwardAndRestoresOriginalSizeWithoutCumulativeGrowth()
        {
            root = UiFactory.CreateOverlayCanvas("RecordReadabilityTest",500);
            var text = UiFactory.CreateLabel(root.transform,"Record",new Vector2(0,60),new Vector2(600,34),
                "Runs 2147483647 · Bosses Defeated 2147483647 · Abyss Shards 2147483647 · Cleared",18,Color.white,TextAnchor.MiddleCenter);
            text.rectTransform.pivot = new Vector2(0.5f,0f);
            var baseline = text.rectTransform.sizeDelta;
            var component = MenuTextReadability.Attach(text.transform);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return null;
            Assert.AreEqual(27,text.fontSize);
            Assert.AreEqual(HorizontalWrapMode.Wrap,text.horizontalOverflow);
            Assert.Greater(text.rectTransform.rect.height,34f);
            Assert.AreEqual(60f,text.rectTransform.anchoredPosition.y);
            component.Apply();
            var expanded = text.rectTransform.sizeDelta;
            component.Apply();
            component.Apply();
            Assert.AreEqual(expanded,text.rectTransform.sizeDelta);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1f));
            yield return null;
            Assert.AreEqual(18,text.fontSize);
            Assert.AreEqual(baseline.x,text.rectTransform.sizeDelta.x);
            Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
            Assert.AreEqual(60f,text.rectTransform.anchoredPosition.y);
        }

        [UnityTest]
        public IEnumerator CanvasScaleChangeReflowsTextWithoutChangingSavedMenuSize()
        {
            root = UiFactory.CreateOverlayCanvas("CanvasScaleReadabilityTest",500);
            root.GetComponent<CanvasScaler>().enabled = false;
            var canvas = root.GetComponent<Canvas>();
            var text = UiFactory.CreateLabel(root.transform,"Record",new Vector2(0,60),new Vector2(600,34),
                "Runs 2147483647 · Bosses Defeated 2147483647 · Abyss Shards 2147483647 · Cleared",18,Color.white,TextAnchor.MiddleCenter);
            MenuTextReadability.Attach(text.transform);
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            foreach (float scale in new[] {1f,0.75f,0.5f})
            {
                canvas.scaleFactor = scale;
                yield return null;
                yield return null;
                Assert.AreEqual(27,text.fontSize);
                Assert.AreEqual(1.5f,MenuTextReadability.Scale);
                Assert.GreaterOrEqual(text.rectTransform.rect.height+0.01f,text.preferredHeight);
            }
        }

        [UnityTest]
        public IEnumerator SavedMenuSizeRoundTripsWithoutChangingNarrativeOrEffects()
        {
            var settings = MetaSaveService.Instance.Current.settings;
            settings.narrativeTextScale = 1.25f;
            settings.cameraShakeScale = 0.5f;
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            MetaSaveService.Instance.UnloadWithoutSaving();
            Assert.AreEqual(1.5f,MetaSaveService.Instance.Current.settings.menuTextScale);
            Assert.AreEqual(1.25f,NarrativeReadability.Scale);
            Assert.AreEqual(0.5f,MetaSaveService.Instance.Current.settings.cameraShakeScale);
            Assert.False(MetaSaveService.Instance.TrySaveMenuTextScale(float.NaN));
            Assert.False(MetaSaveService.Instance.TrySaveMenuTextScale(2f));
            yield return null;
        }

        private sealed class FailingOperations : SaveFileOperations
        {
            public override void WriteAllText(string path,string contents) => throw new IOException("menu text test failure");
        }

        [UnityTest]
        public IEnumerator FailedSaveRestoresScaleWidgetRuntimeAndFile()
        {
            SettingsPanel.Open();
            var settings = Object.FindAnyObjectByType<SettingsPanel>();
            Field<Button>(settings,"menuTextNext").onClick.Invoke();
            string path = Path.Combine(guard.TestDirectory,MetaSave.FileName);
            string disk = File.ReadAllText(path);
            string timestamp = MetaSaveService.Instance.Current.saveTimeUtc;
            SaveSystem.Instance.SetFileOperations(new FailingOperations());
            LogAssert.Expect(LogType.Error,new Regex("저장 실패: menu text test failure"));
            Field<Button>(settings,"menuTextNext").onClick.Invoke();
            Assert.AreEqual(1.25f,MenuTextReadability.Scale);
            Assert.AreEqual("125%",Field<Text>(settings,"menuTextValue").text);
            Assert.AreEqual(21,Field<Text>(settings,"menuTextValue").fontSize);
            Assert.AreEqual(timestamp,MetaSaveService.Instance.Current.saveTimeUtc);
            Assert.AreEqual(disk,File.ReadAllText(path));
            SaveSystem.Instance.SetFileOperations(null);
            yield return null;
        }
    }
}
