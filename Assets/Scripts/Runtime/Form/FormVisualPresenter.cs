using Abyss.Runtime.Form;
using Anim.Core;
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
    ///
    /// 실제 적용 규칙은 <see cref="FormVisualApplier"/>가 갖는다 — 로비도 같은 규칙으로 그리는데
    /// 거기에는 <see cref="FormController"/>가 없다. 이 클래스가 정하는 것은 <b>출처</b>뿐이다.
    ///
    /// 🔴 <b>실행 순서가 <see cref="Player.WeaponSocket"/>보다 앞이어야 한다</b> (2026-09-15).
    /// 둘 다 <c>LateUpdate</c>에서 도는데 Unity 가 정해 주는 순서가 없다. 소켓이 먼저 돌면
    /// <b>폼이 바뀐 프레임에 이전 폼의 앵커로 무기를 한 번 그린다</b> — 오류도 로그도 안 나고
    /// 한 프레임이라 눈에 걸릴까 말까다. 그래서 값으로 못박는다.
    /// </summary>
    [DefaultExecutionOrder(VisualOrder)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FormVisualPresenter : MonoBehaviour
    {
        /// <summary>폼이 그림·애니메이션·앵커를 정하는 차례. 무기 소켓은 이 뒤다.</summary>
        public const int VisualOrder = 30;

        [Tooltip("비우면 부모에서 찾는다.")]
        [SerializeField] private FormController formController;
        [Tooltip("비우면 자기 자신에서 찾는다.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("bodySprite가 없는 폼에서 쓸 폴백 스프라이트(기존 흰 사각형).")]
        [SerializeField] private Sprite fallbackSprite;

        [Tooltip("비우면 자기 자신에서 찾는다. 없으면 애니메이션 없이 정지 그림만 바뀐다.")]
        [SerializeField] private AnimatorDriver animatorDriver;

        [Tooltip("비우면 자식에서 찾는다. 없으면 무기가 안 붙는다(맨손으로 보인다).")]
        [SerializeField] private Player.WeaponSocket weaponSocket;

        [Tooltip("비우면 부모에서 찾는다. 없으면 무기 공격 배율이 안 붙는다(위력만 기본값으로 남는다).")]
        [SerializeField] private Player.PlayerCharacter playerCharacter;

        private FormData applied;
        private bool hasApplied;

        // 마지막으로 반영한 무기 보유 상태의 판(版). 폼이 안 바뀌어도 이 숫자가 오르면 다시 그린다.
        private int appliedWeaponVersion;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (formController == null) formController = GetComponentInParent<FormController>();
            if (fallbackSprite == null && target != null) fallbackSprite = target.sprite;
            if (animatorDriver == null) animatorDriver = GetComponent<AnimatorDriver>();
            if (weaponSocket == null) weaponSocket = GetComponentInChildren<Player.WeaponSocket>(true);
            if (playerCharacter == null) playerCharacter = GetComponentInParent<Player.PlayerCharacter>();
        }

        private void LateUpdate()
        {
            if (formController == null || target == null) return;

            // 🔑 <b>무기 보유 상태도 같이 미러링한다.</b> 제단·상점·가차가 무기를 주는 순간에는
            //    폼이 안 바뀌므로, 폼 참조만 보면 방금 얻은 무기가 다음 폼 교체까지 손에 안 들린다.
            //    이벤트를 새로 파지 않는 이유는 위 주석과 같다 — 발행을 빠뜨린 창구가 조용히 안 바뀐다.
            var current = formController.CurrentForm;
            int weaponVersion = CurrentWeaponVersion();
            if (hasApplied && ReferenceEquals(current, applied) && weaponVersion == appliedWeaponVersion) return;

            applied = current;
            appliedWeaponVersion = weaponVersion;
            hasApplied = true;
            FormVisualApplier.Apply(target, current, fallbackSprite, animatorDriver, weaponSocket, playerCharacter);
        }

        /// <summary>런의 무기 보유 상태 판 번호. 런이 없으면(로비·테스트 씬) 0으로 고정된다.</summary>
        private static int CurrentWeaponVersion()
            => Run.RunManager.HasInstance ? Run.RunManager.Instance.Weapons.Version : 0;
    }
}
