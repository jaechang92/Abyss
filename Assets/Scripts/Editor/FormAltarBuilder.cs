#if UNITY_EDITOR
using Abyss.Runtime.Form;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Player;
using Abyss.Runtime.Stage;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 활성 씬(Run)에 폼 보상 제단(FormAltar)을 배치하는 에디터 툴.
    /// Run 씬은 전용 씬 빌더가 없고 "in Active Scene" 툴들로 구성되므로 같은 관례를 따른다
    /// (StageDirector/RoomLayouts/Platforms 배치와 동형).
    ///
    /// 함께 수행:
    /// - Run 플레이어에 PlayerInteractor가 있는지 <b>확인</b>한다(없으면 에러 로그 — 붙이지는 않는다).
    /// - PlayerInteractor.promptLabel을 HUD의 InteractPrompt Text에 배선한다(근접 안내 문구 표시).
    ///
    /// 🔴 <b>2026-08-26 판단 번복 — 씬 오버라이드를 그만뒀다.</b>
    /// 예전에는 여기서 씬 인스턴스에 직접 붙였고, 이유는 <i>"프리팹을 force 재생성하면 GUID가 바뀌어
    /// 씬 참조가 끊긴다"</i>였다. 그 우려 자체는 맞다(<c>DeleteAsset</c> 후 재생성 경로).
    /// 그런데 <b>피하려던 것보다 큰 문제를 만들었다</b> — 오버라이드는 프리팹이나 씬을 다시 빌드하면
    /// <b>조용히 사라진다.</b> GUID가 끊기면 콘솔에 미싱으로 뜨지만,
    /// 오버라이드가 날아가면 <b>아무 표시도 없다.</b> 제단 앞에 서도 그냥 아무 일이 없다.
    /// 실제로 그렇게 없어져서 제단이 반응하지 않았고, 원인을 찾는 데 시간이 걸렸다.
    /// → 컴포넌트는 <b>Player.prefab</b>(PrefabBuilder)에 두고, 여기서는 확인만 한다.
    ///
    /// 멱등: 이미 있으면 건너뛰고 재실행 시 누락분만 복구한다. 단 <b>스프라이트에 종속된 값</b>
    /// (트리거 콜라이더 크기)은 데이터 구동이므로 재실행으로 갱신한다.
    /// 씬 조회는 반드시 <c>FindObjectsInactive.Include</c>로 한다 — 제단도 HUD 모달도 비활성으로
    /// 저장되므로 기본 조회로는 못 찾고, 그러면 "없다"고 판정해 중복 생성한다.
    /// </summary>
    public static class FormAltarBuilder
    {
        private const string AltarName = "FormAltar";
        private const string RewardFormFile = "AncientShield.asset"; // 기본 슬롯(암흑검사/공허궁수) 밖의 3번째 폼
        private const string PromptName = "InteractPrompt";

        // 제단 스프라이트 32x48 @PPU32 = 1x1.5 유닛. 트리거를 여기에 맞춘다.
        private static readonly Vector2 AltarTriggerSize = new(1f, 1.5f);

        [MenuItem(AbyssMenu.BuildFormAltar)]
        public static void BuildFormAltarInActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("FormAltarBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            var rewardForm = AssetDatabase.LoadAssetAtPath<FormData>($"{AbyssPaths.Forms}/{RewardFormFile}");
            if (rewardForm == null)
            {
                EditorUtility.DisplayDialog(
                    "FormAltarBuilder 실패",
                    $"보상 FormData 미발견: {AbyssPaths.Forms}/{RewardFormFile}\n먼저 '{AbyssMenu.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            var interactor = EnsureRunPlayerInteractor();
            WireInteractPrompt(interactor);
            var altar = CreateOrReuseAltar(rewardForm);

            WireStageDirector(altar.GetComponent<FormAltar>());
            altar.SetActive(false); // 룸 게이트: 보상 룸 클리어 시 StageDirector가 활성화. 기본 숨김.

            EditorUtility.SetDirty(altar);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = altar;
            EditorGUIUtility.PingObject(altar);

            Debug.Log($"[FormAltarBuilder] 폼 제단 배치 완료 — 보상 폼='{rewardForm.displayName}' ({rewardForm.formId}), 기본 비활성(룸 게이트)");
        }

        /// <summary>
        /// Run 플레이어의 인터랙터를 찾는다. <b>없으면 만들지 않고 알린다.</b>
        ///
        /// 🔴 <b>2026-08-26 — 씬 오버라이드로 붙이던 것을 그만뒀다.</b>
        /// 예전에는 여기서 <c>Undo.AddComponent</c>로 씬 인스턴스에 직접 붙였는데,
        /// 그 오버라이드는 <b>프리팹을 다시 만들거나 씬을 다시 빌드하면 조용히 사라진다.</b>
        /// 실제로 그렇게 없어져서 런 중 폼 제단이 반응하지 않았고,
        /// 오류도 경고도 안 나서 <b>제단 앞에 서도 아무 일이 없는 것</b>으로만 드러났다.
        ///
        /// 📌 이제 <c>PlayerInteractor</c>는 <b>Player.prefab</b>에 들어간다(<c>PrefabBuilder</c>).
        /// 여기서 다시 오버라이드를 만들면 같은 함정이 그대로 돌아오므로, 안내만 하고 멈춘다.
        /// </summary>
        private static PlayerInteractor EnsureRunPlayerInteractor()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[FormAltarBuilder] 씬에서 PlayerCharacter 미발견 — 인터랙터 확인 생략. Run 씬에서 실행하세요.");
                return null;
            }

            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                Debug.LogError(
                    $"[FormAltarBuilder] Run 플레이어 '{player.gameObject.name}'에 PlayerInteractor가 없다 — 제단이 반응하지 않는다.\n" +
                    $"  → 메뉴 '{AbyssMenu.GeneratePrefabs}'(또는 '{AbyssMenu.GenerateRebuildPlayer}')를 먼저 실행해 프리팹에 심을 것.\n" +
                    "  씬 오버라이드로 붙이지 않는 이유: 프리팹·씬을 다시 빌드하면 날아가 같은 문제가 재발한다.");
            }
            return interactor;
        }

        /// <summary>PlayerInteractor.promptLabel을 HUD의 InteractPrompt Text에 연결한다(없으면 폴백 생성).</summary>
        private static void WireInteractPrompt(PlayerInteractor interactor)
        {
            if (interactor == null) return;

            var so = new SerializedObject(interactor);
            var promptProp = so.FindProperty("promptLabel");
            if (promptProp == null) return;
            if (promptProp.objectReferenceValue != null) return; // 이미 연결됨

            var hud = Object.FindAnyObjectByType<HUDPresenter>(FindObjectsInactive.Include);
            if (hud == null)
            {
                Debug.LogWarning("[FormAltarBuilder] HUDPresenter 미발견 — 프롬프트 배선 생략. HUD 빌드 후 재실행하세요.");
                return;
            }

            var prompt = FindPrompt(hud.transform) ?? HudBuilder.CreateInteractPrompt(hud.transform);
            promptProp.objectReferenceValue = prompt;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(interactor);
            Debug.Log("[FormAltarBuilder] PlayerInteractor.promptLabel → HUD InteractPrompt 연결.");
        }

        private static Text FindPrompt(Transform hudRoot)
        {
            foreach (var t in hudRoot.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == PromptName) return t;
            }
            return null;
        }

        /// <summary>StageDirector.formAltar에 제단을 배선한다(보상 룸 게이트가 이 제단을 활성화).</summary>
        private static void WireStageDirector(FormAltar altar)
        {
            var director = Object.FindAnyObjectByType<StageDirector>();
            if (director == null)
            {
                Debug.LogWarning("[FormAltarBuilder] StageDirector 미발견 — formAltar 배선 생략. StageDirector 빌드 후 재실행하세요.");
                return;
            }

            var so = new SerializedObject(director);
            var prop = so.FindProperty("formAltar");
            if (prop == null) return;
            prop.objectReferenceValue = altar;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
            Debug.Log("[FormAltarBuilder] StageDirector.formAltar → FormAltar 배선.");
        }

        /// <summary>기존 FormAltar가 있으면 재사용, 없으면 생성. 보상 폼·스프라이트를 주입한다.</summary>
        private static GameObject CreateOrReuseAltar(FormData rewardForm)
        {
            // 비활성 포함 검색이 필수다 — 이 빌더는 마지막에 제단을 SetActive(false)로 끄고(룸 게이트),
            // FindAnyObjectByType의 기본값은 비활성 오브젝트를 제외한다. 기본값으로 두면 재실행 때마다
            // "제단이 없다"고 판정해 하나씩 새로 만들고, WireStageDirector는 그중 하나만 배선한다
            // (같은 이유로 아래 HUD 조회도 이미 Include를 쓰고 있다).
            var existing = Object.FindAnyObjectByType<FormAltar>(FindObjectsInactive.Include);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                Debug.Log($"[FormAltarBuilder] 기존 FormAltar 재사용: {go.name}");
            }
            else
            {
                go = new GameObject(AltarName);
                go.transform.position = new Vector3(5f, -2.5f, 0f);
                Undo.RegisterCreatedObjectUndo(go, "Create FormAltar");

                go.AddComponent<SpriteRenderer>();
                go.AddComponent<FormAltar>();
            }

            ApplyAltarTrigger(go);
            ApplyAltarVisual(go);

            var altar = go.GetComponent<FormAltar>();
            var so = new SerializedObject(altar);
            var rewardProp = so.FindProperty("rewardForm");
            if (rewardProp != null) rewardProp.objectReferenceValue = rewardForm;
            so.ApplyModifiedProperties();

            return go;
        }

        /// <summary>
        /// 근접 감지용 트리거 콜라이더를 보장한다. 크기는 제단 스프라이트(32x48@PPU32 = 1x1.5 유닛)에
        /// 맞춘다 — 기본값 1x1로 두면 제단 위쪽 절반이 감지 범위 밖이라 프롬프트가 잘 뜨지 않는다.
        ///
        /// 생성 분기가 아니라 <b>공통 경로</b>에 둔다. 크기는 스프라이트에 종속된 데이터 구동 값이므로
        /// 이미 배치된 제단도 재실행으로 갱신되어야 한다(StageBuilder가 폼 보상·이벤트 참조를
        /// 재실행 시 반영하는 것과 같은 규약).
        /// </summary>
        private static void ApplyAltarTrigger(GameObject go)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null) col = Undo.AddComponent<BoxCollider2D>(go);

            col.isTrigger = true; // 물리 차단 없이 PlayerInteractor가 감지(로비 NPC와 동일)
            col.size = AltarTriggerSize;
        }

        /// <summary>제단 스프라이트를 정식 도트로 교체한다(없으면 흰 사각 폴백).</summary>
        private static void ApplyAltarVisual(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            var altarSprite = LoadAltarSprite();
            if (altarSprite != null)
            {
                if (sr.sprite != altarSprite)
                {
                    sr.sprite = altarSprite;
                    sr.color = Color.white; // 스프라이트 자체 색 사용
                    // 32x48@PPU32 = 1x1.5 유닛. 흰 사각용 (1,2,1) 스트레치를 정규화(재튜닝 시 Y 위치만 조정).
                    go.transform.localScale = Vector3.one;
                }
            }
            else if (sr.sprite == null)
            {
                // 폴백: 스프라이트 미생성(generate_form_altar.py 미실행) — 흰 사각 호박색.
                sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
                sr.color = new Color(0.95f, 0.75f, 0.2f);
                go.transform.localScale = new Vector3(1f, 2f, 1f);
                Debug.LogWarning($"[FormAltarBuilder] 제단 스프라이트 미발견({AbyssPaths.FormAltarSprite}) — 흰 사각 폴백. 'python Tools/PixelArt/generate_form_altar.py' 실행 권장.");
            }
        }

        /// <summary>제단 스프라이트 임포트 설정 보장(Sprite/Point/PPU32) 후 로드. 미생성 시 null.</summary>
        private static Sprite LoadAltarSprite()
        {
            var importer = AssetImporter.GetAtPath(AbyssPaths.FormAltarSprite) as TextureImporter;
            if (importer == null) return null;

            if (importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point
                || Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.1f)
            {
                importer.textureType        = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 32;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(AbyssPaths.FormAltarSprite);
        }
    }
}
#endif
