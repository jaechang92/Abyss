using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 상점 진열 품목 하나. 완주 루프 계획 1-2.
    ///
    /// 효과는 <see cref="EventEffect"/>를 그대로 쓴다 — 상점이 파는 것(회복·스킬·골드)은
    /// 이벤트가 이미 주던 것과 같아서, 어휘를 새로 만들면 같은 동작이 두 벌이 된다.
    /// <b>가격도 효과에서 파생시킨다</b>(<see cref="EventEffectType.GoldSpend"/>) —
    /// 표시 가격과 실제 차감액이 어긋날 방법 자체를 없앤다.
    /// </summary>
    [Serializable]
    public sealed class ShopItem
    {
        [Tooltip("진열대에 표시할 품목 이름. 예: \"잊힌 기술서\"")]
        public string label;

        [Tooltip("한 줄 설명. 무엇을 얻는지 + 언제 적용되는지를 적을 것.")]
        [TextArea(1, 3)] public string description;

        [Tooltip("한 번 방문에 살 수 있는 횟수. 다 팔리면 '품절'로 잠긴다.")]
        [Min(1)] public int stock = 1;

        [Tooltip("가격(GoldSpend)과 보상을 함께 담는다. 가격은 여기서 파생된다.")]
        public List<EventEffect> effects = new();

        /// <summary>이 품목의 골드 가격(0이면 골드를 받지 않는다).</summary>
        public int GoldCost => EventEffectApplier.GoldCost(effects);

        /// <summary>이 품목이 요구하는 체력 대가(최대 HP 대비 %).</summary>
        public int HpCostPercent => EventEffectApplier.HpCostPercent(effects);

        /// <summary>
        /// 진열대에 찍을 대가 문구. 골드와 체력을 같이 요구할 수 있어 한 줄로 합친다.
        /// 대가가 전혀 없으면 "무료" — 빈 칸으로 두면 값을 못 읽은 것처럼 보인다.
        /// </summary>
        public string CostText
        {
            get
            {
                int gold = GoldCost;
                int hp = HpCostPercent;

                if (gold > 0 && hp > 0) return $"골드 {gold} · HP {hp}%";
                if (gold > 0) return $"골드 {gold}";
                if (hp > 0) return $"HP {hp}%";
                return "무료";
            }
        }
    }

    /// <summary>
    /// 상점 방 1개 정의 SO. 완주 루프 계획 1-2.
    ///
    /// <see cref="EventData"/>와 나눈 이유는 <b>진행 방식</b>이 달라서다. 이벤트는 선택지 하나를
    /// 고르면 끝나지만 상점은 골드가 닿는 만큼 여러 번 사고 나서 떠난다 — 재고·반복 구매는
    /// 선택지에 없는 개념이라 <c>EventChoice</c>에 얹으면 뜻이 흐려진다.
    /// 반면 <b>효과 어휘는 공유</b>한다(<see cref="EventEffectApplier"/>).
    ///
    /// 스테이지마다 하나씩 두고 가격을 다르게 잡는다 — 스테이지가 깊어질수록 골드 수입이
    /// 늘어나므로 같은 값이면 뒤로 갈수록 상점이 공짜가 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopData", menuName = "Abyss/Data/Shop Data")]
    public sealed class ShopData : ScriptableObject
    {
        [Header("식별자")]
        public string shopId;

        [Header("표시")]
        public string title;
        [TextArea(2, 4)] public string description;

        [Tooltip("진열 품목 3~4개 권장. 골드가 남아돌지 않도록 전부 사면 예산을 넘기게 잡을 것.")]
        public List<ShopItem> items = new();
    }
}
