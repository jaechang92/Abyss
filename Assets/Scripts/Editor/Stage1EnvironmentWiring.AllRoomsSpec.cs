#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// Stage1 전 방(11곳) 지형 규격 — 기획 all-rooms-level-design.md 를 좌표로 옮긴 것. 적용·검증·결과 문서가 같은 표를 읽는다.
    ///
    /// 🔑 지형은 두 종류뿐이다.
    /// <list type="bullet">
    /// <item><b>테라스</b> — 공용 지면 윗면(-0.5)에서 솟은 실체 단. 기본 보행 경로의 오르내림이다. 아래로 지나갈 수 없고 적도 그 위에 선다.</item>
    /// <item><b>석판</b> — 두께 0.5 공중 발판. 전부 <b>선택 경로</b>다(이단 점프 이상). 아래 바닥과 머리 공간 ≥ 2.5 를 둬 기본 보행을 막지 않는다.</item>
    /// </list>
    /// 좌표 규약: 맵 중심 x 0, 입구 = −길이/2 + 2, 보상 문 = 길이/2 − 3 − i·3.5, 제단 x 0 (StageDirector.Map.cs).
    /// 높이 값은 월드 윗면 y 다(공용 지면 윗면 -0.5 기준). 적용 시 씬 지면이 -0.5 가 아니면 멈춘다.
    ///
    /// ⚠️ RoomLayoutBuilder 에 남아 있는 Stage1 옛 좌표는 <b>복원용 원본</b>이다 — 그 빌더를 다시 돌리면 이 지형이 사라진다.
    /// </summary>
    public static partial class Stage1EnvironmentWiring
    {
        /// <summary>규격이 전제하는 공용 지면 윗면(Run 씬 Ground 콜라이더 윗면).</summary>
        internal const float SpecGroundTop = -0.5f;
        internal const float SlabThickness = 0.5f;

        /// <summary>필수 경로의 한 번 오르기 최대. 최저 폼(점프 1회) 최고점의 60% 이하여야 한다(검증이 실제 값으로 다시 잰다).</summary>
        internal const float RequiredRiseMax = 1.25f;
        /// <summary>필수 경로 착지면 최소 폭(플레이어 폭 1 + 좌우 여유).</summary>
        internal const float MinLandingWidth = 3f;
        /// <summary>석판 아래 머리 공간 = 플레이어 키 + 이만큼.</summary>
        internal const float HeadroomMargin = 0.5f;
        /// <summary>선택 석판: 필요 점프 최고점의 이 비율 안이어야 「닿는다」로 본다.</summary>
        internal const float OptionalReachRatio = 0.9f;

        private const float EntranceClear = 1f;      // 입구 x 기준 좌우(지면 높이 평탄)
        private const float DoorClear = 1.2f;        // 문 폭 1.6 + 테 0.2 의 절반 + 여유
        private const float AltarClear = 1.5f;
        private const float AltarHeadroom = 4f;      // 제단 위 비워 둘 높이(상호작용·판독)
        private const float SpawnEdgeMin = 1f;       // 적 등장 x 와 그 면 가장자리 거리
        private const float BossFlatMin = 12f;       // 보스 등장 면의 최소 평탄 폭
        private const float MapDoorRightInset = 3f;  // StageDirector.Map DOOR_RIGHT_INSET
        private const float MapDoorSpacing = 3.5f;   // StageDirector.Map DOOR_SPACING
        private const float MapEntranceInset = 2f;   // StageDirector.Map MAP_ENTRANCE_INSET

        internal readonly struct TerraceSpec
        {
            public readonly float XMin;
            public readonly float XMax;
            public readonly float Top;

            public TerraceSpec(float xMin, float xMax, float top)
            {
                XMin = xMin;
                XMax = xMax;
                Top = top;
            }

            public Vector2 Center => new((XMin + XMax) * 0.5f, (SpecGroundTop + Top) * 0.5f);
            public Vector2 Size => new(XMax - XMin, Top - SpecGroundTop);
        }

        internal readonly struct SlabSpec
        {
            public readonly float X;
            public readonly float Top;
            public readonly float Width;

            public SlabSpec(float x, float top, float width)
            {
                X = x;
                Top = top;
                Width = width;
            }

            public float XMin => X - Width * 0.5f;
            public float XMax => X + Width * 0.5f;
            public float Bottom => Top - SlabThickness;
            public Vector2 Center => new(X, Top - SlabThickness * 0.5f);
            public Vector2 Size => new(Width, SlabThickness);
        }

        internal sealed class Stage1RoomSpec
        {
            public string AssetName;
            public string Role;
            public TerraceSpec[] Terraces;
            public SlabSpec[] Slabs;
        }

        private static TerraceSpec T(float xMin, float xMax, float top) => new(xMin, xMax, top);
        private static SlabSpec S(float x, float top, float width) => new(x, top, width);

        /// <summary>Stage1 11방 규격. 키 = RoomData 에셋 파일 이름.</summary>
        internal static readonly Stage1RoomSpec[] Stage1RoomSpecs =
        {
            new() { AssetName = "Room1_Intro", Role = "작업장 입구 통로 — 안전한 시작 → 낮은 단차 2개(0.5·0.5) → 넓은 첫 전투면 → 낮은 내려오기",
                    Terraces = new[] { T(-16f, -11f, 0f), T(-11f, 10f, 0.5f) },
                    Slabs = new[] { S(-1f, 3.5f, 3f) } },
            new() { AssetName = "Room2_Skirmish", Role = "선반 사이 비대칭 통로 — 상승·전투면 → 하강 → 낮은 전투면 → 사수 선반(상단) → 하강",
                    Terraces = new[] { T(-15f, -7f, 0.5f), T(-2f, 5f, 0.25f), T(5f, 11f, 1.5f) },
                    Slabs = new[] { S(-4.5f, 3.5f, 3f) } },
            new() { AssetName = "Room3_Event_BrokenAltar", Role = "어긋난 생활방 — 작은 오름 → 중앙 평탄 사건면 → 작은 오름 → 평탄 출구",
                    Terraces = new[] { T(-8f, -3f, 0.25f), T(2f, 7f, 0.25f) },
                    Slabs = new[] { S(-0.5f, 3f, 3f) } },
            new() { AssetName = "Room3_Crowd", Role = "대표 작업장 전투방 — 좌 전투 바닥 → 좌 디딤 → 중앙 고지대(폼 제단) → 우 디딤 → 우 전투 바닥",
                    Terraces = new[] { T(-8f, -3f, 0.5f), T(-3f, 4f, 1.5f), T(4f, 9f, 0.5f) },
                    Slabs = new[] { S(-13f, 2.75f, 3f), S(13f, 2.75f, 3f) } },
            new() { AssetName = "Room4_Bonefield", Role = "다층 통로 — 근접 평지 → 엄폐 단 → 사수 상단1 → 골 → 사수 상단2 → 출구",
                    Terraces = new[] { T(-6f, -1f, 0.5f), T(-1f, 5f, 1.5f), T(5f, 10f, 0.5f), T(10f, 15f, 1.5f) },
                    Slabs = new[] { S(-12.5f, 2.75f, 3f) } },
            new() { AssetName = "Room4_Elite", Role = "좁은 접근부 → 넓은 결투 테라스(가는 기둥 대체) → 하강 → 분기 문 두 개",
                    Terraces = new[] { T(-14f, -8f, 0.25f), T(-8f, 5f, 1f), T(5f, 10f, 0.25f) },
                    Slabs = new[] { S(-12f, 3.25f, 3f) } },
            new() { AssetName = "Room5_Ambush", Role = "어긋난 계단 — 상승 → 중간 착지 → 정상(매복 예고 전 안전면) → 하강 → 출구",
                    Terraces = new[] { T(-15f, -6f, 0.25f), T(-6f, -1f, 1f), T(-1f, 7f, 2f), T(7f, 12f, 1f) },
                    Slabs = new[] { S(-11f, 3.5f, 3f) } },
            new() { AssetName = "Room5_Alt_SealedDoor", Role = "막힌 문 분기 — 완만한 상승 2단 → 넓은 문 앞 평탄면 → 내려와 출구",
                    Terraces = new[] { T(-8f, -4f, 0f), T(-4f, 6f, 0.5f) },
                    Slabs = new SlabSpec[0] },
            new() { AssetName = "Room6_Shop_Peddler", Role = "임시 교역소 — 짧은 접근 단차 → 넓은 평탄 상점 바닥 → 출구(서비스를 가리는 발판 없음)",
                    Terraces = new[] { T(-8f, -3f, 0.25f) },
                    Slabs = new SlabSpec[0] },
            new() { AssetName = "Room6_Rest_Ember", Role = "조용한 준비 공간 — 낮은 단 → 쉬는 중앙 바닥 → 보스 문 앞 마지막 짧은 상승·하강",
                    Terraces = new[] { T(-8f, -3f, 0f), T(2f, 7f, 0.25f) },
                    Slabs = new SlabSpec[0] },
            new() { AssetName = "Room6_Boss", Role = "수문장 방 — 넓고 평탄한 중앙 전투면 + 좌우 낮은 회피 단(보스 근접·탄막이 닿는 높이)",
                    Terraces = new[] { T(-11.5f, -7f, 0.25f), T(6.5f, 10.5f, 0.25f) },
                    Slabs = new SlabSpec[0] },
        };

        internal static Stage1RoomSpec FindRoomSpec(string assetName)
            => Stage1RoomSpecs.FirstOrDefault(spec => spec.AssetName == assetName);

        // ───────────────────────────────────────────────────────────── 점프 모델

        /// <summary>
        /// 실제 플레이어 값으로 잰 도약 능력. 최고점 = v²/2g(점프마다 속도를 v 로 덮어쓴다 — PlayerCharacter.OnJump),
        /// 가로 도달 = 최저 이동 속도 × 그 높이 위에 머무는 시간.
        /// </summary>
        internal readonly struct Stage1JumpModel
        {
            public readonly float JumpSpeed;
            public readonly float Gravity;
            public readonly float MinMoveSpeed;
            public readonly float PlayerHeight;
            public readonly int MinJumps;
            public readonly int MaxJumps;
            public readonly string Source;

            public Stage1JumpModel(float jumpSpeed, float gravity, float minMoveSpeed, float playerHeight, int minJumps, int maxJumps, string source)
            {
                JumpSpeed = jumpSpeed;
                Gravity = gravity;
                MinMoveSpeed = minMoveSpeed;
                PlayerHeight = playerHeight;
                MinJumps = minJumps;
                MaxJumps = maxJumps;
                Source = source;
            }

            public float SingleApex => JumpSpeed * JumpSpeed / (2f * Gravity);
            public float ApexFor(int jumps) => SingleApex * Mathf.Max(1, jumps);

            /// <summary>한 번 점프로 <paramref name="rise"/> 높이 위에 머무는 동안 최저 속도로 가는 가로 거리(오를 수 없으면 0).</summary>
            public float ReachAtRise(float rise)
            {
                float disc = JumpSpeed * JumpSpeed - 2f * Gravity * Mathf.Max(0f, rise);
                if (disc < 0f) return 0f;
                return MinMoveSpeed * (JumpSpeed + Mathf.Sqrt(disc)) / Gravity;
            }

            public string Describe()
                => $"점프 속도 {JumpSpeed:0.##} · 중력 {Gravity:0.##} · 최저 이동 {MinMoveSpeed:0.##} · 키 {PlayerHeight:0.##} · 점프 {MinJumps}~{MaxJumps}회\n" +
                   $"    1회 최고 {SingleApex:0.###} · 2회 {ApexFor(2):0.###} · {MaxJumps}회 {ApexFor(MaxJumps):0.###} · " +
                   $"필수 오르기 한도 {RequiredRiseMax}(= 1회 최고의 {RequiredRiseMax / SingleApex:P0}) · 그 높이에서 가로 도달 {ReachAtRise(RequiredRiseMax):0.##}\n    ({Source})";
        }

        // ───────────────────────────────────────────────────────────── 지형 해석(순수 계산)

        /// <summary>x 의 걸어서 서는 바닥 — 테라스 안이면 테라스 윗면, 아니면 공용 지면(석판은 바닥이 아니다).</summary>
        internal static float SpecFloorAt(Stage1RoomSpec spec, float x)
        {
            float floor = SpecGroundTop;
            foreach (var t in spec.Terraces)
            {
                if (x > t.XMin && x < t.XMax) floor = Mathf.Max(floor, t.Top);
            }
            return floor;
        }

        /// <summary>맵 [−half, half] 를 바닥 높이가 같은 구간으로 나눈다(왼쪽부터).</summary>
        internal static List<(float XMin, float XMax, float Top)> SpecSegments(Stage1RoomSpec spec, float half)
        {
            var cuts = new SortedSet<float> { -half, half };
            foreach (var t in spec.Terraces)
            {
                cuts.Add(Mathf.Clamp(t.XMin, -half, half));
                cuts.Add(Mathf.Clamp(t.XMax, -half, half));
            }

            var list = cuts.ToList();
            var result = new List<(float, float, float)>();
            for (int i = 0; i + 1 < list.Count; i++)
            {
                float a = list[i], b = list[i + 1];
                if (b - a <= Epsilon) continue;
                float top = SpecFloorAt(spec, (a + b) * 0.5f);
                if (result.Count > 0 && Mathf.Approximately(result[^1].Item3, top))
                    result[^1] = (result[^1].Item1, b, top);
                else
                    result.Add((a, b, top));
            }
            return result;
        }

        /// <summary>한 x 가 속한 구간과 그 구간 가장자리까지 거리.</summary>
        internal static (float XMin, float XMax, float Top) SegmentAt(List<(float XMin, float XMax, float Top)> segments, float x)
            => segments.FirstOrDefault(s => x >= s.XMin && x <= s.XMax);

        /// <summary>맵 방 보상 문 x 목록(StageDirector.Map 과 같은 식).</summary>
        internal static List<float> DoorXs(float half, int doorCount)
        {
            var result = new List<float>();
            for (int i = 0; i < Mathf.Max(1, doorCount); i++) result.Add(half - MapDoorRightInset - i * MapDoorSpacing);
            return result;
        }

        internal static float EntranceX(float half) => -half + MapEntranceInset;
    }
}
#endif
