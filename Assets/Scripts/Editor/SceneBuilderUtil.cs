#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 씬·패널 빌더(<see cref="LobbySceneBuilder"/>·<see cref="TitleSceneBuilder"/>·<see cref="DraftPanelBuilder"/>·
    /// <see cref="ResultPanelBuilder"/>)가 파일마다 한 벌씩 들고 있던 에디터 전용 헬퍼.
    ///
    /// uGUI 조립 원시 함수(CreateRect·Stretch·ApplyFont)는 여기 두지 않는다 — 런타임 <see cref="UiFactory"/>에 이미 있고,
    /// 빌더가 만든 씬 UI와 코드로 만든 패널이 같은 폰트·같은 기준 해상도를 써야 하므로 그쪽이 SoT다.
    /// 여기에는 <b>에디터 API가 필요한 것</b>(SerializedObject 배선·빌드 설정·에셋 폴더)과 씬 뼈대만 둔다.
    ///
    /// 호출부는 <c>using static Abyss.EditorTools.SceneBuilderUtil;</c>로 이름만 부른다.
    /// </summary>
    public static class SceneBuilderUtil
    {
        private const string SCENES_PARENT = "Assets";
        private const string SCENES_FOLDER_NAME = "Scenes";
        private const string BOOTSTRAP_SCENE_SUFFIX = "/Bootstrap.unity";

        // ───────────────────────── SerializedObject 배선 ─────────────────────────
        // 필드를 못 찾으면 조용히 넘어간다(기존 동작). 경고가 필요한 곳은 호출부가 따로 검사한다.

        public static void SetObject(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        public static void SetString(SerializedObject so, string field, string value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.stringValue = value;
        }

        public static void SetObjectArray(SerializedObject so, string field, List<Object> values)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // ───────────────────────── 씬 뼈대 ─────────────────────────

        /// <summary>New Input System용 EventSystem. 구 StandaloneInputModule을 쓰면 UI 입력이 안 들어온다.</summary>
        public static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// 씬에 배치되는 오버레이 캔버스. 기준 해상도는 <see cref="UiFactory.ReferenceResolution"/>를 따른다 —
        /// 동적 패널과 값이 갈리면 같은 크기로 적은 UI가 씬마다 다르게 보인다.
        /// </summary>
        public static Canvas CreateSceneCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiFactory.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ───────────────────────── 에셋·빌드 설정 ─────────────────────────

        public static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder($"{SCENES_PARENT}/{SCENES_FOLDER_NAME}"))
            {
                AssetDatabase.CreateFolder(SCENES_PARENT, SCENES_FOLDER_NAME);
            }
        }

        /// <summary>
        /// 씬을 빌드 설정에 등록한다. 이미 있으면 중복 추가하지 않는다.
        /// 가능하면 Bootstrap 바로 뒤에 넣는다 — 씬은 buildIndex가 아니라 SceneNames로 로드하므로 순서는 동작에 무관하다.
        /// </summary>
        /// <param name="logTag">로그 접두어(호출한 빌더 이름).</param>
        public static void RegisterSceneAfterBootstrap(string scenePath, string logTag)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    Debug.Log($"[{logTag}] 빌드 설정에 이미 등록됨 — 스킵.");
                    return;
                }
            }

            int insertAt = scenes.Count;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path.EndsWith(BOOTSTRAP_SCENE_SUFFIX))
                {
                    insertAt = i + 1;
                    break;
                }
            }
            scenes.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[{logTag}] 빌드 설정에 등록 (index {insertAt}).");
        }
    }
}
#endif
