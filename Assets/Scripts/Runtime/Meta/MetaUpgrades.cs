using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 메타 업그레이드 카탈로그 + 효과 합산 조회. Resources/Data/MetaUpgrades/의 모든 SO를 로드해
    /// 현재 MetaSave 레벨과 결합, 런에 적용할 실효 보너스를 계산한다.
    /// MetaSaveService가 없으면(에디터 단독 플레이 등) 보너스 0/배율 1로 안전 폴백.
    /// </summary>
    public static class MetaUpgrades
    {
        private const string RESOURCES_FOLDER = "Data/MetaUpgrades";
        private static MetaUpgradeData[] all;

        /// <summary>카탈로그 전체(지연 로드). 에디터 재생성 후엔 Reload 필요.</summary>
        public static MetaUpgradeData[] All => all ??= Resources.LoadAll<MetaUpgradeData>(RESOURCES_FOLDER);

        /// <summary>카탈로그 캐시 무효화(에디터 빌더/치트에서 SO 갱신 후 호출).</summary>
        public static void Reload() => all = null;

        /// <summary>최대 HP 가산 보너스 합.</summary>
        public static int MaxHpBonus()
        {
            // Instance 접근으로 즉석 생성+로드 — Bootstrap 미경유(로비 단독 플레이 등)에서도
            // 저장된 업그레이드 레벨을 반영해 초기화 순서에 따른 HP 불일치를 막는다.
            var svc = MetaSaveService.Instance;
            if (svc == null) return 0;
            int bonus = 0;
            foreach (var up in All)
            {
                if (up == null || up.type != MetaUpgradeType.MaxHp) continue;
                bonus += Mathf.RoundToInt(up.EffectAtLevel(svc.GetUpgradeLevel(up.upgradeId)));
            }
            return bonus;
        }

        /// <summary>공격 배율(1 + 가산 합). 업그레이드 없으면 1.</summary>
        public static float AttackMultiplier()
        {
            var svc = MetaSaveService.Instance;  // 즉석 생성+로드(초기화 순서 무관 일관성)
            if (svc == null) return 1f;
            float bonus = 0f;
            foreach (var up in All)
            {
                if (up == null || up.type != MetaUpgradeType.AttackMultiplier) continue;
                bonus += up.EffectAtLevel(svc.GetUpgradeLevel(up.upgradeId));
            }
            return 1f + bonus;
        }

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

        private static int SumAsInt(MetaUpgradeType type)
        {
            var svc = MetaSaveService.Instance;  // 즉석 생성+로드(초기화 순서 무관 일관성)
            if (svc == null) return 0;
            float sum = 0f;
            foreach (var up in All)
            {
                if (up == null || up.type != type) continue;
                sum += up.EffectAtLevel(svc.GetUpgradeLevel(up.upgradeId));
            }
            return Mathf.RoundToInt(sum);
        }
    }
}
