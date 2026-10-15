using System;
using System.Linq;
using Abyss.Runtime.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Tests.EditMode
{
    /// <summary>저장은 메모리 delegate로 격리한다. 사용자 세이브나 런타임 싱글톤에 접근하지 않는다.</summary>
    public sealed class GameplayKeyBindingsTests
    {
        private InputActionAsset source;
        private GameplayKeyboardBindingProfile profile;
        private string saved;
        private bool canSave;
        private string sourceState;

        [SetUp]
        public void SetUp()
        {
            source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.NotNull(source);
            sourceState = source.SaveBindingOverridesAsJson();
            canSave = true;
            saved = string.Empty;
            profile = NewProfile(string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            profile?.Dispose();
            Assert.AreEqual(sourceState, source.SaveBindingOverridesAsJson(), "Imported shared asset must stay unchanged");
        }

        private GameplayKeyboardBindingProfile NewProfile(string json) =>
            new GameplayKeyboardBindingProfile(source, json, value =>
            {
                if (!canSave) return false;
                saved = value;
                return true;
            });

        private KeyboardBindingRow Row(string action, string part = "") =>
            profile.Rows.Single(row => row.ActionName == action && row.PartName == part);

        [Test]
        public void RowsExposeOnlyTwelveGameplayKeyboardBindings()
        {
            Assert.AreEqual(12, profile.Rows.Count);
            Assert.AreEqual(12, profile.Rows.Select(row => row.BindingId).Distinct().Count());
            CollectionAssert.AreEquivalent(new[] { "up", "down", "left", "right" },
                profile.Rows.Where(row => row.ActionName == "Move").Select(row => row.PartName));
            Assert.False(profile.Rows.Any(row => row.ActionName == "Pause"));
            Assert.AreSame(profile.Rows, profile.Rows, "Readonly view must not allocate per prompt update");
        }

        [Test]
        public void RebindRoundtripAndResetPreserveUiGamepadAndActivation()
        {
            string id = Row("Jump").BindingId;
            Assert.True(profile.TrySetKey(id, "<Keyboard>/space", out var error));
            Assert.AreEqual(KeyBindingError.None, error);
            string persisted = saved;
            var clone = UnityEngine.Object.Instantiate(source);
            try
            {
                clone.FindActionMap("Player").Enable();
                clone.FindActionMap("UI").Disable();
                var protectedBindings = clone.bindings.Where(binding => !binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) || binding.action == "Pause")
                    .ToDictionary(binding => binding.id, binding => binding.effectivePath);
                var uiPaths = clone.FindActionMap("UI").bindings.Select(binding => binding.effectivePath).ToArray();
                profile.ApplyTo(clone);
                Assert.True(clone.FindActionMap("Player").enabled);
                Assert.False(clone.FindActionMap("UI").enabled);
                foreach (var binding in clone.bindings)
                    if (protectedBindings.TryGetValue(binding.id, out string path)) Assert.AreEqual(path, binding.effectivePath);
                CollectionAssert.AreEqual(uiPaths, clone.FindActionMap("UI").bindings.Select(binding => binding.effectivePath));
                using (var restored = NewProfile(persisted))
                    Assert.AreEqual(profile.Rows.Single(row => row.BindingId == id).DisplayKey,
                        restored.Rows.Single(row => row.BindingId == id).DisplayKey);
                Assert.True(profile.ResetKeyboard(out error));
                Assert.AreEqual(string.Empty, saved);
                profile.ApplyTo(clone);
                Assert.AreEqual("<Keyboard>/c", clone.FindAction("Player/Jump").bindings.First(binding => binding.id.ToString() == id).effectivePath);
            }
            finally { clone.Disable(); UnityEngine.Object.DestroyImmediate(clone); }
        }

        [TestCase("<Keyboard>/escape", KeyBindingError.Reserved)]
        [TestCase("<Keyboard>/enter", KeyBindingError.Reserved)]
        [TestCase("<Keyboard>/numpadEnter", KeyBindingError.Reserved)]
        [TestCase("<Keyboard>/tab", KeyBindingError.Reserved)]
        [TestCase("<Keyboard>/z", KeyBindingError.Conflict)]
        [TestCase("<Keyboard>/leftShift", KeyBindingError.Conflict)]
        [TestCase("<Gamepad>/buttonSouth", KeyBindingError.Invalid)]
        [TestCase("<Keyboard>/anyKey", KeyBindingError.Invalid)]
        public void RejectInvalidReservedAndConflictingKeys(string path, KeyBindingError expected)
        {
            string previous = Row("Jump").DisplayKey;
            Assert.False(profile.TrySetKey(Row("Jump").BindingId, path, out var error));
            Assert.AreEqual(expected, error);
            Assert.AreEqual(previous, Row("Jump").DisplayKey);
            Assert.AreEqual(string.Empty, saved);
        }

        [Test]
        public void FailedSaveAndResetKeepCurrentBindingAndStoredJson()
        {
            Assert.True(profile.TrySetKey(Row("Jump").BindingId, "<Keyboard>/space", out _));
            string beforeJson = saved;
            string beforeKey = Row("Jump").DisplayKey;
            canSave = false;
            Assert.False(profile.TrySetKey(Row("Jump").BindingId, "<Keyboard>/j", out var error));
            Assert.AreEqual(KeyBindingError.SaveFailed, error);
            Assert.False(profile.ResetKeyboard(out error));
            Assert.AreEqual(KeyBindingError.SaveFailed, error);
            Assert.AreEqual(beforeKey, Row("Jump").DisplayKey);
            Assert.AreEqual(beforeJson, saved);
        }

        [Test]
        public void UntrustedSavedEntriesRejectWholeSet()
        {
            string id = Row("Jump").BindingId;
            string attackId = Row("Attack").BindingId;
            foreach (string json in new[]
            {
                "invalid-json",
                "{\"entries\":[{\"bindingId\":\"" + id + "\",\"path\":\"<Gamepad>/buttonSouth\"}]}",
                "{\"entries\":[{\"bindingId\":\"" + id + "\",\"path\":\"<Keyboard>/escape\"}]}",
                "{\"entries\":[{\"bindingId\":\"unknown\",\"path\":\"<Keyboard>/j\"}]}",
                "{\"entries\":[{\"bindingId\":\"" + id + "\",\"path\":\"<Keyboard>/space\"},{\"bindingId\":\"" + attackId + "\",\"path\":\"<Keyboard>/space\"}]}"
            })
            {
                using var restored = NewProfile(json);
                Assert.AreEqual(Row("Jump").DisplayKey, restored.Rows.Single(row => row.BindingId == id).DisplayKey);
                Assert.AreEqual(string.Empty, saved);
            }
        }

        [Test]
        public void ProfileApplyRefusesSharedSource()
        {
            Assert.True(profile.TrySetKey(Row("Jump").BindingId, "<Keyboard>/space", out _));
            profile.ApplyTo(source);
            Assert.AreEqual(sourceState, source.SaveBindingOverridesAsJson());
        }

        [Test]
        public void UnknownBindingIdCannotWriteOrChangeRows()
        {
            string before = Row("Jump").DisplayKey;
            Assert.False(profile.TrySetKey(Guid.NewGuid().ToString(), "<Keyboard>/space", out var error));
            Assert.AreEqual(KeyBindingError.Invalid, error);
            Assert.AreEqual(before, Row("Jump").DisplayKey);
            Assert.AreEqual(string.Empty, saved);
        }
    }
}
