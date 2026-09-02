using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// <see cref="EventEffect"/> 적용·가격 계산의 단일 소스(SoT).
    ///
    /// 이벤트 방(1-1)이 이 어휘를 처음 만들었고 휴식 방(1-3)·상점 방(1-2)이 그대로 쓴다.
    /// 소비자가 둘이 되는 순간 각자 switch 문을 들고 있으면 효과가 하나 늘 때마다 한쪽을 빠뜨리므로
    /// 여기로 뺐다(<see cref="RoomTypeDisplay"/>·<see cref="Abyss.Runtime.Draft.SynergyAxis"/>와 같은 판단).
    ///
    /// <b>모달을 여는 효과는 즉시 적용하지 않는다.</b> <see cref="EventEffectType.SkillDraft"/>는
    /// 드래프트 모달을 열며 전역 정지를 다시 걸기 때문에, 선택 패널이 열려 있는 동안 적용하면
    /// 두 모달이 겹치고 먼저 닫히는 쪽이 정지를 풀어버린다. 그래서 <see cref="ApplyNonModal"/>은
    /// 미뤄야 할 횟수만 돌려주고, 호출자가 <b>패널을 닫은 뒤</b> <see cref="GrantDrafts"/>를 부른다.
    /// </summary>
    public static class EventEffectApplier
    {
        /// <summary>효과 묶음이 요구하는 골드 총액(0이면 무료).</summary>
        public static int GoldCost(IReadOnlyList<EventEffect> effects) => SumAmount(effects, EventEffectType.GoldSpend);

        /// <summary>효과 묶음이 요구하는 체력 대가(최대 HP 대비 %).</summary>
        public static int HpCostPercent(IReadOnlyList<EventEffect> effects) => SumAmount(effects, EventEffectType.HpCostPercent);

        /// <summary>
        /// 모달을 열지 않는 효과를 즉시 적용하고, 미뤄야 할 스킬 드래프트 횟수를 돌려준다.
        ///
        /// 골드 잔액은 호출자가 미리 걸러야 한다 — 여기서는 지불이 실패해도 조용히 넘어가므로
        /// (<see cref="RunManager.SpendGoldShards"/>는 상태를 바꾸지 않고 false를 준다)
        /// 잔액 검사 없이 부르면 나머지 효과만 공짜로 적용된다.
        /// </summary>
        public static int ApplyNonModal(IReadOnlyList<EventEffect> effects)
        {
            if (effects == null) return 0;

            var run = RunManager.HasInstance ? RunManager.Instance : null;
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            int deferredDrafts = 0;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                switch (effect.type)
                {
                    case EventEffectType.GoldGain:
                        run?.GainGoldShards(effect.amount);
                        break;

                    case EventEffectType.GoldSpend:
                        run?.SpendGoldShards(effect.amount);
                        break;

                    case EventEffectType.HealPercent:
                        if (player != null) player.Heal(PercentOfMaxHp(player, effect.amount));
                        break;

                    case EventEffectType.HpCostPercent:
                        // 전투 피해가 아니다 — 방어 버프를 타지 않고 최소 1을 남기는 전용 경로.
                        if (player != null) player.PayHpCost(PercentOfMaxHp(player, effect.amount));
                        break;

                    case EventEffectType.SkillDraft:
                        deferredDrafts += 1;
                        break;

                    case EventEffectType.RerollTicket:
                        // 드래프트와 달리 미룰 필요가 없다 — 모달을 열지 않고 숫자만 올린다.
                        // 세션이 없을 때 사는 것이 정상 경로라(상점 방), 다음 드래프트에서 쓰인다.
                        var draft = Object.FindAnyObjectByType<DraftSessionController>();
                        if (draft != null) draft.GrantExtraRerolls(effect.amount);
                        break;
                }
            }

            return deferredDrafts;
        }

        /// <summary>
        /// 미뤄둔 스킬 드래프트를 연다. <b>선택 패널을 닫은 뒤</b> 호출할 것.
        ///
        /// 2회 이상이어도 그냥 연달아 요청하면 된다 — <c>DraftSessionController</c>가 세션 진행 중
        /// 도착한 레벨업을 큐에 쌓아 순차로 열어준다.
        /// <see cref="DraftTriggerReason.RoomReward"/>는 "방이 주는 보상"이라는 뜻이 그대로 맞는다.
        /// </summary>
        public static void GrantDrafts(int count)
        {
            if (count <= 0) return;

            var run = RunManager.HasInstance ? RunManager.Instance : null;
            if (run == null) return;

            for (int i = 0; i < count; i++)
            {
                run.GrantBonusLevel(DraftTriggerReason.RoomReward);
            }
        }

        /// <summary>최대 HP 대비 백분율을 실제 HP 값으로. 0%가 아닌 이상 최소 1은 나오게 한다.</summary>
        private static int PercentOfMaxHp(PlayerCharacter player, int percent)
        {
            if (percent <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(player.BaseHp * percent / 100f));
        }

        private static int SumAmount(IReadOnlyList<EventEffect> effects, EventEffectType type)
        {
            if (effects == null) return 0;

            int sum = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].type == type) sum += Mathf.Max(0, effects[i].amount);
            }
            return sum;
        }
    }
}
