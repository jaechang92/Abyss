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
    }
}
