#if UNITY_EDITOR
using Abyss.Runtime.Form;
using Abyss.Runtime.Stage;
using Abyss.Runtime.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        private const string Tag = "[FormAltarBuilder]";

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
                    $"보상 FormData 미발견: {AbyssPaths.Forms}/{RewardFormFile}\n먼저 '{AbyssToolNames.GenerateContent}'를 실행하세요.",
                    "확인");
                return;
            }

            var interactor = AltarBuilderCommon.EnsureRunPlayerInteractor(Tag);
            AltarBuilderCommon.WireInteractPrompt(interactor, Tag);
            var altar = CreateOrReuseAltar(rewardForm);

            WireStageDirector(altar.GetComponent<FormAltar>());
            altar.SetActive(false); // 룸 게이트: 보상 룸 클리어 시 StageDirector가 활성화. 기본 숨김.

            EditorUtility.SetDirty(altar);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = altar;
            EditorGUIUtility.PingObject(altar);

            Debug.Log($"[FormAltarBuilder] 폼 제단 배치 완료 — 보상 폼='{rewardForm.displayName}' ({rewardForm.formId}), 기본 비활성(룸 게이트)");
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

            AltarBuilderCommon.ApplyTrigger(go);
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

            var altarSprite = AltarBuilderCommon.LoadPropSprite(AbyssPaths.FormAltarSprite);
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
    }
}
#endif
