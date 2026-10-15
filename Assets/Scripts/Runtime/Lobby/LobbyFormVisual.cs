using Abyss.Runtime.Flow;
using Abyss.Runtime.Form;
using Anim.Core;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비 캐릭터에 <b>지금 들고 있는 폼</b>의 그림을 비춘다.
    ///
    /// 🔑 <b>출처는 공유 <see cref="FormController"/> 하나다</b>(2026-10-12). 로비도 Run 과 같은 PlayerCharacter·FormController 를
    /// 쓰고, 각인사에서 고른 시작 폼(<see cref="RunStartContext.StartingForm"/>)은 <see cref="LobbyPlayerController"/>가
    /// 그 슬롯에 꽂는다. 그래서 그림·이동 배율·점프 횟수가 같은 폼에서 나온다. 예전처럼 RunStartContext 를 따로 읽으면
    /// 그림만 새 폼이고 몸은 이전 폼인 출처 두 개가 된다.
    ///
    /// Run 의 <see cref="FormVisualPresenter"/>를 쓰지 않는 이유: 그쪽은 무기 소켓·전투 배율까지 꽂으며
    /// 영속 RunManager 의 무기 보유 상태를 읽고 <b>시작 무기를 보유로 들인다</b>(Adopt). 로비에서 그러면 지난 런의
    /// 무기 기록을 건드린다. 그림 규칙(<see cref="FormVisualApplier"/>)은 같고 무기·전투만 비운다.
    /// 그림 writer 는 이것 하나, 상태별 재생은 공유 PlayerAnimationBinder 하나다.
    ///
    /// 🔑 <b>이벤트를 구독하지 않고 상태를 미러링한다.</b> 시작 폼 주입·EquipForm 은 이벤트를 내지 않는다
    /// (<see cref="FormVisualPresenter"/> 주석과 같은 이유). 비용은 프레임당 참조 비교 한 번이다.
    ///
    /// <see cref="defaultForm"/>은 폼 컨트롤러를 못 찾을 때의 폴백이자 에디터 리뷰 도구가 읽는 기본 폼이다.
    /// 빌더가 <b>Run 의 Player 프리팹이 실제로 들고 시작하는 폼</b>에서 뽑아 물린다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LobbyFormVisual : MonoBehaviour
    {
        [Tooltip("비우면 자기 자신에서 찾는다.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("폼 컨트롤러가 없을 때의 폴백 폼. Run 이 실제로 시작하는 폼과 같아야 한다.")]
        [SerializeField] private FormData defaultForm;

        [Tooltip("bodySprite 가 없는 폼에서 쓸 폴백 스프라이트(흰 사각형).")]
        [SerializeField] private Sprite fallbackSprite;

        [Tooltip("폼의 애니메이션 한 벌을 틀 재생기. 비우면 자기 자신에서 찾는다. 없으면 정지 그림만 나온다.")]
        [SerializeField] private AnimatorDriver animatorDriver;

        [Tooltip("비우면 부모에서 찾는다. 로비 캐릭터의 공유 폼 상태(그림의 출처).")]
        [SerializeField] private FormController formController;

        private FormData applied;
        private bool hasApplied;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (animatorDriver == null) animatorDriver = GetComponent<AnimatorDriver>();
            if (formController == null) formController = GetComponentInParent<FormController>();
            // 빌더가 깔아 둔 초기 스프라이트를 폴백으로 삼는다 — 별도 배선 없이 예전 모습이 남는다.
            if (fallbackSprite == null && target != null) fallbackSprite = target.sprite;
        }

        private void LateUpdate()
        {
            var current = ResolveCurrentForm();
            if (hasApplied && ReferenceEquals(current, applied)) return;

            applied = current;
            hasApplied = true;
            // 그림과 애니메이션을 한 진입점으로 — 따로 부르면 정지 그림과 움직이는 그림이 다른 폼이 된다(FormVisualApplier).
            // 무기 소켓·전투 배율은 비운다 — 런 무기 보유 상태를 건드리지 않는다(클래스 주석).
            FormVisualApplier.Apply(target, current, fallbackSprite, animatorDriver);
        }

        /// <summary>공유 폼 상태가 출처다. 컨트롤러가 없거나 폼이 비었을 때만 기본 폼으로 물러난다.</summary>
        private FormData ResolveCurrentForm()
        {
            var current = formController != null ? formController.CurrentForm : null;
            return current != null ? current : defaultForm;
        }
    }
}
