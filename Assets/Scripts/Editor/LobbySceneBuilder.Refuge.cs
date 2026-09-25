#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Abyss.Runtime.Camera;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class LobbySceneBuilder
    {
        private const string RefugeArtPath = "Assets/Art/Environment/lobby_refuge/lobby_refuge_v2.png";
        private const float RefugeWidth = 44f;
        // Measured walk lane in the 2172 x 724 source; a foot rests on its upper stone face.
        private const float RefugeFloorFromBottom = (724f - 431f) / 724f;

        private static bool ApplyRefugeArt(GameObject ground, PlayerCameraFollow follow)
        {
            if (!File.Exists(RefugeArtPath)) return false;
            AssetDatabase.ImportAsset(RefugeArtPath);
            var importer = AssetImporter.GetAtPath(RefugeArtPath) as TextureImporter;
            if (importer == null) return false;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelScale.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RefugeArtPath);
            if (sprite == null) return false;

            var root = new GameObject(EnvironmentArtRootName);
            var artwork = new GameObject("LobbyRefugePanorama");
            artwork.transform.SetParent(root.transform, false);
            float scale = RefugeWidth / sprite.bounds.size.x;
            float height = sprite.bounds.size.y * scale;
            float bottom = FloorTopY - height * RefugeFloorFromBottom;
            artwork.transform.position = new Vector3(0f, bottom + height * 0.5f, 0f);
            artwork.transform.localScale = Vector3.one * scale;
            var renderer = artwork.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -20;
            follow.SetEnvironmentBounds(new Vector2(-RefugeWidth * 0.5f, bottom),
                new Vector2(RefugeWidth * 0.5f, bottom + height));
            foreach (var graybox in ground.GetComponentsInChildren<SpriteRenderer>(true)) graybox.enabled = false;
            return true;
        }

        // Update only the environment in the existing scene; preserve NPC, UI and progression wiring.
        public static void ApplyRefugeBatch()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(AbyssPaths.LobbyScene);
                var ground = scene.GetRootGameObjects().Single(go => go.name == "Environment");
                var follow = UnityEngine.Object.FindAnyObjectByType<PlayerCameraFollow>();
                if (follow == null) throw new InvalidOperationException("Lobby camera missing.");
                var collidersBefore = ground.GetComponentsInChildren<Collider2D>(true)
                    .Select(c => (c, c.transform.position, c.transform.localScale)).ToArray();
                foreach (var old in scene.GetRootGameObjects().Where(go => go.name == EnvironmentArtRootName))
                    UnityEngine.Object.DestroyImmediate(old);
                if (!ApplyRefugeArt(ground, follow)) throw new InvalidOperationException("Refuge artwork could not be loaded.");
                var collidersAfter = ground.GetComponentsInChildren<Collider2D>(true)
                    .Select(c => (c, c.transform.position, c.transform.localScale)).ToArray();
                if (!collidersBefore.SequenceEqual(collidersAfter)) throw new InvalidOperationException("Lobby collision changed.");
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Lobby scene.");
                CaptureRefugeReview(follow.GetComponent<UnityEngine.Camera>());
                Debug.Log("[LobbyRefuge] Applied; existing collision, NPCs and UI preserved. Review renders saved.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void CaptureRefugeReview(UnityEngine.Camera camera)
        {
            string output = "Art_Source/lobby_refuge/review";
            Directory.CreateDirectory(output);
            // Sample the actual starting form for editor-only review (the scene was already saved).
            var visual = UnityEngine.Object.FindAnyObjectByType<Abyss.Runtime.Lobby.LobbyFormVisual>();
            if (visual != null)
            {
                var form = new SerializedObject(visual).FindProperty("defaultForm").objectReferenceValue as Abyss.Runtime.Form.FormData;
                var renderer = visual.GetComponent<SpriteRenderer>();
                if (form != null && renderer != null)
                {
                    renderer.color = Color.white;
                    if (form.bodySprite != null) renderer.sprite = form.bodySprite;
                    var clip = form.animatorController != null
                        ? form.animatorController.animationClips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle")) : null;
                    if (clip != null) clip.SampleAnimation(visual.gameObject, 0f);
                    var player = visual.transform.parent;
                    if (player != null) player.position = new Vector3(player.position.x, FloorTopY, player.position.z);
                }
            }
            var originalPosition = camera.transform.position;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                var art = GameObject.Find("LobbyRefugePanorama").GetComponent<SpriteRenderer>();
                float y = Mathf.Clamp(0f, art.bounds.min.y + camera.orthographicSize, art.bounds.max.y - camera.orthographicSize);
                // Repeat the first view after texture uploads have warmed up in batch mode.
                foreach (var shot in new[] { ("left", -12f), ("center", 0f), ("right", 12f), ("left", -12f) })
                {
                    camera.transform.position = new Vector3(shot.Item2, y, -10f);
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{output}/{shot.Item1}.png", image.EncodeToPNG());
                }
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.targetTexture = originalTarget;
                RenderTexture.active = originalActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
#endif
