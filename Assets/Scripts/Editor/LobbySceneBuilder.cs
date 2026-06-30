#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 빈 Lobby 씬을 생성하고 빌드 설정에 등록한다.
    /// 메뉴 경로는 <see cref="AbyssMenu.BuildLobbyScene"/>.
    /// 구성: Main Camera + EventSystem(InputSystem UI) + Canvas(타이틀 + 시작 버튼 + LobbyController).
    /// 씬 분리 2단계 — Bootstrap → Lobby → Run 흐름의 로비 골격.
    /// </summary>
    public static class LobbySceneBuilder
    {
        [MenuItem(AbyssMenu.BuildLobbyScene)]
        public static void Build()
        {
            if (System.IO.File.Exists(AbyssPaths.LobbyScene))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "LobbySceneBuilder",
                    $"'{AbyssPaths.LobbyScene}'가 이미 존재합니다. 새로 생성해 덮어쓸까요?",
                    "덮어쓰기", "취소");
                if (!overwrite) return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[LobbySceneBuilder] 현재 씬 저장 취소 — 중단.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();

            CreateText(canvas.transform, "Title", "ABYSS", 72,
                new Vector2(0, 160), new Vector2(900, 120), new Color(0.85f, 0.2f, 0.25f));
            CreateText(canvas.transform, "Subtitle", "심연으로 — 시작하려면 Enter 또는 클릭", 22,
                new Vector2(0, 70), new Vector2(900, 48), new Color(0.8f, 0.8f, 0.85f));

            var (startButton, _) = CreateStartButton(canvas.transform);

            var controller = canvas.gameObject.AddComponent<LobbyController>();
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("startButton");
            if (prop != null) prop.objectReferenceValue = startButton;
            so.ApplyModifiedProperties();

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, AbyssPaths.LobbyScene);
            RegisterInBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"[LobbySceneBuilder] 완료 — '{AbyssPaths.LobbyScene}' 생성 + 빌드 설정 등록. Bootstrap 플레이로 흐름 확인.");
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // New Input System 사용 프로젝트 — StandaloneInputModule 대신 InputSystemUIInputModule.
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("LobbyCanvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, Vector2 pos, Vector2 rectSize, Color color)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, rectSize);
            var t = go.AddComponent<Text>();
            ApplyFont(t);
            t.text = content;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            return t;
        }

        private static (Button button, Text label) CreateStartButton(Transform parent)
        {
            var go = CreateRect(parent, "StartButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(320, 80));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.4f, 0.25f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.4f, 0.25f);
            colors.highlightedColor = new Color(0.4f, 0.6f, 0.35f);
            colors.selectedColor = new Color(0.45f, 0.65f, 0.4f);
            colors.pressedColor = new Color(0.2f, 0.3f, 0.2f);
            btn.colors = colors;

            var labelGo = CreateRect(go.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)labelGo.transform);
            var label = labelGo.AddComponent<Text>();
            ApplyFont(label);
            label.text = "시작 (Enter)";
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            return (btn, label);
        }

        private static GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ApplyFont(Text text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        /// <summary>
        /// Lobby 씬을 빌드 설정에 등록한다. 이미 있으면 중복 추가하지 않는다.
        /// 가능하면 Bootstrap 바로 뒤(로비가 부트 직후 진입)에 삽입한다.
        /// buildIndex가 아니라 SceneNames로 로드하므로 순서 자체는 동작에 무관하다.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == AbyssPaths.LobbyScene)
                {
                    Debug.Log("[LobbySceneBuilder] 빌드 설정에 이미 등록됨 — 스킵.");
                    return;
                }
            }

            int insertAt = scenes.Count;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path.EndsWith("/Bootstrap.unity"))
                {
                    insertAt = i + 1;
                    break;
                }
            }
            scenes.Insert(insertAt, new EditorBuildSettingsScene(AbyssPaths.LobbyScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[LobbySceneBuilder] 빌드 설정에 등록 (index {insertAt}).");
        }
    }
}
#endif
