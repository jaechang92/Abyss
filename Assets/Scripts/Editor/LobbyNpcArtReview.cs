#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Form;
using Abyss.Runtime.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    internal static class LobbyNpcArtReview
    {
        // Review only. ApplyBatch saves the real scene before sampling any animation/UI here.
        internal static void Capture(Scene scene)
        {
            const string OUTPUT = "Art_Source/npc_cast_v1/review";
            Directory.CreateDirectory(OUTPUT);
            var camera = LobbyNpcArtBuilder.Find(scene, "Main Camera").GetComponent<Camera>();
            var art = LobbyNpcArtBuilder.Find(scene, "LobbyRefugePanorama")?.GetComponent<SpriteRenderer>();
            float y = art != null ? Mathf.Min(0f, art.bounds.max.y - camera.orthographicSize) : 0f;
            camera.transform.position = new Vector3(0f, y, -10f);
            var playerVisual = Object.FindAnyObjectByType<LobbyFormVisual>();
            if (playerVisual != null)
            {
                var form = new SerializedObject(playerVisual).FindProperty("defaultForm").objectReferenceValue as FormData;
                var clip = form?.animatorController?.animationClips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
                if (clip != null) clip.SampleAnimation(playerVisual.gameObject, 0f);
                playerVisual.GetComponent<SpriteRenderer>().color = Color.white;
                playerVisual.transform.parent.position = new Vector3(0f, -3f, 0f);
            }
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var canvas = LobbyNpcArtBuilder.Find(scene, "LobbyCanvas").GetComponent<Canvas>();
            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalDistance = canvas.planeDistance;
            int originalOrder = canvas.sortingOrder;
            var dialogue = LobbyNpcArtBuilder.Find(scene, "DialogueRoot");
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 100;
                camera.Render();
                foreach (int frame in Enumerable.Range(0, 4))
                {
                    foreach (var animator in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Animator>())
                        .Where(a => a.gameObject.name == "NpcArt"))
                        animator.runtimeAnimatorController.animationClips[0].SampleAnimation(animator.gameObject, frame / 4f);
                    Save($"idle-{frame}");
                }
                var presenter = dialogue.GetComponent<DialoguePortraitPresenter>();
                var speaker = dialogue.transform.Find("Speaker").GetComponent<Text>();
                var body = dialogue.transform.Find("Body").GetComponent<Text>();
                var examples = new[]
                {
                    ("guide", "Npc_Guide_Name", "안내자", "심연의 입구다. 여기서 눈을 뜬 이가 자네가 처음은 아닐세."),
                    ("chronicler", "Npc_Recordkeeper_Name", "기록자", "새로 적은 것이 없다. 자네가 더 가면 적을 것이 생기지."),
                    ("engraver", "Npc_Engraver_Name", "각인사", "이 날에 새긴 이름은 이레브다. 삼백 해 이어진 전쟁을 끝냈다고 적혀 있지. 몇 해였는지는 말이 갈린다.")
                };
                dialogue.SetActive(true);
                foreach (var example in examples)
                {
                    presenter.ShowSpeaker(example.Item2);
                    speaker.text = example.Item3;
                    body.text = example.Item4;
                    Save("dialogue-" + example.Item1);
                }
                dialogue.SetActive(false);
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalDistance;
                canvas.sortingOrder = originalOrder;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }

            void Save(string name)
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                File.WriteAllBytes($"{OUTPUT}/{name}.png", pixels.EncodeToPNG());
            }
        }
    }
}
#endif
