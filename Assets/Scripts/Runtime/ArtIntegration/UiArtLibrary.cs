using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.ArtIntegration
{
    /// <summary>
    /// 그림 한 장의 배치 정보. 크기는 원본 픽셀 기준 보이는 사각형(투명 여백 제외)이고,
    /// 개구부·왼쪽 칸은 보이는 사각형 안의 0..1 비율(아래→위)이다.
    /// </summary>
    public readonly struct UiArtLayout
    {
        public readonly Vector2 VisibleSize;
        public readonly Rect Inner;
        public readonly Rect Slot;

        public UiArtLayout(Vector2 visibleSize, Rect inner, Rect slot)
        {
            VisibleSize = visibleSize;
            Inner = inner;
            Slot = slot;
        }

        public bool HasInner => Inner.width > 0f && Inner.height > 0f;
        public bool HasSlot => Slot.width > 0f && Slot.height > 0f;

        /// <summary>보이는 영역의 가로/세로 비.</summary>
        public float Aspect => VisibleSize.y > 0f ? VisibleSize.x / VisibleSize.y : 1f;
    }

    /// <summary>
    /// <b>채택 UI 그림 런타임 색인</b>(A2 · 2026-10-09). <c>Resources/ArtIntegration/UI</c> 의 PNG 44장과
    /// 색인 JSON(<c>Tools/ArtIntegration/build_ui_art_runtime.py</c> 가 원본에서 생성)을 읽어
    /// 키 하나당 스프라이트를 <b>한 번만</b> 만들고 공유한다. 에디터 메뉴 없이 기존 화면의 빌드·갱신 경로가 부른다.
    ///
    /// <list type="bullet">
    /// <item><b>투명 여백</b> — 원본 캔버스 전체가 아니라 색인의 보이는 사각형만 Sprite rect 로 자른다(PNG 는 그대로).</item>
    /// <item><b>임포트 축소</b> — maxTextureSize 로 줄어든 텍스처도 실제 크기/원본 크기 비율로 같은 자리를 자른다.</item>
    /// <item><b>읽기 불가 텍스처</b> — 픽셀을 읽지 않는다. Sprite.Create 는 참조만 한다.</item>
    /// </list>
    ///
    /// 📌 그림·색인이 없으면 null 을 돌려주고 호출한 쪽은 기존 표시를 그대로 둔다.
    /// </summary>
    public static class UiArtLibrary
    {
        private const string INDEX_PATH = "ArtIntegration/UI/ui_art_index";
        private const float SPRITE_PPU = 100f;

        private static Dictionary<string, UiArtRecord> records;
        private static readonly Dictionary<string, Sprite> sprites = new();
        private static readonly Dictionary<string, Sprite> slicedSprites = new();
        private static readonly HashSet<string> warnedKeys = new();

        /// <summary>Sprite.Create 로 만든 것 전부 — 캐시를 비울 때 파괴할 대상(텍스처는 Resources 소유라 넣지 않는다).</summary>
        private static readonly List<Sprite> createdSprites = new();

        /// <summary>색인에 있는 키인지(그림 파일 로드는 하지 않는다).</summary>
        public static bool Has(string key)
        {
            var all = LoadRecords();
            return key != null && all.ContainsKey(key);
        }

        /// <summary>보이는 사각형으로 자른 스프라이트(중심 피벗 · PPU 100). 없으면 null.</summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Create(key, sliced: false);
            if (sprite != null) sprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 개구부 바깥을 9-slice 테두리로 둔 스프라이트. 개구부가 없는 그림이면 null —
        /// 테두리 띠가 고른 그림(입력 키 틀)만 쓴다.
        /// </summary>
        public static Sprite GetSliced(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (slicedSprites.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Create(key, sliced: true);
            if (sprite != null) slicedSprites[key] = sprite;
            return sprite;
        }

        /// <summary>배치 정보. 색인에 없으면 false.</summary>
        public static bool TryGetLayout(string key, out UiArtLayout layout)
        {
            layout = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!LoadRecords().TryGetValue(key, out var record)) return false;

            layout = new UiArtLayout(
                new Vector2(record.W, record.H),
                record.HasInner ? new Rect(record.InnerX, record.InnerY, record.InnerW, record.InnerH) : Rect.zero,
                record.HasSlot ? new Rect(record.SlotX, record.SlotY, record.SlotW, record.SlotH) : Rect.zero);
            return true;
        }

        private static Sprite Create(string key, bool sliced)
        {
            if (!LoadRecords().TryGetValue(key, out var record))
            {
                WarnOnce(key, $"[UiArtLibrary] 색인에 없는 키 '{key}' — 기존 표시를 유지한다.");
                return null;
            }

            var texture = Resources.Load<Texture2D>(record.Resource);
            if (texture == null || record.SrcW <= 0 || record.SrcH <= 0)
            {
                WarnOnce(key, $"[UiArtLibrary] 그림 없음 — Resources/{record.Resource}. 기존 표시를 유지한다.");
                return null;
            }

            // 원본 위→아래 픽셀 사각형 → 실제 텍스처 아래→위 픽셀 사각형(임포트 축소 비율 반영).
            float scaleX = texture.width / (float)record.SrcW;
            float scaleY = texture.height / (float)record.SrcH;
            float xMin = Mathf.Floor(record.X * scaleX);
            float yMin = Mathf.Floor((record.SrcH - record.Y - record.H) * scaleY);
            float xMax = Mathf.Ceil((record.X + record.W) * scaleX);
            float yMax = Mathf.Ceil((record.SrcH - record.Y) * scaleY);
            xMin = Mathf.Clamp(xMin, 0f, texture.width - 1f);
            yMin = Mathf.Clamp(yMin, 0f, texture.height - 1f);
            xMax = Mathf.Clamp(xMax, xMin + 1f, texture.width);
            yMax = Mathf.Clamp(yMax, yMin + 1f, texture.height);
            var rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

            var border = Vector4.zero;
            if (sliced)
            {
                if (!record.HasInner) return null;
                // 9-slice 테두리 = 개구부 바깥 띠(왼·아래·오른·위, 텍스처 픽셀).
                border = new Vector4(
                    Mathf.Floor(record.InnerX * rect.width),
                    Mathf.Floor(record.InnerY * rect.height),
                    Mathf.Floor((1f - record.InnerX - record.InnerW) * rect.width),
                    Mathf.Floor((1f - record.InnerY - record.InnerH) * rect.height));
            }

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), SPRITE_PPU, 0, SpriteMeshType.FullRect, border);
            sprite.name = key + (sliced ? " (UiArt sliced)" : " (UiArt)");
            sprite.hideFlags = HideFlags.DontSave;
            createdSprites.Add(sprite);
            return sprite;
        }

        private static Dictionary<string, UiArtRecord> LoadRecords()
        {
            if (records != null) return records;
            records = new Dictionary<string, UiArtRecord>();

            var asset = Resources.Load<TextAsset>(INDEX_PATH);
            if (asset == null)
            {
                Debug.LogWarning($"[UiArtLibrary] 색인 없음 — Resources/{INDEX_PATH}. 기존 UI 표시를 그대로 쓴다.");
                return records;
            }

            UiArtIndexRecord index;
            try
            {
                index = JsonUtility.FromJson<UiArtIndexRecord>(asset.text);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[UiArtLibrary] 색인 해석 실패 — {exception.Message}. 기존 UI 표시를 그대로 쓴다.");
                return records;
            }

            if (index?.Entries == null) return records;
            foreach (var entry in index.Entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.Key)) records[entry.Key] = entry;
            }
            return records;
        }

        private static void WarnOnce(string key, string message)
        {
            if (warnedKeys.Add(key ?? string.Empty)) Debug.LogWarning(message);
        }

        // 도메인 리로드 비활성화 대비 — 이전 플레이의 색인을 버리고 직접 만든 스프라이트는 파괴한다.
        // 🔴 텍스처는 Resources 원본이라 파괴하지 않고 참조만 놓는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (var sprite in createdSprites)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            createdSprites.Clear();

            records = null;
            sprites.Clear();
            slicedSprites.Clear();
            warnedKeys.Clear();
        }

        // ───────────────────────────── 색인 JSON 형식 (JsonUtility — 키 이름 = 필드 이름)

        [Serializable]
        private sealed class UiArtIndexRecord
        {
            [SerializeField] private int version;
            [SerializeField] private UiArtRecord[] entries;

            public int Version => version;
            public UiArtRecord[] Entries => entries;
        }

        [Serializable]
        private sealed class UiArtRecord
        {
            [SerializeField] private string key;
            [SerializeField] private string resource;
            [SerializeField] private int srcW;
            [SerializeField] private int srcH;
            [SerializeField] private int x;
            [SerializeField] private int y;
            [SerializeField] private int w;
            [SerializeField] private int h;
            [SerializeField] private bool hasInner;
            [SerializeField] private float innerX;
            [SerializeField] private float innerY;
            [SerializeField] private float innerW;
            [SerializeField] private float innerH;
            [SerializeField] private bool hasSlot;
            [SerializeField] private float slotX;
            [SerializeField] private float slotY;
            [SerializeField] private float slotW;
            [SerializeField] private float slotH;

            public string Key => key;
            public string Resource => resource;
            public int SrcW => srcW;
            public int SrcH => srcH;
            public int X => x;
            public int Y => y;
            public int W => w;
            public int H => h;
            public bool HasInner => hasInner;
            public float InnerX => innerX;
            public float InnerY => innerY;
            public float InnerW => innerW;
            public float InnerH => innerH;
            public bool HasSlot => hasSlot;
            public float SlotX => slotX;
            public float SlotY => slotY;
            public float SlotW => slotW;
            public float SlotH => slotH;
        }
    }
}
