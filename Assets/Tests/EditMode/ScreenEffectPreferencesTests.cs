using Abyss.Runtime.Feedback;
using Abyss.Runtime.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class ScreenEffectPreferencesTests
    {
        [Test]
        public void LegacySettingsKeepOriginalEffects()
        {
            var settings = JsonUtility.FromJson<MetaSettings>("{\"masterVolume\":0.8}");
            Assert.AreEqual(1f, settings.cameraShakeScale);
            Assert.False(settings.reduceBossScreenFlashes);
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void InvalidStoredScaleHasSafeFiniteOutput(float input, float expected)
        {
            Assert.AreEqual(expected, ScreenEffectPreferences.NormalizeShakeScale(input));
        }
    }
}
