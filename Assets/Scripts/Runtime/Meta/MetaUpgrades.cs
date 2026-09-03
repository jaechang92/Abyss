using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 메타 업그레이드 카탈로그 + 효과 합산 조회. Resources/Data/MetaUpgrades/의 모든 SO를 로드해
    /// 현재 MetaSave 레벨과 결합, 런에 적용할 실효 보너스를 계산한다.
    /// MetaSaveService가 없으면(에디터 단독 플레이 등) 보너스 0/배율 1로 안전 폴백.
    ///
    /// 🔑 <b>합산 규칙은 하나, 출처는 둘이다.</b>
    /// <list type="bullet">
    /// <item>제단 — <see cref="MetaUpgradeData"/> × 구매 레벨 (확정 구매)</item>
    /// <item>유물 — <see cref="RelicData"/> × 보유 레벨, 단 <b>장착된 것만</b> (확률 획득)</item>
    /// </list>
    /// 둘 다 "런을 시작할 때 이미 적용돼 있는 것"이라 효과 어휘를 공유한다
    /// (<see cref="MetaUpgradeType"/>). 여기서 합치지 않고 호출부마다 유물을 따로 더하면,
    /// 새 효과 종류를 추가할 때 한쪽만 반영되는 자리가 생긴다.
    ///
    /// <b>보유가 아니라 장착을 세는 이유</b>: 뽑기는 되돌릴 수 없고 장착은 언제든 바꾸는
    /// 빌드 선택이다. 보유만으로 효과가 붙으면 슬롯 3칸이 아무 의미가 없다.
    /// </summary>
    public static class MetaUpgrades
    {
        private const string RESOURCES_FOLDER = "Data/MetaUpgrades";
        private static MetaUpgradeData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static MetaUpgradeData[] All => all ??= Resources.LoadAll<MetaUpgradeData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        /// <summary>최대 HP 가산 보너스 합(제단 + 장착 유물).</summary>
        public static int MaxHpBonus() => SumAsInt(MetaUpgradeType.MaxHp);

        /// <summary>공격 배율(1 + 가산 합). 업그레이드·유물이 없으면 1.</summary>
        public static float AttackMultiplier() => 1f + Sum(MetaUpgradeType.AttackMultiplier);

        // ───────────────────────── 시작 특전 (3-2) ─────────────────────────
        //
        // 스탯 강화와 달리 런을 시작할 때 한 번 적용된다. 둘 다 "뺏는 게 아니라 더하는 것"이라
        // 기존 플레이 밸런스를 건드리지 않는다 — 해금(잠금)을 옵트인으로 둔 것과 같은 방침이다.

        /// <summary>런 시작 골드 보너스 합. 업그레이드가 없으면 0.</summary>
        public static int StartingGoldBonus() => SumAsInt(MetaUpgradeType.StartingGold);

        /// <summary>
        /// 드래프트 무료 리롤 횟수 합. 업그레이드가 없으면 0.
        ///
        /// 리롤 <b>가능 횟수</b>를 늘리지 않는다 — 상한은 `RunConfig.rerollCostLadder` 길이 그대로이고,
        /// 앞에서부터 이 횟수만큼이 <b>공짜</b>가 된다. 상한을 건드리면 드래프트 한 번에 볼 수 있는
        /// 카드 수가 바뀌어 밸런스가 흔들린다.
        /// </summary>
        public static int FreeRerollCount() => SumAsInt(MetaUpgradeType.FreeReroll);

        private static int SumAsInt(MetaUpgradeType type) => Mathf.RoundToInt(Sum(type));

        /// <summary>
        /// 이 효과 종류의 실효 합. 제단 업그레이드와 <b>장착된</b> 유물을 함께 센다.
        ///
        /// <see cref="MetaSaveService"/>가 없으면(에디터 단독 플레이 등) 0 — 호출부는
        /// 그 0을 "보너스 없음"으로 안전하게 해석한다. HP 가산이든 공격 배율의 가산분이든
        /// 0이 곧 무효과라 이 폴백이 어느 종류에서도 틀리지 않는다.
        ///
        /// 🔴 <b>Instance 접근으로 즉석 생성+로드</b>한다. Bootstrap 을 안 거친 진입(로비 단독
        /// 플레이 등)에서도 저장된 레벨을 반영해야, 초기화 순서에 따라 HP가 달라지는 일이 없다.
        /// </summary>
        private static float Sum(MetaUpgradeType type)
        {
            var svc = MetaSaveService.Instance;
            if (svc == null) return 0f;

            float sum = 0f;

            // 출처 ① 제단 — 산 만큼 레벨이 오른다.
            foreach (var up in All)
            {
                if (up == null || up.type != type) continue;
                sum += up.EffectAtLevel(svc.GetUpgradeLevel(up.upgradeId));
            }

            // 출처 ② 유물 — 장착한 것만. 보유 목록이 아니라 슬롯을 훑는 이유가 여기 있다.
            var equipped = svc.EquippedRelicIds;
            for (int i = 0; i < equipped.Count; i++)
            {
                var relic = RelicCatalog.GetById(equipped[i]);
                if (relic == null || relic.effectType != type || !relic.IsUsableEffect) continue;
                sum += relic.EffectAtLevel(svc.GetRelicLevel(relic.relicId));
            }

            return sum;
        }
    }
}
