using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// "폼 하나를 <see cref="SpriteRenderer"/>에 비추는 규칙"의 단일 출처(SoT).
    ///
    /// 규칙 자체는 짧지만 <b>비추는 곳이 둘</b>이다 — 런의 <see cref="FormVisualPresenter"/>
    /// (현재 폼)와 로비의 <c>LobbyFormVisual</c>(시작 폼). 규칙을 양쪽에 적어 두면
    /// 한쪽만 고쳐지고 다른 쪽이 남는다. 이 프로젝트가 UI 정렬 순위·화폐 표기에서
    /// 이미 겪은 파편화 모양이라, 규칙은 여기 하나로 모으고 <b>출처만 각자 다르게</b> 둔다.
    ///
    /// <b>색은 그림이 있을 때 한 번만 건드린다.</b> 런에서는 피격 플래시·시전 플래시가
    /// <c>sr.color</c>를 자기 것으로 쓰므로, 흰색으로 되돌린 뒤로는 놔둬야 한다.
    ///
    /// 🔴 <b>스케일은 건드리지 않는다.</b> 호출부는 폼 그림을 담는 자식(Visual)이고
    /// 그 자식의 로컬 스케일은 부모의 늘림을 상쇄하는 값일 수 있다. 여기서 만지면 상쇄가 풀린다.
    /// </summary>
    public static class FormVisualApplier
    {
        /// <summary>
        /// <paramref name="form"/>의 그림을 <paramref name="target"/>에 적용한다.
        /// <see cref="FormData.bodySprite"/>가 없으면 <paramref name="fallback"/> + 폼 색으로 물러난다 —
        /// 그림이 아직 없는 폼도 화면에서 사라지지 않게.
        /// </summary>
        public static void Apply(SpriteRenderer target, FormData form, Sprite fallback)
        {
            if (target == null) return;

            var sprite = form != null ? form.bodySprite : null;
            if (sprite != null)
            {
                target.sprite = sprite;
                // 폴백 시절의 폼 색 틴트를 걷는다 — 그림 위에 곱해지면 전부 물든다.
                target.color = Color.white;
                return;
            }

            // 폴백 — 흰 사각형 + 폼 색. 새 폼의 그림이 아직 없을 때 쓰인다.
            if (fallback != null) target.sprite = fallback;
            if (form != null) target.color = form.castColor;
        }
    }
}
