#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Runtime.Camera;
using Abyss.Runtime.Dialogue;
using Abyss.Runtime.Form;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Stage;
using Abyss.Runtime.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Abyss.EditorTools.SceneBuilderUtil;
using static Abyss.Runtime.UI.UiFactory;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 허브 로비 씬을 생성하고 빌드 설정에 등록한다. Abyss Tools 창의 <see cref="AbyssToolNames.BuildLobbyScene"/>.
    /// 구성: 바닥/벽 + Run 과 같은 공유 플레이어(PlayerCharacter, 로비 맥락) + 카메라 추적 + 던전 포털 + 폼 선택 패널.
    /// 씬 분리 3단계(H1) — 스컬식 플레이 가능 허브 골격.
    /// </summary>
    public static partial class LobbySceneBuilder
    {
        // 폼 버튼 강조 색(LobbyController/FormSelectPanel과 일치)
        private static readonly Color FormNormal = new Color(0.18f, 0.18f, 0.22f);

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
            var ground = CreateGround();

            // 폼 목록과 기본 폼은 한 번만 정하고 여러 곳에 나눠 준다.
            // 각자 로드하면 "패널이 강조하는 폼"과 "서 있는 캐릭터"가 조용히 갈라질 수 있다.
            var forms = LoadForms();
            var defaultForm = ResolveDefaultForm(forms);

            // 플레이어는 Run 의 Player.prefab 과 같은 PlayerCharacter·FSM·폼 컨트롤러다(LobbySceneBuilder.Npc).
            var player = CreatePlayer(defaultForm, out var controller, out var interactor);
            var portal = CreatePortal();

            var canvas = CreateSceneCanvas("LobbyCanvas");
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
            // 배경 높이는 카메라 추적 offset 에서 파생하므로 카메라 배선 뒤에 입힌다.
            bool isGatePainted = ApplyEnvironmentArt(ground, cameraFollow);
            WireInteractor(interactor, prompt);
            WirePortal(portal, isGatePainted);
            // 각인사 내력은 정비 NPC가 폼 선택 패널보다 먼저 재생하므로 와이어링 전에 만들어 둔다.
            var engraverData = LoadOrCreateEngraverStoryData();
            WireServiceNpc(serviceNpc, panel.component, controller, engraverData, dialogueUI);
            WireDialogueNpc(guideNpc, dialogueUI, controller);
            LoadOrCreateMetaUpgrades();
            WireAltarNpc(altarNpc, altarPanel, controller);
            var storyData = LoadOrCreateStoryData();
            WireStoryNpc(storyNpc, storyData, dialogueUI, controller);
            WireRelicShopNpc(relicShopNpc, relicShopPanel, controller);

            LobbyNpcArtBuilder.ApplyToScene(scene);

            EnsureScenesFolder();
            EditorSceneManager.SaveScene(scene, AbyssPaths.LobbyScene);
            RegisterSceneAfterBootstrap(AbyssPaths.LobbyScene, nameof(LobbySceneBuilder));
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
            // C3: Run 과 같은 C 프로필. 프로필이 붙어 있으면 Follow 가 Run 축별 추적(C2)을 쓴다.
            cam.orthographicSize = RunCameraProfile.OrthographicSizeFor(RunCameraProfileId.C_Wide);
            go.AddComponent<RunCameraProfile>();
            go.AddComponent<AudioListener>();
            return go.AddComponent<PlayerCameraFollow>();
        }

        // 바닥·벽(CreateGround)과 환경 아트는 LobbySceneBuilder.Environment.cs.

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
            return CreateNpcObject<DungeonPortal>("DungeonPortal", new Vector2(PortalX, -2.5f), new Vector2(1.5f, 2f), new Color(0.6f, 0.3f, 0.8f));
        }

        private static ServiceNpc CreateServiceNpc()
        {
            return CreateNpcObject<ServiceNpc>("ServiceNpc", new Vector2(3f, -2.5f), new Vector2(1f, 2f), new Color(0.4f, 0.8f, 0.6f));
        }

        // ───────────────────────── UI ─────────────────────────

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
            const float FORM_SPACING = 285f;
            const float FORM_BUTTON_WIDTH = 260f;
            // 버튼 전체 span + 좌우 여백(각 50)이 박스 안에 들어오도록 폭을 확장(최소 940).
            float boxWidth = Mathf.Max(940f, (forms.Length - 1) * FORM_SPACING + FORM_BUTTON_WIDTH + 100f);

            var box = CreateRect(root.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(boxWidth, 460));
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.1f, 0.1f, 0.14f, 0.98f);

            var title = CreateText(box.transform, "Title", "시작 폼 선택", 30, new Vector2(0, -50), new Vector2(700, 48), TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));

            // 폼 버튼
            var formButtons = new List<Object>();
            float fx = -(forms.Length - 1) * FORM_SPACING / 2f;
            foreach (var f in forms)
            {
                formButtons.Add(CreateFormButton(box.transform, f, new Vector2(fx, 30)));
                fx += FORM_SPACING;
            }

            var (confirm, _) = CreateButton(box.transform, "ConfirmButton", new Vector2(-130, -160), new Vector2(220, 64), new Color(0.25f, 0.4f, 0.25f), "선택 (Enter)");
            var (cancel, _) = CreateButton(box.transform, "CancelButton", new Vector2(130, -160), new Vector2(220, 64), new Color(0.4f, 0.25f, 0.25f), "취소 (Esc)");

            root.SetActive(false);

            // 와이어
            var so = new SerializedObject(panel);
            SetObject(so, "root", root);
            SetObject(so, "confirmButton", confirm);
            SetObject(so, "cancelButton", cancel);
            SetObject(so, "titleLabel", title);
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

        private static void WireInteractor(PlayerInteractor interactor, Text prompt)
        {
            var so = new SerializedObject(interactor);
            SetObject(so, "promptLabel", prompt);
            so.ApplyModifiedProperties();
        }

        private static void WirePortal(DungeonPortal portal, bool isGatePainted)
        {
            // 그림 속 아치가 포털이다 — 자리표시자 사각형은 끄고 트리거만 남긴다.
            if (isGatePainted) portal.GetComponent<SpriteRenderer>().enabled = false;
            var so = new SerializedObject(portal);
            var p = so.FindProperty("promptKey");
            if (p != null) p.stringValue = StringKey.Portal_Prompt;
            // 입구 목적은 Run 씬 StageDirector와 같은 시퀀스 에셋을 읽는다(별도 Stage1 표를 두지 않는다).
            var sequence = AssetDatabase.LoadAssetAtPath<StageSequenceData>(AbyssPaths.Stages + "/MainRunSequence.asset");
            if (sequence == null) Debug.LogWarning("[LobbySceneBuilder] MainRunSequence 없음 — 입구 목적 표시 생략");
            SetObject(so, "previewSequence", sequence);
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
                Debug.LogWarning($"[LobbySceneBuilder] {AbyssPaths.Forms}에 FormData 없음 — '{AbyssToolNames.GenerateContent}' 먼저 실행.");

            // 파일명 기준 정렬로 빌드 재현성 확보(FindAssets 순서는 비결정적).
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }
    }
}
#endif
