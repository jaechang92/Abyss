using System.Collections;
using System.IO;
using Abyss.Runtime.Analytics;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>실제 RunManager 종료 신호부터 파일 기록까지의 통합 계약. 저장은 임시 폴더로 격리한다.</summary>
    public sealed class AnalyticsTerminalPlayModeSmoke
    {
        private readonly PlayModeSaveGuard saveGuard = new();
        private string ownedLogPath;
        private RoomData room;

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
            ownedLogPath = AnalyticsLogger.Instance.LogPath;
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
                if (room != null) Object.DestroyImmediate(room);
                if (!string.IsNullOrEmpty(ownedLogPath) && File.Exists(ownedLogPath))
                    File.Delete(ownedLogPath); // 이 테스트가 새로 만든 GUID 로그만 지운다.
            }
            finally
            {
                saveGuard.Release();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Abandon_RecordsBalanceBeforeReset_WithoutCompletingOpenRoom()
        {
            var run = RunManager.Instance;
            run.StartNewRun();
            run.GainGoldShards(73);
            int balance = run.GoldShards;
            room = ScriptableObject.CreateInstance<RoomData>();
            room.roomId = "synthetic_unfinished_room";
            GameEvents.RaiseRoomEntered(room);
            run.AbandonRun();
            run.AbandonRun();
            yield return null;

            string text = ReadLog();
            Assert.AreEqual(1, CountEvent(text, "run_abandoned"));
            Assert.AreEqual(0, CountEvent(text, "run_end"));
            Assert.AreEqual(0, CountEvent(text, "room_cleared"));
            StringAssert.Contains("\"end_reason\":\"Abandoned\"", text);
            StringAssert.Contains("\"gold_shards_balance\":" + balance, text);
            StringAssert.Contains("\"abyss_shards_earned\":0", text);
            Assert.AreEqual(0, run.GoldShards);

            GameEvents.RaisePlayerDead();
            yield return null;
            string[] lines = ReadLog().Trim().Split('\n');
            StringAssert.Contains("\"event\":\"death\"", lines[^1]);
            StringAssert.Contains("\"run_id\":null", lines[^1]);
        }

        [UnityTest]
        public IEnumerator Cleared_RecordsActualSettlement_AndBuildIdentity()
        {
            var run = RunManager.Instance;
            run.StartNewRun();
            run.GainGoldShards(100);
            run.EndRun(RunEndReason.Cleared);
            yield return null;

            string text = ReadLog();
            Assert.AreEqual(1, CountEvent(text, "run_end"));
            StringAssert.Contains("\"end_reason\":\"Cleared\"", text);
            Assert.Greater(run.LastRunAbyssShardsEarned, 0);
            StringAssert.Contains("\"abyss_shards_earned\":" + run.LastRunAbyssShardsEarned, text);
            StringAssert.Contains("\"schema_version\":2", text);
            StringAssert.Contains("\"build\":{", text);
            StringAssert.Contains("\"is_editor\":true", text);
        }

        [UnityTest]
        public IEnumerator Death_RecordsDeathReason_SeparatelyFromClearAndAbandon()
        {
            var run = RunManager.Instance;
            run.StartNewRun();
            run.EndRun(RunEndReason.Death);
            GameEvents.RaisePlayerDead();
            yield return null;

            string text = ReadLog();
            Assert.AreEqual(1, CountEvent(text, "run_end"));
            Assert.AreEqual(1, CountEvent(text, "death"));
            Assert.AreEqual(0, CountEvent(text, "run_abandoned"));
            StringAssert.Contains("\"end_reason\":\"Death\"", text);
            string[] lines = text.Trim().Split('\n');
            StringAssert.DoesNotContain("\"run_id\":null", lines[^1]);
        }

        private string ReadLog()
        {
            using var stream = new FileStream(ownedLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static int CountEvent(string text, string eventName)
        {
            int count = 0;
            foreach (string line in text.Split('\n'))
                if (line.Contains("\"event\":\"" + eventName + "\"")) count++;
            return count;
        }
    }
}
