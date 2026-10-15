using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    public abstract partial class EnvironmentalEnemyBase
    {
        private float localGravityScale = 1f;
        private string localGroundProblem;
        protected virtual bool UsesLocalGround => false;

        protected override void Start()
        {
            if (UsesLocalGround)
            {
                hasContext = true;
                originalPosition = transform.position;
                if (Body != null && Body.gravityScale > 0f) localGravityScale = Body.gravityScale;
                RefreshLocalGround();
            }
            base.Start();
        }

        private bool RefreshLocalGround()
        {
            hasContext = true;
            groundMask = LayerMask.GetMask("Ground");
            obstacleMask = groundMask;
            if (profile == null || Data == null || physicalBody == null || !physicalBody.enabled
                || physicalBody.isTrigger || !physicalBody.transform.IsChildOf(transform)
                || Body == null || !Body.simulated || Body.bodyType != RigidbodyType2D.Dynamic || groundMask == 0)
                return WaitForLocalGround("Requires EnemyData, profile, solid body, dynamic Rigidbody2D and Ground layer.");

            Bounds bounds = physicalBody.bounds;
            bodySize = bounds.size;
            bodyOffset = (Vector2)bounds.center - (Vector2)transform.position;
            if (!EnvironmentalSpawnResolver.IsFinite(bodySize) || bodySize.x <= 0f || bodySize.y <= 0f)
                return WaitForLocalGround("Body collider has no usable size.");
            if (!TryLocalGroundPosition(Body.position, out Vector2 grounded))
                return WaitForLocalGround("Waiting for solid Ground immediately beneath the feet.");

            localGroundProblem = null;
            if (isInitialized) return true;
            CancelPattern();
            SynchronizePosition(grounded);
            safePosition = grounded;
            isInitialized = true;
            requiresRecovery = false;
            SetEnvironmentExposed(true);
            OnEnvironmentInitialized();
            EnterEnvironmentalState(RECOVERY_STATE, "move", EnvironmentRecoveryDuration);
            return true;
        }

        private bool WaitForLocalGround(string reason)
        {
            if (isInitialized)
            {
                isInitialized = false;
                CancelPattern();
                SetEnvironmentExposed(true);
            }
            if (Body != null) Body.gravityScale = localGravityScale;
            if (localGroundProblem != reason)
                Debug.LogWarning($"[EnvironmentalGround] enemyId={Data?.enemyId ?? name} reason={reason}", this);
            localGroundProblem = reason;
            return false;
        }

        protected bool CanMoveOnLocalGround(Vector2 desired)
        {
            if (!isInitialized || !TryLocalGroundPosition(desired, out Vector2 grounded)) return false;
            Vector2 travel = grounded - Body.position;
            float distance = travel.magnitude;
            if (distance <= 0.0001f) return true;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
            Vector2 size = Vector2.Max(Vector2.one * 0.01f, bodySize - Vector2.one * (profile.FloorSkin * 2f));
            int count = Physics2D.BoxCast(Body.position + bodyOffset, size, 0f,
                travel / distance, filter, castResults, distance);
            if (count >= castResults.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = castResults[i].collider;
                if (collider != null && !collider.transform.IsChildOf(transform)) return false;
            }
            return Mathf.Abs(grounded.y - Body.position.y) <= profile.FloorSkin;
        }

        private bool TryLocalGroundPosition(Vector2 root, out Vector2 grounded)
        {
            grounded = root;
            Vector2 foot = root + bodyOffset - Vector2.up * (bodySize.y * 0.5f);
            float halfWidth = Mathf.Max(0f, bodySize.x * 0.5f - profile.FloorSkin);
            if (!TryLocalFloor(foot, out float centerY)
                || !TryLocalFloor(foot + Vector2.left * halfWidth, out float leftY)
                || !TryLocalFloor(foot + Vector2.right * halfWidth, out float rightY)
                || Mathf.Abs(leftY - centerY) > profile.FloorSkin
                || Mathf.Abs(rightY - centerY) > profile.FloorSkin) return false;
            grounded.y += centerY + profile.FloorSkin - foot.y;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
            Vector2 size = Vector2.Max(Vector2.one * 0.01f, bodySize - Vector2.one * (profile.FloorSkin * 2f));
            int count = Physics2D.OverlapBox(grounded + bodyOffset, size, 0f, filter, overlapResults);
            if (count >= overlapResults.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = overlapResults[i];
                if (collider != null && !collider.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        private bool TryLocalFloor(Vector2 foot, out float y)
        {
            y = 0f;
            float reach = profile.MaxFloorStep + profile.FloorSkin;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            int count = Physics2D.Raycast(foot + Vector2.up * reach, Vector2.down, filter, castResults, reach * 2f);
            if (count >= castResults.Length) return false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = castResults[i];
                if (!EnvironmentalSpawnResolver.IsSolidGround(hit.collider, transform)
                    || hit.distance <= 0f || hit.normal.y < profile.MinimumGroundNormalY
                    || hit.distance >= nearest) continue;
                nearest = hit.distance;
                y = hit.point.y;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
