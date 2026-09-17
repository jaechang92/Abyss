#if UNITY_EDITOR
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Abyss.EditorTools.SceneBuilderUtil;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 타이틀 씬을 생성하고 빌드 설정에 등록한다. Abyss Tools 창의 <see cref="AbyssToolNames.BuildTitleScene"/>.
    /// 구성: 카메라 + EventSystem + 캔버스(타이틀 로고·메뉴 버튼 4종·누적 기록 줄).
    /// 완주 루프 계획 Phase 0-2 — 게임을 켜고 끌 수 있는 앱 셸의 진입점.
    /// </summary>
    public static partial class TitleSceneBuilder
    {
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
            var canvas = CreateSceneCanvas("TitleCanvas");
            BuildMenu(canvas);

            EnsureScenesFolder();
            EditorSceneManager.SaveScene(scene, AbyssPaths.TitleScene);
            // 순서는 Bootstrap → Title → Lobby → Run 이어야 하므로 Bootstrap 바로 뒤에 넣는다.
            RegisterSceneAfterBootstrap(AbyssPaths.TitleScene, nameof(TitleSceneBuilder));
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
            cam.orthographicSize = Abyss.Runtime.Camera.PixelScale.OrthographicSize;
            go.AddComponent<AudioListener>();
        }

        // ───────────────────────── UI ─────────────────────────

        private static void BuildMenu(Canvas canvas)
        {
            var root = CreateRect(canvas.transform, "TitleRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.07f, 1f);
            bg.raycastTarget = false;

            // 배경 레이어를 UI보다 먼저 만든다 — uGUI는 계층 순서가 곧 렌더 순서다.
            var backdrop = BuildBackdrop(root.transform);

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

            // 버튼이 4개로 늘어 세로 간격(70)을 유지하며 위로 30 올렸다 — 아래 누적 기록 줄과 겹치지 않게.
            var start = CreateMenuButton(root.transform, "StartButton", new Vector2(0, 70), "게임 시작");
            var codex = CreateMenuButton(root.transform, "CodexButton", new Vector2(0, 0), "도감");
            var settings = CreateMenuButton(root.transform, "SettingsButton", new Vector2(0, -70), "설정");
            var quit = CreateMenuButton(root.transform, "QuitButton", new Vector2(0, -140), "게임 종료");

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
            SetPrivateField(panel, "codexButton", codex.button);
            SetPrivateField(panel, "settingsButton", settings.button);
            SetPrivateField(panel, "quitButton", quit.button);
            SetPrivateField(panel, "startLabel", start.label);
            SetPrivateField(panel, "recordText", record);

            // 로고는 배경보다 나중에 생성되므로 여기서 연결한다(등장 페이드·숨쉬기 대상).
            if (backdrop != null) SetPrivateField(backdrop, "logo", title);
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
    }
}
#endif
