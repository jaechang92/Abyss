#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Dialogue;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.EditorTools
{
    /// <summary>Import explicit atlas rectangles and patch lobby visuals without rebuilding its gameplay wiring.</summary>
    public static class LobbyNpcArtBuilder
    {
        private const string Art = "Assets/Art/NPCs";
        [Serializable] private sealed class Cast { public Npc[] entries; }
        [Serializable] private sealed class Npc { public string id; public string sceneObject; public string speakerKey; }
        [Serializable] private sealed class Manifest { public Layout frame_layout; public Animation animation; public Cell cell; }
        [Serializable] private sealed class Layout { public int sheetHeight; public Rows rows; }
        [Serializable] private sealed class Rows { public Frame[] idle; }
        [Serializable] private sealed class Frame { public int x; public int y; public int w; public int h; }
        [Serializable] private sealed class Cell { public int safe_margin_y; }
        [Serializable] private sealed class Animation { public AnimRows rows; }
        [Serializable] private sealed class AnimRows { public Idle idle; }
        [Serializable] private sealed class Idle { public int frames; public float fps; public bool loop; }

        public static void ApplyToScene(Scene scene)
        {
            if (!File.Exists(Art + "/npc-cast.json")) return;
            var cast = JsonUtility.FromJson<Cast>(File.ReadAllText(Art + "/npc-cast.json"));
            // Validate the complete cast before changing the scene.
            foreach (var npc in cast.entries)
            {
                foreach (string file in new[] { "manifest.json", "idle-sheet.png", "portrait.png" })
                    if (!File.Exists($"{Art}/{npc.id}/{file}")) throw new FileNotFoundException($"Missing NPC art: {npc.id}/{file}");
                if (Find(scene, npc.sceneObject) == null) throw new InvalidOperationException($"Missing NPC: {npc.sceneObject}");
            }
            var portraits = cast.entries.Select(npc =>
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText($"{Art}/{npc.id}/manifest.json"));
                var sprites = ImportFrames(npc.id, manifest);
                var controller = BakeIdle(npc.id, sprites, manifest.animation.rows.idle);
                AttachVisual(Find(scene, npc.sceneObject), sprites[0], controller);
                return new DialoguePortraitPresenter.Entry
                {
                    speakerKey = npc.speakerKey,
                    portrait = ImportPortrait($"{Art}/{npc.id}/portrait.png")
                };
            }).ToArray();
            WireDialogue(scene, portraits);
            AddServicePortrait(scene, "FormSelectRoot", portraits.Single(p => p.speakerKey == "Npc_Engraver_Name").portrait);
            AddServicePortrait(scene, "AltarRoot", portraits.Single(p => p.speakerKey == "Npc_AltarKeeper_Name").portrait);
            AddServicePortrait(scene, "RelicShopRoot", portraits.Single(p => p.speakerKey == "Npc_RelicMerchant_Name").portrait);
            AssetDatabase.SaveAssets();
        }

        private static Sprite[] ImportFrames(string id, Manifest manifest)
        {
            string path = $"{Art}/{id}/idle-sheet.png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelScale.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
            var frames = manifest.frame_layout.rows.idle;
            if (frames.Length != manifest.animation.rows.idle.frames || frames.Length == 0)
                throw new InvalidDataException($"Invalid frame count: {id}");
            var rects = frames.Select((frame, i) =>
            {
                string name = $"{id}_idle_{i}";
                return new SpriteRect
                {
                    name = name,
                    spriteID = existing.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate(),
                    rect = new Rect(frame.x, manifest.frame_layout.sheetHeight - frame.y - frame.h, frame.w, frame.h),
                    alignment = SpriteAlignment.Custom,
                    // Extractor bottom-aligns content above this padding; no runtime alpha guessing.
                    pivot = new Vector2(0.5f, manifest.cell.safe_margin_y / (float)frame.h)
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            return rects.Select(r => imported[r.name]).ToArray();
        }

        private static AnimatorController BakeIdle(string id, Sprite[] frames, Idle idle)
        {
            if (idle.fps <= 0 || !idle.loop) throw new InvalidDataException($"Invalid idle timing: {id}");
            string clipPath = $"{Art}/{id}/{id}_idle.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.name = id + "_idle";
            clip.frameRate = idle.fps;
            // 스프라이트 커브는 마지막 키도 1/fps 동안 유지된다(clip.length = 마지막 키 + 1/fps).
            // 첫 프레임을 닫는 키로 한 번 더 넣으면 주기가 1.25초가 되고 루프 경계에서 첫 프레임이 두 칸 머문다.
            var keys = Enumerable.Range(0, frames.Length).Select(i => new ObjectReferenceKeyframe
                { time = i / idle.fps, value = frames[i] }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" }, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            string controllerPath = $"{Art}/{id}/{id}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
            state.motion = clip;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Sprite ImportPortrait(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AttachVisual(GameObject npc, Sprite initial, AnimatorController controller)
        {
            var old = npc.transform.Find("NpcArt");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("NpcArt");
            go.transform.SetParent(npc.transform, false);
            // NPC roots carry legacy trigger scale (1,2). Cancel it only for the picture.
            go.transform.localScale = new Vector3(1f / npc.transform.lossyScale.x, 1f / npc.transform.lossyScale.y, 1f);
            go.transform.position = new Vector3(npc.transform.position.x, -3f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = initial;
            renderer.sortingOrder = 1;
            go.AddComponent<Animator>().runtimeAnimatorController = controller;
            var placeholder = npc.GetComponent<SpriteRenderer>();
            if (placeholder != null) placeholder.enabled = false;
        }

        private static void WireDialogue(Scene scene, DialoguePortraitPresenter.Entry[] entries)
        {
            var root = Find(scene, "DialogueRoot");
            if (root == null) throw new InvalidOperationException("DialogueRoot missing");
            SetRect(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(1640f, 300f));
            var speaker = root.transform.Find("Speaker").GetComponent<Text>();
            var body = root.transform.Find("Body").GetComponent<Text>();
            var hint = root.transform.Find("Hint").GetComponent<Text>();
            SetRect(speaker.rectTransform, Vector2.zero, Vector2.zero, new Vector2(410, 242), new Vector2(1140, 42));
            SetRect(body.rectTransform, Vector2.zero, Vector2.zero, new Vector2(410, 54), new Vector2(1150, 170));
            SetRect(hint.rectTransform, Vector2.zero, Vector2.zero, new Vector2(1300, 12), new Vector2(300, 30));
            speaker.fontSize = 28;
            body.fontSize = 24;
            speaker.alignment = TextAnchor.MiddleLeft;
            body.alignment = TextAnchor.UpperLeft;
            var old = root.transform.Find("SpeakerPortrait");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var portrait = CreateRect(root.transform, "SpeakerPortrait", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(28, 18), new Vector2(340, 510)).AddComponent<Image>();
            var presenter = root.GetComponent<DialoguePortraitPresenter>() ?? root.AddComponent<DialoguePortraitPresenter>();
            presenter.Configure(portrait, entries);
            var ui = Find(scene, "LobbyCanvas").GetComponent<DialogueUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("portraitPresenter").objectReferenceValue = presenter;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddServicePortrait(Scene scene, string rootName, Sprite sprite)
        {
            var root = Find(scene, rootName);
            if (root == null) throw new InvalidOperationException($"Missing service panel: {rootName}");
            var old = root.transform.Find("NpcPortrait");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var image = CreateRect(root.transform, "NpcPortrait", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(0, 0.5f), new Vector2(48, 0), new Vector2(340, 510)).AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)?.gameObject;

        public static void ApplyBatch()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");
                var colliders = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider2D>(true));
                var before = colliders.Select(c => (c, c.transform.position, c.transform.lossyScale)).ToArray();
                ApplyToScene(scene);
                var after = colliders.Select(c => (c, c.transform.position, c.transform.lossyScale)).ToArray();
                if (!before.SequenceEqual(after)) throw new InvalidOperationException("NPC art changed collision geometry");
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Lobby save failed");
                LobbyNpcArtReview.Capture(scene);
                Debug.Log("[LobbyNpcArt] Five NPC idle animations and portraits wired; colliders unchanged.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
#endif
