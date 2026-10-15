using System.Collections;
using System.IO;
using Abyss.Runtime.Analytics;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using GAS.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    public sealed class AnalyticsAbilityPlayModeSmoke
    {
        private readonly PlayModeSaveGuard saveGuard = new();
        private string logPath;
        private GameObject contextObject;
        private bool ownsAbilitySystem;
        private IGameplayContext previousContext;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            RunManager.Instance.AbandonRun();
            if (AnalyticsLogger.HasInstance)
            {
                AnalyticsLogger.Instance.CloseLog();
                Object.DestroyImmediate(AnalyticsLogger.Instance.gameObject);
            }
            ownsAbilitySystem = !AbilitySystem.HasInstance;
            previousContext = (IGameplayContext)ContextField.GetValue(AbilitySystem.Instance);
            contextObject = new GameObject("AnalyticsAbilityTestContext");
            AbilitySystem.Instance.Initialize(contextObject.AddComponent<DefaultGameplayContext>());
            logPath = AnalyticsLogger.Instance.LogPath;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                RunManager.Instance.AbandonRun();
                if (AnalyticsLogger.HasInstance)
                {
                    AnalyticsLogger.Instance.CloseLog();
                    Object.DestroyImmediate(AnalyticsLogger.Instance.gameObject);
                }
                if (ownsAbilitySystem && AbilitySystem.HasInstance)
                    Object.DestroyImmediate(AbilitySystem.Instance.gameObject);
                else if (AbilitySystem.HasInstance)
                {
                    AbilitySystem.Instance.UnregisterAbility("slot1:analytics_probe");
                    ContextField.SetValue(AbilitySystem.Instance, previousContext);
                }
                if (contextObject != null) Object.DestroyImmediate(contextObject);
                if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath)) File.Delete(logPath);
            }
            finally { saveGuard.Release(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AbilityStarted_WritesSlotNameAndRunBeforeEffectCompletes()
        {
            var ability = new DelayedAbility();
            AbilitySystem.Instance.RegisterAbility(ability);
            RunManager.Instance.StartNewRun();
            var execution = AbilitySystem.Instance.TryExecuteAbilityAsync(ability.AbilityName);
            string line = FindAbilityLine();
            StringAssert.Contains("\"ability_name\":\"analytics_probe\"", line);
            StringAssert.Contains("\"slot\":1", line);
            StringAssert.DoesNotContain("\"run_id\":null", line);
            Assert.That(ability.IsExecuting, Is.True);
            while (ability.IsExecuting) yield return null;
            Assert.That(execution.GetAwaiter().GetResult(), Is.True);
            Assert.That(CountAbilityLines(), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RejectedAbility_DoesNotWriteUsage()
        {
            var ability = new DelayedAbility { IsAllowed = false };
            AbilitySystem.Instance.RegisterAbility(ability);
            RunManager.Instance.StartNewRun();
            var execution = AbilitySystem.Instance.TryExecuteAbilityAsync(ability.AbilityName);
            Assert.That(execution.GetAwaiter().GetResult(), Is.False);
            yield return null;
            Assert.That(CountAbilityLines(), Is.Zero);
        }

        private string FindAbilityLine()
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            string line;
            while ((line = reader.ReadLine()) != null)
                if (line.Contains("\"event\":\"ability_used\"")) return line;
            Assert.Fail("AbilitySystem 개시 이벤트가 실제 로그에 기록되지 않았다.");
            return string.Empty;
        }

        private int CountAbilityLines()
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            int count = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
                if (line.Contains("\"event\":\"ability_used\"")) count++;
            return count;
        }

        private static System.Reflection.FieldInfo ContextField => typeof(AbilitySystem).GetField(
            "ownerContext", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private sealed class DelayedAbility : IAbility
        {
            public string AbilityName => "slot1:analytics_probe";
            public bool IsExecuting { get; private set; }
            public bool IsAllowed { get; set; } = true;
            public AbilityCooldown Cooldown => null;
            public bool CanExecute(IGameplayContext context) => IsAllowed && context != null;
            public async Awaitable ExecuteAsync(IGameplayContext context)
            {
                IsExecuting = true;
                await Awaitable.NextFrameAsync();
                IsExecuting = false;
            }
            public void Cancel() => IsExecuting = false;
        }
    }
}
