using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// StageDirector 진행 스모크.
    ///
    /// 지금까지 PlayMode 커버리지는 세이브·계측 계층에만 있었고 <b>런 진행에는 하나도 없었다</b>.
    /// 방이 안 넘어가는 스톨과 방을 건너뛰는 스킵은 둘 다 완주를 막는데, 증상이 정반대라
    /// 한쪽만 막으면 다른 쪽이 열린다. 그래서 이 파일은 두 방향을 함께 본다.
    ///
    /// 실제 시퀀스 에셋(31방) 대신 <b>합성 시퀀스</b>를 쓴다 — 여기서 보는 것은 콘텐츠 분량이
    /// 아니라 진행 규칙이다.
    ///
    /// 🔑 <b>방마다 적을 한 마리 세운다.</b> 처음에는 적 없는 빈 방으로 만들었는데,
    /// StageDirector 는 빈 방을 감지하면 즉시 클리어하고 곧바로 다음 방을 예약한다. 지연이 0이면
    /// 그 예약이 <b>같은 프레임 안에서 연쇄 발화</b>해 시퀀스 전체가 한 프레임에 끝났다 —
    /// 프레임 경계에서 값을 읽는 방식으로는 중간 단계가 아예 보이지 않는다(관측된 단계열이 [0,2]였다).
    /// 적이 서 있으면 방은 <b>테스트가 죽일 때까지</b> 기다리므로 한 걸음씩 확인할 수 있다.
    ///
    /// 진행 관측도 프레임 샘플링이 아니라 <see cref="GameEvents.OnRoomEntered"/> 구독으로 한다.
    /// 실제로 일어난 일을 세는 쪽이 언제 보러 갔느냐에 좌우되지 않는다.
    /// </summary>
    public sealed class StageDirectorPlayModeSmoke
    {
        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private readonly List<Object> spawned = new List<Object>();

        private StageDirector director;
        private GameObject enemyPrefabSource;

        // 방에 들어갈 때마다 기록되는 단계 인덱스. 진행의 사실 기록이다.
        private readonly List<int> enteredSteps = new List<int>();
        private readonly List<int> enteredStages = new List<int>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();

            enteredSteps.Clear();
            enteredStages.Clear();
            GameEvents.OnRoomEntered += HandleRoomEntered;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;

            if (director != null) Object.Destroy(director.gameObject);
            director = null;

            // 스폰된 적 정리. 비활성 개체까지 포함해야 한다 — 아래 BuildDirector 주석 참조.
            var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
            foreach (var enemy in enemies)
            {
                if (enemy != null) Object.Destroy(enemy.gameObject);
            }
            enemyPrefabSource = null;

            foreach (var obj in spawned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            spawned.Clear();

            yield return null;  // Destroy 반영(다음 테스트가 잔재를 보지 않게)

            // 런은 전역 상태다. 켜둔 채 끝내면 다음 테스트의 StartNewRun 이 "이미 진행 중"으로
            // 조용히 무시되어, 실패가 이 테스트가 아니라 다음 테스트에서 나타난다.
            if (RunManager.HasInstance && RunManager.Instance.IsRunActive) RunManager.Instance.EndRun();

            saveGuard.Release();
        }

        private void HandleRoomEntered(RoomData _)
        {
            if (director == null) return;
            enteredSteps.Add(director.CurrentStepIndex);
            enteredStages.Add(director.CurrentStageIndex);
        }

        // ───────────────────────── 테스트 ─────────────────────────

        /// <summary>
        /// 완주 경로가 끝까지 닿는가. 스테이지 2개 × 3단계를 전부 지나 런이 Cleared 로 끝나야 한다.
        /// 한 방이라도 스톨하면 여기서 타임아웃으로 걸린다.
        /// </summary>
        [UnityTest]
        public IEnumerator 모든_방을_클리어하면_완주로_끝난다()
        {
            BuildDirector(stageCount: 2, stepsPerStage: 3);

            director.StartSequence();

            // 방마다 적을 치우며 끝까지 민다. 적이 안 죽거나 방이 안 넘어가면 프레임 예산에서 걸린다.
            for (int frame = 0; frame < 300 && RunManager.Instance.IsRunActive; frame++)
            {
                KillLiveEnemies();
                yield return null;
            }

            Assert.IsFalse(RunManager.Instance.IsRunActive, "모든 스테이지를 지났으면 런이 끝나 있어야 한다.");
            Assert.AreEqual(RunEndReason.Cleared, RunManager.Instance.LastRunEndReason,
                "마지막 스테이지까지 클리어했으므로 사망이 아니라 완주여야 한다.");
            Assert.AreEqual(6, enteredSteps.Count,
                "2스테이지 × 3단계 = 6개 방을 모두 밟아야 한다. 실제 진입: " + enteredSteps.Count);
        }

        /// <summary>
        /// 방을 건너뛰지 않는가. 진입한 단계 인덱스가 0,1,2,3 순서여야 한다.
        ///
        /// 방 스킵은 완주 자체는 되기 때문에 위 테스트를 통과한다 — 오히려 더 빨리 끝난다.
        /// 그래서 "끝까지 갔다"와 "빠짐없이 갔다"를 따로 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator 단계는_한_번에_하나씩만_넘어간다()
        {
            BuildDirector(stageCount: 1, stepsPerStage: 4);

            director.StartSequence();

            for (int frame = 0; frame < 300 && RunManager.Instance.IsRunActive; frame++)
            {
                KillLiveEnemies();
                yield return null;
            }

            Assert.AreEqual(new[] { 0, 1, 2, 3 }, enteredSteps.ToArray(),
                "단계를 건너뛰었거나 중복 진입했다: [" + string.Join(",", enteredSteps) + "]");
        }

        /// <summary>
        /// 남은 방 카운터가 진행과 함께 줄어드는가.
        /// HUD 표시가 여기에 의존하는데, 인덱스만 늘고 카운터가 안 줄면 화면만 멈춘 것처럼 보인다.
        /// </summary>
        [UnityTest]
        public IEnumerator 남은_방_수는_진행에_따라_줄어든다()
        {
            BuildDirector(stageCount: 1, stepsPerStage: 3);

            director.StartSequence();
            yield return null;  // 첫 방 진입까지

            int first = director.RemainingRooms;
            Assert.AreEqual(2, first, "3단계 시퀀스의 첫 방에서는 남은 방이 2여야 한다.");

            // 첫 방을 치우고 다음 방으로 넘어가기를 기다린다.
            KillLiveEnemies();
            yield return WaitUntilStepAtLeast(1, maxFrames: 120);

            Assert.Less(director.RemainingRooms, first, "다음 방으로 넘어갔으면 남은 방이 줄어야 한다.");
        }

        /// <summary>
        /// 치트 점프가 목표 좌표에 정확히 서는가.
        ///
        /// <c>DebugJumpTo</c>는 <b>예약된 진행(Invoke)을 취소</b>한다. 취소를 빠뜨리면 점프 직후
        /// 남은 예약이 발화해 한 단계를 더 넘긴다. 그래서 "도착했는가"가 아니라
        /// <b>"도착한 자리에 머무는가"</b>를 본다.
        ///
        /// 방을 하나 치워 예약을 만들어 둔 <b>직후</b>에 점프하는 것이 이 테스트의 핵심이다 —
        /// 예약이 없는 상태에서 점프하면 취소 로직이 없어도 통과해 버린다.
        /// </summary>
        [UnityTest]
        public IEnumerator 치트_점프는_목표_단계에_머무른다()
        {
            BuildDirector(stageCount: 2, stepsPerStage: 4);

            director.StartSequence();
            yield return null;  // 1스테이지 1방 진입

            KillLiveEnemies();          // 여기서 ProceedToNextRoom 예약이 걸린다
            director.DebugJumpTo(1, 2); // 그 예약을 취소하고 목표로 이동해야 한다

            Assert.AreEqual(1, director.CurrentStageIndex, "점프 직후 스테이지 인덱스가 목표와 달랐다.");
            Assert.AreEqual(2, director.CurrentStepIndex, "점프 직후 단계 인덱스가 목표와 달랐다.");

            yield return null;  // 취소되지 않은 예약이 있었다면 여기서 발화한다
            yield return null;

            Assert.AreEqual(1, director.CurrentStageIndex, "점프 후 스테이지가 저절로 넘어갔다 — 예약 취소 누락.");
            Assert.AreEqual(2, director.CurrentStepIndex, "점프 후 단계가 저절로 넘어갔다 — 예약 취소 누락.");
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        /// <summary>
        /// 합성 시퀀스를 물린 StageDirector 를 만든다. 방마다 적 1마리를 세운다.
        ///
        /// 적 프리팹 원본은 <b>비활성</b> GameObject 다. 이유가 두 가지 있다.
        /// ① <c>WarnUntrackedEnemies</c>·<c>DespawnAllEnemies</c>가 쓰는 <c>FindObjectsByType</c>은
        ///    기본적으로 비활성을 제외한다 — 원본이 활성이면 방마다 "추적되지 않는 적" 경고가 쏟아지고,
        ///    치트 점프의 정리 과정에서 <b>원본까지 파괴</b>되어 이후 방의 스폰이 빈다.
        /// ② 복제본도 비활성으로 태어나 Awake 가 돌지 않는다. 여기서는 적의 전투 행동이 아니라
        ///    <b>사망 신호</b>만 필요하므로 그 편이 오히려 결정적이다(HP 0에서 시작해 첫 타격에 죽는다).
        /// </summary>
        private void BuildDirector(int stageCount, int stepsPerStage)
        {
            var enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.enemyId = "test_room_enemy";
            enemyData.baseHp = 1;
            enemyData.expReward = 0;   // 레벨업 드래프트가 끼어들지 않게 보상은 0으로
            enemyData.goldReward = 0;
            enemyData.tier = EnemyTier.Normal;
            spawned.Add(enemyData);

            enemyPrefabSource = new GameObject("TestEnemyPrefab");
            enemyPrefabSource.SetActive(false);
            var sourceEnemy = enemyPrefabSource.AddComponent<EnemyBase>();
            SetPrivate(sourceEnemy, "data", enemyData);
            enemyData.spawnPrefab = enemyPrefabSource;

            var sequence = ScriptableObject.CreateInstance<StageSequenceData>();
            sequence.sequenceId = "test_sequence";
            spawned.Add(sequence);

            for (int s = 0; s < stageCount; s++)
            {
                var stage = ScriptableObject.CreateInstance<StageData>();
                stage.stageId = "test_stage_" + s;
                stage.displayName = "테스트 스테이지 " + (s + 1);
                spawned.Add(stage);

                for (int r = 0; r < stepsPerStage; r++)
                {
                    var room = ScriptableObject.CreateInstance<RoomData>();
                    room.roomId = "test_room_" + s + "_" + r;
                    room.roomType = RoomType.Combat;
                    room.enemies.Add(new EnemySpawnEntry { data = enemyData, count = 1 });
                    spawned.Add(room);

                    var step = new StageStep();
                    step.options.Add(room);   // 옵션 1개 = 선형 진행(분기 패널 안 뜸)
                    stage.steps.Add(step);
                }

                sequence.stages.Add(stage);
            }

            var go = new GameObject("TestStageDirector");
            go.SetActive(false);  // OnEnable 의 자동 시작을 막고 값부터 넣는다
            director = go.AddComponent<StageDirector>();

            SetPrivate(director, "sequence", sequence);
            SetPrivate(director, "startOnEnable", false);   // 시작 시점을 테스트가 쥔다
            SetPrivate(director, "delayBetweenRooms", 0f);  // 2초 × 방 수를 기다리지 않는다
            SetPrivate(director, "delayBetweenStages", 0f);

            go.SetActive(true);
        }

        /// <summary>
        /// 살아 있는 스폰 적을 모두 죽인다. 원본 프리팹은 건드리지 않는다.
        /// 복제본이 비활성이라 <c>FindObjectsInactive.Include</c> 가 필요하다.
        /// </summary>
        private void KillLiveEnemies()
        {
            var enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                if (enemyPrefabSource != null && enemy.gameObject == enemyPrefabSource) continue;
                enemy.TakeDamage(9999);
            }
        }

        /// <summary>
        /// SerializeField 는 private 이라 리플렉션으로 넣는다.
        ///
        /// 테스트를 위해 런타임에 setter 를 뚫지 않는 쪽을 택했다 — 진행 값을 밖에서 바꿀 수 있게
        /// 열어 두면 본편 코드에도 그 경로가 생긴다. 이름이 바뀌면 여기서 즉시 실패하도록
        /// 조용한 no-op 대신 단언으로 막는다.
        /// </summary>
        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");
            field.SetValue(target, value);
        }

        private IEnumerator WaitUntilStepAtLeast(int step, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (director.CurrentStepIndex >= step) yield break;
                yield return null;
            }
            Assert.Fail(maxFrames + "프레임 안에 단계 " + step + "에 도달하지 못했다.");
        }
    }
}
