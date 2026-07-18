#if UNITY_EDITOR
using Abyss.Runtime.Form;
using Abyss.Runtime.Lobby;
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
    /// - Run 플레이어(PlayerCharacter)에 상호작용 파이프라인이 없으므로 PlayerInteractor + 트리거 콜라이더를
    ///   씬 인스턴스 오버라이드로 보강한다(프리팹 GUID 무손상). 폼 프리팹을 force 재생성하면 GUID가 바뀌어
    ///   씬 참조가 끊기므로, 프리팹 대신 씬 인스턴스에 부착한다.
    /// - PlayerInteractor.promptLabel을 HUD의 InteractPrompt Text에 배선한다(근접 안내 문구 표시).
    /// 멱등: 이미 있으면 건너뛴다. 재실행 시 누락분만 복구.
    /// </summary>
    public static class FormAltarBuilder
    {
        private const string AltarName = "FormAltar";
        private const string RewardFormFile = "AncientShield.asset"; // 기본 슬롯(암흑검사/공허궁수) 밖의 3번째 폼
        private const string PromptName = "InteractPrompt";

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

        /// <summary>Run 플레이어에 상호작용 컴포넌트가 없으면 씬 인스턴스에 보강한다(멱등). 인터랙터 반환.</summary>
        private static PlayerInteractor EnsureRunPlayerInteractor()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[FormAltarBuilder] 씬에서 PlayerCharacter 미발견 — PlayerInteractor 보강 생략. Run 씬에서 실행하세요.");
                return null;
            }

            var go = player.gameObject;
            var interactor = go.GetComponent<PlayerInteractor>();
            if (interactor != null) return interactor; // 이미 있음

            // 근접 감지용 트리거(넓은 반경) — 로비 플레이어와 동일 구성.
            var trigger = Undo.AddComponent<CircleCollider2D>(go);
            trigger.isTrigger = true;
            trigger.radius = 1.8f;

            interactor = Undo.AddComponent<PlayerInteractor>(go);
            EditorUtility.SetDirty(go);
            Debug.Log($"[FormAltarBuilder] Run 플레이어 '{go.name}'에 PlayerInteractor + 트리거 콜라이더 보강(씬 오버라이드).");
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
            var existing = Object.FindAnyObjectByType<FormAltar>();
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

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true; // 물리 차단 없이 PlayerInteractor가 감지(로비 NPC와 동일)

                go.AddComponent<FormAltar>();
            }

            ApplyAltarVisual(go);

            var altar = go.GetComponent<FormAltar>();
            var so = new SerializedObject(altar);
            var rewardProp = so.FindProperty("rewardForm");
            if (rewardProp != null) rewardProp.objectReferenceValue = rewardForm;
            so.ApplyModifiedProperties();

            return go;
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
