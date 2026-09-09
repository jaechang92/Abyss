#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Form;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Story;
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

            // 폼 목록과 기본 폼은 한 번만 정하고 여러 곳에 나눠 준다.
            // 각자 로드하면 "패널이 강조하는 폼"과 "서 있는 캐릭터"가 조용히 갈라질 수 있다.
            var forms = LoadForms();
            var defaultForm = ResolveDefaultForm(forms);

            var player = CreatePlayer(defaultForm, out var controller, out var interactor, out var groundCheck);
            var portal = CreatePortal();

            var canvas = CreateCanvas();
            var prompt = CreatePrompt(canvas.transform);
            var panel = CreateFormSelectPanel(canvas, forms, defaultForm);
            var dialogueUI = CreateDialogueUI(canvas);
            var altarPanel = CreateMetaUpgradePanel(canvas);
            var relicShopPanel = CreateRelicShopPanel(canvas);

            var guideNpc = CreateGuideNpc();
            var serviceNpc = CreateServiceNpc();
            var altarNpc = CreateAltarNpc();
            var storyNpc = CreateStoryNpc();
            var relicShopNpc = CreateRelicShopNpc();

            // 와이어링
            WireCamera(cameraFollow, player.transform);
            WireController(controller, groundCheck);
            WireInteractor(interactor, prompt);
            WirePortal(portal);
            // 각인사 내력은 정비 NPC가 폼 선택 패널보다 먼저 재생하므로 와이어링 전에 만들어 둔다.
            var engraverData = LoadOrCreateEngraverStoryData();
            WireServiceNpc(serviceNpc, panel.component, controller, engraverData, dialogueUI);
            WireDialogueNpc(guideNpc, dialogueUI, controller);
            LoadOrCreateMetaUpgrades();
            WireAltarNpc(altarNpc, altarPanel, controller);
            var storyData = LoadOrCreateStoryData();
            WireStoryNpc(storyNpc, storyData, dialogueUI, controller);
            WireRelicShopNpc(relicShopNpc, relicShopPanel, controller);

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
            // 첫 프레임 전 위치. 실제 추적 오프셋은 WireCamera가 정한다(발밑 기준점 보정 포함).
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.orthographic = true;
            cam.orthographicSize = Abyss.Runtime.Camera.PixelScale.OrthographicSize;
            go.AddComponent<AudioListener>();
            return go.AddComponent<PlayerCameraFollow>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // 바닥 판의 중심과 두께 — 플레이어 스폰 높이를 여기서 파생시킨다.
        // 값을 양쪽에 적어 두면 바닥을 옮길 때 캐릭터만 공중에 남는다.
        private const float FloorCenterY = -3.5f;
        private const float FloorThickness = 1f;
        private static float FloorTopY => FloorCenterY + FloorThickness * 0.5f;

        private static void CreateGround()
        {
            var root = new GameObject("Environment");
            int groundLayer = EditorPlatformFactory.GetGroundLayer();
            var sprite = EditorPlatformFactory.LoadWhiteSquare();
            var mat = EditorPlatformFactory.GetOrCreateFrictionlessMaterial();
            var color = EditorPlatformFactory.DefaultPlatformColor;

            EditorPlatformFactory.CreatePlatform(root.transform, "Floor", new Vector2(0f, FloorCenterY), new Vector2(30f, FloorThickness), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallLeft", new Vector2(-14f, 0f), new Vector2(1f, 8f), groundLayer, sprite, mat, color);
            EditorPlatformFactory.CreatePlatform(root.transform, "WallRight", new Vector2(14f, 0f), new Vector2(1f, 8f), groundLayer, sprite, mat, color);
        }

        /// <summary>
        /// 로비 플레이어 생성. <b>원점 = 발밑</b>이며 이는 Run 의 <c>Player.prefab</c> 규약과 같다 —
        /// 폼 스프라이트의 피벗이 발밑에 실측돼 있어(<c>prepare_form_sprite.py</c>),
        /// 원점을 발에 두어야 같은 그림이 두 씬에서 같은 높이로 선다.
        /// 그래서 콜라이더는 크기 1×2 에 offset (0,1) 로 원점 위에 세운다(Run 과 동일).
        /// </summary>
        private static GameObject CreatePlayer(
            FormData defaultForm,
            out LobbyPlayerController controller, out PlayerInteractor interactor, out Transform groundCheck)
        {
            var go = new GameObject("LobbyPlayer");
            // 바닥 바로 위에서 시작해 한 프레임 만에 내려앉는다. 정확히 바닥면에 두면 시작부터 겹친다.
            go.transform.position = new Vector3(0f, FloorTopY + 0.1f, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 물리(바닥/벽 충돌). 발밑 원점이라 offset 으로 몸통을 위로 올린다.
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 2f);
            box.offset = new Vector2(0f, 1f);

            // 상호작용 감지용 트리거(넓은 반경). Run 과 같이 발밑 중심.
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;

            controller = go.AddComponent<LobbyPlayerController>();
            interactor = go.AddComponent<PlayerInteractor>();

            var input = go.AddComponent<PlayerInput>();
            ConfigurePlayerInput(input);

            CreatePlayerVisual(go.transform, defaultForm);

            // 지면 체크 자식. 원점이 이미 발이라 살짝만 내린다(Run 의 Player.prefab 과 같은 -0.06).
            var gc = new GameObject("GroundCheck");
            gc.transform.SetParent(go.transform, false);
            gc.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            groundCheck = gc.transform;

            return go;
        }

        /// <summary>
        /// 그림을 담는 Visual 자식. Run 의 Player.prefab 과 같은 구성이다.
        ///
        /// 루트가 아니라 자식에 두는 이유: <see cref="LobbyPlayerController"/>가 좌우를 뒤집을 때
        /// 루트의 <c>localScale.x</c>를 쓰는데, 스프라이트가 루트에 있으면 그림과 콜라이더가
        /// 한 덩어리로 묶여 앞으로 폼별 크기·오프셋을 따로 줄 자리가 없다.
        /// </summary>
        private static void CreatePlayerVisual(Transform parent, FormData defaultForm)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            // 초기값은 예전 그대로. LobbyFormVisual 이 이것을 폴백으로 집어 가므로
            // 그림 없는 폼이 생겨도 화면에서 사라지지 않는다.
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = new Color(0.4f, 0.7f, 1f);

            var visual = go.AddComponent<LobbyFormVisual>();
            var so = new SerializedObject(visual);
            SetObject(so, "target", sr);
            SetObject(so, "defaultForm", defaultForm);
            so.ApplyModifiedProperties();

            if (defaultForm == null)
            {
                Debug.LogWarning("[LobbySceneBuilder] 기본 폼 미해석 — 시작 폼을 고르기 전까지 로비 캐릭터가 흰 사각형으로 남는다.");
            }
        }

        /// <summary>
        /// 시작 폼을 안 고르고 포털로 들어갔을 때 <b>런이 실제로 시작하는 폼</b>을 찾는다.
        /// 출처는 Player 프리팹의 <c>FormController.slots[activeSlot]</c> — 그것이 사실이기 때문이다.
        /// 여기서 목록의 첫 폼을 쓰면 로비 표시와 런 실제가 갈라진다(정렬 순서와 프리팹 배선은 무관하다).
        /// 프리팹을 못 읽으면 예전 동작(첫 폼)으로 물러난다 — 빌더가 멈출 일은 아니다.
        /// </summary>
        private static FormData ResolveDefaultForm(FormData[] forms)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{AbyssPaths.PlayerPrefabs}/Player.prefab");
            var controller = prefab != null ? prefab.GetComponentInChildren<FormController>(true) : null;
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                var slots = so.FindProperty("slots");
                var activeProp = so.FindProperty("activeSlot");
                int active = activeProp != null ? activeProp.intValue : 0;
                if (slots != null && active >= 0 && active < slots.arraySize
                    && slots.GetArrayElementAtIndex(active).objectReferenceValue is FormData form)
                {
                    return form;
                }
            }

            Debug.LogWarning("[LobbySceneBuilder] Player.prefab 의 시작 폼을 못 읽었다 — 목록 첫 폼으로 대체. "
                           + "폼을 안 고르고 던전에 들어가면 로비 캐릭터와 다른 폼으로 시작할 수 있다.");
            return forms.Length > 0 ? forms[0] : null;
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

        private static PanelRefs CreateFormSelectPanel(Canvas canvas, FormData[] forms, FormData defaultForm)
        {
            var panel = canvas.gameObject.AddComponent<FormSelectPanel>();

            // 토글 대상 루트(전체 화면 반투명 배경)
            var root = CreateRect(canvas.transform, "FormSelectRoot", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch((RectTransform)root.transform);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            // N개 폼을 중앙 정렬(폼 수가 늘어도 자동 대응). 간격 285 + 버튼폭 260 → 25px 여백.
            const float formSpacing = 285f;
            const float formButtonWidth = 260f;
            // 버튼 전체 span + 좌우 여백(각 50)이 박스 안에 들어오도록 폭을 확장(최소 940).
            float boxWidth = Mathf.Max(940f, (forms.Length - 1) * formSpacing + formButtonWidth + 100f);

            var box = CreateRect(root.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(boxWidth, 460));
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            CreateText(box.transform, "Title", "시작 폼 선택", 30, new Vector2(0, -50), new Vector2(700, 48), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));

            // 폼 버튼
            var formButtons = new List<Object>();
            float fx = -(forms.Length - 1) * formSpacing / 2f;
            foreach (var f in forms)
            {
                formButtons.Add(CreateFormButton(box.transform, f, new Vector2(fx, 30)));
                fx += formSpacing;
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
            // 로비 캐릭터와 같은 기본 폼 — 강조된 버튼과 서 있는 그림이 어긋나지 않게.
            SetObject(so, "defaultForm", defaultForm);
            so.ApplyModifiedProperties();

            return new PanelRefs { component = panel };
        }

        private static Button CreateFormButton(Transform parent, FormData form, Vector2 pos)
        {
            var go = CreateRect(parent, "FormButton_" + form.formId, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(260, 150));
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

            // 추적 기준점이 몸통 중앙에서 발밑으로 1유닛 내려갔다(폼 스프라이트 피벗 규약).
            // 오프셋을 그대로 두면 화면 전체가 1유닛 내려가 바닥 아래 빈 공간이 늘어난다.
            // 카메라가 잡던 그림은 그대로 두고 싶으므로 기준점이 내려간 만큼 올려 상쇄한다.
            var offset = so.FindProperty("offset");
            if (offset != null) offset.vector3Value = new Vector3(0f, 3f, -10f);
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

        /// <summary>
        /// 정비 NPC(각인사) 배선. 폼 선택 패널에 더해 내력 재생용 StoryData·DialogueUI를 물린다.
        /// DialogueUI는 기록자·안내자와 공용이다 — 셋이 동시에 열릴 일이 없어 인스턴스를 나눌 이유가 없다.
        /// </summary>
        private static void WireServiceNpc(
            ServiceNpc npc, FormSelectPanel panel, LobbyPlayerController player,
            StoryData engraverStory, DialogueUI dialogueUI)
        {
            var so = new SerializedObject(npc);
            SetObject(so, "formSelectPanel", panel);
            SetObject(so, "player", player);
            SetObject(so, "story", engraverStory);
            SetObject(so, "dialogueUI", dialogueUI);
            SetString(so, "speakerId", StorySpeakerIds.Engraver);
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
            // 폴더가 SoT: Forms 폴더의 모든 FormData를 로드해 새 폼이 자동 편입되게 한다(FormCatalog와 동형).
            // 하드코딩 목록을 두면 새 폼마다 이 빌더를 고쳐야 하므로 폴더 스캔으로 대체.
            var list = new List<FormData>();
            string[] guids = AssetDatabase.FindAssets("t:FormData", new[] { AbyssPaths.Forms });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var form = AssetDatabase.LoadAssetAtPath<FormData>(path);
                if (form != null) list.Add(form);
            }

            if (list.Count == 0)
                Debug.LogWarning($"[LobbySceneBuilder] {AbyssPaths.Forms}에 FormData 없음 — '{AbyssMenu.GenerateContent}' 먼저 실행.");

            // 파일명 기준 정렬로 빌드 재현성 확보(FindAssets 순서는 비결정적).
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static void SetObject(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.stringValue = value;
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
