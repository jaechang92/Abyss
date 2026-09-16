#if UNITY_EDITOR
using Abyss.Runtime.Stage;
using Abyss.Runtime.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 활성 씬(Run)에 무기 보상 제단(<see cref="WeaponAltar"/>)을 배치하는 에디터 툴.
    /// <c>FormAltarBuilder</c>와 형태가 같고, 공유 규약은 <see cref="AltarBuilderCommon"/>이 갖는다.
    ///
    /// 🔑 <b>보상 무기를 여기서 안 꽂는다.</b> 폼 제단은 기본 보상 폼을 에셋에서 찾아 넣지만,
    /// 무기는 <b>현재 폼이 무엇이냐에 따라 후보가 달라진다</b>(무기는 폼 전용이다).
    /// 빌드 시점에는 그 폼을 알 수 없으므로, 런타임에 <c>StageDirector</c>가 <c>Configure</c>로 꽂는다.
    /// 여기서 아무거나 미리 넣으면 <b>인스펙터에 보이는 무기와 실제로 나오는 무기가 달라진다.</b>
    ///
    /// 🔴 <b>위치를 폼 제단과 같게 둔다.</b> 한 방은 제단을 하나만 열므로(§3-B) 런타임에 겹칠 일이 없고,
    /// 플레이어 입장에서 <b>보상이 놓이는 자리는 하나</b>인 편이 읽기 쉽다.
    /// 에디터 하이어라키에서는 이름으로 구분한다.
    ///
    /// 멱등: 이미 있으면 재사용하고 누락분만 복구한다. 씬 조회는 반드시
    /// <c>FindObjectsInactive.Include</c>로 한다 — 제단은 비활성으로 저장되므로 기본 조회로는 못 찾고,
    /// 그러면 "없다"고 판정해 재실행 때마다 하나씩 더 만든다.
    /// </summary>
    public static class WeaponAltarBuilder
    {
        private const string AltarName = "WeaponAltar";
        private const string Tag = "[WeaponAltarBuilder]";

        // 폼 제단과 같은 자리(FormAltarBuilder 참조). 둘은 동시에 활성화되지 않는다.
        private static readonly Vector3 AltarPosition = new(5f, -2.5f, 0f);

        [MenuItem(AbyssMenu.BuildWeaponAltar)]
        public static void BuildWeaponAltarInActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("WeaponAltarBuilder", "활성 씬이 유효하지 않습니다.", "확인");
                return;
            }

            var interactor = AltarBuilderCommon.EnsureRunPlayerInteractor(Tag);
            AltarBuilderCommon.WireInteractPrompt(interactor, Tag);

            var altar = CreateOrReuseAltar();
            WireStageDirector(altar.GetComponent<WeaponAltar>());
            altar.SetActive(false); // 룸 게이트: 무기 보상 룸 클리어 시 StageDirector가 활성화. 기본 숨김.

            EditorUtility.SetDirty(altar);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = altar;
            EditorGUIUtility.PingObject(altar);

            Debug.Log($"{Tag} 무기 제단 배치 완료 — 보상 무기는 런타임에 StageDirector가 꽂는다(폼에 따라 달라짐), 기본 비활성(룸 게이트)");
        }

        /// <summary>StageDirector.weaponAltar에 제단을 배선한다(무기 보상 룸 게이트가 이 제단을 활성화).</summary>
        private static void WireStageDirector(WeaponAltar altar)
        {
            var director = Object.FindAnyObjectByType<StageDirector>();
            if (director == null)
            {
                Debug.LogWarning($"{Tag} StageDirector 미발견 — weaponAltar 배선 생략. StageDirector 빌드 후 재실행하세요.");
                return;
            }

            var so = new SerializedObject(director);
            var prop = so.FindProperty("weaponAltar");
            if (prop == null)
            {
                Debug.LogError($"{Tag} StageDirector에 weaponAltar 필드가 없다 — 스크립트 컴파일 상태를 확인할 것.");
                return;
            }
            prop.objectReferenceValue = altar;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
            Debug.Log($"{Tag} StageDirector.weaponAltar → WeaponAltar 배선.");
        }

        /// <summary>기존 WeaponAltar가 있으면 재사용, 없으면 생성.</summary>
        private static GameObject CreateOrReuseAltar()
        {
            var existing = Object.FindAnyObjectByType<WeaponAltar>(FindObjectsInactive.Include);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                Debug.Log($"{Tag} 기존 WeaponAltar 재사용: {go.name}");
            }
            else
            {
                go = new GameObject(AltarName);
                go.transform.position = AltarPosition;
                Undo.RegisterCreatedObjectUndo(go, "Create WeaponAltar");

                go.AddComponent<SpriteRenderer>();
                go.AddComponent<WeaponAltar>();
            }

            AltarBuilderCommon.ApplyTrigger(go);
            ApplyAltarVisual(go);
            return go;
        }

        /// <summary>
        /// 제단 그림. 전용 도트가 아직 없어 <b>폼 제단 그림을 강철빛으로 틴트해</b> 쓴다.
        ///
        /// 🔴 <b>이건 자리표시자다.</b> 틴트만 다르면 플레이어는 두 제단을 <b>같은 것으로 읽는다</b> —
        /// 무기 보상이 나왔는지 폼 보상이 나왔는지 그림으로는 구별이 안 된다.
        /// 그래서 폴백을 탈 때마다 <b>경고를 남긴다.</b> 조용히 굴러가면
        /// 「전용 그림이 없다」가 영영 안 드러난다(자리표시자가 결손을 가리는 모양).
        /// </summary>
        private static void ApplyAltarVisual(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            var own = AltarBuilderCommon.LoadPropSprite(AbyssPaths.WeaponAltarSprite);
            if (own != null)
            {
                sr.sprite = own;
                sr.color = Color.white; // 스프라이트 자체 색 사용
                go.transform.localScale = Vector3.one;
                return;
            }

            var borrowed = AltarBuilderCommon.LoadPropSprite(AbyssPaths.FormAltarSprite);
            if (borrowed != null)
            {
                sr.sprite = borrowed;
                sr.color = new Color(0.62f, 0.72f, 0.85f); // 강철빛 — 폼 제단(그림 그대로)과 구분만 해 준다
                go.transform.localScale = Vector3.one;
                Debug.LogWarning(
                    $"{Tag} 무기 제단 전용 그림 미발견({AbyssPaths.WeaponAltarSprite}) — 폼 제단 그림을 강철빛으로 틴트해 폴백.\n" +
                    "  ⚠️ 틴트만으로는 두 제단이 같은 것으로 읽힌다. 전용 도트를 그리면 이 경고가 사라진다.");
                return;
            }

            // 둘 다 없음 — 흰 사각 폴백(폼 제단과 같은 최후 경로).
            sr.sprite = EditorPlatformFactory.LoadWhiteSquare();
            sr.color = new Color(0.62f, 0.72f, 0.85f);
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            Debug.LogWarning($"{Tag} 제단 그림을 하나도 못 찾았다({AbyssPaths.WeaponAltarSprite} · {AbyssPaths.FormAltarSprite}) — 흰 사각 폴백.");
        }
    }
}
#endif
