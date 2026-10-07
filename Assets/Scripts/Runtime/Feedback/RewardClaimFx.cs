using Abyss.Runtime.Events;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>획득은 제단 그림이 떠오르며 고리가 펴지고, 거절은 회색 그림이 가라앉는다. 절차 도형 시안.</summary>
    public sealed class RewardClaimFx : MonoBehaviour
    {
        private SpriteRenderer ghost;
        private SpriteRenderer ring;
        private Vector3 origin;
        private Vector3 sourceScale;
        private float diameter;
        private float elapsed;
        private bool acquired;

        public static void Play(SpriteRenderer source, bool acquired)
        {
            if (source == null || source.sprite == null) return;
            var fx = new GameObject("RewardClaimFx").AddComponent<RewardClaimFx>();
            fx.acquired = acquired;
            fx.origin = source.transform.position;
            fx.sourceScale = source.transform.lossyScale;
            fx.diameter = Mathf.Max(0.5f, source.bounds.size.x);
            fx.ghost = fx.CreateRenderer("ResolvedAltar", source.sprite, source);
            fx.ghost.flipX = source.flipX;
            fx.ghost.flipY = source.flipY;
            fx.ghost.transform.rotation = source.transform.rotation;
            fx.ring = fx.CreateRenderer("ResolutionRing", FormComboSprites.Ring, source);
            fx.Apply(0f);
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, SpriteRenderer source)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sprite = sprite;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + 1;
            return renderer;
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoom;
            GameEvents.OnPlayerDead += Clear;
            GameEvents.OnRunEnded += Clear;
            GameEvents.OnRunAbandoned += Clear;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoom;
            GameEvents.OnPlayerDead -= Clear;
            GameEvents.OnRunEnded -= Clear;
            GameEvents.OnRunAbandoned -= Clear;
            if (ghost != null) ghost.enabled = false;
            if (ring != null) ring.enabled = false;
        }

        private void HandleRoom(RoomData room) => Clear();
        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (acquired ? 0.4f : 0.3f));
            Apply(t);
            if (t >= 1f) Clear();
        }

        private void Apply(float t)
        {
            ghost.transform.position = origin + Vector3.up * ((acquired ? 0.2f : -0.1f) * t);
            ghost.transform.localScale = sourceScale * (acquired ? 1f : 1f - 0.15f * t);
            ghost.color = acquired ? new Color(1f, 0.9f, 0.65f, 0.65f * (1f - t))
                : new Color(0.55f, 0.55f, 0.6f, 0.4f * (1f - t));
            ghost.enabled = true;
            ring.transform.position = origin;
            ring.transform.localScale = Vector3.one * diameter * Mathf.Lerp(0.5f, 1.35f, t);
            ring.color = new Color(1f, 0.85f, 0.45f, 0.4f * Mathf.Sin(t * Mathf.PI));
            ring.enabled = acquired;
        }

        private void Clear()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
