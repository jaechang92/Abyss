#if UNITY_EDITOR
using Abyss.Runtime.Draft;
using Abyss.Runtime.Player;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// HUD 루트(HUDPresenter) 자식 UI를 일괄 생성·연결하는 에디터 툴.
    /// 사용법: Hierarchy에서 HUDPresenter가 붙은 GameObject 선택 → 메뉴 <see cref="AbyssMenu.BuildHud"/>.
    /// 자식이 이미 있으면 삭제 후 재생성(확인 다이얼로그).
    /// 모달 생성 파트는 HudBuilder.Modals.cs로 분리돼 있다(500줄 규칙).
    /// </summary>
    public static partial class HudBuilder
    {
        private const string UndoLabel = "Build HUD Children";

        [MenuItem(AbyssMenu.BuildHud)]
        public static void Build()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("HUD Builder", "HUD 루트 GameObject를 선택하고 다시 실행하세요.", "확인");
                return;
            }

            var hud = go.GetComponent<HUDPresenter>();
            if (hud == null)
            {
                EditorUtility.DisplayDialog("HUD Builder", $"선택한 '{go.name}'에 HUDPresenter 컴포넌트가 없습니다.", "확인");
                return;
            }

            if (go.transform.childCount > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "HUD Builder",
                    $"'{go.name}'에 이미 {go.transform.childCount}개 자식이 있습니다. 모두 제거하고 재생성할까요?",
                    "재생성", "취소");
                if (!confirm) return;

                for (int i = go.transform.childCount - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
                }
            }

            Undo.RegisterCompleteObjectUndo(hud, UndoLabel);

            var healthBar = CreateHealthBar(go.transform);
            var formSlot = CreateFormSlot(go.transform);
            var skillSlot0 = CreateSkillSlot(go.transform, "SkillSlot0", new Vector2(24, 24));
            var skillSlot1 = CreateSkillSlot(go.transform, "SkillSlot1", new Vector2(104, 24));
            CreateFormBiasWarning(go.transform); // 편향 경고 배너(자체 이벤트 구독 — HUDPresenter 배선 불필요)
            CreateSynergyCounter(go.transform); // 시너지 축 카운터(자체 이벤트 구독 — HUDPresenter 배선 불필요)
            var modal = CreateReplacementModal(go.transform);
            var formModal = CreateFormRewardModal(go.transform);
            CreateInteractPrompt(go.transform); // 근접 상호작용 프롬프트(비활성). PlayerInteractor.promptLabel 배선은 FormAltarBuilder가 담당
            CreatePausePanel(go.transform); // 일시정지 패널(자체 이벤트 구독 — HUDPresenter 배선 불필요)

            WireHUDPresenter(hud, healthBar, formSlot, new[] { skillSlot0, skillSlot1 }, modal);
            SetPrivateField(hud, "formRewardModal", formModal);
            WireSceneReferences(hud);

            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(go);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            Debug.Log($"[HudBuilder] HUD 자식 UI 생성 완료: HealthBar / FormSlot / FormBiasWarning(비활성) / SynergyCounter(비활성) / SkillSlot ×2 / ReplacementModal(비활성) / PausePanel(비활성)");
            Selection.activeGameObject = go;
        }

        // ==================== Health Bar ====================
        private static HealthBarPresenter CreateHealthBar(Transform parent)
        {
            var root = CreateRectChild(parent, "HealthBar", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(320, 28));

            var bg = CreateRectChild(root.transform, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            StretchFill((RectTransform)bg.transform);

            var fill = CreateRectChild(root.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)fill.transform);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = GetUISprite();
            fillImg.color = new Color(0.9f, 0.22f, 0.28f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            var textGo = CreateRectChild(root.transform, "HPText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)textGo.transform);
            var text = textGo.AddComponent<Text>();
            ApplyDefaultFont(text);
            text.text = "100/100";
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            var presenter = root.AddComponent<HealthBarPresenter>();
            SetPrivateField(presenter, "fillImage", fillImg);
            SetPrivateField(presenter, "hpText", text);
            return presenter;
        }

        // ==================== Form Slot ====================
        private static FormSlotPresenter CreateFormSlot(Transform parent)
        {
            var root = CreateRectChild(parent, "FormSlot", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -64), new Vector2(200, 80));

            var current = CreateIcon(root.transform, "CurrentFormIcon", new Vector2(8, -8), new Vector2(64, 64), new Color(0.35f, 0.35f, 0.4f));
            var other = CreateIcon(root.transform, "OtherFormIcon", new Vector2(80, -20), new Vector2(48, 48), new Color(0.25f, 0.25f, 0.3f, 0.7f));

            var cooldownGo = CreateRectChild(root.transform, "CooldownFill", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -8), new Vector2(64, 64));
            var cooldownImg = cooldownGo.AddComponent<Image>();
            cooldownImg.sprite = GetUISprite();
            cooldownImg.color = new Color(0f, 0f, 0f, 0.55f);
            cooldownImg.type = Image.Type.Filled;
            cooldownImg.fillMethod = Image.FillMethod.Radial360;
            cooldownImg.fillOrigin = (int)Image.Origin360.Top;
            cooldownImg.fillClockwise = true;
            cooldownImg.fillAmount = 0f;

            var presenter = root.AddComponent<FormSlotPresenter>();
            SetPrivateField(presenter, "currentFormIcon", current);
            SetPrivateField(presenter, "otherFormIcon", other);
            SetPrivateField(presenter, "cooldownFill", cooldownImg);
            return presenter;
        }

        // ==================== Skill Slot ====================
        private static SkillSlotPresenter CreateSkillSlot(Transform parent, string name, Vector2 anchoredOffsetFromBottomLeft)
        {
            var root = CreateRectChild(parent, name, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), anchoredOffsetFromBottomLeft, new Vector2(64, 64));

            var icon = CreateIcon(root.transform, "Icon", Vector2.zero, new Vector2(64, 64), new Color(0.35f, 0.35f, 0.4f));
            ((RectTransform)icon.transform).anchorMin = Vector2.zero;
            ((RectTransform)icon.transform).anchorMax = Vector2.one;
            ((RectTransform)icon.transform).offsetMin = Vector2.zero;
            ((RectTransform)icon.transform).offsetMax = Vector2.zero;

            var cooldownGo = CreateRectChild(root.transform, "CooldownFill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)cooldownGo.transform);
            var cooldownImg = cooldownGo.AddComponent<Image>();
            cooldownImg.sprite = GetUISprite();
            cooldownImg.color = new Color(0f, 0f, 0f, 0.55f);
            cooldownImg.type = Image.Type.Filled;
            cooldownImg.fillMethod = Image.FillMethod.Vertical;
            cooldownImg.fillAmount = 0f;

            var labelGo = CreateRectChild(root.transform, "Label", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, -14), new Vector2(0, 14));
            var label = labelGo.AddComponent<Text>();
            ApplyDefaultFont(label);
            label.text = "—";
            label.fontSize = 10;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            var presenter = root.AddComponent<SkillSlotPresenter>();
            SetPrivateField(presenter, "iconImage", icon);
            SetPrivateField(presenter, "cooldownFill", cooldownImg);
            SetPrivateField(presenter, "labelText", label);
            return presenter;
        }

        // ==================== Form Bias Warning ====================
        /// <summary>
        /// 폼 편향 경고 배너(FormSlot 바로 아래). Presenter는 항상 활성 상태로 두고 Body만 토글해
        /// 숨김 중에도 GameEvents 구독이 유지되게 한다(자기 자신을 끄면 다시 켜줄 주체가 없다).
        /// </summary>
        private static FormBiasWarningPresenter CreateFormBiasWarning(Transform parent)
        {
            var root = CreateRectChild(parent, "FormBiasWarning", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -152), new Vector2(360, 48));

            var body = CreateRectChild(root.transform, "Body", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)body.transform);
            var bodyImg = body.AddComponent<Image>();
            bodyImg.color = new Color(0.32f, 0.18f, 0.06f, 0.82f);
            bodyImg.raycastTarget = false;

            var labelGo = CreateRectChild(body.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)labelGo.transform);
            var label = labelGo.AddComponent<Text>();
            ApplyDefaultFont(label);
            label.text = "폼 편향 경고";
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.86f, 0.62f);
            label.raycastTarget = false;

            var presenter = root.AddComponent<FormBiasWarningPresenter>();
            SetPrivateField(presenter, "root", body);
            SetPrivateField(presenter, "label", label);

            body.SetActive(false);
            return presenter;
        }

        // ==================== Synergy Counter ====================
        /// <summary>
        /// 시너지 축 카운터(편향 경고 배너 바로 아래). 축 개수가 가변이라 칩을 프리팹 없이
        /// 템플릿 복제로 만든다 — 빌더는 컨테이너와 비활성 템플릿만 놓고, 개수 조절은 Presenter가 한다.
        /// FormBiasWarning과 동일하게 Presenter는 항상 활성이고 Body만 토글해 구독을 유지한다.
        /// </summary>
        private static SynergyCounterPresenter CreateSynergyCounter(Transform parent)
        {
            var root = CreateRectChild(parent, "SynergyCounter", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -208), new Vector2(800, 32));

            var body = CreateRectChild(root.transform, "Body", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)body.transform);

            var layout = body.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var template = CreateSynergyChipTemplate(body.transform);

            var presenter = root.AddComponent<SynergyCounterPresenter>();
            SetPrivateField(presenter, "root", body);
            SetPrivateField(presenter, "chipContainer", (RectTransform)body.transform);
            SetPrivateField(presenter, "chipTemplate", template);

            body.SetActive(false); // 보유 축이 생기면 Presenter가 켠다
            return presenter;
        }

        /// <summary>
        /// 칩 1개의 복제 원본(비활성). 폭은 LayoutElement로 고정한다 — 축 이름 길이가 제각각이라
        /// ContentSizeFitter를 부모 LayoutGroup 안에서 쓰면 재계산이 불안정하다.
        /// </summary>
        private static GameObject CreateSynergyChipTemplate(Transform parent)
        {
            var chip = CreateRectChild(parent, "ChipTemplate", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(124, 28));
            var bg = chip.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.14f, 0.70f);
            bg.raycastTarget = false;

            var element = chip.AddComponent<LayoutElement>();
            element.preferredWidth = 124f;
            element.preferredHeight = 28f;

            var labelGo = CreateRectChild(chip.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFill((RectTransform)labelGo.transform);
            var label = labelGo.AddComponent<Text>();
            ApplyDefaultFont(label);
            label.text = "[축] 0";
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; // 긴 축 이름이 줄바꿈되지 않게
            label.color = Color.white;
            label.raycastTarget = false;

            chip.SetActive(false);
            return chip;
        }

        // ==================== Wiring ====================
        private static void WireHUDPresenter(HUDPresenter hud, HealthBarPresenter healthBar, FormSlotPresenter formSlot, SkillSlotPresenter[] skillSlots, ReplacementModal modal)
        {
            SetPrivateField(hud, "healthBar", healthBar);
            SetPrivateField(hud, "formSlot", formSlot);
            SetPrivateFieldArray(hud, "skillSlots", skillSlots);
            SetPrivateField(hud, "replacementModal", modal);
        }

        /// <summary>
        /// 씬에 존재하는 PlayerCharacter·DraftSessionController를 HUDPresenter에 자동 연결.
        /// 빌드 시점에 미리 채워 런타임 FindAnyObjectByType 폴백 의존을 제거한다.
        /// 셋업 순서상 대상이 아직 없을 수 있으므로, 못 찾으면 경고만 남기고 빌드는 계속한다.
        /// </summary>
        private static void WireSceneReferences(HUDPresenter hud)
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            if (player != null)
            {
                SetPrivateField(hud, "player", player);
                Debug.Log($"[HudBuilder] player 자동 연결: {player.name}");
            }
            else
            {
                Debug.LogWarning("[HudBuilder] 씬에서 PlayerCharacter를 찾지 못했습니다. HUDPresenter.player는 런타임 폴백 또는 수동 연결이 필요합니다.");
            }

            var draftSession = Object.FindAnyObjectByType<DraftSessionController>(FindObjectsInactive.Include);
            if (draftSession != null)
            {
                SetPrivateField(hud, "draftSession", draftSession);
                Debug.Log($"[HudBuilder] draftSession 자동 연결: {draftSession.name}");
            }
            else
            {
                Debug.LogWarning("[HudBuilder] 씬에서 DraftSessionController를 찾지 못했습니다. HUDPresenter.draftSession은 수동 연결이 필요합니다.");
            }
        }

        /// <summary>
        /// 근접 상호작용 프롬프트 Text(하단 중앙, 기본 비활성). 로비 InteractPrompt와 동형.
        /// 근접 시 표시·문구 갱신은 PlayerInteractor가 promptLabel을 토글해 담당한다.
        /// FormAltarBuilder가 기존 HUD에 프롬프트가 없을 때 폴백 생성용으로도 호출한다(internal).
        /// </summary>
        internal static Text CreateInteractPrompt(Transform parent)
        {
            var go = CreateRectChild(parent, "InteractPrompt",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(700f, 48f));
            var t = go.AddComponent<Text>();
            ApplyDefaultFont(t);
            t.text = "폼 획득 (G)";
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.95f, 0.6f);
            go.SetActive(false); // 근접 시에만 표시(PlayerInteractor가 토글)
            return t;
        }

        // ==================== Helpers ====================
        private static GameObject CreateRectChild(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return go;
        }

        private static void StretchFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image CreateIcon(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = CreateRectChild(parent, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), anchoredPosition, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void ApplyDefaultFont(Text text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
        }

        private static Sprite uiSpriteCache;

        /// <summary>
        /// 빌트인 UI 스프라이트. Image.Type.Filled는 Source Image(sprite)가 없으면 fillAmount가
        /// 렌더링되지 않으므로, HP·쿨다운 등 Filled 이미지에 반드시 할당해야 한다.
        /// </summary>
        private static Sprite GetUISprite()
        {
            if (uiSpriteCache == null)
            {
                uiSpriteCache = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }
            return uiSpriteCache;
        }

        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[HudBuilder] {target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetPrivateFieldArray<T>(Object target, string fieldName, T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[HudBuilder] {target.GetType().Name}.{fieldName} 배열 필드를 찾지 못했습니다.");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
