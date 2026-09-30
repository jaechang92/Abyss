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

        // ───────────────────────────── 방 전용 아트(대표방)

        [Test]
        public void 방_전용_아트는_지정한_일반_방에서만_켜진다()
        {
            var art = Room("art");
            var other = Room("other");
            var boss = Room("boss");
            var stage = Stage(new[] { art }, new[] { other }, new[] { boss });

            Assert.IsTrue(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, art), art, art));
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, other), art, other), "다른 방");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, boss), boss, boss), "보스 방은 지정해도 안 켠다");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, null), art, null), "스테이지 밖");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentLook.Field, null, art), "방 미지정");
        }

        [Test]
        public void 방_전용_아트는_그_방에서만_교체_대상을_끄고_나가면_복원한다()
        {
            var art = Room("art");
            var other = Room("other");
            var boss = Room("boss");
            var stage = Stage(new[] { art }, new[] { other }, new[] { boss });

            var host = Track(new GameObject("Stage1Environment"));
            var shared = Track(new GameObject("Shared"));
            var groundSkin = Track(new GameObject("GroundSkin"));
            groundSkin.transform.SetParent(shared.transform);
            var platformSkin = Track(new GameObject("Stage1Skin"));
            var field = Track(new GameObject("Field"));
            var arena = Track(new GameObject("BossArena"));
            var roomArt = Track(new GameObject("RoomArt"));
            var graybox = Track(new GameObject("Graybox")).AddComponent<SpriteRenderer>();

            var presenter = host.AddComponent<StageEnvironmentPresenter>();
            var so = new SerializedObject(presenter);
            SetArray(so.FindProperty("sharedRoots"), shared, platformSkin);
            so.FindProperty("fieldRoot").objectReferenceValue = field;
            so.FindProperty("bossArenaRoot").objectReferenceValue = arena;
            SetArray(so.FindProperty("grayboxRenderers"), graybox);
            so.FindProperty("roomArtRoom").objectReferenceValue = art;
            so.FindProperty("roomArtRoot").objectReferenceValue = roomArt;
            SetArray(so.FindProperty("roomArtReplacedRoots"), field, groundSkin, platformSkin);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 기대값 순서: 공용 층 · 지면 스킨 · 발판 스킨 · 일반 방 배경 · 보스 방 배경 · 방 아트 · 그레이박스
            void Enter(RoomData room) => presenter.Apply(StageEnvironmentRule.Resolve(stage, boss, room), room);
            bool[] States() => new[] { shared.activeSelf, groundSkin.activeSelf, platformSkin.activeSelf, field.activeSelf,
                                       arena.activeSelf, roomArt.activeSelf, graybox.enabled };

            Enter(art);
            AssertRoomArtStates("대표방", new[] { true, false, false, false, false, true, false }, States());
            Assert.IsTrue(presenter.IsRoomArtShown);

            Enter(other);
            AssertRoomArtStates("다른 방", new[] { true, true, true, true, false, false, false }, States());

            Enter(art);
            Enter(boss);
            AssertRoomArtStates("보스 방", new[] { true, true, true, false, true, false, false }, States());

            Enter(art);
            Enter(null);
            AssertRoomArtStates("스테이지 밖", new[] { false, true, false, false, false, false, true }, States());
            Assert.IsFalse(presenter.IsRoomArtShown);

            presenter.Apply(StageEnvironmentLook.Field);
            AssertRoomArtStates("방 없이 Field", new[] { true, true, true, true, false, false, false }, States());
        }

        [Test]
        public void 방_아트_루트가_비어_있으면_기존_표시를_유지한다()
        {
            var art = Room("art");
            var stage = Stage(new[] { art });

            var host = Track(new GameObject("Stage1Environment"));
            var field = Track(new GameObject("Field"));
            var presenter = host.AddComponent<StageEnvironmentPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("fieldRoot").objectReferenceValue = field;
            so.FindProperty("roomArtRoom").objectReferenceValue = art;
            SetArray(so.FindProperty("roomArtReplacedRoots"), field);
            so.ApplyModifiedPropertiesWithoutUndo();

            presenter.Apply(StageEnvironmentRule.Resolve(stage, null, art), art);
            Assert.IsTrue(field.activeSelf, "아트 파일·배선 전에는 일반 방 배경을 끄지 않는다");
            Assert.IsFalse(presenter.IsRoomArtShown);
        }

        // ───────────────────────────── 방 아트(스테이지 전체 — Stage1 전 방 적용)

        [Test]
        public void 스테이지_전체_방_아트는_보스_방까지_켜고_스테이지_밖에서만_끈다()
        {
            var normal = Room("normal");
            var boss = Room("boss");
            var stage = Stage(new[] { normal }, new[] { boss });
            var outside = Room("outside");

            Assert.IsTrue(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, normal), null, normal, true));
            Assert.IsTrue(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, boss), null, boss, true), "보스 방");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, outside), null, outside, true), "다른 스테이지 방");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, null), null, null, true), "방 없음");
            Assert.IsFalse(StageEnvironmentRule.ShowsRoomArt(StageEnvironmentRule.Resolve(stage, boss, boss), normal, boss, false),
                           "전체 모드가 꺼져 있으면 한 방 규칙 그대로");
        }

        [Test]
        public void 스테이지_전체_방_아트는_일반_방과_보스_방_배경을_모두_대체하고_밖에서_복원한다()
        {
            var normal = Room("normal");
            var boss = Room("boss");
            var stage = Stage(new[] { normal }, new[] { boss });
            var outside = Room("outside");

            var host = Track(new GameObject("Stage1Environment"));
            var shared = Track(new GameObject("Shared"));
            var groundSkin = Track(new GameObject("GroundSkin"));
            groundSkin.transform.SetParent(shared.transform);
            var terrainSkin = Track(new GameObject("Stage1Skin"));
            var field = Track(new GameObject("Field"));
            var arena = Track(new GameObject("BossArena"));
            var roomArt = Track(new GameObject("RoomArt"));
            var graybox = Track(new GameObject("Graybox")).AddComponent<SpriteRenderer>();

            var presenter = host.AddComponent<StageEnvironmentPresenter>();
            var so = new SerializedObject(presenter);
            SetArray(so.FindProperty("sharedRoots"), shared, terrainSkin);
            so.FindProperty("fieldRoot").objectReferenceValue = field;
            so.FindProperty("bossArenaRoot").objectReferenceValue = arena;
            SetArray(so.FindProperty("grayboxRenderers"), graybox);
            so.FindProperty("roomArtWholeStage").boolValue = true;
            so.FindProperty("roomArtRoot").objectReferenceValue = roomArt;
            SetArray(so.FindProperty("roomArtReplacedRoots"), field, arena, groundSkin);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 기대값 순서: 공용 층 · 지면 스킨 · 발판 스킨 · 일반 방 배경 · 보스 방 배경 · 방 아트 · 그레이박스
            void Enter(RoomData room) => presenter.Apply(StageEnvironmentRule.Resolve(stage, boss, room), room);
            bool[] States() => new[] { shared.activeSelf, groundSkin.activeSelf, terrainSkin.activeSelf, field.activeSelf,
                                       arena.activeSelf, roomArt.activeSelf, graybox.enabled };

            Enter(normal);
            AssertRoomArtStates("일반 방", new[] { true, false, true, false, false, true, false }, States());

            Enter(boss);
            AssertRoomArtStates("보스 방", new[] { true, false, true, false, false, true, false }, States());

            Enter(outside);
            AssertRoomArtStates("다른 스테이지", new[] { false, true, false, false, false, false, true }, States());
            Assert.IsFalse(presenter.IsRoomArtShown);

            Enter(normal);
            AssertRoomArtStates("복귀", new[] { true, false, true, false, false, true, false }, States());
        }

        // ───────────────────────────── 맵 바닥 판정(테라스 위 적·문)

        [Test]
        public void 바닥은_묻히지_않은_가장_낮은_윗면이다()
        {
            // 지면 윗면 -0.5 가 테라스(-0.5~1.5) 안에 묻혀 있다 → 테라스 윗면 1.5. 그 위 공중 발판 4 는 더 높아 고르지 않는다.
            var tops = new List<float> { 4f, 1.5f, -0.5f };
            bool IsBlocked(float y) => y > -0.5f && y < 1.5f;

            Assert.IsTrue(MapFloorRule.TryPickFloor(tops, IsBlocked, out float floor));
            Assert.AreEqual(1.5f, floor);
        }

        [Test]
        public void 공중_발판만_있으면_바닥은_예전처럼_지면이다()
        {
            var tops = new List<float> { 2.5f, -0.5f };
            Assert.IsTrue(MapFloorRule.TryPickFloor(tops, _ => false, out float floor));
            Assert.AreEqual(-0.5f, floor);
            Assert.IsFalse(MapFloorRule.TryPickFloor(new List<float>(), _ => false, out _), "맞은 면이 없으면 호출자가 폴백한다");
        }

        private static void AssertRoomArtStates(string where, bool[] expected, bool[] actual)
        {
            string[] names = { "공용 층", "지면 스킨", "발판 스킨", "일반 방 배경", "보스 방 배경", "방 아트", "그레이박스" };
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], $"{where}: {names[i]}");
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
