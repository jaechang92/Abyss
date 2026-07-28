#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 타이틀 씬을 생성하고 빌드 설정에 등록한다. 메뉴 경로는 <see cref="AbyssMenu.BuildTitleScene"/>.
    /// 구성: 카메라 + EventSystem + 캔버스(타이틀 로고·메뉴 버튼 3종·누적 기록 줄).
    /// 완주 루프 계획 Phase 0-2 — 게임을 켜고 끌 수 있는 앱 셸의 진입점.
    /// </summary>
    public static class TitleSceneBuilder
    {
        [MenuItem(AbyssMenu.BuildTitleScene)]
        public static void Build()
        {
            if (System.IO.File.Exists(AbyssPaths.TitleScene))
            {
                bool overwrite = EditorUtility.DisplayDialog("TitleSceneBuilder",
                    $"'{AbyssPaths.TitleScene}'가 이미 존재합니다. 새로 생성해 덮어쓸까요?", "덮어쓰기", "취소");
                if (!overwrite) return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[TitleSceneBuilder] 현재 씬 저장 취소 — 중단.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();
            BuildMenu(canvas);

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, AbyssPaths.TitleScene);
            RegisterInBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"[TitleSceneBuilder] 타이틀 씬 생성 완료 — '{AbyssPaths.TitleScene}'. Bootstrap 플레이로 흐름 확인.");
        }

        // ───────────────────────── 씬 오브젝트 ─────────────────────────

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("TitleCanvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ───────────────────────── UI ─────────────────────────

        private static void BuildMenu(Canvas canvas)
        {
            var root = CreateRect(canvas.transform, "TitleRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.07f, 1f);
            bg.raycastTarget = false;

            var titleGo = CreateRect(root.transform, "TitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -220), new Vector2(900, 120));
            var title = titleGo.AddComponent<Text>();
            ApplyFont(title);
            title.text = "ABYSS";
            title.fontSize = 96;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.86f, 0.82f, 1f);
            title.raycastTarget = false;

            var subtitleGo = CreateRect(root.transform, "SubtitleText", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -318), new Vector2(900, 40));
            var subtitle = subtitleGo.AddComponent<Text>();
            ApplyFont(subtitle);
            subtitle.text = "심연으로 추락한 자";
            subtitle.fontSize = 22;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.55f, 0.55f, 0.68f);
            subtitle.raycastTarget = false;

            var start = CreateMenuButton(root.transform, "StartButton", new Vector2(0, 40), "게임 시작");
            var settings = CreateMenuButton(root.transform, "SettingsButton", new Vector2(0, -30), "설정");
            var quit = CreateMenuButton(root.transform, "QuitButton", new Vector2(0, -100), "게임 종료");

            // 누적 기록 줄(기록이 없으면 TitleMenuPanel이 빈 문자열로 비운다)
            var recordGo = CreateRect(root.transform, "RecordText", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(1000, 34));
            var record = recordGo.AddComponent<Text>();
            ApplyFont(record);
            record.text = string.Empty;
            record.fontSize = 18;
            record.alignment = TextAnchor.MiddleCenter;
            record.color = new Color(0.5f, 0.5f, 0.6f);
            record.raycastTarget = false;

            var panel = root.AddComponent<TitleMenuPanel>();
            SetPrivateField(panel, "startButton", start.button);
            SetPrivateField(panel, "settingsButton", settings.button);
            SetPrivateField(panel, "quitButton", quit.button);
            SetPrivateField(panel, "startLabel", start.label);
            SetPrivateField(panel, "recordText", record);
        }

        private struct ButtonHandle
        {
            public Button button;
            public Text label;
        }

        private static ButtonHandle CreateMenuButton(Transform parent, string name, Vector2 position, string labelText)
        {
            var root = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(360, 58));
            var img = root.AddComponent<Image>();
            img.color = new Color(0.16f, 0.16f, 0.24f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.16f, 0.24f);
            colors.highlightedColor = new Color(0.28f, 0.28f, 0.40f);
            colors.pressedColor = new Color(0.12f, 0.12f, 0.18f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.15f, 0.6f);
            button.colors = colors;

            var textGo = CreateRect(root.transform, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            ApplyFont(text);
            text.text = labelText;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; // 2단계 확인 문구가 길어져도 줄바꿈되지 않게
            text.color = Color.white;

            return new ButtonHandle { button = button, label = text };
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

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

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[TitleSceneBuilder] {target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        /// <summary>
        /// 빌드 설정에 등록한다. 순서는 Bootstrap → Title → Lobby → Run이어야 하므로 Bootstrap 바로 뒤에 넣는다.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == AbyssPaths.TitleScene)
                {
                    Debug.Log("[TitleSceneBuilder] 빌드 설정에 이미 등록됨 — 스킵.");
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
            scenes.Insert(insertAt, new EditorBuildSettingsScene(AbyssPaths.TitleScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[TitleSceneBuilder] 빌드 설정에 등록 (index {insertAt}).");
        }
    }
}
#endif
