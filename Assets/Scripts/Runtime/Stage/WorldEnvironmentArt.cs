using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>Room-owned visual skins only. Colliders, camera bounds and room progression stay untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldEnvironmentArt : MonoBehaviour
    {
        private StageDirector director;
        private GameObject artRoot;
        private UnityEngine.Camera view;
        private bool pending;
        private readonly List<SpriteRenderer> hiddenRenderers = new();
        private readonly List<Layer> layers = new();

        private readonly struct Layer
        {
            public readonly Transform Root;
            public readonly float Rate;
            public Layer(Transform root, float rate) { Root = root; Rate = rate; }
        }

        public static void Attach(StageDirector owner)
        {
            if (!owner.TryGetComponent<WorldEnvironmentArt>(out _)) owner.gameObject.AddComponent<WorldEnvironmentArt>();
        }

        private void Awake() => director = GetComponent<StageDirector>();
        private void OnEnable()
        {
            GameEvents.OnRoomEntered += QueueRoom;
            GameEvents.OnRunEnded += Clear;
            GameEvents.OnRunAbandoned += Clear;
            pending = true;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= QueueRoom;
            GameEvents.OnRunEnded -= Clear;
            GameEvents.OnRunAbandoned -= Clear;
            Clear();
        }

        private void QueueRoom(RoomData room) => pending = true;

        private void LateUpdate()
        {
            // All OnRoomEntered subscribers (layout activation, Stage1 skins) finish before this pass.
            if (pending) { pending = false; Rebuild(); }
            if (view == null) return;
            foreach (var layer in layers)
                if (layer.Root != null) layer.Root.localPosition = new Vector3(view.transform.position.x * layer.Rate, 0f, 0f);
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

            string stage = id == "stage_2_flame_corridor" ? "stage2" : "stage3";
            float height = view != null ? Mathf.Max(14f, view.orthographicSize * 2.4f) : 18f;
            if (room.roomType == RoomType.Boss)
                AddLayer(stage + "/boss", width, floorY - 4f, height, -60, 0.08f);
            else
            {
                if (stage == "stage2") AddLayer(stage + "/sky", width, floorY - 4f, height, -70, 0.02f);
                AddLayer(stage + "/far", width, floorY - 4f, height, -60, 0.08f);
                AddLayer(stage + "/mid", width, floorY - 3f, height * 0.8f, -40, 0.15f);
                AddLayer(stage + "/near", width, floorY - 2f, height * 0.45f, -20, 0.22f);
            }
            foreach (var surface in surfaces) AddSurface(surface, stage, surface == floor);
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

        private void AddLayer(string key, float width, float bottom, float height, int order, float rate)
        {
            var sprite = WorldArtLibrary.Get(key);
            if (sprite == null) return;
            float tileWidth = height * sprite.bounds.size.x / sprite.bounds.size.y;
            var group = new GameObject(key.Replace('/', '_')).transform;
            group.SetParent(artRoot.transform, false);
            int radius = Mathf.CeilToInt((width + tileWidth * 2f) / (2f * tileWidth));
            for (int i = -radius; i <= radius; i++)
                WorldArtLibrary.Place(group, key, new Vector3(i * tileWidth, bottom, 0f), height, order);
            layers.Add(new Layer(group, rate));
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
        }
    }
}
