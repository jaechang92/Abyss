using Abyss.Runtime.Form;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 현재 폼의 <see cref="FormData.bodySprite"/>를 플레이어 <see cref="SpriteRenderer"/>에 비춘다(4-1 아트).
    ///
    /// 🔑 <b>이벤트를 구독하지 않고 상태를 미러링한다.</b> 폼이 바뀌는 경로가 셋인데
    /// 이벤트를 내는 것은 하나뿐이다:
    /// <list type="bullet">
    /// <item><see cref="FormController.RequestSwap"/> → <c>OnFormSwapped</c> 발행 ✅</item>
    /// <item><see cref="FormController.EquipForm"/>(미드런 폼 보상) → <b>이벤트 없음</b></item>
    /// <item>시작 폼 주입(<c>ApplyStartingForm</c>) → <b>이벤트 없음</b></item>
    /// </list>
    /// 이벤트만 구독하면 시작 폼과 폼 보상에서 그림이 안 바뀌는데 <b>오류는 안 난다</b> —
    /// 이 프로젝트가 죽은 목록·죽은 게이트로 여러 번 겪은 실패 모양이다.
    /// 뷰가 상태에서 파생되면 어긋날 수가 없고, 비용은 프레임당 참조 비교 한 번이다.
    ///
    /// <b>색은 건드리지 않는다</b> — 피격 플래시·시전 플래시가 <c>sr.color</c>를 자기 것으로 쓴다.
    /// 스프라이트가 있으면 흰색으로 한 번만 되돌리고(틴트가 그림을 물들이지 않게) 그 뒤로는 놔둔다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FormVisualPresenter : MonoBehaviour
    {
        [Tooltip("비우면 부모에서 찾는다.")]
        [SerializeField] private FormController formController;
        [Tooltip("비우면 자기 자신에서 찾는다.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("bodySprite가 없는 폼에서 쓸 폴백 스프라이트(기존 흰 사각형).")]
        [SerializeField] private Sprite fallbackSprite;

        private FormData applied;
        private bool hasApplied;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (formController == null) formController = GetComponentInParent<FormController>();
            if (fallbackSprite == null && target != null) fallbackSprite = target.sprite;
        }

        private void LateUpdate()
        {
            if (formController == null || target == null) return;

            var current = formController.CurrentForm;
            if (hasApplied && ReferenceEquals(current, applied)) return;

            applied = current;
            hasApplied = true;
            ApplyCurrent(current);
        }

        private void ApplyCurrent(FormData form)
        {
            var sprite = form != null ? form.bodySprite : null;

            if (sprite != null)
            {
                target.sprite = sprite;
                // 폴백 시절의 폼 색 틴트를 걷는다 — 그림 위에 곱해지면 전부 물든다.
                target.color = Color.white;
                // 🔴 스케일은 건드리지 않는다. 이 컴포넌트는 Visual 자식에 붙고,
                //    그 자식이 루트의 (1,2,1) 늘림을 상쇄하는 (1,0.5,1)을 이미 갖고 있다.
                //    여기서 localScale을 만지면 그 상쇄가 풀려 그림이 세로로 늘어난다.
                return;
            }

            // 폴백 — 예전 그대로(흰 사각형 + 폼 색). 새 폼의 그림이 아직 없을 때 쓰인다.
            if (fallbackSprite != null) target.sprite = fallbackSprite;
            if (form != null) target.color = form.castColor;
        }
    }
}
