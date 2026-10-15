using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Abyss.Runtime.UI;

namespace Abyss.Runtime.ArtIntegration
{
    /// <summary>Immutable world art with offline alpha bounds. No runtime texture reads.</summary>
    public static class WorldArtLibrary
    {
        private static Dictionary<string, Entry> entries;
        private static readonly Dictionary<string, Sprite> sprites = new();

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (sprites.TryGetValue(key, out var cached) && cached != null) return cached;
            if (entries == null)
            {
                entries = new Dictionary<string, Entry>();
                var json = Resources.Load<TextAsset>("ArtIntegration/World/world_art_index");
                if (json == null) return null;
                var index = JsonUtility.FromJson<Index>(json.text);
                if (index?.Entries != null)
                    foreach (var item in index.Entries) entries[item.Key] = item;
            }
            if (!entries.TryGetValue(key, out var record)) return null;
            var texture = Resources.Load<Texture2D>(record.Resource);
            if (texture == null || record.SrcW <= 0 || record.SrcH <= 0) return null;
            float sx = (float)texture.width / record.SrcW;
            float sy = (float)texture.height / record.SrcH;
            var rect = new Rect(record.X * sx, (record.SrcH - record.Y - record.H) * sy, record.W * sx, record.H * sy);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            sprite.hideFlags = HideFlags.DontSave;
            sprites[key] = sprite;
            return sprite;
        }

        public static string EventKey(string eventId, bool spent)
        {
            switch (eventId)
            {
                case "broken_altar": case "sealed_door": case "forgotten_cache":
                case "hollow_crown": case "oath_stone": case "abyssal_spring":
                    return "event/" + eventId + (spent ? "/spent" : "/ready");
                default: return null;
            }
        }

        /// <summary>Bottom-aligned world decoration. Creates no collider or interaction.</summary>
        public static SpriteRenderer Place(Transform parent, string key, Vector3 feet, float height, int order)
        {
            var sprite = Get(key);
            if (sprite == null || height <= 0f) return null;
            var go = new GameObject("Art_" + key.Replace('/', '_'));
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = feet + Vector3.up * height * 0.5f;
            return renderer;
        }

        /// <summary>Stable child reused by modal rebuilds. Never intercepts navigation/input.</summary>
        public static Image ApplyUi(RectTransform parent, string name, string key, Rect box)
        {
            if (parent == null) return null;
            var sprite = Get(key);
            if (sprite == null) { UiArtDecor.Hide(parent, name); return null; }
            var image = UiArtDecor.EnsureImage(parent, name);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.enabled = true;
            UiArtDecor.SetCentered(image.rectTransform, box.center, box.size);
            return image;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (var sprite in sprites.Values)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            sprites.Clear();
            entries = null;
        }

        // JsonUtility field names match the generated index.
        [Serializable] private sealed class Index
        {
            [SerializeField] private Entry[] entries;
            public Entry[] Entries => entries;
        }
        [Serializable] private sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private string resource;
            [SerializeField] private int srcW, srcH, x, y, w, h;
            public string Key => key;
            public string Resource => resource;
            public int SrcW => srcW;
            public int SrcH => srcH;
            public int X => x;
            public int Y => y;
            public int W => w;
            public int H => h;
        }
    }
}
