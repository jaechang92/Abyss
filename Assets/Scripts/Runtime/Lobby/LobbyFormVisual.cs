using Abyss.Runtime.Flow;
using Abyss.Runtime.Form;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 로비 캐릭터에 <b>이 런을 시작할 폼</b>의 그림을 비춘다.
    /// 런의 <see cref="FormVisualPresenter"/>와 같은 자리(Visual 자식)에 놓이지만 <b>출처가 다르다</b> —
    /// 로비에는 <see cref="FormController"/>가 없다. 전투·스킬·체력과 폼 플레이타임 기록이
    /// 딸려오기 때문에 일부러 두지 않았다(<see cref="LobbyPlayerController"/> 주석 참조).
    /// 대신 각인사에서 고른 결과가 남는 <see cref="RunStartContext.StartingForm"/>을 본다.
    ///
    /// 🔑 <b>이벤트를 구독하지 않고 상태를 미러링한다.</b> <see cref="RunStartContext"/>는 이벤트를
    /// 하나도 내지 않는 정적 컨텍스트라 구독할 대상 자체가 없다. 폴링을 피하려면 발행자를 만들어야
    /// 하는데, 그러면 로비 한 곳을 위해 정적 컨텍스트에 이벤트 표면이 생긴다.
    /// 비용은 프레임당 참조 비교 한 번이고, <b>뷰가 상태에서 파생되면 어긋날 수가 없다.</b>
    ///
    /// 미선택(최초 실행·폼 선택 없이 곧장 포털) 시 <see cref="defaultForm"/>으로 물러난다.
    /// 이 값은 빌더가 <b>Run 의 Player 프리팹이 실제로 들고 시작하는 폼</b>에서 뽑아 물린다 —
    /// 로비에 선 캐릭터와 던전에 들어간 캐릭터가 달라 보이면 안 되기 때문이다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LobbyFormVisual : MonoBehaviour
    {
        [Tooltip("비우면 자기 자신에서 찾는다.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("시작 폼 미선택 시 보여줄 폼. Run 이 실제로 시작하는 폼과 같아야 한다.")]
        [SerializeField] private FormData defaultForm;

        [Tooltip("bodySprite 가 없는 폼에서 쓸 폴백 스프라이트(흰 사각형).")]
        [SerializeField] private Sprite fallbackSprite;

        private FormData applied;
        private bool hasApplied;

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            // 빌더가 깔아 둔 초기 스프라이트를 폴백으로 삼는다 — 별도 배선 없이 예전 모습이 남는다.
            if (fallbackSprite == null && target != null) fallbackSprite = target.sprite;
        }

        private void LateUpdate()
        {
            var current = RunStartContext.StartingForm != null ? RunStartContext.StartingForm : defaultForm;
            if (hasApplied && ReferenceEquals(current, applied)) return;

            applied = current;
            hasApplied = true;
            FormVisualApplier.Apply(target, current, fallbackSprite);
        }
    }
}
