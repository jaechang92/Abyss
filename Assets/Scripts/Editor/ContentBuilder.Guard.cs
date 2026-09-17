#if UNITY_EDITOR
using Abyss.Runtime.Form;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    public static partial class ContentBuilder
    {
        /// <summary>
        /// 방패병 가드 설정. 기획: <c>Docs/game-design/16-shield-guard.md</c> §3.
        ///
        /// 🔑 <b>수치는 여기 한 곳에만 있다</b>(원거리 공격의 <c>WireFormRangedAttacks</c> 와 같은 구조).
        /// 조정은 값 하나씩 — 「너무 안전하다」면 <c>holdDamageScale</c> 부터 올린다.
        /// <see cref="CreateOrSkip"/> 가 기존 에셋을 건너뛰므로 매번 도는 별도 패스다.
        /// </summary>
        private static void WireFormGuards()
        {
            FormData shield = FindForm("ancient_shield");
            if (shield == null)
            {
                Debug.LogWarning("[ContentBuilder] 폼 에셋 없음: ancient_shield — 가드를 연결하지 못했다.");
                return;
            }

            shield.guard = new FormGuardSpec
            {
                isEnabled = true,
                holdDamageScale = 0.3f,      // 일반 가드 — 70% 감소, 최소 1
                justGuardWindow = 0.2f,      // 누른 순간부터
                justGuardRearmDelay = 0.3f,  // 뗀 뒤 이 시간 안의 재입력은 저스트 창을 안 연다
                counterDamageScale = 1.5f    // 로드맵 「반격 1.5배」
            };

            EditorUtility.SetDirty(shield);
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] 방패병 가드 연결: ancient_shield.");
        }
    }
}
#endif
