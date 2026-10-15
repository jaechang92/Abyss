using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Abyss.Runtime.Dialogue;
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
    public sealed class NarrativeReadabilityPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new PlayModeSaveGuard();
        private GameObject root;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object obj,string name) => (T)obj.GetType().GetField(name,Private).GetValue(obj);

        [SetUp] public void SetUp() { guard.Acquire(); _ = MetaSaveService.Instance.Current; }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                SaveSystem.Instance.SetFileOperations(null);
                SettingsPanel.Close();
                if (root != null) Object.Destroy(root);
                yield return null;
            }
            finally { guard.Release(); }
        }

        private Text CreateText()
        {
            root = UiFactory.CreateOverlayCanvas("ReadabilityTest",500);
            return UiFactory.CreateLabel(root.transform,"TestText",Vector2.zero,new Vector2(600,44),
                "This subtitle should wrap when the player chooses larger dialogue text.",26,new Color(0.8f,0.7f,0.6f,0.3f),TextAnchor.MiddleCenter);
        }

        [UnityTest]
        public IEnumerator TextScaleWrapContrastAndRestorationPreserveAlphaAndTopEdge()
        {
            var text = CreateText();
            var rect = text.rectTransform;
            var size = rect.sizeDelta;
            var pivot = rect.pivot;
            var position = rect.anchoredPosition;
            var originalColor = text.color;
            float top = position.y + size.y * (1f - pivot.y);
            var component = NarrativeReadability.Attach(text);
            Assert.AreSame(component,NarrativeReadability.Attach(text));
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            yield return null;
            Assert.AreEqual(39,text.fontSize);
            Assert.AreEqual(HorizontalWrapMode.Wrap,text.horizontalOverflow);
            Assert.GreaterOrEqual(rect.rect.height,text.preferredHeight);
            Assert.AreEqual(top,rect.anchoredPosition.y+rect.rect.height*(1f-rect.pivot.y));
            Assert.AreEqual(0.3f,text.color.a);
            var background = Field<Image>(component,"background");
            Assert.True(background.gameObject.activeSelf);
            Assert.False(background.raycastTarget);
            Assert.Less(background.transform.GetSiblingIndex(),text.transform.GetSiblingIndex());
            text.gameObject.SetActive(false);
            Assert.False(background.gameObject.activeSelf);
            text.gameObject.SetActive(true);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1f,false));
            yield return null;
            Assert.AreEqual(26,text.fontSize);
            Assert.AreEqual(size,rect.sizeDelta);
            Assert.AreEqual(pivot,rect.pivot);
            Assert.AreEqual(position,rect.anchoredPosition);
            Assert.AreEqual(originalColor,text.color);
            Assert.False(background.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator NpcGeometryExpandsWithoutMovingBottomOrOverlappingSpeaker()
        {
            root = UiFactory.CreateOverlayCanvas("NpcReadabilityTest",500);
            var panel = UiFactory.CreateRect(root.transform,"Panel",Vector2.one*0.5f,Vector2.one*0.5f,
                Vector2.one*0.5f,new Vector2(0,-300),new Vector2(1200,220)).GetComponent<RectTransform>();
            var text = UiFactory.CreateLabel(panel,"Body",new Vector2(0,-5),new Vector2(1120,120),
                string.Join(" ",new string[100]).Replace(" ","long dialogue "),24,Color.white,TextAnchor.UpperLeft);
            var speaker = UiFactory.CreateLabel(panel,"Speaker",new Vector2(0,75),new Vector2(1120,36),"Speaker",22,Color.white,TextAnchor.MiddleLeft);
            float bottom = panel.anchoredPosition.y-panel.rect.height*panel.pivot.y;
            var initialSize = panel.sizeDelta;
            var initialPosition = panel.anchoredPosition;
            NarrativeReadability.Attach(text,panel,speaker.rectTransform);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            yield return null;
            Assert.Greater(panel.rect.height,initialSize.y);
            Assert.AreEqual(bottom,panel.anchoredPosition.y-panel.rect.height*panel.pivot.y,0.01f);
            Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
            float textTop = text.rectTransform.anchoredPosition.y+text.rectTransform.rect.height*(1f-text.rectTransform.pivot.y);
            float speakerBottom = speaker.rectTransform.anchoredPosition.y-speaker.rectTransform.rect.height*speaker.rectTransform.pivot.y;
            Assert.Greater(speakerBottom,textTop);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1f,false));
            yield return null;
            Assert.AreEqual(initialSize,panel.sizeDelta);
            Assert.AreEqual(initialPosition,panel.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator ActualNpcDialogueAttachesStyleAndPreservesCompletion()
        {
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            root = UiFactory.CreateOverlayCanvas("DialogueIntegrationTest",500);
            root.SetActive(false);
            var panel = UiFactory.CreateRect(root.transform,"DialogueRoot",Vector2.one*0.5f,Vector2.one*0.5f,
                Vector2.one*0.5f,new Vector2(0,-300),new Vector2(1200,220));
            var body = UiFactory.CreateLabel(panel.transform,"Body",new Vector2(0,-5),new Vector2(1120,120),"",24,Color.white,TextAnchor.UpperLeft);
            var speaker = UiFactory.CreateLabel(panel.transform,"Speaker",new Vector2(0,75),new Vector2(1120,36),"",22,Color.white,TextAnchor.MiddleLeft);
            var dialogue = root.AddComponent<DialogueUI>();
            typeof(DialogueUI).GetField("root",Private).SetValue(dialogue,panel);
            typeof(DialogueUI).GetField("bodyLabel",Private).SetValue(dialogue,body);
            typeof(DialogueUI).GetField("speakerLabel",Private).SetValue(dialogue,speaker);
            root.SetActive(true);
            int completions = 0;
            dialogue.Play(new[] {new DialogueLine {speakerKey="Npc_Guide_Name",textKey="Npc_Guide_Line1"}},() => completions++);
            yield return null;
            Assert.True(dialogue.IsOpen);
            Assert.NotNull(body.GetComponent<NarrativeReadability>());
            Assert.AreEqual(36,body.fontSize);
            Assert.AreEqual(Loc.Get("Npc_Guide_Line1"),body.text);
            typeof(DialogueUI).GetMethod("Advance",Private).Invoke(dialogue,null);
            Assert.False(dialogue.IsOpen);
            Assert.AreEqual(1,completions);
        }

        [UnityTest]
        public IEnumerator ActualBossSubtitleUsesStyleAndPreservesCanvasFade()
        {
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            var boss = (BossPresenter)typeof(BossPresenter).GetMethod("EnsureInstance",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            root = boss.gameObject;
            typeof(BossPresenter).GetMethod("ShowSubtitle",Private).Invoke(boss,new object[] {"Readable boss subtitle"});
            yield return null;
            var text = Field<Text>(boss,"subtitleLabel");
            var group = Field<CanvasGroup>(boss,"subtitleGroup");
            Assert.AreEqual(39,text.fontSize);
            Assert.True(Field<Image>(text.GetComponent<NarrativeReadability>(),"background").gameObject.activeSelf);
            Assert.False(group.blocksRaycasts);
            group.alpha = 0.4f;
            text.GetComponent<NarrativeReadability>().Apply();
            Assert.AreEqual(0.4f,group.alpha);
        }

        [UnityTest]
        public IEnumerator CurrentLocalizedNpcAndBossLinesFitAtMaximumSize()
        {
            var text = CreateText();
            NarrativeReadability.Attach(text);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            var records = CsvParser.SplitCsvRecords(Resources.Load<TextAsset>("Data/GameText").text);
            var columns = CsvParser.BuildColumnIndex(records[0]);
            int checkedLines = 0;
            foreach (var record in records)
            {
                var values = CsvParser.ParseCsvLine(record);
                string key = CsvParser.GetValue(values,columns,"StringKey");
                bool npc = key.StartsWith("Npc_") && key.Contains("_Line");
                bool boss = key.StartsWith("Boss_") && (key.Contains("_Phase") || key.EndsWith("_Death"));
                if (!npc && !boss) continue;
                text.rectTransform.sizeDelta = new Vector2(npc ? 1150f : 1400f,44f);
                // 기존 컴포넌트를 중복 붙이지 않고 실제 적용 대상의 너비로 측정한다.
                var component = text.GetComponent<NarrativeReadability>();
                typeof(NarrativeReadability).GetField("originalSize",Private).SetValue(component,text.rectTransform.sizeDelta);
                foreach (string language in new[] {"Korean","English","Japanese"})
                {
                    string line = CsvParser.GetValue(values,columns,language);
                    if (string.IsNullOrEmpty(line)) line = CsvParser.GetValue(values,columns,"Korean");
                    text.text = line;
                    component.Apply();
                    Assert.LessOrEqual(text.preferredHeight,npc ? 170f : 180f,key+" "+language);
                    Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight,key);
                    checkedLines++;
                }
            }
            Assert.Greater(checkedLines,30);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedStyleReloadsAndInvalidRequestsLeaveOtherSettingsIntact()
        {
            MetaSaveService.Instance.Current.settings.cameraShakeScale = 0.5f;
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.25f,true));
            MetaSaveService.Instance.UnloadWithoutSaving();
            Assert.AreEqual(1.25f,MetaSaveService.Instance.Current.settings.narrativeTextScale);
            Assert.True(NarrativeReadability.HighContrast);
            Assert.AreEqual(0.5f,MetaSaveService.Instance.Current.settings.cameraShakeScale);
            Assert.False(MetaSaveService.Instance.TrySaveReadability(float.NaN,false));
            Assert.False(MetaSaveService.Instance.TrySaveReadability(2f,false));
            Assert.AreEqual(1.25f,NarrativeReadability.Scale);
            yield return null;
        }

        private sealed class FailingOperations : SaveFileOperations
        {
            public override void WriteAllText(string path,string contents) => throw new IOException("readability test failure");
        }

        [UnityTest]
        public IEnumerator SettingsNavigationAndSaveFailureRestoreWidgetRuntimeAndDisk()
        {
            SettingsPanel.Open();
            var settings = Object.FindAnyObjectByType<SettingsPanel>();
            var next = Field<Button>(settings,"textSizeNext");
            var previous = Field<Button>(settings,"textSizePrev");
            var toggle = Field<Toggle>(settings,"narrativeContrastToggle");
            Assert.False(previous.interactable);
            next.onClick.Invoke();
            Assert.AreEqual(1.25f,NarrativeReadability.Scale);
            next.onClick.Invoke();
            Assert.AreEqual(1.5f,NarrativeReadability.Scale);
            Assert.False(next.interactable);
            Assert.AreSame(toggle,previous.navigation.selectOnDown);
            toggle.isOn = true;
            Assert.True(NarrativeReadability.HighContrast);
            string path = Path.Combine(guard.TestDirectory,MetaSave.FileName);
            string disk = File.ReadAllText(path);
            string timestamp = MetaSaveService.Instance.Current.saveTimeUtc;
            SaveSystem.Instance.SetFileOperations(new FailingOperations());
            LogAssert.Expect(LogType.Error,new Regex("저장 실패: readability test failure"));
            previous.onClick.Invoke();
            Assert.AreEqual("150%",Field<Text>(settings,"textSizeValue").text);
            Assert.AreEqual(timestamp,MetaSaveService.Instance.Current.saveTimeUtc);
            Assert.AreEqual(disk,File.ReadAllText(path));
            LogAssert.Expect(LogType.Error,new Regex("저장 실패: readability test failure"));
            toggle.isOn = false;
            Assert.True(toggle.isOn);
            SaveSystem.Instance.SetFileOperations(null);
            yield return null;
        }
    }
}
