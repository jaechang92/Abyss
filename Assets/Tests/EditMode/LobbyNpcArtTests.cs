using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class LobbyNpcArtTests
    {
        [TestCase("guide")]
        [TestCase("chronicler")]
        [TestCase("engraver")]
        [TestCase("altar_keeper")]
        [TestCase("relic_merchant")]
        public void IdleHasFourDistinctFramesAndAFullSeamlessOneSecondCycle(string id)
        {
            string folder = $"Assets/Art/NPCs/{id}";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(folder + "/idle-sheet.png").OfType<Sprite>().ToArray();
            Assert.AreEqual(4, sprites.Length);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folder}/{id}_idle.anim");
            Assert.NotNull(clip);
            Assert.IsTrue(AnimationUtility.GetAnimationClipSettings(clip).loopTime);
            Assert.That(clip.length, Is.EqualTo(1f).Within(0.001f));
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            // 닫는 키(첫 프레임 반복)가 없어야 한다 — 마지막 키도 1/fps 유지되므로 넣으면 첫 프레임이 두 칸 머문다.
            Assert.AreEqual(4, keys.Length);
            Assert.AreEqual(4, keys.Select(k => k.value).Distinct().Count());
            Assert.That(keys[3].time, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(clip.length - keys[3].time, Is.EqualTo(0.25f).Within(0.001f));
            foreach (var sprite in sprites)
            {
                Assert.That(sprite.pivot.y, Is.EqualTo(12f).Within(0.001f));
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
            }
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/portrait.png"));
        }
    }
}
