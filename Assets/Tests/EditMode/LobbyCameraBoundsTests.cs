using Abyss.Runtime.Camera;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class LobbyCameraBoundsTests
    {
        [TestCase(16f / 9f)]
        [TestCase(21f / 9f)]
        public void LobbyCameraKeepsItsViewportInsideArtwork(float aspect)
        {
            var cameraObject = new GameObject("camera");
            var player = new GameObject("player");
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.orthographic = true;
                camera.orthographicSize = PixelScale.OrthographicSize;
                camera.aspect = aspect;
                var follow = cameraObject.AddComponent<PlayerCameraFollow>();
                follow.SetTarget(player.transform);
                var min = new Vector2(-22f, -9f);
                var max = new Vector2(22f, 5.67f);
                follow.SetEnvironmentBounds(min, max);
                foreach (float edge in new[] { -100f, 100f })
                {
                    player.transform.position = new Vector3(edge, edge, 0f);
                    typeof(PlayerCameraFollow).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(follow, null);
                    float halfWidth = camera.orthographicSize * aspect;
                    Assert.That(camera.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(min.x - 0.001f));
                    Assert.That(camera.transform.position.x + halfWidth, Is.LessThanOrEqualTo(max.x + 0.001f));
                    Assert.That(camera.transform.position.y - camera.orthographicSize, Is.GreaterThanOrEqualTo(min.y - 0.001f));
                    Assert.That(camera.transform.position.y + camera.orthographicSize, Is.LessThanOrEqualTo(max.y + 0.001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ExistingCamerasRemainUnboundedByDefault()
        {
            var cameraObject = new GameObject("camera");
            var player = new GameObject("player");
            try
            {
                cameraObject.AddComponent<UnityEngine.Camera>();
                var follow = cameraObject.AddComponent<PlayerCameraFollow>();
                player.transform.position = new Vector3(100f, 50f, 0f);
                follow.SetTarget(player.transform);
                typeof(PlayerCameraFollow).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(follow, null);
                Assert.That(cameraObject.transform.position, Is.EqualTo(new Vector3(100f, 52f, -10f)));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(player);
            }
        }
    }
}
