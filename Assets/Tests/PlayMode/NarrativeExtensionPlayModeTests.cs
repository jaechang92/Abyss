using System.Collections;
using System.Reflection;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Abyss.Tests.PlayMode
{
    public sealed class NarrativeExtensionPlayModeTests
    {
        private readonly PlayModeSaveGuard guard = new PlayModeSaveGuard();
        private GameObject root;
        private KeeperIntroPanel.Lease keeper;
        private float oldTimeScale;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object value,string name) => (T)value.GetType().GetField(name,Private).GetValue(value);

        [SetUp]
        public void SetUp()
        {
            guard.Acquire();
            oldTimeScale = Time.timeScale;
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                keeper?.Close();
                if (root != null) Object.Destroy(root);
                Time.timeScale = oldTimeScale;
                yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator StorySequenceKeepsCenterTimingSkipAndMatchingContrastFade()
        {
            root = UiFactory.CreateOverlayCanvas("StoryReadabilityTest",500);
            var sequence = SubtitleSequence.Create(root.transform);
            var text = root.transform.Find("Subtitle").GetComponent<Text>();
            var readability = text.GetComponent<NarrativeReadability>();
            var background = Field<Image>(readability,"background");
            sequence.Restart(new[] {"A readable story paragraph.","Next paragraph."});
            Time.timeScale = 0f;
            sequence.Tick(SubtitleSequence.FADE_IN*0.5f);
            yield return null;
            Assert.AreEqual(45,text.fontSize);
            Assert.AreEqual(Vector2.zero,text.rectTransform.anchoredPosition);
            Assert.AreEqual(0.5f,text.color.a,0.001f);
            Assert.AreEqual(text.color.a*0.96f,background.color.a,0.001f);
            Assert.AreEqual(SubtitleSequence.FADE_IN*0.5f,sequence.ElapsedSeconds);
            Assert.False(sequence.Skip());
            yield return null;
            Assert.AreEqual("Next paragraph.",text.text);
            Assert.AreEqual(0f,text.color.a);
            Assert.AreEqual(0f,background.color.a);
            sequence.Tick(SubtitleSequence.FADE_IN);
            yield return null;
            Assert.AreEqual(1f,text.color.a);
            Assert.AreEqual(0.96f,background.color.a);
            sequence.Clear();
            yield return null;
            Assert.AreEqual(0f,background.color.a);
            Assert.True(sequence.Skip());
            Assert.True(sequence.IsFinished);
        }

        [UnityTest]
        public IEnumerator EmptyCaptionHasNoContrastBandAndStyleChangeDoesNotRestartTime()
        {
            root = UiFactory.CreateOverlayCanvas("EmptyCaptionReadabilityTest",500);
            var sequence = SubtitleSequence.Create(root.transform);
            var text = root.transform.Find("Subtitle").GetComponent<Text>();
            var background = Field<Image>(text.GetComponent<NarrativeReadability>(),"background");
            yield return null;
            Assert.False(background.gameObject.activeSelf);
            sequence.Restart(new[] {"Story in progress."});
            sequence.Tick(SubtitleSequence.FADE_IN*0.5f);
            yield return null;
            Assert.True(background.gameObject.activeSelf);
            float elapsed = sequence.ElapsedSeconds;
            float alpha = text.color.a;
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1f,false));
            yield return null;
            Assert.AreEqual(30,text.fontSize);
            Assert.AreEqual(alpha,text.color.a);
            Assert.AreEqual(elapsed,sequence.ElapsedSeconds);
            Assert.False(background.gameObject.activeSelf);
            sequence.Restart(new[] {string.Empty});
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1.5f,true));
            yield return null;
            Assert.False(background.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ActualPrologueKeepsAuthoredLowerPositionAndRestoresStyle()
        {
            typeof(PrologueSequencePanel).GetMethod("EnsureInstance",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            var panel = Object.FindAnyObjectByType<PrologueSequencePanel>();
            root = panel.gameObject;
            var body = Field<GameObject>(panel,"body");
            body.SetActive(true);
            var sequence = Field<SubtitleSequence>(panel,"subtitles");
            sequence.Restart(SubtitleSequence.Localize(StringKey.Story_Prologue_Line1));
            var text = body.transform.Find("Subtitle").GetComponent<Text>();
            yield return null;
            Assert.AreEqual(new Vector2(0,-320),text.rectTransform.anchoredPosition);
            Assert.AreEqual(45,text.fontSize);
            Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1f,false));
            yield return null;
            Assert.AreEqual(30,text.fontSize);
            Assert.AreEqual(new Vector2(1100,200),text.rectTransform.sizeDelta);
            Assert.AreEqual(new Vector2(0,-320),text.rectTransform.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator ActualEndingUsesSharedSequenceStyleWithoutStartingEndingFlow()
        {
            // 흐름/정산을 시작하지 않고 실제 패널 생성·자막만 검증한다.
            var ensure = typeof(EndingSequencePanel).GetMethod("EnsureInstance",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.NotNull(ensure);
            ensure.Invoke(null,null);
            var panel = Object.FindAnyObjectByType<EndingSequencePanel>();
            root = panel.gameObject;
            var body = Field<GameObject>(panel,"body");
            body.SetActive(true);
            var sequence = Field<SubtitleSequence>(panel,"subtitles");
            sequence.Restart(SubtitleSequence.Localize(StringKey.Story_Ending_Line1));
            yield return null;
            var text = body.transform.Find("Subtitle").GetComponent<Text>();
            Assert.AreEqual(45,text.fontSize);
            Assert.NotNull(text.GetComponent<NarrativeReadability>());
            Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
        }

        [UnityTest]
        public IEnumerator KeeperLineExpandsUpwardWithoutOverlappingPagerAndRestoresHeading()
        {
            keeper = KeeperIntroPanel.Open("Keeper","Epithet");
            root = keeper.Panel.gameObject;
            var text = Field<Text>(keeper.Panel,"lineLabel");
            var pager = Field<Text>(keeper.Panel,"pagerLabel");
            var title = Field<CanvasGroup>(keeper.Panel,"titleGroup").GetComponent<RectTransform>();
            float bottom = text.rectTransform.anchoredPosition.y-text.rectTransform.rect.height*text.rectTransform.pivot.y;
            keeper.Panel.SetLine("A longer keeper introduction line that should wrap into more than one line when the largest text setting is selected. A longer keeper introduction line that should wrap into more than one line.","1/2",true);
            yield return null;
            Assert.AreEqual(39,text.fontSize);
            Assert.AreEqual(bottom,text.rectTransform.anchoredPosition.y-text.rectTransform.rect.height*text.rectTransform.pivot.y,0.01f);
            Assert.Greater(title.anchoredPosition.y,0f);
            Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
            float pagerTop = pager.rectTransform.anchoredPosition.y+pager.rectTransform.rect.height*(1f-pager.rectTransform.pivot.y);
            Assert.Less(pagerTop,bottom);
            Assert.True(keeper.IsUsable);
            Assert.AreEqual("1/2",pager.text);
            Assert.True(MetaSaveService.Instance.TrySaveReadability(1f,false));
            yield return null;
            Assert.AreEqual(26,text.fontSize);
            Assert.AreEqual(Vector2.zero,title.anchoredPosition);
            Assert.AreEqual(new Vector2(0,-445),text.rectTransform.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator StandardBossIntroAttachesReadabilityToActualDialogueBody()
        {
            // 비동기 재생 대신 실제 BuildContent를 호출해 카드 구조만 생성한다.
            root = UiFactory.CreateOverlayCanvas("BossIntroReadabilityTest",500);
            var panel = root.AddComponent<BossIntroPanel>();
            typeof(BossIntroPanel).GetMethod("BuildContent",Private).Invoke(panel,new object[] {root.transform});
            var text = Field<Text>(panel,"lineLabel");
            text.text = "A readable boss introduction.";
            yield return null;
            Assert.AreEqual(45,text.fontSize);
            Assert.True(Field<Image>(text.GetComponent<NarrativeReadability>(),"background").gameObject.activeSelf);
            Assert.AreEqual(64,Field<Text>(panel,"titleLabel").fontSize);
        }

        [UnityTest]
        public IEnumerator CurrentStoryAndBossIntroTranslationsHaveRoomAtMaximumSize()
        {
            root = UiFactory.CreateOverlayCanvas("TranslationReadabilityTest",500);
            var sequence = SubtitleSequence.Create(root.transform);
            var text = root.transform.Find("Subtitle").GetComponent<Text>();
            var component = text.GetComponent<NarrativeReadability>();
            var records = CsvParser.SplitCsvRecords(Resources.Load<TextAsset>("Data/GameText").text);
            var columns = CsvParser.BuildColumnIndex(records[0]);
            int count = 0;
            foreach (var record in records)
            {
                var values = CsvParser.ParseCsvLine(record);
                string key = CsvParser.GetValue(values,columns,"StringKey");
                bool story = key.StartsWith("Story_Prologue_Line") || key.StartsWith("Story_Ending_Line");
                bool intro = key.StartsWith("Boss_") && key.Contains("_Intro");
                if (!story && !intro) continue;
                foreach (string language in new[] {"Korean","English","Japanese"})
                {
                    string line = CsvParser.GetValue(values,columns,language);
                    if (string.IsNullOrEmpty(line)) line = CsvParser.GetValue(values,columns,"Korean");
                    sequence.Restart(new[] {line.Replace("\\n","\n")});
                    component.Apply();
                    Assert.LessOrEqual(text.preferredHeight,story ? 200f : 180f,key+" "+language);
                    Assert.GreaterOrEqual(text.rectTransform.rect.height,text.preferredHeight);
                    count++;
                }
            }
            Assert.Greater(count,30);
            yield return null;
        }
    }
}
