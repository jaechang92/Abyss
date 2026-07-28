#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.EditorTools
{
    /// <summary>
    /// Abyss 전용 입력 액션을 기존 InputSystem_Actions.inputactions에 추가하는 패쳐.
    /// 없는 액션만 추가하고 기존은 보존. Player 맵 대상.
    /// 메뉴 경로는 <see cref="AbyssMenu.PatchInputActions"/>.
    /// </summary>
    public static class InputActionsPatcher
    {
        private const string TargetMap = "Player";

        // 키보드 전용 조작(방향키 이동 + 액션 키). .inputactions의 실제 바인딩과 동기화 유지.
        private static readonly (string name, string path)[] DesiredActions =
        {
            ("FormSwap", "<Keyboard>/leftCtrl"),
            ("Dash", "<Keyboard>/d"),
            ("AttackHeavy", "<Keyboard>/x"),
            ("Skill1", "<Keyboard>/a"),
            ("Skill2", "<Keyboard>/s"),
            // 일시정지. 정지 중에는 InputRouter가 UI 모드로 전환해 Player 맵이 꺼지므로,
            // 정지를 '닫는' 입력은 UI 맵의 기존 Cancel 액션(*/{Cancel} — ESC 자동 매핑)이 담당한다.
            ("Pause", "<Keyboard>/escape")
        };

        [MenuItem(AbyssMenu.PatchInputActions)]
        public static void Patch()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AbyssPaths.InputActions);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("InputActionsPatcher",
                    $"{AbyssPaths.InputActions}을 찾지 못했습니다.",
                    "확인");
                return;
            }

            var map = asset.FindActionMap(TargetMap);
            if (map == null)
            {
                EditorUtility.DisplayDialog("InputActionsPatcher",
                    $"'{TargetMap}' 맵을 찾지 못했습니다.",
                    "확인");
                return;
            }

            bool wasEnabled = map.enabled;
            if (wasEnabled) map.Disable();

            int added = 0;
            int skipped = 0;

            foreach (var (actionName, bindingPath) in DesiredActions)
            {
                if (map.FindAction(actionName) != null)
                {
                    Debug.Log($"[InputActionsPatcher] 건너뜀 (존재): {actionName}");
                    skipped += 1;
                    continue;
                }

                var action = map.AddAction(actionName, InputActionType.Button);
                action.AddBinding(bindingPath);
                Debug.Log($"[InputActionsPatcher] 추가: {actionName} ← {bindingPath}");
                added += 1;
            }

            if (added > 0)
            {
                string json = asset.ToJson();
                File.WriteAllText(AbyssPaths.InputActions, json);
                AssetDatabase.ImportAsset(AbyssPaths.InputActions, ImportAssetOptions.ForceUpdate);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log($"[InputActionsPatcher] {added}개 액션 추가됨. 기존 {skipped}개 유지. 파일 저장 완료.");
            }
            else
            {
                Debug.Log("[InputActionsPatcher] 추가할 액션 없음 (모두 이미 존재)");
            }

            if (wasEnabled) map.Enable();

            EditorUtility.DisplayDialog("InputActionsPatcher",
                $"추가 {added}개 / 건너뜀 {skipped}개\n\n키보드 전용 조작: ←→=이동, Z=공격, X=강공격, A=Skill1, S=Skill2, C=점프, D=대시, LCtrl=폼교체, ESC=일시정지.",
                "확인");
        }
    }
}
#endif
