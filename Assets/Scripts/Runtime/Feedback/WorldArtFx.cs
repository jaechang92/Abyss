using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Events;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss.Runtime.Feedback
{
    /// <summary>Bounded, scene-owned, scaled-time visual effects. No damage or RNG.</summary>
    public sealed class WorldArtFx : MonoBehaviour
    {
        private const int MAX_ACTIVE = 48;
        private static int activeCount;
        private SpriteRenderer visual;
        private float elapsed;
        private float duration;
        private Vector3 initialScale;
        private bool counted;

        public static bool Play(string key, Vector3 position, float height = 0.9f, float seconds = 0.22f,
            int facing = 1, Transform owner = null, float maxWidth = 0f)
        {
            if (activeCount >= MAX_ACTIVE || seconds <= 0f) return false;
            var sprite = WorldArtLibrary.Get("fx/" + key);
            if (sprite == null) return false;
            var go = new GameObject("Fx_" + key);
            if (owner != null) SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            go.transform.position = position;
            var effect = go.AddComponent<WorldArtFx>();
            effect.visual = go.AddComponent<SpriteRenderer>();
            effect.visual.sprite = sprite;
            effect.visual.sortingOrder = 20;
            effect.visual.flipX = facing < 0;
            effect.duration = seconds;
            effect.initialScale = Vector3.one * (height / sprite.bounds.size.y);
            if (maxWidth > 0f && sprite.bounds.size.x * effect.initialScale.x > maxWidth)
                effect.initialScale = Vector3.one * (maxWidth / sprite.bounds.size.x);
            go.transform.localScale = effect.initialScale;
            effect.counted = true;
            activeCount++;
            return true;
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += EndRoom;
            GameEvents.OnRunEnded += End;
            GameEvents.OnRunAbandoned += End;
        }

        private void EndRoom(RoomData room) => End();
        private void End() { gameObject.SetActive(false); Destroy(gameObject); }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            visual.color = new Color(1f, 1f, 1f, 1f - t * t);
            transform.localScale = initialScale * Mathf.Lerp(0.85f, 1f, t);
            if (elapsed >= duration) Destroy(gameObject);
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= EndRoom;
            GameEvents.OnRunEnded -= End;
            GameEvents.OnRunAbandoned -= End;
            if (counted) { activeCount = Mathf.Max(0, activeCount - 1); counted = false; }
            if (visual != null) visual.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCount() => activeCount = 0;
    }
}
