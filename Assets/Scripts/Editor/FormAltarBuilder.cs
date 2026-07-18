#if UNITY_EDITOR
using Abyss.Runtime.Form;
using Abyss.Runtime.Lobby;
using Abyss.Runtime.Player;
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
    /// 함께 수행: Run 플레이어(PlayerCharacter)에 상호작용 파이프라인이 없으므로
    /// PlayerInteractor + 트리거 콜라이더를 씬 인스턴스 오버라이드로 보강한다(프리팹 GUID 무손상).
    /// 폼 프리팹을 force 재생성하면 GUID가 바뀌어 씬 참조가 끊기므로, 프리팹 대신 씬 인스턴스에 부착한다.
    /// 멱등: 이미 있으면 건너뛴다. 재실행 시 누락분만 복구.
    /// </summary>
    public static class FormAltarBuilder
    {
        private const string AltarName = "FormAltar";
        private const string RewardFormFile = "AncientShield.asset"; // 기본 슬롯(암흑검사/공허궁수) 밖의 3번째 폼

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

            EnsureRunPlayerInteractor();
            var altar = CreateOrReuseAltar(rewardForm);

            EditorUtility.SetDirty(altar);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = altar;
            EditorGUIUtility.PingObject(altar);

            Debug.Log($"[FormAltarBuilder] 폼 제단 배치 완료 — 보상 폼='{rewardForm.displayName}' ({rewardForm.formId})");
        }

        /// <summary>Run 플레이어에 상호작용 컴포넌트가 없으면 씬 인스턴스에 보강한다(멱등).</summary>
        private static void EnsureRunPlayerInteractor()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (player == null)
            {
                Debug.LogWarning("[FormAltarBuilder] 씬에서 PlayerCharacter 미발견 — PlayerInteractor 보강 생략. Run 씬에서 실행하세요.");
                return;
            }

            var go = player.gameObject;
            if (go.GetComponent<PlayerInteractor>() != null) return; // 이미 있음

            // 근접 감지용 트리거(넓은 반경) — 로비 플레이어와 동일 구성.
            var trigger = Undo.AddComponent<CircleCollider2D>(go);
            trigger.isTrigger = true;
            trigger.radius = 1.8f;

            Undo.AddComponent<PlayerInteractor>(go);
            EditorUtility.SetDirty(go);
            Debug.Log($"[FormAltarBuilder] Run 플레이어 '{go.name}'에 PlayerInteractor + 트리거 콜라이더 보강(씬 오버라이드).");
        }

        /// <summary>기존 FormAltar가 있으면 재사용, 없으면 생성. 보상 폼을 직렬화 필드에 주입한다.</summary>
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
                go.transform.localScale = new Vector3(1f, 2f, 1f);
                Undo.RegisterCreatedObjectUndo(go, "Create FormAltar");

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
                sr.color = new Color(0.95f, 0.75f, 0.2f); // 호박색(보상 제단)

                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true; // 물리 차단 없이 PlayerInteractor가 감지(로비 NPC와 동일)

                go.AddComponent<FormAltar>();
            }

            var altar = go.GetComponent<FormAltar>();
            var so = new SerializedObject(altar);
            var rewardProp = so.FindProperty("rewardForm");
            if (rewardProp != null) rewardProp.objectReferenceValue = rewardForm;
            so.ApplyModifiedProperties();

            return go;
        }
    }
}
#endif
