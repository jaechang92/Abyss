using System.Collections.Generic;
using Abyss.Runtime.Events;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// 첫 보스(심연의 수호자) 조우 반응 · 처치 잔향(E1). 순수 표현이다 — 피해·보상·진행을 소유하지 않는다.
    ///
    /// <list type="bullet">
    /// <item><b>기존 보스 스프라이트를 복제</b>한 잔상만 움직인다(<see cref="AfterimageEffect"/> 와 같은 방식). 원본 루트·콜라이더·위치는 건드리지 않는다.</item>
    /// <item>먼지·빛은 <see cref="BossAreaEffect"/> 처럼 런타임에 만든 단색 도형이다 — <b>임시 표현</b>이며 완성 아트가 아니다.</item>
    /// <item>정지(소개 모달)·처치 슬로 위에서 흐르므로 실시간으로 잰다. 보스가 0.3초 뒤 파괴돼도 참조하지 않는다(생성 시 복제).</item>
    /// <item>방 진입·런 종료·사망·포기에서 즉시 스스로 지운다. 씬 전환은 씬과 함께 파괴된다.</item>
    /// <item>화면 흔들림·전체 플래시는 추가하지 않는다(기존 처치 플래시·슬로를 중복 호출하지 않는다).</item>
    /// </list>
    /// </summary>
    public sealed class KeeperEncounterFx : MonoBehaviour
    {
        private const float REACTION_SECONDS = 0.8f;
        private const float REACTION_SWELL = 0.12f;
        private const float AFTERMATH_SECONDS = 2.4f;
        private const float AFTERMATH_RISE = 0.35f;
        private const int REACTION_DUST_COUNT = 10;
        private const int AFTERMATH_DUST_COUNT = 14;
        private const int GLOW_TEXTURE_SIZE = 64;

        private static readonly Color ReactionGhostColor = new Color(0.8f, 0.92f, 1f, 0.55f);
        private static readonly Color AftermathGhostColor = new Color(0.85f, 0.85f, 0.95f, 0.6f);
        private static readonly Color GlowColor = new Color(1f, 0.86f, 0.6f, 0.45f);
        private static readonly Color DustColor = new Color(0.62f, 0.58f, 0.55f, 0.8f);

        private static Sprite dustSprite;
        private static Sprite glowSprite;

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public Vector3 Velocity;
            public Vector3 BaseScale;
            public float ScaleGrowth;
            public float Gravity;
            public Color Color;
            public float FadeStart;
        }

        private readonly List<Piece> pieces = new();
        private float duration;
        private float elapsed;

        /// <summary>조우 반응 — 보스 그림 잔상이 한 번 부풀며 걷히고, 발밑 먼지가 양옆으로 인다. 약 0.8초.</summary>
        public static void PlayReaction(SpriteRenderer source)
        {
            if (!IsUsable(source)) return;

            var fx = Create("KeeperEncounterReaction", REACTION_SECONDS);
            fx.AddGhost(source, ReactionGhostColor, REACTION_SWELL, Vector3.zero, 0f);
            fx.AddDust(source, REACTION_DUST_COUNT, 1.6f, 0.9f, 0f);
        }

        /// <summary>처치 잔향 — 보스 형체가 옅게 떠올라 걷히고, 짧은 빛이 퍼지며 사그라들고, 먼지가 내려앉는다. 약 2.4초.</summary>
        public static void PlayAftermath(SpriteRenderer source)
        {
            if (!IsUsable(source)) return;

            var fx = Create("KeeperEncounterAftermath", AFTERMATH_SECONDS);
            fx.AddGlow(source);
            fx.AddGhost(source, AftermathGhostColor, 0.05f, Vector3.up * (AFTERMATH_RISE / AFTERMATH_SECONDS), 0.15f);
            fx.AddDust(source, AFTERMATH_DUST_COUNT, 0.9f, 0.5f, 0.6f);
        }

        private static bool IsUsable(SpriteRenderer source) => source != null && source.sprite != null;

        private static KeeperEncounterFx Create(string name, float seconds)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<KeeperEncounterFx>();
            fx.duration = Mathf.Max(0.05f, seconds);
            return fx;
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
            GameEvents.OnRunEnded += Clear;
            GameEvents.OnPlayerDead += Clear;
            GameEvents.OnRunAbandoned += Clear;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
            GameEvents.OnRunEnded -= Clear;
            GameEvents.OnPlayerDead -= Clear;
            GameEvents.OnRunAbandoned -= Clear;
        }

        private void HandleRoomEntered(RoomData room) => Clear();

        private void Clear()
        {
            if (this != null) Destroy(gameObject);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < pieces.Count; i++) Apply(pieces[i], t);

            if (t >= 1f) Destroy(gameObject);
        }

        private void Apply(Piece piece, float t)
        {
            if (piece.Renderer == null) return;

            float seconds = t * duration;
            var tr = piece.Renderer.transform;
            tr.position = piece.Origin + piece.Velocity * seconds + Vector3.down * (0.5f * piece.Gravity * seconds * seconds);
            tr.localScale = piece.BaseScale * (1f + piece.ScaleGrowth * Mathf.Sin(Mathf.Min(1f, t * 2f) * Mathf.PI * 0.5f));

            float fade = piece.FadeStart >= 1f ? 1f : Mathf.Clamp01((t - piece.FadeStart) / (1f - piece.FadeStart));
            var color = piece.Color;
            color.a = piece.Color.a * (1f - fade);
            piece.Renderer.color = color;
        }

        private SpriteRenderer AddRenderer(string name, Sprite sprite, SpriteRenderer source, int orderOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            sr.sprite = sprite;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder + orderOffset;
            return sr;
        }

        private void AddPiece(Piece piece)
        {
            Apply(piece, 0f);
            pieces.Add(piece);
            piece.Renderer.enabled = true;
        }

        private void AddGhost(SpriteRenderer source, Color color, float swell, Vector3 velocity, float fadeStart)
        {
            var sr = AddRenderer("Ghost", source.sprite, source, -1);
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;

            AddPiece(new Piece
            {
                Renderer = sr,
                Origin = source.transform.position,
                Velocity = velocity,
                BaseScale = source.transform.lossyScale,
                ScaleGrowth = swell,
                Color = color,
                FadeStart = fadeStart
            });
        }

        private void AddGlow(SpriteRenderer source)
        {
            var bounds = source.bounds;
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y) * 1.2f;
            var sr = AddRenderer("Glow", GetGlowSprite(), source, -2);

            AddPiece(new Piece
            {
                Renderer = sr,
                Origin = bounds.center,
                BaseScale = new Vector3(diameter, diameter, 1f),
                ScaleGrowth = 0.6f,
                Color = GlowColor,
                FadeStart = 0.1f
            });
        }

        private void AddDust(SpriteRenderer source, int count, float speed, float rise, float fadeStart)
        {
            var bounds = source.bounds;
            var feet = new Vector3(bounds.center.x, bounds.min.y, source.transform.position.z);
            float halfWidth = bounds.extents.x;
            var sprite = GetDustSprite();

            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float spread = (i / 2 + 1f) / (count / 2 + 1f);
                var sr = AddRenderer("Dust", sprite, source, 1);
                float size = Mathf.Lerp(0.12f, 0.24f, spread);

                AddPiece(new Piece
                {
                    Renderer = sr,
                    Origin = feet + Vector3.right * (side * halfWidth * 0.4f * spread),
                    Velocity = new Vector3(side * speed * (0.5f + spread), rise * (1.2f - spread), 0f),
                    BaseScale = new Vector3(size, size, 1f),
                    ScaleGrowth = 0.5f,
                    Gravity = rise * 0.8f,
                    Color = DustColor,
                    FadeStart = fadeStart
                });
            }
        }

        /// <summary>1유닛 흰 사각형(PPU = 크기). 색은 SpriteRenderer.color 로 입힌다.</summary>
        private static Sprite GetDustSprite()
        {
            if (dustSprite != null) return dustSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            dustSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return dustSprite;
        }

        /// <summary>지름 1유닛의 가장자리가 흐린 흰 원. 색은 SpriteRenderer.color 로 입힌다.</summary>
        private static Sprite GetGlowSprite()
        {
            if (glowSprite != null) return glowSprite;

            int size = GLOW_TEXTURE_SIZE;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float center = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return glowSprite;
        }
    }
}
