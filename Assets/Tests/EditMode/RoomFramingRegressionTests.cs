using Abyss.Runtime.Camera;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    public sealed class RoomFramingRegressionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void WideRoomFramingKeepsCenterAndFloorWhenTargetJumps(bool maintenance)
        {
            WithCamera((camera, follow, player) =>
            {
                follow.SetEnvironmentBounds(new Vector2(-16f, -5.5f), new Vector2(16f, 30f));
                if (maintenance) follow.SetMaintenanceRoomFraming(-15f, 15f, 0f);
                else follow.SetBossRoomFraming(-15f, 15f, 0f);
                player.position = new Vector3(-12f, 0f, 0f);
                follow.SnapToTarget();
                Vector3 initial = camera.transform.position;
                player.position = new Vector3(12f, 8f, 0f);
                follow.SnapToTarget();
                Assert.That(camera.transform.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(camera.transform.position.y, Is.EqualTo(initial.y).Within(0.001f));
            });
        }

        [Test]
        public void NarrowViewKeepsTargetVisibleAndReturnToWideRecenters()
        {
            WithCamera((camera, follow, player) =>
            {
                follow.SetEnvironmentBounds(new Vector2(-16f, -5.5f), new Vector2(16f, 30f));
                follow.SetBossRoomFraming(-15f, 15f, 0f);
                camera.aspect = 4f / 3f;
                player.position = new Vector3(12f, 0f, 0f);
                follow.SnapToTarget();
                Assert.That(camera.transform.position.x, Is.GreaterThan(0f));
                float halfWidth = camera.orthographicSize * camera.aspect;
                Assert.That(Mathf.Abs(player.position.x - camera.transform.position.x), Is.LessThan(halfWidth));
                camera.aspect = 16f / 9f;
                follow.SnapToTarget();
                Assert.That(camera.transform.position.x, Is.EqualTo(0f).Within(0.001f));
            });
        }

        [Test]
        public void RoomTransitionClearsFramingAndRejectsOldPresentationOwner()
        {
            WithCamera((camera, follow, player) =>
            {
                follow.SetBossRoomFraming(-15f, 15f, 0f);
                var owner = new object();
                Assert.That(follow.TryBeginPresentationFocus(owner, 5f, 3f), Is.True);
                follow.SetPresentationWeight(owner, 1f);
                follow.SetEnvironmentBounds(new Vector2(-40f, -5.5f), new Vector2(40f, 30f));
                Assert.That(follow.IsBossFramingActive, Is.False);
                Assert.That(follow.IsMaintenanceFramingActive, Is.False);
                Assert.That(follow.TryBeginPresentationFocus(owner, 5f, 3f), Is.False);
                follow.SetMaintenanceRoomFraming(-12f, 12f, 0f);
                Assert.That(follow.IsMaintenanceFramingActive, Is.True);
                Assert.That(follow.IsBossFramingActive, Is.False);
                follow.EndPresentationFocus(owner);
                Assert.That(follow.TryBeginPresentationFocus(owner, 5f, 3f), Is.True);
            });
        }

        private static void WithCamera(System.Action<UnityEngine.Camera, PlayerCameraFollow, Transform> action)
        {
            var cameraObject = new GameObject("RoomFramingRegressionCamera");
            var player = new GameObject("RoomFramingRegressionTarget");
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.orthographic = true;
                camera.orthographicSize = RunCameraProfile.OrthographicSizeFor(RunCameraProfileId.C_Wide);
                camera.aspect = 16f / 9f;
                cameraObject.AddComponent<RunCameraProfile>();
                var follow = cameraObject.AddComponent<PlayerCameraFollow>();
                // EditMode does not run the normal scene lifecycle; bind profile explicitly.
                typeof(PlayerCameraFollow).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(follow, null);
                follow.SetTarget(player.transform);
                action(camera, follow, player.transform);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(player);
            }
        }
    }
}
