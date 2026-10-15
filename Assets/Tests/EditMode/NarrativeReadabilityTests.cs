using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class NarrativeReadabilityTests
    {
        [Test]
        public void LegacySettingsKeepOriginalNarrativeStyle()
        {
            var settings = JsonUtility.FromJson<MetaSettings>("{\"cameraShakeScale\":0.5}");
            Assert.AreEqual(1f, settings.narrativeTextScale);
            Assert.False(settings.highContrastNarrative);
            Assert.AreEqual(0.5f, settings.cameraShakeScale);
        }

        [TestCase(1920f,1080f)]
        [TestCase(1280f,720f)]
        [TestCase(1024f,768f)]
        [TestCase(2560f,1080f)]
        public void ExpandedSettingsFitSupportedCanvasRatios(float width,float height)
        {
            float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
            Assert.Less(SettingsPanelLayout.PanelHeight * scale, height);
            Assert.Less(SettingsPanelLayout.PanelWidth * scale, width);
        }
    }
}
