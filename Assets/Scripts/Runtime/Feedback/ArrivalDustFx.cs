using Abyss.Runtime.Audio;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>착지·귀환의 발밑 먼지. 기존 도형과 짧은 절차 생성 접촉음을 사용한 시안이다.</summary>
    public sealed class ArrivalDustFx : MonoBehaviour
    {
        private static AudioClip contactClip;
        private SpriteRenderer[] dots;
        private Vector3 origin;
        private float duration;
        private float elapsed;
        private int transitionVersion;

        public static void Play(Vector3 feet, float seconds, int version, bool withSound)
        {
            var go = new GameObject("ArrivalDust");
            var fx = go.AddComponent<ArrivalDustFx>();
            fx.origin = feet;
            fx.duration = seconds;
            fx.transitionVersion = version;
            fx.dots = new SpriteRenderer[10];
            for (int i = 0; i < fx.dots.Length; i++)
            {
                var part = new GameObject("Dust");
                part.transform.SetParent(go.transform, false);
                var renderer = part.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                renderer.sprite = FormComboSprites.White;
                renderer.sortingOrder = 3;
                fx.dots[i] = renderer;
            }
            fx.Apply(0f);
            if (withSound && AudioManager.HasInstance) AudioManager.Instance.PlaySfx(ContactClip(), 0.4f);
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
            if (dots != null) foreach (var dot in dots) if (dot != null) dot.enabled = false;
        }

        private void HandleRoom(RoomData room) => Clear();
        private void LateUpdate()
        {
            if (JourneyPresentation.CurrentTransitionVersion != transitionVersion) { Clear(); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Apply(t);
            if (t >= 1f) Clear();
        }

        private void Apply(float t)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float spread = 0.3f + (i / 2) * 0.12f;
                var dot = dots[i];
                dot.transform.position = origin + new Vector3(side * spread * t,
                    0.16f * Mathf.Sin(t * Mathf.PI) * (1.3f - spread), 0f);
                dot.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.14f, t);
                dot.color = new Color(0.65f, 0.62f, 0.6f, 0.65f * (1f - t));
                dot.enabled = true;
            }
        }

        private static AudioClip ContactClip()
        {
            if (contactClip != null) return contactClip;
            const int RATE = 22050;
            var samples = new float[RATE / 6];
            var random = new System.Random(73);
            float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / RATE;
                low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, 0.15f);
                float envelope = Mathf.Min(1f, time / 0.008f) * Mathf.Exp(-time * 28f) * (1f - (float)i / samples.Length);
                samples[i] = (low * 0.35f + Mathf.Sin(time * 70f * Mathf.PI * 2f) * 0.2f) * envelope;
            }
            contactClip = AudioClip.Create("ArrivalContactPrototype", samples.Length, 1, RATE, false);
            contactClip.SetData(samples, 0);
            return contactClip;
        }

        private void Clear()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
