using System.Collections;
using System.IO;
using Abyss.Runtime.Analytics;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// AnalyticsLogger가 이벤트를 <b>실제로 파일에 남기는지</b> 확인하는 스모크.
    ///
    /// 계측의 실패는 조용하다 — 로거가 이벤트를 놓쳐도 게임은 멀쩡히 돌아가고, 그 사실은
    /// <b>플레이테스트를 끝낸 뒤 로그를 열어 봐야</b> 드러난다. 그때는 이미 그 런들이 낭비된 뒤다.
    /// 그래서 "구독이 걸려 있고 줄이 쓰인다"만이라도 자동으로 잡는다.
    ///
    /// <c>ability_used</c>는 여기서 검증하지 않는다 — <c>AbilitySystem</c>의 인스턴스 이벤트라
    /// 외부에서 발행할 수 없고 실제 어빌리티 실행이 필요하다(수동 검증 항목).
    ///
    /// ⚠️ 로거는 <b>세션당 파일 하나</b>에 계속 덧붙인다. 그래서 파일 전체를 훑는 단언은
    /// 앞선 테스트가 쓴 줄에 걸려 실행 순서에 의존하게 된다 — 방금 쓴 줄을 봐야 하는 단언은
    /// <see cref="ReadLastEvent"/>로 <b>마지막 한 줄만</b> 읽는다.
    /// </summary>
    public sealed class AnalyticsLoggerPlayModeSmoke
    {
        private string logPath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // AbilitySystem 미생성 경고 등 이 테스트의 관심 밖 로그를 무시한다.
            LogAssert.ignoreFailingMessages = true;

            logPath = AnalyticsLogger.Instance.LogPath;
            Assert.IsFalse(string.IsNullOrEmpty(logPath), "로그 경로가 비었다 — 세션 초기화 실패.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        /// <summary>
        /// 정리는 <b>픽스처 끝에 한 번</b> 한다. 로거는 세션당 파일 하나에 계속 쓰므로 테스트마다
        /// 지울 수 없고(파일이 열려 있어 Windows에서 삭제가 막힌다), 남겨 두면
        /// <b>`_playtest_analyzer.py`가 테스트 줄까지 집계</b>해 플레이테스트 통계가 오염된다.
        /// 세션 ID는 실행마다 새로 발급되므로 실제 플레이테스트 로그를 건드릴 위험은 없다.
        /// </summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // 경로를 필드에 의존하지 않고 다시 묻는다 — 픽스처 수명 규칙이 바뀌어도 정리가 새지 않게.
            string path = AnalyticsLogger.HasInstance ? AnalyticsLogger.Instance.LogPath : logPath;
            if (AnalyticsLogger.HasInstance) AnalyticsLogger.Instance.CloseLog();

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator EnemyDefeated_IsWrittenWithEnemyId()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.enemyId = "test_grunt";

            GameEvents.RaiseEnemyKilled(enemy, Vector3.zero);
            yield return null;

            string line = ReadLastEvent();
            StringAssert.Contains("enemy_defeated", line);
            StringAssert.Contains("test_grunt", line);

            Object.DestroyImmediate(enemy);
        }

        /// <summary>
        /// <c>room_cleared</c>는 방을 <b>떠날 때</b> 기록된다. 클리어 시점에 바로 쓰면
        /// 비전투 방(적 0마리 = 진입과 동시에 클리어)이 전부 0초로 남는다.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomCleared_IsWrittenWhenLeavingRoom()
        {
            var first = MakeRoom("first_room");
            var second = MakeRoom("second_room");

            GameEvents.RaiseRoomEntered(first);
            GameEvents.RaiseRoomCleared(first);
            yield return null;

            // 아직 떠나지 않았다 — 이 시점에는 기록이 없어야 한다.
            Assert.IsFalse(ReadLastEvent().Contains("first_room"),
                "방을 떠나기 전에 기록됐다 — 체류 시간이 0으로 굳는다.");

            GameEvents.RaiseRoomEntered(second);
            yield return null;

            string line = ReadLastEvent();
            StringAssert.Contains("room_cleared", line);
            StringAssert.Contains("first_room", line);

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        /// <summary>
        /// 클리어 뒤에도 방에 머문 시간이 <c>duration_sec</c>에 들어가고,
        /// <c>combat_sec</c>(진입 → 클리어)은 그보다 짧아야 한다. 이 둘을 나눠야
        /// "런이 길다"의 원인이 전투인지 모달·지연인지 가려진다.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomCleared_SeparatesCombatTimeFromTotalStay()
        {
            var room = MakeRoom("timed_room");
            var next = MakeRoom("next_room");

            GameEvents.RaiseRoomEntered(room);
            GameEvents.RaiseRoomCleared(room);   // 즉시 클리어 = 비전투 방과 같은 모양
            yield return null;
            yield return null;
            GameEvents.RaiseRoomEntered(next);
            yield return null;

            var payload = ReadLastEvent();
            StringAssert.Contains("timed_room", payload);
            float duration = ParseFloat(payload, "duration_sec");
            float combat = ParseFloat(payload, "combat_sec");

            Assert.Greater(duration, 0f, "체류 시간이 0이다 — 비전투 방이 0초로 굳는 회귀.");
            Assert.LessOrEqual(combat, duration, "전투 시간이 총 체류보다 길 수 없다.");

            Object.DestroyImmediate(room);
            Object.DestroyImmediate(next);
        }

        /// <summary>런 종료 시 마지막 방도 마감돼야 한다 — 안 그러면 보스 방이 늘 빠진다.</summary>
        [UnityTest]
        public IEnumerator RoomCleared_IsFlushedOnRunEnd()
        {
            var room = MakeRoom("last_room");

            GameEvents.RaiseRoomEntered(room);
            GameEvents.RaiseRoomCleared(room);
            yield return null;
            GameEvents.RaiseRunEnded();
            yield return null;

            // run_end 앞에 방이 마감돼야 분석기가 그 방을 런 안의 사건으로 본다.
            string all = ReadAll();
            int roomIdx = all.LastIndexOf("last_room", System.StringComparison.Ordinal);
            int endIdx = all.LastIndexOf("\"run_end\"", System.StringComparison.Ordinal);
            Assert.Greater(roomIdx, -1, "마지막 방이 기록되지 않았다.");
            Assert.Less(roomIdx, endIdx, "room_cleared가 run_end 뒤에 기록됐다.");

            Object.DestroyImmediate(room);
        }

        /// <summary>
        /// 클리어하지 못한 방(그 방에서 죽은 경우)은 <c>room_cleared</c>로 남기지 않는다 —
        /// 이름이 데이터와 어긋난다. 그 방의 위치는 <c>death.stage_id</c>가 이미 말한다.
        /// </summary>
        [UnityTest]
        public IEnumerator UnclearedRoom_IsNotRecorded()
        {
            var room = MakeRoom("failed_room");
            var next = MakeRoom("after_room");

            GameEvents.RaiseRoomEntered(room);   // 클리어 없이
            yield return null;
            GameEvents.RaiseRoomEntered(next);
            yield return null;

            Assert.IsFalse(ReadAll().Contains("failed_room"),
                "클리어하지 못한 방이 room_cleared로 기록됐다.");

            Object.DestroyImmediate(room);
            Object.DestroyImmediate(next);
        }

        /// <summary>
        /// Bug-035 회귀 가드 — <b>사망 이벤트가 런에 귀속돼야 한다.</b>
        ///
        /// 부트스트랩이 RunManager를 먼저 만들어 <c>OnPlayerDead</c> 구독 순서가 항상 RunManager
        /// 우선이다. 그래서 로거가 사망을 받을 때는 이미 <c>run_end</c>가 지나가 run_id가 비어 있고,
        /// 그대로 기록하면 분석기가 그 줄을 버려 <b>사망 분석이 통째로 사라진다</b>
        /// (실제 로그의 death 35건이 전부 그랬다).
        /// </summary>
        [UnityTest]
        public IEnumerator Death_IsAttributedToRun_EvenAfterRunEnded()
        {
            GameEvents.RaiseRunStarted();
            yield return null;

            // 실제 순서 재현: 런 종료가 먼저 처리되고 사망 기록이 뒤따른다.
            GameEvents.RaiseRunEnded();
            yield return null;
            GameEvents.RaisePlayerDead();
            yield return null;

            string line = ReadLastEvent();
            StringAssert.Contains("\"event\":\"death\"", line);
            Assert.IsFalse(line.Contains("\"run_id\":null"),
                "사망 이벤트가 어느 런에도 속하지 않는다 — 분석기가 이 줄을 버린다(Bug-035).");
        }

        /// <summary>방에서 죽인 수가 room_cleared에 함께 실리는지(방별 전투 밀도).</summary>
        [UnityTest]
        public IEnumerator RoomCleared_CountsKillsInThatRoom()
        {
            var room = MakeRoom("kill_room");
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.enemyId = "test_grunt";

            GameEvents.RaiseRoomEntered(room);
            GameEvents.RaiseEnemyKilled(enemy, Vector3.zero);
            GameEvents.RaiseEnemyKilled(enemy, Vector3.zero);
            GameEvents.RaiseRoomCleared(room);
            yield return null;
            GameEvents.RaiseRunEnded();
            yield return null;

            StringAssert.Contains("\"kills\":2", ReadAll());

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(room);
        }

        private static RoomData MakeRoom(string roomId)
        {
            var room = ScriptableObject.CreateInstance<RoomData>();
            room.roomId = roomId;
            return room;
        }

        /// <summary>payload에서 숫자 필드 하나를 꺼낸다(정식 파서를 들이지 않기 위한 최소 구현).</summary>
        private static float ParseFloat(string line, string field)
        {
            string key = $"\"{field}\":";
            int i = line.IndexOf(key, System.StringComparison.Ordinal);
            Assert.Greater(i, -1, $"{field} 필드가 없다.");

            int start = i + key.Length;
            int end = start;
            while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' ||
                                         line[end] == '-' || line[end] == 'E' || line[end] == '+'))
            {
                end += 1;
            }
            return float.Parse(line[start..end], System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>로그 전체. 순서에 의존하지 않는 단언에만 쓸 것.</summary>
        private string ReadAll()
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// 마지막으로 기록된 이벤트 한 줄. 로거가 파일을 연 채로 쓰고 있으므로 공유 모드를 열어 읽는다
        /// (<c>Log</c>가 줄마다 flush하므로 별도 동기화는 필요 없다).
        /// </summary>
        /// <summary>
        /// 제시된 카드를 <b>전부</b> 남기는지 본다. 획득 로그(<c>skill_drafted</c>)는
        /// 고른 하나만 남기므로, 이것이 없으면 <b>인기 있는 카드와 자주 나오는 카드가
        /// 구분되지 않는다.</b>
        /// </summary>
        [UnityTest]
        public IEnumerator DraftOffered_제시된_카드를_전부_남긴다()
        {
            var cards = new[] { MakeSkill("a_burn"), MakeSkill("b_guard"), MakeSkill("c_void") };
            GameEvents.RaiseDraftOptionsReady(new DraftOptions(cards, DraftTriggerReason.LevelUp, 0));
            yield return null;

            string line = ReadLastEvent();
            StringAssert.Contains("draft_offered", line);
            StringAssert.Contains("a_burn", line);
            StringAssert.Contains("b_guard", line);
            StringAssert.Contains("c_void", line);
            StringAssert.Contains("LevelUp", line);

            foreach (var c in cards) Object.DestroyImmediate(c);
        }

        /// <summary>
        /// 🔴 <c>DraftSessionController.CancelReplacement</c>가 <c>OnDraftOptionsReady</c>를
        /// <b>재발행</b>한다 — 교체 모달을 닫고 패널을 되돌리기 위해서다.
        /// 그건 새로 뽑은 제시가 아니라 같은 화면을 다시 그리는 것이므로 세면 안 된다.
        /// 세면 제시 횟수가 부풀고, 풀 분포가 <b>교체를 많이 취소한 런</b> 쪽으로 기운다.
        /// </summary>
        [UnityTest]
        public IEnumerator DraftOffered_같은_제시가_다시_와도_한_번만_센다()
        {
            var cards = new[] { MakeSkill("dup_1"), MakeSkill("dup_2"), MakeSkill("dup_3") };
            var options = new DraftOptions(cards, DraftTriggerReason.BossBonus, 0);

            int before = CountEvents("draft_offered");
            GameEvents.RaiseDraftOptionsReady(options);
            yield return null;
            GameEvents.RaiseDraftOptionsReady(options);   // CancelReplacement 가 하는 일
            yield return null;

            Assert.AreEqual(before + 1, CountEvents("draft_offered"),
                "같은 제시가 두 번 기록됐다 — 재발행 걸러내기가 동작하지 않는다.");

            foreach (var c in cards) Object.DestroyImmediate(c);
        }

        private static SkillData MakeSkill(string id)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.skillId = id;
            s.rarity = SkillRarity.Common;
            s.synergyTag = "test";
            return s;
        }

        private int CountEvents(string eventName)
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            int n = 0;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains($"\"{eventName}\"")) n++;
            }
            return n;
        }

        private string ReadLastEvent()
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string last = string.Empty;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line)) last = line;
            }

            Assert.IsNotEmpty(last, "로그에 기록된 줄이 없다 — 이벤트 구독이 걸리지 않았을 수 있다.");
            return last;
        }
    }
}
