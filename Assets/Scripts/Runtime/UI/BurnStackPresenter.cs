using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 연소 스택 표시(HUD 하단, 스킬 슬롯 오른쪽).
    ///
    /// 그전까지 연소는 <b>링 펄스로만</b> 인지됐다. 타고 있다는 것은 보이지만 몇 겹인지는 안 보여서,
    /// 스택이 상한(10)에 닿았는지도 "연소 스택 5+ 폭발"(03 §6-1) 조건에 걸렸는지도 알 수 없었다.
    /// 불꽃 축을 고를지 말지는 그 숫자에 달려 있는데 화면에 없었다.
    ///
    /// <b>가장 최근에 불붙인 하나만</b> 보여준다. 다수의 적을 동시에 태우면 정보가 흩어지는데,
    /// 플레이어가 실제로 궁금한 것은 "지금 패고 있는 이 녀석이 몇 겹인가"이고 그게 대개 최근 대상이다.
    /// 전부 보여주려면 적 머리 위로 가야 하고, 그건 다수 전투에서 화면을 뒤덮는다.
    ///
    /// <see cref="GoldCounterPresenter"/>·<see cref="SynergyCounterPresenter"/>와 같은 자체 구독형이라
    /// <c>HUDPresenter</c> 배선이 필요 없다. 다만 골드와 달리 <b>숨긴다</b> — 잔액 0은 정보지만
    /// "아무도 안 타고 있음"은 정보가 아니라 기본 상태다.
    /// </summary>
    public sealed class BurnStackPresenter : MonoBehaviour
    {
        [Header("표시 요소")]
        // 이 컴포넌트가 붙은 오브젝트가 아니라 자식을 토글한다. 자기를 끄면 OnDisable이 돌아
        // 구독이 끊기고, 그다음 연소 이벤트가 와도 다시 켜 줄 주체가 없어 영영 안 보인다.
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;

        private const string FORMAT = "연소 {0}스택";

        // EnemyBase.BURN_COLOR와 같은 주황. 링 펄스와 같은 색이어야 화면의 불꽃과 이 숫자가
        // 같은 것을 가리킨다는 게 설명 없이 읽힌다.
        private static readonly Color BurnColor = new(1f, 0.55f, 0.2f);

        /// <summary>
        /// 지금 표시 중인 대상. 스택이 0이 되었을 때 <b>이 적의 소식인지</b> 가려내는 데 쓴다 —
        /// 뒤쪽에서 남의 불이 꺼진 것 때문에 내가 보던 숫자가 사라지면 안 된다.
        /// </summary>
        private EnemyBase tracked;

        private void Awake()
        {
            // 씬 수동 편집이나 빌더 미실행으로 참조가 끊기면 표시가 조용히 사라진다.
            if (root == null || label == null)
            {
                Debug.LogWarning($"[BurnStackPresenter] 참조 미배선 ({name}) — 연소 스택이 표시되지 않는다. Build HUD 재실행 필요.");
            }

            if (label != null) label.color = BurnColor;
            Hide();
        }

        private void OnEnable()
        {
            GameEvents.OnBurnStacksChanged += HandleBurnChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnBurnStacksChanged -= HandleBurnChanged;
        }

        private void Update()
        {
            if (tracked == null) return;

            // 방 정리(DespawnAllEnemies)처럼 ClearBurn을 거치지 않고 사라지는 경로가 있다.
            // 그 경우 0 이벤트가 오지 않으므로 숫자가 죽은 적에 붙은 채 남는다.
            // Unity의 == null은 파괴된 오브젝트에도 true라 이 한 줄이 그 경로를 덮는다.
            if (!tracked) Hide();
        }

        private void HandleBurnChanged(EnemyBase enemy, int stacks)
        {
            if (enemy == null) return;

            if (stacks > 0)
            {
                tracked = enemy;
                Show(stacks);
                return;
            }

            // 꺼졌다는 소식은 보고 있던 적의 것일 때만 받는다.
            if (enemy == tracked) Hide();
        }

        private void Show(int stacks)
        {
            if (label != null) label.text = string.Format(FORMAT, stacks);
            if (root != null && !root.activeSelf) root.SetActive(true);
        }

        private void Hide()
        {
            tracked = null;
            if (root != null && root.activeSelf) root.SetActive(false);
        }
    }
}
