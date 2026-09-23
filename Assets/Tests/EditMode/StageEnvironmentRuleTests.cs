using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Stage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// Stage1 환경 표현의 전환 규칙(C2). Run 은 S1~S3 공용 씬이라 <b>S2·S3 방에서 S1 그림이 남으면 인수 불가</b>다(총괄 결정 6).
    /// 규칙 단위 · 실제 스테이지 데이터 · 표현 컴포넌트의 활성 토글을 따로 고정한다.
    /// 씬은 열지 않는다(씬 배선은 에디터 진입점 <c>Stage1EnvironmentWiring.ValidateRunScene</c> 이 본다).
    /// </summary>
    public sealed class StageEnvironmentRuleTests
    {
        private const string SequencePath = "Assets/Data/Stages/MainRunSequence.asset";
        private const string Stage1Path = "Assets/Data/Stages/Stage1_AbyssEntrance.asset";
        private const string BossRoomPath = "Assets/Data/Rooms/Room6_Boss.asset";

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        // ───────────────────────────── 규칙 단위

        [Test]
        public void 스테이지의_일반_방은_Field_보스_방은_BossArena다()
        {
            var normal = Room("normal");
            var branchA = Room("branchA");
            var branchB = Room("branchB");
            var boss = Room("boss");
            var stage = Stage(new[] { normal }, new[] { branchA, branchB }, new[] { boss });

            Assert.AreEqual(StageEnvironmentLook.Field, StageEnvironmentRule.Resolve(stage, boss, normal));
            Assert.AreEqual(StageEnvironmentLook.Field, StageEnvironmentRule.Resolve(stage, boss, branchB), "분기 두 번째 선택지도 이 스테이지 방이다");
            Assert.AreEqual(StageEnvironmentLook.BossArena, StageEnvironmentRule.Resolve(stage, boss, boss));
        }

        [Test]
        public void 다른_스테이지의_방은_보스_방이어도_Hidden이다()
        {
            var s1Boss = Room("s1Boss");
            var s2Room = Room("s2Room");
            var s2Boss = Room("s2Boss");
            var stage1 = Stage(new[] { Room("s1Room") }, new[] { s1Boss });
            Stage(new[] { s2Room }, new[] { s2Boss });

            Assert.AreEqual(StageEnvironmentLook.Hidden, StageEnvironmentRule.Resolve(stage1, s1Boss, s2Room));
            Assert.AreEqual(StageEnvironmentLook.Hidden, StageEnvironmentRule.Resolve(stage1, s1Boss, s2Boss), "보스 방 배경은 S1 보스 방에만");
        }

        [Test]
        public void 방이나_스테이지가_없으면_Hidden이다()
        {
            var room = Room("room");
            var stage = Stage(new[] { room });
            Assert.AreEqual(StageEnvironmentLook.Hidden, StageEnvironmentRule.Resolve(stage, null, null));
            Assert.AreEqual(StageEnvironmentLook.Hidden, StageEnvironmentRule.Resolve(null, null, room));
            Assert.AreEqual(StageEnvironmentLook.Field, StageEnvironmentRule.Resolve(stage, null, room), "보스 방 미지정이면 일반 방으로만 본다");
        }

        [Test]
        public void 그레이박스와_스킨은_어느_모습에서도_동시에_켜지지_않는다()
        {
            foreach (StageEnvironmentLook look in System.Enum.GetValues(typeof(StageEnvironmentLook)))
            {
                Assert.AreNotEqual(StageEnvironmentRule.ShowsShared(look), StageEnvironmentRule.ShowsGraybox(look), look.ToString());
                Assert.IsFalse(StageEnvironmentRule.ShowsField(look) && StageEnvironmentRule.ShowsBossArena(look), look.ToString());
            }
        }

        // ───────────────────────────── 실제 데이터

        [Test]
        public void 실제_시퀀스에서_S2_S3_방은_전부_Hidden이고_보스_배경은_한_곳이다()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>(SequencePath);
            var stage1 = AssetDatabase.LoadAssetAtPath<StageData>(Stage1Path);
            var bossRoom = AssetDatabase.LoadAssetAtPath<RoomData>(BossRoomPath);
            Assert.IsNotNull(sequence, SequencePath);
            Assert.IsNotNull(stage1, Stage1Path);
            Assert.IsNotNull(bossRoom, BossRoomPath);
            Assert.AreSame(stage1, sequence.stages[0], "런은 Stage1 에서 시작한다 — 표현 초기값 Field 의 전제");

            int arenas = 0;
            foreach (var stage in sequence.stages)
            {
                foreach (var room in Rooms(stage))
                {
                    var look = StageEnvironmentRule.Resolve(stage1, bossRoom, room);
                    if (stage == stage1)
                        Assert.AreNotEqual(StageEnvironmentLook.Hidden, look, room.roomId);
                    else
                        Assert.AreEqual(StageEnvironmentLook.Hidden, look, $"{stage.stageId}/{room.roomId} 에 S1 그림이 남는다");

                    if (look == StageEnvironmentLook.BossArena) arenas++;
                }
            }
            Assert.AreEqual(1, arenas);
        }

        [Test]
        public void S1_방은_다른_스테이지와_공유되지_않는다()
        {
            // 공유되면 같은 RoomData 로 들어온 S2 에서 S1 그림이 켜진다 — 판정이 방 단위라서다.
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>(SequencePath);
            var stage1 = AssetDatabase.LoadAssetAtPath<StageData>(Stage1Path);
            var stage1Rooms = new HashSet<RoomData>(Rooms(stage1));

            foreach (var stage in sequence.stages.Where(s => s != stage1))
            {
                foreach (var room in Rooms(stage))
                    Assert.IsFalse(stage1Rooms.Contains(room), $"{stage.stageId} 가 S1 방 {room.roomId} 을 쓴다");
            }
        }

        // ───────────────────────────── 표현 컴포넌트 토글

        [Test]
        public void 표현_컴포넌트는_모습마다_루트와_그레이박스를_규칙대로_켜고_끈다()
        {
            var host = Track(new GameObject("Stage1Environment"));
            var shared = Track(new GameObject("Shared"));
            var skin = Track(new GameObject("Stage1Skin"));
            var field = Track(new GameObject("Field"));
            var arena = Track(new GameObject("BossArena"));
            var grayboxGo = Track(new GameObject("Graybox"));
            var graybox = grayboxGo.AddComponent<SpriteRenderer>();

            var presenter = host.AddComponent<StageEnvironmentPresenter>();
            var so = new SerializedObject(presenter);
            SetArray(so.FindProperty("sharedRoots"), shared, skin);
            so.FindProperty("fieldRoot").objectReferenceValue = field;
            so.FindProperty("bossArenaRoot").objectReferenceValue = arena;
            SetArray(so.FindProperty("grayboxRenderers"), graybox);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 기대값 순서: 공용 층 · 발판 스킨 · 일반 방 배경 · 보스 방 배경 · 그레이박스
            presenter.Apply(StageEnvironmentLook.Field);
            AssertStates("Field", new[] { true, true, true, false, false },
                         shared.activeSelf, skin.activeSelf, field.activeSelf, arena.activeSelf, graybox.enabled);

            presenter.Apply(StageEnvironmentLook.BossArena);
            AssertStates("BossArena", new[] { true, true, false, true, false },
                         shared.activeSelf, skin.activeSelf, field.activeSelf, arena.activeSelf, graybox.enabled);

            presenter.Apply(StageEnvironmentLook.Hidden);
            AssertStates("Hidden", new[] { false, false, false, false, true },
                         shared.activeSelf, skin.activeSelf, field.activeSelf, arena.activeSelf, graybox.enabled);
            Assert.IsTrue(host.activeSelf, "표현 컴포넌트는 자기 자신을 끄지 않는다(구독이 풀린다)");
            Assert.AreEqual(StageEnvironmentLook.Hidden, presenter.CurrentLook);
        }

        // ───────────────────────────── 도우미

        private static void AssertStates(string look, bool[] expected, params bool[] actual)
        {
            string[] names = { "공용 층", "발판 스킨", "일반 방 배경", "보스 방 배경", "그레이박스" };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], $"{look}: {names[i]}");
        }

        private GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        private RoomData Room(string id)
        {
            var room = ScriptableObject.CreateInstance<RoomData>();
            room.roomId = id;
            created.Add(room);
            return room;
        }

        private StageData Stage(params RoomData[][] steps)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            foreach (var options in steps)
                stage.steps.Add(new StageStep { options = new List<RoomData>(options) });
            created.Add(stage);
            return stage;
        }

        private static IEnumerable<RoomData> Rooms(StageData stage)
            => stage.steps.Where(step => step?.options != null).SelectMany(step => step.options).Where(room => room != null);

        private static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
