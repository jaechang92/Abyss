using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 맵 방의 바닥 높이 — 적 배치·추가 소환·보상 문·보상 제단이 <b>그 x 의 실제 바닥</b>에 서게 한다(<see cref="MapFloorRule"/>).
    ///
    /// 예전에는 적이 스폰 지점 높이(y 0) 고정, 문은 「가장 낮은 면」이라 지면 위 테라스가 생기면 테라스 <b>속</b>에 섰다.
    /// 🔑 공중 발판만 있는 방(Stage2/3)에서는 바닥 = 공용 지면이라 예전 값과 같다 — 적 높이는 「바닥 + (스폰 지점 − 지면)」이다.
    ///
    /// ⚠️ 방 입구는 여기를 쓰지 않는다 — 입구 배치(<see cref="PrepareRoomMap"/>)는 방 레이아웃이 바뀌기 <b>전</b>(방 진입 이벤트 전)이라
    /// 직전 방의 지형이 아직 켜져 있다. 입구는 모든 방에서 공용 지면 높이다(Stage1 레이아웃 검증이 확인한다).
    /// </summary>
    public sealed partial class StageDirector
    {
        private readonly List<float> floorTops = new();
        private readonly List<Collider2D> floorColliders = new();
        // 보상 제단의 씬 원래 높이. 여러 방에서 같은 제단을 다시 쓰므로 올린 높이가 쌓이지 않게 처음 값을 기억한다.
        private readonly Dictionary<Transform, float> altarBaseHeights = new();

        /// <summary>x 위치에서 걸어서 서는 바닥 윗면(묻히지 않은 가장 낮은 면). 맞은 면이 없으면 <see cref="ProbeGroundY"/>.</summary>
        private float ProbeFloorY(float x)
        {
            int mask = LayerMask.GetMask("Ground");
            var hits = Physics2D.RaycastAll(new Vector2(x, GROUND_PROBE_TOP), Vector2.down, GROUND_PROBE_TOP * 2f, mask);
            floorTops.Clear();
            floorColliders.Clear();
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || mapRoot != null && hit.collider.transform.IsChildOf(mapRoot)) continue;
                floorTops.Add(hit.point.y);
                floorColliders.Add(hit.collider);
            }

            return MapFloorRule.TryPickFloor(floorTops, y => IsInsideFloorCollider(x, y), out float floor)
                ? floor
                : ProbeGroundY(x);
        }

        private bool IsInsideFloorCollider(float x, float y)
        {
            var point = new Vector2(x, y);
            for (int i = 0; i < floorColliders.Count; i++)
            {
                if (floorColliders[i].OverlapPoint(point)) return true;
            }
            return false;
        }

        /// <summary>
        /// 맵 방 적의 등장 높이 — 그 x 의 바닥 + 스폰 지점이 지면에서 떠 있던 만큼.
        /// 공중 발판만 있는 방은 바닥이 지면이라 예전 스폰 높이(스폰 지점 y)와 같다.
        /// </summary>
        private float MapSpawnY(float x)
        {
            var spawn = GetSpawnPoint(0).position;
            float lift = Mathf.Max(0f, spawn.y - ProbeGroundY(spawn.x));
            return ProbeFloorY(x) + lift;
        }

        /// <summary>제단을 맵 가운데(x 0) 바닥 높이에 맞춘다 — 씬 높이는 공용 지면 기준이므로 바닥이 올라간 만큼만 올린다.</summary>
        private void PlaceAltarOnFloor(Transform altar)
        {
            if (!altarBaseHeights.TryGetValue(altar, out float baseY))
            {
                baseY = altar.position.y;
                altarBaseHeights[altar] = baseY;
            }
            float lift = ProbeFloorY(0f) - ProbeGroundY(0f);
            altar.position = new Vector3(0f, baseY + Mathf.Max(0f, lift), altar.position.z);
        }
    }
}
