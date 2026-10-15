using System.Collections;
using NUnit.Framework;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Abyss.Tests.PlayMode
{
    public sealed partial class DraftReadabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator PageDownPollingScrollsWhilePausedAndDoesNotAcquire()
        {
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            Time.timeScale = 0f;
            var scroll = Field<ScrollRect>(Detail, "readingScroll");
            float before = scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.PageDown));
            yield return null;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, before);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            AssertReadingDidNotAcquire();
        }

        [UnityTest]
        public IEnumerator RightStickPollingUsesUnscaledTimeAndNeutralInputStopsMovement()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
                yield return OpenDraft();
                Time.timeScale = 0f;
                var scroll = Field<ScrollRect>(Detail, "readingScroll");
                float before = scroll.content.anchoredPosition.y;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(0f, -1f) });
                yield return null;
                yield return null;
                Assert.Greater(scroll.content.anchoredPosition.y, before);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                yield return null;
                float stopped = scroll.content.anchoredPosition.y;
                yield return null;
                yield return null;
                Assert.AreEqual(stopped, scroll.content.anchoredPosition.y, 0.5f);
                AssertReadingDidNotAcquire();
            }
            finally { InputSystem.RemoveDevice(gamepad); }
        }

        [UnityTest]
        public IEnumerator WheelPollingOnlyScrollsWithPointerOverDetailAndNeverAcquires()
        {
            mouse = InputSystem.AddDevice<Mouse>();
            Assert.True(MetaSaveService.Instance.TrySaveMenuTextScale(1.5f));
            yield return OpenDraft();
            Time.timeScale = 0f;
            var scroll = Field<ScrollRect>(Detail, "readingScroll");
            var inside = RectTransformUtility.WorldToScreenPoint(null,
                scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            float before = scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside, scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, before);
            float afterInside = scroll.content.anchoredPosition.y;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-10000f, -10000f), scroll = new Vector2(0f, -120f) });
            yield return null;
            yield return null;
            Assert.AreEqual(afterInside, scroll.content.anchoredPosition.y, 0.5f);
            AssertReadingDidNotAcquire();
        }

        private void AssertReadingDidNotAcquire()
        {
            Assert.True(root.activeSelf);
            Assert.True(session.IsSessionActive);
            Assert.Zero(grants);
            Assert.Zero(closed);
            Assert.Zero(session.Owned.Count);
            Assert.AreEqual(500, RunManager.Instance.GoldShards);
            Assert.AreEqual(0f, Time.timeScale);
        }
    }
}