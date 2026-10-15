using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>명시된 바닥 seed에서 보수적인 연속 바닥만 유한 탐색한다. Room API에 의존하지 않는다.</summary>
    public static class EnvironmentalSpawnResolver
    {
        private const int QUERY_CAPACITY = 64;

        public static bool TryResolve(Rect roomBounds, Vector2 originalRoot, Vector2 accessibleFloorAnchor,
            Vector2 bodySize, Vector2 bodyOffset, int groundMask, int obstacleMask, Transform ignoredRoot,
            EnvironmentalEnemyProfile profile, out Vector2 safeRoot, out string failure)
        {
            safeRoot = originalRoot;
            failure = "No connected clear Ground candidate within bounded search.";
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (profile == null || groundLayer < 0 || groundMask != (1 << groundLayer)
                || (obstacleMask & groundMask) != groundMask || !IsFinite(originalRoot)
                || !IsFinite(accessibleFloorAnchor) || !IsFinite(bodySize) || !IsFinite(bodyOffset)
                || bodySize.x <= 0f || bodySize.y <= 0f || !ValidBounds(roomBounds)
                || !roomBounds.Contains(accessibleFloorAnchor))
            {
                failure = "Invalid profile, room bounds, floor seed, body bounds or Ground/obstacle masks.";
                return false;
            }

            var rays = new RaycastHit2D[QUERY_CAPACITY];
            var overlaps = new Collider2D[QUERY_CAPACITY];
            var groundFilter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            var obstacleFilter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
            if (!TryFloor(accessibleFloorAnchor, groundFilter, ignoredRoot, profile, rays, out Vector2 seed))
            {
                failure = "Accessible floor seed has no supported solid non-one-way Ground surface.";
                return false;
            }

            for (int candidateIndex = 0; candidateIndex < profile.MaxCandidates; candidateIndex++)
            {
                int ring = (candidateIndex + 1) / 2;
                float sign = candidateIndex % 2 == 1 ? -1f : 1f;
                float offset = ring * profile.CandidateSpacing * sign;
                if (Mathf.Abs(offset) > profile.SearchRadius) continue;
                float x = originalRoot.x + bodyOffset.x + offset;
                float halfWidth = bodySize.x * 0.5f;
                if (x - halfWidth < roomBounds.xMin || x + halfWidth > roomBounds.xMax) continue;
                float spacing = Mathf.Min(profile.ConnectionSpacing, bodySize.x * 0.5f);
                int samples = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(x - seed.x) / spacing));
                if (samples > profile.MaxConnectionSamples) continue;
                Vector2 floor = seed;
                bool connected = true;
                // Include seed clearance: an authored seed inside a wall is not a walkable route.
                for (int sampleIndex = 0; sampleIndex <= samples; sampleIndex++)
                {
                    float sampleX = Mathf.Lerp(seed.x, x, (float)sampleIndex / samples);
                    float previousY = floor.y;
                    if (!TryFloor(new Vector2(sampleX, floor.y), groundFilter, ignoredRoot, profile, rays, out floor)
                        || Mathf.Abs(floor.y - previousY) > profile.FloorSkin
                        || !HasFullWidthSupport(floor, halfWidth, groundFilter, ignoredRoot, profile, rays)
                        || !TryClearRoot(floor, roomBounds, bodySize, bodyOffset, obstacleFilter,
                            ignoredRoot, profile, overlaps, out safeRoot))
                    {
                        connected = false;
                        break;
                    }
                    // Conservative swept body corridor rejects narrow terrain between floor samples.
                    if (sampleIndex > 0)
                    {
                        float previousX = Mathf.Lerp(seed.x, x, (float)(sampleIndex - 1) / samples);
                        Vector2 corridor = new Vector2((sampleX + previousX) * 0.5f,
                            floor.y + profile.FloorSkin + bodySize.y * 0.5f + profile.MaxFloorStep);
                        Vector2 corridorSize = new Vector2(bodySize.x + Mathf.Abs(sampleX - previousX), bodySize.y);
                        if (!IsClear(corridor, corridorSize, obstacleFilter, ignoredRoot, overlaps))
                        {
                            connected = false;
                            break;
                        }
                    }
                }
                if (!connected) continue;
                failure = string.Empty;
                return true;
            }
            safeRoot = originalRoot;
            return false;
        }

        /// <summary>이미 검증된 바닥 위에서 천장 아랫면·몸 전체 여백·수직 낙하 통로를 확인한다.</summary>
        public static bool TryResolveCeiling(Rect roomBounds, Vector2 safeFloorRoot, Vector2 bodySize,
            Vector2 bodyOffset, int groundMask, int obstacleMask, Transform ignoredRoot,
            EnvironmentalEnemyProfile profile, out Vector2 ceilingRoot, out string failure)
        {
            ceilingRoot = safeFloorRoot;
            failure = "No solid ceiling with a clear bounded drop corridor; use exposed ground fallback.";
            int layer = LayerMask.NameToLayer("Ground");
            if (profile == null || layer < 0 || groundMask != (1 << layer)
                || (obstacleMask & groundMask) != groundMask || !ValidBounds(roomBounds)
                || !IsFinite(safeFloorRoot) || !IsFinite(bodySize) || !IsFinite(bodyOffset)
                || bodySize.x <= 0f || bodySize.y <= 0f) return false;
            var rays = new RaycastHit2D[QUERY_CAPACITY];
            var overlaps = new Collider2D[QUERY_CAPACITY];
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            var obstacles = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
            Vector2 floorCenter = safeFloorRoot + bodyOffset;
            int count = Physics2D.Raycast(floorCenter, Vector2.up, filter, rays, profile.AttachmentProbeDistance);
            if (count >= rays.Length) return false;
            float closest = float.PositiveInfinity;
            Vector2 ceilingPoint = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = rays[i];
                if (!IsSolidGround(hit.collider, ignoredRoot) || hit.normal.y > -profile.MinimumGroundNormalY
                    || hit.distance <= 0f || hit.distance >= closest) continue;
                closest = hit.distance;
                ceilingPoint = hit.point;
                found = true;
            }
            if (!found) return false;
            Vector2 center = ceilingPoint - Vector2.up * (bodySize.y * 0.5f + profile.FloorSkin);
            Vector2 min = center - bodySize * 0.5f;
            Vector2 max = center + bodySize * 0.5f;
            float drop = center.y - floorCenter.y;
            if (drop <= 0f || drop > profile.MaxDropDistance || min.x < roomBounds.xMin
                || max.x > roomBounds.xMax || min.y < roomBounds.yMin || max.y > roomBounds.yMax
                || !IsClear(center, bodySize, obstacles, ignoredRoot, overlaps)) return false;
            count = Physics2D.BoxCast(center, bodySize, 0f, Vector2.down, obstacles, rays, drop);
            if (count >= rays.Length) return false;
            for (int i = 0; i < count; i++)
            {
                if (rays[i].collider != null
                    && (ignoredRoot == null || !rays[i].collider.transform.IsChildOf(ignoredRoot))) return false;
            }
            ceilingRoot = center - bodyOffset;
            failure = string.Empty;
            return true;
        }

        private static bool HasFullWidthSupport(Vector2 floor, float halfWidth, ContactFilter2D filter,
            Transform ignoredRoot, EnvironmentalEnemyProfile profile, RaycastHit2D[] rays)
        {
            float reach = profile.MaxFloorStep + profile.FloorSkin;
            int count = Physics2D.Raycast(floor + Vector2.up * reach, Vector2.down, filter, rays, reach * 2f);
            if (count >= rays.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var box = rays[i].collider as BoxCollider2D;
                if (!IsSolidGround(box, ignoredRoot) || box.edgeRadius > 0f
                    || Mathf.Abs(Mathf.DeltaAngle(box.transform.eulerAngles.z, 0f)) > 0.01f
                    || Mathf.Abs(box.bounds.max.y - floor.y) > 0.001f) continue;
                // Full footprint on axis-aligned boxes proves support between overlapping samples.
                if (box.bounds.min.x <= floor.x - halfWidth && box.bounds.max.x >= floor.x + halfWidth) return true;
            }
            return false;
        }

        private static bool TryFloor(Vector2 near, ContactFilter2D filter, Transform ignoredRoot,
            EnvironmentalEnemyProfile profile, RaycastHit2D[] rays, out Vector2 surface)
        {
            surface = near;
            float reach = profile.MaxFloorStep + profile.FloorSkin;
            int count = Physics2D.Raycast(near + Vector2.up * reach, Vector2.down, filter, rays, reach * 2f);
            if (count >= rays.Length) return false;
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = rays[i];
                if (!IsSolidGround(hit.collider, ignoredRoot) || hit.distance <= 0f
                    || hit.normal.y < profile.MinimumGroundNormalY || hit.distance >= nearest) continue;
                nearest = hit.distance;
                surface = hit.point;
                found = true;
            }
            return found;
        }

        private static bool TryClearRoot(Vector2 surface, Rect bounds, Vector2 size, Vector2 offset,
            ContactFilter2D filter, Transform ignoredRoot, EnvironmentalEnemyProfile profile,
            Collider2D[] overlaps, out Vector2 root)
        {
            Vector2 center = surface + Vector2.up * (size.y * 0.5f + profile.FloorSkin);
            root = center - offset;
            Vector2 min = center - size * 0.5f;
            Vector2 max = center + size * 0.5f;
            if (min.x < bounds.xMin || max.x > bounds.xMax || min.y < bounds.yMin || max.y > bounds.yMax) return false;
            return IsClear(center, size, filter, ignoredRoot, overlaps);
        }

        private static bool IsClear(Vector2 center, Vector2 size, ContactFilter2D filter,
            Transform ignoredRoot, Collider2D[] overlaps)
        {
            int count = Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = overlaps[i];
                if (collider != null && (ignoredRoot == null || !collider.transform.IsChildOf(ignoredRoot))) return false;
            }
            return true;
        }

        internal static bool IsSolidGround(Collider2D collider, Transform ignoredRoot)
        {
            if (collider == null || !collider.enabled || collider.isTrigger
                || (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot))) return false;
            if (collider.gameObject.layer != LayerMask.NameToLayer("Ground")) return false;
            var effector = collider.GetComponent<PlatformEffector2D>();
            if (effector == null) effector = collider.GetComponentInParent<PlatformEffector2D>();
            return !collider.usedByEffector || effector == null || !effector.enabled || !effector.useOneWay;
        }

        internal static bool IsFinite(Vector2 value) => EnvironmentalEnemyProfile.IsFinite(value.x)
            && EnvironmentalEnemyProfile.IsFinite(value.y);

        private static bool ValidBounds(Rect value) => EnvironmentalEnemyProfile.IsFinite(value.xMin)
            && EnvironmentalEnemyProfile.IsFinite(value.xMax) && EnvironmentalEnemyProfile.IsFinite(value.yMin)
            && EnvironmentalEnemyProfile.IsFinite(value.yMax) && value.width > 0f && value.height > 0f;
    }
}
