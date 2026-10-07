using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>기존 몸 그림 복제·고리 시안. 원본 렌더러·루트·콜라이더·전투 판정을 바꾸지 않는다.</summary>
    public sealed class FormTransitionFx : MonoBehaviour
    {
        public struct BodySnapshot
        {
            public Sprite Sprite;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Vector3 Center;
            public float Height;
            public int Layer;
            public int Order;
            public bool FlipX;
            public bool FlipY;

            public static BodySnapshot Capture(SpriteRenderer source) => new BodySnapshot
            {
                Sprite = source.sprite, Position = source.transform.position, Rotation = source.transform.rotation,
                Scale = source.transform.lossyScale, Center = source.bounds.center, Height = source.bounds.size.y,
                Layer = source.sortingLayerID, Order = source.sortingOrder, FlipX = source.flipX, FlipY = source.flipY
            };
        }

        private SpriteRenderer oldGhost;
        private SpriteRenderer newGhost;
        private SpriteRenderer ring;
        private Transform target;
        private Vector3 targetStart;
        private Vector3 origin;
        private BodySnapshot incoming;
        private BodySnapshot outgoing;
        private Color tint;
        private float duration;
        private float elapsed;
        private bool isAcquisition;

        public static FormTransitionFx PlayAcquisition(BodySnapshot body, Transform target, Vector3? source,
            Color color, float seconds)
        {
            var fx = Create(body, color, seconds);
            fx.isAcquisition = true;
            fx.target = target;
            fx.targetStart = target.position;
            fx.origin = source ?? body.Center + Vector3.up * Mathf.Max(0.5f, body.Height);
            fx.newGhost = fx.CreateRenderer("AbsorbedShape", body.Sprite, body);
            fx.ring = fx.CreateRenderer("FormSettles", FormComboSprites.Ring, body);
            fx.Apply(0f);
            return fx;
        }

        public static FormTransitionFx PlaySwap(BodySnapshot previous, BodySnapshot next, Color color)
        {
            var fx = Create(next, color, 0.2f);
            fx.outgoing = previous;
            fx.oldGhost = fx.CreateRenderer("PreviousForm", previous.Sprite, previous);
            fx.newGhost = fx.CreateRenderer("NextForm", next.Sprite, next);
            fx.Apply(0f);
            return fx;
        }

        private static FormTransitionFx Create(BodySnapshot body, Color color, float seconds)
        {
            var fx = new GameObject("FormTransitionFx").AddComponent<FormTransitionFx>();
            fx.incoming = body;
            fx.tint = new Color(color.r, color.g, color.b, 1f);
            fx.duration = seconds;
            return fx;
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, BodySnapshot pose)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            var renderer = part.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sprite = sprite;
            renderer.sortingLayerID = pose.Layer;
            renderer.sortingOrder = pose.Order - 1;
            renderer.flipX = pose.FlipX;
            renderer.flipY = pose.FlipY;
            return renderer;
        }

        private void LateUpdate()
        {
            if (isAcquisition && target == null) { Clear(); return; }
            // 사용자 정지·모달·히트스톱 위에서 전투 효과 시간을 소비하지 않는다.
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Apply(t);
            if (t >= 1f) Clear();
        }

        private void Apply(float t)
        {
            if (isAcquisition)
            {
                Vector3 delta = target != null ? target.position - targetStart : Vector3.zero;
                float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.7f));
                Vector3 position = Vector3.Lerp(origin, incoming.Position + delta, approach);
                Pose(newGhost, incoming, position, (0.3f + 0.4f * Mathf.Sin(t * Mathf.PI)) * (1f - t));
                ring.transform.position = incoming.Center + delta;
                float size = Mathf.Max(0.5f, incoming.Height) * Mathf.Lerp(0.4f, 1.25f, t);
                ring.transform.localScale = new Vector3(size, size, 1f);
                SetColor(ring, 0.4f * Mathf.Sin(t * Mathf.PI));
            }
            else
            {
                Pose(oldGhost, outgoing, outgoing.Position + Vector3.left * (0.15f * t), 0.45f * (1f - t));
                Pose(newGhost, incoming, incoming.Position + Vector3.right * (0.1f * t), 0.3f * (1f - t));
            }
        }

        private void Pose(SpriteRenderer renderer, BodySnapshot pose, Vector3 position, float alpha)
        {
            renderer.transform.SetPositionAndRotation(position, pose.Rotation);
            renderer.transform.localScale = pose.Scale;
            SetColor(renderer, alpha);
        }

        private void SetColor(SpriteRenderer renderer, float alpha)
        {
            renderer.color = new Color(tint.r, tint.g, tint.b, alpha);
            renderer.enabled = renderer.sprite != null && alpha > 0f;
        }

        public void Clear()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (oldGhost != null) oldGhost.enabled = false;
            if (newGhost != null) newGhost.enabled = false;
            if (ring != null) ring.enabled = false;
        }
    }
}
