using System;
using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 분기 방의 손익 예고 — 월드 문(<see cref="RoomExitDoor"/>)과 갈림길 모달(<see cref="UI.NodeMapPanel"/>)이
    /// 같은 문장을 쓰게 하는 읽기 전용 표시 원본(26-expedition-discovery §E2, expedition-discovery-contract §2).
    ///
    /// 🔑 <b>값을 여기 적지 않는다.</b> 금액·비율·적 구성은 전부 <see cref="RoomData"/>와
    /// <see cref="EventData"/>의 효과 목록에서 읽는다. 대상 방은 식별자만 들고 있다 — 에셋 수치가 바뀌면 예고도 같이 바뀐다.
    /// 문구는 <see cref="ExpeditionTextKeys"/>(GameText.csv)에서 현재 언어로 읽는다.
    ///
    /// 🔴 <b>예고는 아무것도 일으키지 않는다.</b> <see cref="EventEffectApplier.ApplyNonModal(IReadOnlyList{EventEffect})"/>·
    /// <see cref="EventEffectApplier.GrantDrafts"/>·무기 추첨을 부르지 않고, 골드·체력·RNG 상태를 바꾸지 않는다.
    /// 현재 골드는 <b>읽기만</b> 한다 — 살 수 없는 유료 선택지에 표식을 붙이는 데만 쓴다(<see cref="ReadInputs"/>).
    /// 무기 가차는 결과를 미리 뽑지 않으므로 「무작위」로만 말한다.
    ///
    /// 📌 첫 도입은 Stage1 분기 4방만이다. 그 밖의 방은 기존 <see cref="RoomData.hint"/>를 그대로 쓴다
    /// (Stage2/3 표시를 자동으로 바꾸지 않는다). 해석할 수 없는 효과가 섞이면 임의 숫자로 번역하지 않고 같은 폴백으로 돌아간다.
    ///
    /// 소비자는 <see cref="Inputs"/>(골드·언어)가 바뀌면 다시 만들어 쓴다 — 낡은 문장을 들고 있지 않게.
    /// </summary>
    public static class ExpeditionRoomPreview
    {
        private static readonly HashSet<string> TargetRoomIds = new()
        {
            "room2_skirmish",
            "room3_event_brokenaltar",
            "room5_ambush",
            "room5_alt_sealeddoor"
        };

        /// <summary>
        /// 예고 문장이 기대는 바깥 상태 — 현재 보유 골드와 표시 언어. 표시 중에 이것이 바뀌면 문장을 다시 만든다.
        /// 골드 -1은 런이 없어 잔액을 모른다는 뜻이다(부족 표식을 붙이지 않는다).
        /// </summary>
        public readonly struct Inputs : IEquatable<Inputs>
        {
            public readonly int Gold;
            public readonly LocalizationLanguage Language;

            public Inputs(int gold, LocalizationLanguage language)
            {
                Gold = gold;
                Language = language;
            }

            public bool Equals(Inputs other) => Gold == other.Gold && Language == other.Language;
            public override bool Equals(object obj) => obj is Inputs other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Gold, Language);
        }

        /// <summary>
        /// 현재 골드·언어를 읽는다. 둘 다 <c>GetInstanceSafe</c> — 읽으려다 매니저를 만들지 않는다.
        /// </summary>
        public static Inputs ReadInputs()
        {
            var run = RunManager.GetInstanceSafe();
            var localization = LocalizationManager.GetInstanceSafe();
            int gold = run != null && run.IsRunActive ? run.GoldShards : -1;
            var language = localization != null ? localization.CurrentLanguage : default;
            return new Inputs(gold, language);
        }

        /// <summary>손익 예고 대상 방인지(Stage1 분기 4방).</summary>
        public static bool IsTarget(RoomData room)
            => room != null && !string.IsNullOrEmpty(room.roomId) && TargetRoomIds.Contains(room.roomId);

        /// <summary>갈림길 모달의 상세 줄(현재 골드·언어 기준).</summary>
        public static string Detail(RoomData room) => Detail(room, ReadInputs(), out _);

        /// <summary>
        /// 갈림길 모달의 상세 줄. 대상 방이면 데이터에서 만든 예고(<paramref name="isPreview"/>=true),
        /// 아니면(또는 해석 불가면) 기존 힌트. 힌트도 없으면 빈 문자열 — "정보 없음" 같은 문구는 선택에 도움이 안 된다.
        /// </summary>
        public static string Detail(RoomData room, Inputs inputs, out bool isPreview)
        {
            isPreview = false;
            if (room == null) return string.Empty;

            string fallback = string.IsNullOrEmpty(room.hint) ? string.Empty : room.hint;
            if (!IsTarget(room)) return fallback;

            string preview = Describe(room, inputs.Gold);
            if (string.IsNullOrEmpty(preview)) return fallback;

            isPreview = true;
            return preview;
        }

        /// <summary>
        /// 월드 문의 접근 상세 — <see cref="Detail(RoomData, Inputs, out bool)"/>와 같은 결과에서 예고만 쓴다.
        /// 나머지 방은 빈 문자열 — 기존 문은 힌트를 띄우지 않았으므로 대상 밖의 문 표시를 바꾸지 않는다.
        /// </summary>
        public static string WorldDetail(RoomData room, Inputs inputs)
        {
            string detail = Detail(room, inputs, out bool isPreview);
            return isPreview ? detail : string.Empty;
        }

        private static string Describe(RoomData room, int gold)
        {
            if (room.IsEventRoom) return DescribeEvent(room.eventData, gold);
            if (room.IsShopRoom) return null;
            return DescribeCombat(room);
        }

        // ───────────────────────── 전투 방 ─────────────────────────

        /// <summary>
        /// 첫 무리 구성 / 추가 무리 구성 / 클리어 보상. 골드는 <c>RunManager.GainCombatGoldShards</c>에서
        /// 배율·반올림을 거치므로 「기본」값으로만 말한다(실수령 예측이 아니다).
        /// </summary>
        private static string DescribeCombat(RoomData room)
        {
            var lines = new List<string>();

            string opening = Composition(OpeningEnemies(room));
            if (!string.IsNullOrEmpty(opening)) lines.Add(Loc.GetFormat(ExpeditionTextKeys.EnemiesFormat, opening));

            // 추가 무리는 맵 방만 소환한다(StageDirector.Map — 아레나 방은 reinforcements를 읽지 않는다).
            if (room.IsMapRoom)
            {
                string extra = Composition(ReinforcementEnemies(room));
                if (!string.IsNullOrEmpty(extra)) lines.Add(Loc.GetFormat(ExpeditionTextKeys.ReinforcementsFormat, extra));
            }

            if (room.IsFormRewardRoom)
            {
                lines.Add(Loc.Get(room.formReward != null ? ExpeditionTextKeys.FormReward : ExpeditionTextKeys.FormRewardRandom));
            }
            else if (room.IsWeaponRewardRoom)
            {
                lines.Add(Loc.Get(room.weaponReward != null ? ExpeditionTextKeys.WeaponReward : ExpeditionTextKeys.WeaponRewardRandom));
            }

            if (room.clearGoldReward > 0) lines.Add(Loc.GetFormat(ExpeditionTextKeys.ClearGoldFormat, room.clearGoldReward));

            return string.Join("\n", lines);
        }

        /// <summary>첫 무리 — 스폰 규칙과 같게 맵 방은 placements 우선, 비었거나 아레나 방이면 enemies.</summary>
        private static List<EnemyData> OpeningEnemies(RoomData room)
        {
            var result = new List<EnemyData>();

            if (room.IsMapRoom && room.placements != null && room.placements.Count > 0)
            {
                foreach (var placement in room.placements)
                {
                    if (placement != null && placement.data != null) result.Add(placement.data);
                }
                return result;
            }

            if (room.enemies == null) return result;
            foreach (var entry in room.enemies)
            {
                if (entry == null || entry.data == null) continue;
                for (int i = 0; i < entry.count; i++) result.Add(entry.data);
            }
            return result;
        }

        private static List<EnemyData> ReinforcementEnemies(RoomData room)
        {
            var result = new List<EnemyData>();
            if (room.reinforcements == null) return result;

            foreach (var reinforcement in room.reinforcements)
            {
                if (reinforcement?.enemies == null) continue;
                foreach (var placement in reinforcement.enemies)
                {
                    if (placement != null && placement.data != null) result.Add(placement.data);
                }
            }
            return result;
        }

        /// <summary>근접/원거리 수. 위치·순서는 말하지 않는다 — 위험 종류만 공개하고 대응은 현장에서 고른다.</summary>
        private static string Composition(List<EnemyData> enemies)
        {
            int melee = 0;
            int ranged = 0;
            foreach (var enemy in enemies)
            {
                if (enemy.isRanged) ranged += 1;
                else melee += 1;
            }

            if (melee > 0 && ranged > 0) return Loc.GetFormat(ExpeditionTextKeys.MeleeRangedFormat, melee, ranged);
            if (melee > 0) return Loc.GetFormat(ExpeditionTextKeys.MeleeFormat, melee);
            if (ranged > 0) return Loc.GetFormat(ExpeditionTextKeys.RangedFormat, ranged);
            return string.Empty;
        }

        // ───────────────────────── 이벤트 방 ─────────────────────────

        /// <summary>
        /// 선택지마다 「대가 → 결과」 한 줄. 효과가 없는 선택지는 무료 지나침으로 따로 말한다 —
        /// 이벤트 전체를 유료로 읽히지 않게. 하나라도 해석할 수 없으면 null(호출자가 기존 힌트로 폴백).
        /// </summary>
        private static string DescribeEvent(EventData data, int gold)
        {
            if (data == null || data.choices == null || data.choices.Count == 0) return null;

            var lines = new List<string>();
            foreach (var choice in data.choices)
            {
                if (choice == null || !TryDescribeChoice(choice, gold, out string line)) return null;
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        private static bool TryDescribeChoice(EventChoice choice, int gold, out string line)
        {
            line = null;

            // 비용 합계는 실제 결제·검사와 같은 계산식(EventEffectApplier)에서 읽는다.
            int goldCost = EventEffectApplier.GoldCost(choice.effects);
            int hpCostPercent = EventEffectApplier.HpCostPercent(choice.effects);

            int goldGain = 0;
            int healPercent = 0;
            int drafts = 0;
            int rerolls = 0;
            int weaponDraws = 0;

            if (choice.effects != null)
            {
                foreach (var effect in choice.effects)
                {
                    int amount = Mathf.Max(0, effect.amount);
                    switch (effect.type)
                    {
                        case EventEffectType.GoldSpend:
                        case EventEffectType.HpCostPercent:
                            break;  // 위 합계로 처리
                        case EventEffectType.GoldGain:
                            goldGain += amount;
                            break;
                        case EventEffectType.HealPercent:
                            healPercent += amount;
                            break;
                        case EventEffectType.SkillDraft:
                            drafts += 1;
                            break;
                        case EventEffectType.RerollTicket:
                            rerolls += amount;
                            break;
                        case EventEffectType.WeaponGacha:
                            weaponDraws += Mathf.Max(1, amount);   // 적용부와 같은 규칙(0이면 1회)
                            break;
                        default:
                            return false;
                    }
                }
            }

            var costs = new List<string>();
            if (goldCost > 0) costs.Add(Loc.GetFormat(ExpeditionTextKeys.GoldCostFormat, goldCost));
            // 최대 HP 대비 % — 적용부는 반올림 후 최소 1, 지불 뒤 체력 1은 남긴다(선택으로 죽지 않는다).
            if (hpCostPercent > 0) costs.Add(Loc.GetFormat(ExpeditionTextKeys.HpCostFormat, hpCostPercent));

            var outcomes = new List<string>();
            if (goldGain > 0) outcomes.Add(Loc.GetFormat(ExpeditionTextKeys.GoldGainFormat, goldGain));
            if (healPercent > 0) outcomes.Add(Loc.GetFormat(ExpeditionTextKeys.HealFormat, healPercent));
            if (drafts > 0)
            {
                outcomes.Add(drafts > 1
                    ? Loc.GetFormat(ExpeditionTextKeys.SkillDraftCountFormat, drafts)
                    : Loc.Get(ExpeditionTextKeys.SkillDraft));
            }
            if (rerolls > 0) outcomes.Add(Loc.GetFormat(ExpeditionTextKeys.RerollFormat, rerolls));
            if (weaponDraws > 0)
            {
                outcomes.Add(weaponDraws > 1
                    ? Loc.GetFormat(ExpeditionTextKeys.WeaponDrawCountFormat, weaponDraws)
                    : Loc.Get(ExpeditionTextKeys.WeaponDraw));
            }

            if (costs.Count == 0 && outcomes.Count == 0)
            {
                line = Loc.Get(ExpeditionTextKeys.FreePass);
                return true;
            }

            string cost = costs.Count > 0 ? Fold(costs, ExpeditionTextKeys.CostPairFormat) : Loc.Get(ExpeditionTextKeys.Free);
            line = outcomes.Count > 0
                ? Loc.GetFormat(ExpeditionTextKeys.ChoiceFormat, cost, Fold(outcomes, ExpeditionTextKeys.OutcomePairFormat))
                : cost;

            // 잔액 부족 표식 — 판정은 EventRoomPanel과 같은 「골드 비용 > 보유」. 선택지를 숨기거나 문을 막지 않는다.
            // HP 대가·무료 지나침은 골드와 무관하므로 그대로 둔다.
            if (goldCost > 0 && gold >= 0 && goldCost > gold)
            {
                line = Loc.GetFormat(ExpeditionTextKeys.UnaffordableFormat, line, gold);
            }
            return true;
        }

        /// <summary>항목을 두 칸 형식으로 앞에서부터 접는다 — 구분자 앞뒤 공백을 CSV 값이 잃지 않게.</summary>
        private static string Fold(List<string> items, string pairFormatKey)
        {
            string result = items[0];
            for (int i = 1; i < items.Count; i++) result = Loc.GetFormat(pairFormatKey, result, items[i]);
            return result;
        }
    }
}
