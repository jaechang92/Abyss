using Abyss.Runtime.Meta;
using Abyss.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class MenuTextReadabilityTests
    {
        [Test]
        public void LegacySettingsKeepMenuAndNarrativeSizesSeparate()
        {
            var settings = JsonUtility.FromJson<MetaSettings>("{\"narrativeTextScale\":1.5}");
            Assert.AreEqual(1f,settings.menuTextScale);
            Assert.AreEqual(1.5f,settings.narrativeTextScale);
        }
        [TestCase(0f,1f)]
        [TestCase(2f,1f)]
        [TestCase(float.NaN,1f)]
        [TestCase(1.25f,1.25f)]
        [TestCase(1.5f,1.5f)]
        public void UnsupportedSavedSizesFallBackToDefault(float input,float expected)
            => Assert.AreEqual(expected,MenuTextReadability.NormalizeScale(input));
    }
}
