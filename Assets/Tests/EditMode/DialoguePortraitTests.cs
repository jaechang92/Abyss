using System.Reflection;
using Abyss.Runtime.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Tests.EditMode
{
    public sealed class DialoguePortraitTests
    {
        private GameObject canvas;
        private GameObject panel;
        private Image image;
        private DialoguePortraitPresenter presenter;
        private DialogueUI dialogue;
        private Sprite guide;
        private Sprite chronicler;

        [SetUp]
        public void Setup()
        {
            canvas = new GameObject("test-dialogue");
            panel = new GameObject("panel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform);
            var picture = new GameObject("portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            picture.transform.SetParent(panel.transform);
            image = picture.GetComponent<Image>();
            presenter = panel.AddComponent<DialoguePortraitPresenter>();
            guide = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/NPCs/guide/portrait.png");
            chronicler = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/NPCs/chronicler/portrait.png");
            Assert.NotNull(guide);
            Assert.NotNull(chronicler);
            presenter.Configure(image, new[]
            {
                new DialoguePortraitPresenter.Entry { speakerKey = "guide", portrait = guide },
                new DialoguePortraitPresenter.Entry { speakerKey = "chronicler", portrait = chronicler }
            });
            dialogue = canvas.AddComponent<DialogueUI>();
            var so = new SerializedObject(dialogue);
            so.FindProperty("root").objectReferenceValue = panel;
            so.FindProperty("portraitPresenter").objectReferenceValue = presenter;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.SetActive(false);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(canvas);

        [Test]
        public void AdvancingChangesSpeakerAndClosingClearsPortrait()
        {
            bool isCompleted = false;
            dialogue.Play(new[] { new DialogueLine { speakerKey = "guide" }, new DialogueLine { speakerKey = "chronicler" } }, () => isCompleted = true);
            Assert.AreSame(guide, image.sprite);
            Assert.IsTrue(image.enabled);
            Advance();
            Assert.AreSame(chronicler, image.sprite);
            Advance();
            Assert.IsTrue(isCompleted);
            Assert.IsFalse(dialogue.IsOpen);
            Assert.IsFalse(image.enabled);
            Assert.IsNull(image.sprite);
        }

        [Test]
        public void UnknownSpeakerDoesNotReusePreviousPortrait()
        {
            presenter.ShowSpeaker("guide");
            presenter.ShowSpeaker("unknown");
            Assert.IsFalse(image.enabled);
            Assert.IsNull(image.sprite);
            Assert.IsFalse(image.raycastTarget);
            Assert.IsTrue(image.preserveAspect);
        }

        [Test]
        public void EmptyReplacementClosesPortraitAndReleasesBothCallers()
        {
            int completed = 0;
            dialogue.Play(new[] { new DialogueLine { speakerKey = "guide" } }, () => completed++);
            dialogue.Play(System.Array.Empty<DialogueLine>(), () => completed++);
            Assert.AreEqual(2, completed);
            Assert.IsFalse(dialogue.IsOpen);
            Assert.IsNull(image.sprite);
        }

        private void Advance() => typeof(DialogueUI).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialogue, null);
    }
}
