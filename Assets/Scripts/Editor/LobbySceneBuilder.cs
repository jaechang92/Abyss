#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Form;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 허브 로비 씬을 생성하고 빌드 설정에 등록한다. 메뉴 경로는 <see cref="AbyssMenu.BuildLobbyScene"/>.
    /// 구성: 바닥/벽 + 경량 플레이어(이동·상호작용) + 카메라 추적 + 던전 포털 + 폼 선택 패널.
    /// 씬 분리 3단계(H1) — 스컬식 플레이 가능 허브 골격.
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 폼 버튼 강조 색(LobbyController/FormSelectPanel과 일치)
        private static readonly Color FormNormal = new Color(0.18f, 0.18f, 0.22f);

        [MenuItem(AbyssMenu.BuildLobbyScene)]
        public static void Build()
        {
            if (System.IO.File.Exists(AbyssPaths.LobbyScene))
            {
                bool overwrite = EditorUtility.DisplayDialog("LobbySceneBuilder",
                    $"'{AbyssPaths.LobbyScene}'가 이미 존재합니다. 새로 생성해 덮어쓸까요?", "덮어쓰기", "취소");
                if (!overwrite) return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[LobbySceneBuilder] 현재 씬 저장 취소 — 중단.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraFollow = CreateCamera();
            CreateEventSystem();
            CreateGround();

            var player = CreatePlayer(out var controller, out var interactor, out var groundCheck);
            var portal = CreatePortal();

            var canvas = CreateCanvas();
            var prompt = CreatePrompt(canvas.transform);
            var panel = CreateFormSelectPanel(canvas);
            var dialogueUI = CreateDialogueUI(canvas);
            var altarPanel = CreateMetaUpgradePanel(canvas);

            var guideNpc = CreateGuideNpc();
            var serviceNpc = CreateServiceNpc();
            var altarNpc = CreateAltarNpc();
            var storyNpc = CreateStoryNpc();

            // 와이어링
            WireCamera(cameraFollow, player.transform);
            WireController(controller, groundCheck);
            WireInteractor(interactor, prompt);
            WirePortal(portal);
            WireServiceNpc(serviceNpc, panel.component, controller);
            WireDialogueNpc(guideNpc, dialogueUI, controller);
            LoadOrCreateMetaUpgrades();
            WireAltarNpc(altarNpc, altarPanel, controller);
            var storyData = LoadOrCreateStoryData();
            WireStoryNpc(storyNpc, storyData, dialogueUI, controller);

            EnsureSceneFolder();
            EditorSceneManager.SaveScene(scene, AbyssPaths.LobbyScene);
            RegisterInBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log($"[LobbySceneBuilder] 허브 로비 생성 완료 — '{AbyssPaths.LobbyScene}'. Bootstrap 플레이로 흐름 확인.");
        }

        // ───────────────────────── 씬 오브젝트 ─────────────────────────

        private static PlayerCameraFollow CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 2f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            go.AddComponent<AudioListener>();
            return go.AddComponent<PlayerCameraFollow>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static void CreateGround()
        {
            var root = new GameObject("Environment");
            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var sprite = EditorPlatformFactory.LoadWhiteSquare();
            var mat = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();
            var color = EditorPlatformFactory.DefaultPlatformColor;

            EditorPlatformFactory.CreatePlatform(root.transform, "Floor", new Vector2(0f, -3.5f), new Vector2(30f, 1f), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallLeft", new Vector2(-14f, 0f), new Vector2(1f, 8f), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallRight", new Vector2(14f, 0f), new Vector2(1f, 8f), groundLayer, sprite, mat, color);
        }

        private static GameObject CreatePlayer(out LobbyPlayerController controller, out PlayerInteractor interactor, out Transform groundCheck)
        {
            var go = new GameObject("LobbyPlayer");
            go.transform.position = new Vector3(0f, -2f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = new Color(0.4f, 0.7f, 1f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            go.AddComponent<BoxCollider2D>(); // 물리(바닥/벽 충돌)

            // 상호작용 감지용 트리거(넓은 반경)
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;

            controller = go.AddComponent<LobbyPlayerController>();
            interactor = go.AddComponent<PlayerInteractor>();

            var input = go.AddComponent<PlayerInput>();
            ConfigurePlayerInput(input);

            // 지면 체크 자식(발밑)
            var gc = new GameObject("GroundCheck");
            gc.transform.SetParent(go.transform, false);
            gc.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = gc.transform;

            return go;
        }

        private static DungeonPortal CreatePortal()
        {
            return CreateNpcObject<DungeonPortal>("DungeonPortal", new Vector2(8f, -2.5f), new Vector2(1.5f, 2f), new Color(0.6f, 0.3f, 0.8f));
        }

        private static ServiceNpc CreateServiceNpc()
        {
            return CreateNpcObject<ServiceNpc>("ServiceNpc", new Vector2(3f, -2.5f), new Vector2(1f, 2f), new Color(0.4f, 0.8f, 0.6f));
        }

        // ───────────────────────── UI ─────────────────────────

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

        private static Text CreatePrompt(Transform parent)
        {
            var go = CreateRect(parent, "InteractPrompt", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(700, 48));
            var t = go.AddComponent<Text>();
            ApplyFont(t);
            t.text = "던전 입장 (G)";
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.95f, 0.6f);
            go.SetActive(false); // 근접 시에만 표시(PlayerInteractor가 토글)
            return t;
        }

        private struct PanelRefs
        {
            public FormSelectPanel component;
        }

        private static PanelRefs CreateFormSelectPanel(Canvas canvas)
        {
            var panel = canvas.gameObject.AddComponent<FormSelectPanel>();

            // 토글 대상 루트(전체 화면 반투명 배경)
            var root = CreateRect(canvas.transform, "FormSelectRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            var box = CreateRect(root.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 460));
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            CreateText(box.transform, "Title", "시작 폼 선택", 30, new Vector2(0, -50), new Vector2(700, 48), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));

            // 폼 버튼
            var forms = LoadForms();
            var formButtons = new List<Object>();
            float fx = -165f;
            foreach (var f in forms)
            {
                formButtons.Add(CreateFormButton(box.transform, f, new Vector2(fx, 30)));
                fx += 330f;
            }

            var (confirm, _) = CreateButton(box.transform, "ConfirmButton", new Vector2(-130, -160), new Vector2(220, 64), new Color(0.25f, 0.4f, 0.25f), "선택 (Enter)");
            var (cancel, _) = CreateButton(box.transform, "CancelButton", new Vector2(130, -160), new Vector2(220, 64), new Color(0.4f, 0.25f, 0.25f), "취소 (Esc)");

            root.SetActive(false);

            // 와이어
            var so = new SerializedObject(panel);
            SetObject(so, "root", root);
            SetObject(so, "confirmButton", confirm);
            SetObject(so, "cancelButton", cancel);
            SetObjectArray(so, "selectableForms", new List<Object>(forms));
            SetObjectArray(so, "formButtons", formButtons);
            so.ApplyModifiedProperties();

            return new PanelRefs { component = panel };
        }

        private static Button CreateFormButton(Transform parent, FormData form, Vector2 pos)
        {
            var go = CreateRect(parent, "FormButton_" + form.formId, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(300, 150));
            var img = go.AddComponent<Image>();
            img.color = FormNormal;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            if (form.icon != null)
            {
                var iconGo = CreateRect(go.transform, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(72, 72));
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = form.icon;
                iconImg.preserveAspect = true;
            }

            var nameGo = CreateRect(go.transform, "Name", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(280, 40));
            var nameTxt = nameGo.AddComponent<Text>();
            ApplyFont(nameTxt);
            nameTxt.text = string.IsNullOrEmpty(form.displayName) ? form.formId : form.displayName;
            nameTxt.fontSize = 18;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.color = Color.white;

            return btn;
        }

        private static (Button button, Text label) CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, Color baseColor, string labelText)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = go.AddComponent<Image>();
            img.color = baseColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.25f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.35f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.2f);
            btn.colors = colors;

            var labelGo = CreateRect(go.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)labelGo.transform);
            var label = labelGo.AddComponent<Text>();
            ApplyFont(label);
            label.text = labelText;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return (btn, label);
        }

        // ───────────────────────── 와이어링 ─────────────────────────

        private static void WireCamera(PlayerCameraFollow cam, Transform target)
        {
            var so = new SerializedObject(cam);
            SetObject(so, "target", target);
            var find = so.FindProperty("findPlayerAtStart");
            if (find != null) find.boolValue = false; // 런타임 검색은 PlayerCharacter만 찾으므로 직접 와이어
            so.ApplyModifiedProperties();
        }

        private static void WireController(LobbyPlayerController controller, Transform groundCheck)
        {
            var so = new SerializedObject(controller);
            SetObject(so, "groundCheck", groundCheck);
            var layerProp = so.FindProperty("groundLayer");
            if (layerProp != null) layerProp.intValue = 1 << EditorPlatformFactory.GetGroundLayer();
            so.ApplyModifiedProperties();
        }

        private static void WireInteractor(PlayerInteractor interactor, Text prompt)
        {
            var so = new SerializedObject(interactor);
            SetObject(so, "promptLabel", prompt);
            so.ApplyModifiedProperties();
        }

        private static void WirePortal(DungeonPortal portal)
        {
            var so = new SerializedObject(portal);
            var p = so.FindProperty("promptKey");
            if (p != null) p.stringValue = StringKey.Portal_Prompt;
            so.ApplyModifiedProperties();
        }

        private static void WireServiceNpc(ServiceNpc npc, FormSelectPanel panel, LobbyPlayerController player)
        {
            var so = new SerializedObject(npc);
            SetObject(so, "formSelectPanel", panel);
            SetObject(so, "player", player);
            ApplyNpcPrompt(so, StringKey.Npc_Service_Prompt);
        }

        private static void ConfigurePlayerInput(PlayerInput input)
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AbyssPaths.InputActions);
            if (actions == null)
            {
                Debug.LogWarning($"[LobbySceneBuilder] {AbyssPaths.InputActions} 미발견 — PlayerInput.actions 미할당");
                return;
            }
            var so = new SerializedObject(input);
            var actionsProp = so.FindProperty("m_Actions");
            if (actionsProp != null) actionsProp.objectReferenceValue = actions;
            var defaultMapProp = so.FindProperty("m_DefaultActionMap");
            if (defaultMapProp != null) defaultMapProp.stringValue = "Player";
            var notifyProp = so.FindProperty("m_NotificationBehavior");
            if (notifyProp != null) notifyProp.enumValueIndex = (int)PlayerNotifications.SendMessages;
            so.ApplyModifiedProperties();
        }

        // ───────────────────────── UI 헬퍼 ─────────────────────────

        private static Text CreateText(Transform parent, string name, string content, int size, Vector2 pos, Vector2 rectSize, TextAnchor anchor, Color color)
        {
            var go = CreateRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, rectSize);
            var t = go.AddComponent<Text>();
            ApplyFont(t);
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            return t;
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

        private static FormData[] LoadForms()
        {
            var dark = AssetDatabase.LoadAssetAtPath<FormData>(AbyssPaths.Forms + "/DarkBlade.asset");
            var archer = AssetDatabase.LoadAssetAtPath<FormData>(AbyssPaths.Forms + "/VoidArcher.asset");
            var list = new List<FormData>();
            if (dark != null) list.Add(dark);
            else Debug.LogWarning($"[LobbySceneBuilder] DarkBlade.asset 누락 — '{AbyssMenu.GenerateContent}' 먼저 실행.");
            if (archer != null) list.Add(archer);
            else Debug.LogWarning($"[LobbySceneBuilder] VoidArcher.asset 누락 — '{AbyssMenu.GenerateContent}' 먼저 실행.");
            return list.ToArray();
        }

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedObject so, string field, List<Object> values)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
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
        /// 가능하면 Bootstrap 바로 뒤에 삽입한다. buildIndex가 아니라 SceneNames로 로드하므로 순서는 동작에 무관.
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
