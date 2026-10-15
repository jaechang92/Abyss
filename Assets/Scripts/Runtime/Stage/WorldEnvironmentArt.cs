using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// Room-owned visual skins only. Colliders, camera bounds and room progression stay untouched.
    /// C3: 배경 층은 카메라(PlayerCameraFollow, 10)가 최종 위치를 정한 뒤에 따라간다(실행 순서 20).
    /// 층마다 현재 화면 가로(+시차 여유)를 덮을 만큼 조각을 모자랄 때만 덧붙이고, 첫 층(최후방, 불투명 원본)은
    /// 높이를 화면 세로 이상으로 잡고 위아래 가장자리가 화면 안에 들어오면 그 프레임만 밀어 넣는다.
    /// ortho·화면 비율이 바뀌면 배경 층만 다시 만든다(지면·발판 스킨과 숨긴 렌더러는 그대로).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class WorldEnvironmentArt : MonoBehaviour
    {
        // 최후방 층 위아래 여유(유닛)와 층당 조각 상한(먼 순간이동에서도 생성이 끝없이 늘지 않게).
        private const float COVERAGE_MARGIN = 0.5f;
        private const int MAX_TILES_PER_LAYER = 48;

        private StageDirector director;
        private GameObject artRoot;
        private UnityEngine.Camera view;
        private bool pending;
        private readonly List<SpriteRenderer> hiddenRenderers = new();
        private readonly List<Layer> layers = new();

        // 배경 층을 다시 만들 때 쓰는 현재 방 값.
        private string layerStage;
        private bool layerBossRoom;
        private float layerFloorY;
        private float layerWidth;
        private float lastViewOrtho;
        private float lastViewAspect;

        private sealed class Layer
        {
            public Transform Root;
            public string Key;
            public float Rate;
            public float Bottom;
            public float Height;
            public int Order;
            public float TileWidth;
            public bool IsRearmost;
            public int MinIndex;
            public int MaxIndex;
        }

        public static void Attach(StageDirector owner)
        {
            if (!owner.TryGetComponent<WorldEnvironmentArt>(out _)) owner.gameObject.AddComponent<WorldEnvironmentArt>();
        }

        private void Awake() => director = GetComponent<StageDirector>();
        private void OnEnable()
        {
            GameEvents.OnRoomEntered += QueueRoom;
            // 런 종료(사망·결과 화면) 동안에는 방이 그대로 보여야 하므로 OnRunEnded 로는 지우지 않는다.
            // 정리는 방 재구성(Rebuild), 런 포기, 비활성화·씬 해제(OnDisable)에서만 한다.
            GameEvents.OnRunAbandoned += Clear;
            pending = true;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= QueueRoom;
            GameEvents.OnRunAbandoned -= Clear;
            Clear();
        }

        private void QueueRoom(RoomData room) => pending = true;

        private void LateUpdate()
        {
            // All OnRoomEntered subscribers (layout activation, Stage1 skins) finish before this pass.
            if (pending) { pending = false; Rebuild(); }
            if (view == null || artRoot == null) return;
            if (layerStage != null && (view.orthographicSize != lastViewOrtho || view.aspect != lastViewAspect)) BuildLayers();

            Vector3 cameraPosition = view.transform.position;
            float halfHeight = view.orthographicSize;
            float halfWidth = halfHeight * view.aspect;
            foreach (var layer in layers)
            {
                if (layer.Root == null) continue;
                float offsetX = cameraPosition.x * layer.Rate;
                float offsetY = 0f;
                if (layer.IsRearmost)
                {
                    // 최후방 층의 위·아래 가장자리가 화면 안으로 들어오면 그만큼만 민다. 기준(Bottom)은 그대로라 쌓이지 않는다.
                    float bottom = layer.Bottom;
                    float top = layer.Bottom + layer.Height;
                    if (bottom > cameraPosition.y - halfHeight) offsetY = cameraPosition.y - halfHeight - bottom;
                    else if (top < cameraPosition.y + halfHeight) offsetY = cameraPosition.y + halfHeight - top;
                }
                layer.Root.localPosition = new Vector3(offsetX, offsetY, 0f);
                // 층 안 좌표로 본 화면 가로 범위(+반 조각 시차 여유)가 모자라면 그쪽 조각만 덧붙인다.
                float localX = cameraPosition.x - offsetX;
                EnsureTiles(layer, localX - halfWidth - layer.TileWidth * 0.5f, localX + halfWidth + layer.TileWidth * 0.5f);
            }
        }

        private void Rebuild()
        {
            Clear();
            if (director == null || !director.isActiveAndEnabled || director.CurrentRoom == null || director.CurrentStage == null) return;
            string id = director.CurrentStage.stageId;
            if (id != "stage_1_abyss_entrance" && id != "stage_2_flame_corridor" && id != "stage_3_throne_ruins") return;

            var surfaces = new List<BoxCollider2D>();
            BoxCollider2D floor = null;
            int groundLayer = LayerMask.NameToLayer("Ground");
            foreach (var col in FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
            {
                if (col.gameObject.scene != gameObject.scene || col.isTrigger || !col.enabled || col.gameObject.layer != groundLayer) continue;
                var bounds = col.bounds;
                if (bounds.size.x < 0.5f || bounds.size.y > bounds.size.x) continue; // no walls
                surfaces.Add(col);
                if (floor == null || bounds.size.x > floor.bounds.size.x) floor = col;
            }
            if (floor == null) return;
            view = null;
            foreach (var follow in FindObjectsByType<Abyss.Runtime.Camera.PlayerCameraFollow>(FindObjectsSortMode.None))
                if (follow.gameObject.scene == gameObject.scene && follow.isActiveAndEnabled &&
                    follow.TryGetComponent<UnityEngine.Camera>(out var ownedCamera) && ownedCamera.isActiveAndEnabled)
                { view = ownedCamera; break; }
            if (view == null)
            foreach (var camera in FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None))
                if (camera.gameObject.scene == gameObject.scene && camera.orthographic && camera.isActiveAndEnabled)
                { view = camera; break; }

            artRoot = new GameObject("WorldEnvironmentArt_Room");
            artRoot.transform.SetParent(transform, false);
            artRoot.transform.position = Vector3.zero;
            float floorY = floor.bounds.max.y;
            var room = director.CurrentRoom;
            float width = room.IsMapRoom ? Mathf.Min(room.mapLength, floor.bounds.size.x) : floor.bounds.size.x;
            if (id == "stage_1_abyss_entrance")
            {
                AddStageOneScenery(width, floorY);
                return;
            }

            layerStage = id == "stage_2_flame_corridor" ? "stage2" : "stage3";
            layerBossRoom = room.roomType == RoomType.Boss;
            layerFloorY = floorY;
            layerWidth = width;
            BuildLayers();
            foreach (var surface in surfaces) AddSurface(surface, layerStage, surface == floor);
        }

        // 배경 층만 (다시) 만든다. 최후방 = stage2 sky · stage3 far · 보스(원본이 불투명한 장).
        private void BuildLayers()
        {
            foreach (var old in layers)
                if (old.Root != null) { old.Root.gameObject.SetActive(false); Destroy(old.Root.gameObject); }
            layers.Clear();
            lastViewOrtho = view != null ? view.orthographicSize : 0f;
            lastViewAspect = view != null ? view.aspect : 0f;
            if (artRoot == null || layerStage == null) return;

            float height = view != null ? Mathf.Max(14f, view.orthographicSize * 2.4f) : 18f;
            // 최후방은 화면 세로+여유 이상 — 위아래로 밀어 넣어도 반대쪽 가장자리가 드러나지 않는다.
            float rearHeight = view != null ? Mathf.Max(height, view.orthographicSize * 2f + COVERAGE_MARGIN * 2f) : height;
            string stage = layerStage;
            if (layerBossRoom)
                AddLayer(stage + "/boss", layerWidth, layerFloorY - 4f, rearHeight, -60, 0.08f, true);
            else
            {
                bool hasSky = stage == "stage2";
                if (hasSky) AddLayer(stage + "/sky", layerWidth, layerFloorY - 4f, rearHeight, -70, 0.02f, true);
                AddLayer(stage + "/far", layerWidth, layerFloorY - 4f, hasSky ? height : rearHeight, -60, 0.08f, !hasSky);
                AddLayer(stage + "/mid", layerWidth, layerFloorY - 3f, height * 0.8f, -40, 0.15f, false);
                AddLayer(stage + "/near", layerWidth, layerFloorY - 2f, height * 0.45f, -20, 0.22f, false);
            }
        }

        private void AddStageOneScenery(float width, float floorY)
        {
            // Existing production terrain is retained. Small noninteractive scenery sits behind characters.
            int index = Mathf.Max(0, director.CurrentStepIndex);
            float left = -width * 0.28f;
            float right = width * 0.18f;
            WorldArtLibrary.Place(artRoot.transform, "stage1/decor_" + (index % 5 + 1), new Vector3(left, floorY, 0f), 1.1f, -6);
            WorldArtLibrary.Place(artRoot.transform, "stage1/landmark_" + (index % 4 + 1), new Vector3(right, floorY, 0f), 3.1f, -8);
        }

        private void AddLayer(string key, float width, float bottom, float height, int order, float rate, bool isRearmost)
        {
            var sprite = WorldArtLibrary.Get(key);
            if (sprite == null) return;
            // 원본 비율 유지: 조각 폭 = 높이 x 원본 가로/세로.
            float tileWidth = height * sprite.bounds.size.x / sprite.bounds.size.y;
            var group = new GameObject(key.Replace('/', '_')).transform;
            group.SetParent(artRoot.transform, false);
            int radius = Mathf.Min(Mathf.CeilToInt((width + tileWidth * 2f) / (2f * tileWidth)), MAX_TILES_PER_LAYER / 2 - 1);
            for (int i = -radius; i <= radius; i++)
                WorldArtLibrary.Place(group, key, new Vector3(i * tileWidth, bottom, 0f), height, order);
            layers.Add(new Layer
            {
                Root = group, Key = key, Rate = rate, Bottom = bottom, Height = height, Order = order,
                TileWidth = tileWidth, IsRearmost = isRearmost, MinIndex = -radius, MaxIndex = radius
            });
        }

        // 조각 i 는 층 안 x = i·폭을 중심으로 [i·폭 - 폭/2, i·폭 + 폭/2] 를 덮는다. 모자란 쪽 조각만 이어 붙인다.
        private static void EnsureTiles(Layer layer, float left, float right)
        {
            if (layer.TileWidth <= 0f) return;
            int needMin = Mathf.FloorToInt(left / layer.TileWidth + 0.5f);
            int needMax = Mathf.CeilToInt(right / layer.TileWidth - 0.5f);
            while (layer.MinIndex > needMin && layer.MaxIndex - layer.MinIndex + 1 < MAX_TILES_PER_LAYER)
            {
                layer.MinIndex--;
                WorldArtLibrary.Place(layer.Root, layer.Key, new Vector3(layer.MinIndex * layer.TileWidth, layer.Bottom, 0f), layer.Height, layer.Order);
            }
            while (layer.MaxIndex < needMax && layer.MaxIndex - layer.MinIndex + 1 < MAX_TILES_PER_LAYER)
            {
                layer.MaxIndex++;
                WorldArtLibrary.Place(layer.Root, layer.Key, new Vector3(layer.MaxIndex * layer.TileWidth, layer.Bottom, 0f), layer.Height, layer.Order);
            }
        }

        private void AddSurface(BoxCollider2D collider, string stage, bool isFloor)
        {
            var sprite = WorldArtLibrary.Get(stage + (isFloor ? "/ground" : "/platform"));
            if (sprite == null) return;
            var bounds = collider.bounds;
            float height = isFloor ? Mathf.Max(6f, bounds.size.y) : Mathf.Clamp(bounds.size.y, 0.3f, 0.8f);
            float scale = height / sprite.bounds.size.y;
            var go = new GameObject(isFloor ? "GroundArt" : "PlatformArt");
            go.transform.SetParent(artRoot.transform, false);
            go.transform.position = new Vector3(bounds.center.x, bounds.max.y - height * 0.5f, 0f);
            go.transform.localScale = Vector3.one * scale;
            var visual = go.AddComponent<SpriteRenderer>();
            visual.sprite = sprite;
            visual.drawMode = SpriteDrawMode.Tiled;
            visual.size = new Vector2(bounds.size.x / scale, sprite.bounds.size.y);
            visual.sortingOrder = -4;
            // Only the geometry's own placeholder renderer; never other props or colliders.
            if (collider.TryGetComponent<SpriteRenderer>(out var original) && original.enabled)
            {
                hiddenRenderers.Add(original);
                original.enabled = false;
            }
        }

        private void Clear()
        {
            pending = false;
            if (artRoot != null) { artRoot.SetActive(false); Destroy(artRoot); artRoot = null; }
            var presenters = FindObjectsByType<StageEnvironmentPresenter>(FindObjectsSortMode.None);
            foreach (var renderer in hiddenRenderers)
            {
                if (renderer == null) continue;
                bool isOwnedBySkin = false;
                foreach (var presenter in presenters)
                    if (presenter.gameObject.scene == gameObject.scene && presenter.isActiveAndEnabled &&
                        presenter.KeepsRendererHidden(renderer)) { isOwnedBySkin = true; break; }
                renderer.enabled = !isOwnedBySkin;
            }
            hiddenRenderers.Clear();
            layers.Clear();
            layerStage = null;
        }
    }
}
