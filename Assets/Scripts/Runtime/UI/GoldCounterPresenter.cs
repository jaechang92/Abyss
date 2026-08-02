using Abyss.Runtime.Events;
using Abyss.Runtime.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 상시 골드 카운터 HUD(우상단).
    ///
    /// 상점 방(1-2)을 넣으면서 드러난 구멍을 메운다 — <see cref="GameEvents.OnGoldShardsChanged"/>는
    /// 발행되고 있었으나 <b>구독자가 하나도 없었다.</b> 골드는 드래프트 패널의 리롤 비용 옆에서만
    /// 보였고, 전투 중에는 잔액을 알 방법이 없었다. 소비처(상점)가 생긴 이상
    /// "지금 살까 아껴둘까"를 판단하려면 잔액이 상시 보여야 한다.
    ///
    /// 갱신은 이벤트 기반 — 매 프레임 <c>RunManager</c>를 폴링하지 않는다.
    /// <see cref="FormBiasWarningPresenter"/>·<see cref="SynergyCounterPresenter"/>와 같이
    /// 자체 구독형이라 <c>HUDPresenter</c> 배선이 필요 없다.
    /// </summary>
    public sealed class GoldCounterPresenter : MonoBehaviour
    {
        [Header("표시 요소")]
        [SerializeField] private Text label;

        private const string FORMAT = "골드 {0}";

        // 증감 강조 지속 시간. 짧게 둔다 — 상점에서 연속 구매하면 강조가 겹쳐 흐르기만 한다.
        private const float FLASH_DURATION = 0.35f;

        private static readonly Color NormalColor = new(0.95f, 0.82f, 0.45f);
        private static readonly Color GainColor = new(0.62f, 0.98f, 0.62f);
        private static readonly Color SpendColor = new(0.98f, 0.55f, 0.50f);

        // 마지막으로 화면에 찍은 값. 실제 골드(0 이상)와 겹치지 않는 -1로 시작해
        // 첫 갱신이 "변화 없음"으로 걸러지지 않게 한다.
        private int shownAmount = -1;

        private Color flashFrom;
        private float flashRemaining;

        private void Awake()
        {
            // 씬 수동 편집으로 참조가 끊기면 카운터가 조용히 사라진다 — 배선 누락을 표면화한다.
            if (label == null)
            {
                Debug.LogWarning($"[GoldCounterPresenter] label 미배선 ({name}) — 골드가 표시되지 않는다. Build HUD 재실행 필요.");
            }
        }

        private void OnEnable()
        {
            GameEvents.OnGoldShardsChanged += HandleGoldChanged;

            // 런이 이미 시작된 뒤에 켜져도(씬 재활성·프리팹 지연 생성) 칸이 비어 있지 않게 한 번 읽는다.
            // 이벤트는 신호일 뿐이고 값의 주인은 RunManager다.
            Apply(CurrentGold(), flash: false);
        }

        private void OnDisable()
        {
            GameEvents.OnGoldShardsChanged -= HandleGoldChanged;
        }

        private void Update()
        {
            if (flashRemaining <= 0f) return;

            // 상점·이벤트 모달은 timeScale=0 위에서 골드를 바꾼다.
            // deltaTime을 쓰면 정지 중에 시간이 흐르지 않아 강조색이 영영 안 풀린다.
            flashRemaining -= Time.unscaledDeltaTime;

            if (label == null) return;

            if (flashRemaining <= 0f)
            {
                label.color = NormalColor;
                return;
            }

            float t = Mathf.Clamp01(1f - flashRemaining / FLASH_DURATION);
            label.color = Color.Lerp(flashFrom, NormalColor, t);
        }

        private static int CurrentGold()
        {
            var run = RunManager.GetInstanceSafe();
            return run != null ? run.GoldShards : 0;
        }

        private void HandleGoldChanged(int amount) => Apply(amount, flash: true);

        /// <summary>
        /// 숫자를 갱신하고, 변화가 있으면 증감 방향에 따라 강조한다.
        /// 획득(초록)과 지불(빨강)을 갈라 두면 상점에서 <b>구매가 실제로 처리됐는지</b>가 즉시 보인다.
        /// </summary>
        private void Apply(int amount, bool flash)
        {
            bool changed = amount != shownAmount;
            bool increased = amount > shownAmount;
            shownAmount = amount;

            if (label == null) return;
            label.text = string.Format(FORMAT, amount);

            if (!flash || !changed)
            {
                flashRemaining = 0f;
                label.color = NormalColor;
                return;
            }

            flashFrom = increased ? GainColor : SpendColor;
            flashRemaining = FLASH_DURATION;
            label.color = flashFrom;
        }
    }
}
