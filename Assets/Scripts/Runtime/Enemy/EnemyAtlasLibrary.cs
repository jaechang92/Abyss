using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 atlas 행 이름 SoT — 제작 패키지 manifest.frame_layout.rows 의 키와 같다(A1 · 2026-10-09).
    /// 기본 4행은 모든 적, 나머지는 보스 패턴 전용 행이다.
    /// </summary>
    public static class EnemyAtlasRowIds
    {
        public const string Move = "move";
        public const string Attack = "attack";
        public const string Hit = "hit";
        public const string Dead = "dead";

        /// <summary>심연의 수호자 탄막 예고 → 발사.</summary>
        public const string Volley = "volley";

        /// <summary>화염 뱀 화염브레스.</summary>
        public const string Breath = "breath";

        /// <summary>화염 뱀 꼬리치기 예고 → 강타.</summary>
        public const string Tail = "tail";

        /// <summary>왕좌의 영혼 순간이동 슬래시 예고 → 착지 강타.</summary>
        public const string Blink = "blink";
    }

    /// <summary>한 행의 재생 재료 — 스프라이트(프레임 순) · 미리보기 fps · 반복 여부.</summary>
    public sealed class EnemyAtlasClip
    {
        public Sprite[] Sprites { get; }
        public float FramesPerSecond { get; }
        public bool IsLoop { get; }
        public int FrameCount => Sprites.Length;

        public EnemyAtlasClip(Sprite[] sprites, float framesPerSecond, bool isLoop)
        {
            Sprites = sprites;
            FramesPerSecond = framesPerSecond > 0f ? framesPerSecond : 8f;
            IsLoop = isLoop;
        }
    }

    /// <summary>한 적의 행 묶음. 기본 이동 행이 없으면 만들어지지 않는다(→ 기존 그림 폴백).</summary>
    public sealed class EnemyAtlasSet
    {
        private readonly Dictionary<string, EnemyAtlasClip> clips;

        public EnemyAtlasSet(Dictionary<string, EnemyAtlasClip> clips)
        {
            this.clips = clips;
        }

        public EnemyAtlasClip Get(string rowId)
        {
            return rowId != null && clips.TryGetValue(rowId, out var clip) ? clip : null;
        }

        public int RowCount => clips.Count;
    }

    /// <summary>
    /// <b>적 atlas 런타임 색인</b>(A1 · 2026-10-09). <c>Resources/ArtIntegration/Enemies</c> 의 투명 atlas 18장과
    /// 색인 JSON(<c>Tools/ArtIntegration/build_enemy_atlas_runtime.py</c> 가 manifest 에서 생성)을 읽어
    /// 프레임 스프라이트를 <b>한 번만</b> 만들고 공유한다. 에디터 메뉴 실행 없이 기존 프리팹·씬에서 동작한다.
    ///
    /// <list type="bullet">
    /// <item><b>사각형</b> — manifest.frame_layout 은 위→아래 좌표다. 텍스처 좌표는 아래→위라 <c>sheetHeight − y − h</c>.</item>
    /// <item><b>발 피벗</b> — 프레임마다 불투명 최하단(<c>footY</c>)이 렌더러 로컬의 콜라이더 바닥에 오도록 피벗을 잡는다.
    /// 추출기가 프레임마다 가운데 배치해 칸 안의 발 높이가 프레임마다 다르기 때문이다. x 피벗은 칸 가운데(flipX 축).</item>
    /// <item><b>크기</b> — 색인의 <c>pixelsPerUnit</c>. 기존 그림의 보이는 높이를 보존하는 값이다(등급별 크기 규약 유지).</item>
    /// </list>
    ///
    /// 📌 런타임에 에셋을 고치지 않는다. 텍스처·색인이 없으면 null 을 돌려주고 호출한 쪽은 기존 렌더러를 그대로 둔다.
    /// </summary>
    public static class EnemyAtlasLibrary
    {
        private const string INDEX_PATH = "ArtIntegration/Enemies/enemy_atlas_index";

        private static Dictionary<string, AtlasEnemyRecord> records;
        private static readonly Dictionary<string, EnemyAtlasSet> sets = new();
        private static readonly Dictionary<string, Texture2D> textures = new();

        /// <summary>Sprite.Create 로 만든 프레임 전부 — 캐시를 비울 때 파괴할 대상(원본 텍스처는 Resources 소유라 넣지 않는다).</summary>
        private static readonly List<Sprite> createdSprites = new();

        /// <summary>
        /// 적 하나의 행 묶음. <paramref name="localFootY"/> 는 렌더러 로컬 좌표에서 발이 닿을 높이(유닛)다 —
        /// 그림 자식(Visual)이 이미 발밑에 있으면 0, 루트 렌더러(옛 정지 그림 보스)면 콜라이더 바닥(음수).
        /// 없거나 이동 행이 없으면 null.
        /// </summary>
        public static EnemyAtlasSet TryGet(string enemyId, float localFootY)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;

            string key = enemyId + "|" + Mathf.RoundToInt(localFootY * 1000f);
            if (sets.TryGetValue(key, out var cached)) return cached;

            EnemyAtlasSet set = null;
            var all = LoadRecords();
            if (all != null && all.TryGetValue(enemyId, out var record)) set = Build(record, localFootY);

            sets[key] = set;
            return set;
        }

        private static Dictionary<string, AtlasEnemyRecord> LoadRecords()
        {
            if (records != null) return records;
            records = new Dictionary<string, AtlasEnemyRecord>();

            var asset = Resources.Load<TextAsset>(INDEX_PATH);
            if (asset == null)
            {
                Debug.LogWarning($"[EnemyAtlasLibrary] 색인 없음 — Resources/{INDEX_PATH}. 기존 적 그림을 그대로 쓴다.");
                return records;
            }

            AtlasIndexRecord index;
            try
            {
                index = JsonUtility.FromJson<AtlasIndexRecord>(asset.text);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"[EnemyAtlasLibrary] 색인 해석 실패 — {exception.Message}. 기존 적 그림을 그대로 쓴다.");
                return records;
            }

            if (index?.Enemies == null) return records;
            foreach (var enemy in index.Enemies)
            {
                if (enemy != null && !string.IsNullOrEmpty(enemy.EnemyId)) records[enemy.EnemyId] = enemy;
            }
            return records;
        }

        private static EnemyAtlasSet Build(AtlasEnemyRecord record, float localFootY)
        {
            if (record.Rows == null || record.PixelsPerUnit <= 0f) return null;

            var clips = new Dictionary<string, EnemyAtlasClip>();
            foreach (var row in record.Rows)
            {
                var clip = BuildClip(record, row, localFootY);
                if (clip != null) clips[row.Id] = clip;
            }

            if (!clips.ContainsKey(EnemyAtlasRowIds.Move))
            {
                Debug.LogWarning($"[EnemyAtlasLibrary] {record.EnemyId} 이동 행 없음 — 기존 그림을 그대로 쓴다.");
                return null;
            }
            return new EnemyAtlasSet(clips);
        }

        private static EnemyAtlasClip BuildClip(AtlasEnemyRecord record, AtlasRowRecord row, float localFootY)
        {
            if (row == null || string.IsNullOrEmpty(row.Id) || row.Frames == null || row.Frames.Length == 0) return null;

            var texture = LoadTexture(row.Atlas);
            if (texture == null || texture.height != row.SheetHeight)
            {
                Debug.LogWarning($"[EnemyAtlasLibrary] {record.EnemyId}/{row.Id} atlas 없음 또는 크기 불일치 — {row.Atlas}");
                return null;
            }

            float ppu = record.PixelsPerUnit;
            var sprites = new Sprite[row.Frames.Length];
            for (int i = 0; i < row.Frames.Length; i++)
            {
                var frame = row.Frames[i];
                var rect = new Rect(frame.X, row.SheetHeight - frame.Y - frame.H, frame.W, frame.H);

                // 발(불투명 최하단)의 칸 바닥 기준 높이 − 렌더러 로컬 발 높이만큼 피벗을 올린다.
                float footFromBottom = frame.H - frame.FootY;
                float pivotPixelsY = footFromBottom - localFootY * ppu;
                var pivot = new Vector2(0.5f, pivotPixelsY / frame.H);

                var sprite = Sprite.Create(texture, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                sprite.name = $"{record.EnemyId}_{row.Id}_{i}";
                sprites[i] = sprite;
                createdSprites.Add(sprite);
            }
            return new EnemyAtlasClip(sprites, row.Fps, row.Loop);
        }

        private static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (textures.TryGetValue(path, out var cached) && cached != null) return cached;

            var texture = Resources.Load<Texture2D>(path);
            if (texture != null) textures[path] = texture;
            return texture;
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
            sets.Clear();
            textures.Clear();
        }

        // ───────────────────────────── 색인 JSON 형식 (JsonUtility — 키 이름 = 필드 이름)

        [Serializable]
        private sealed class AtlasIndexRecord
        {
            [SerializeField] private int version;
            [SerializeField] private AtlasEnemyRecord[] enemies;

            public int Version => version;
            public AtlasEnemyRecord[] Enemies => enemies;
        }

        [Serializable]
        private sealed class AtlasEnemyRecord
        {
            [SerializeField] private string enemyId;
            [SerializeField] private float pixelsPerUnit;
            [SerializeField] private AtlasRowRecord[] rows;

            public string EnemyId => enemyId;
            public float PixelsPerUnit => pixelsPerUnit;
            public AtlasRowRecord[] Rows => rows;
        }

        [Serializable]
        private sealed class AtlasRowRecord
        {
            [SerializeField] private string id;
            [SerializeField] private string atlas;
            [SerializeField] private int sheetHeight;
            [SerializeField] private float fps;
            [SerializeField] private bool loop;
            [SerializeField] private AtlasFrameRecord[] frames;

            public string Id => id;
            public string Atlas => atlas;
            public int SheetHeight => sheetHeight;
            public float Fps => fps;
            public bool Loop => loop;
            public AtlasFrameRecord[] Frames => frames;
        }

        [Serializable]
        private sealed class AtlasFrameRecord
        {
            [SerializeField] private int x;
            [SerializeField] private int y;
            [SerializeField] private int w;
            [SerializeField] private int h;
            [SerializeField] private int footY;

            public int X => x;
            public int Y => y;
            public int W => w;
            public int H => h;
            public int FootY => footY;
        }
    }
}
