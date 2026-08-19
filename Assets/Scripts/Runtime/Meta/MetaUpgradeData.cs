using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 메타 영구 업그레이드 효과 종류.
    ///
    /// ⚠️ 숫자가 직렬화된다 — <b>순서를 바꾸거나 중간에 끼워 넣지 말 것.</b> 새 종류는 뒤에 붙인다.
    /// </summary>
    public enum MetaUpgradeType
    {
        MaxHp,             // 최대 HP 가산 (valuePerLevel = 레벨당 +HP)
        AttackMultiplier,  // 공격 배율 가산 (valuePerLevel = 레벨당 +비율, 0.08 = +8%)

        // ── 시작 특전 (3-2, 2026-08-19) ──
        // 스탯 강화와 달리 "런을 시작할 때 한 번" 적용된다. 뺏는 게 아니라 더하는 것이라
        // 기존 플레이 밸런스를 건드리지 않는다.
        StartingGold,      // 런 시작 골드 (valuePerLevel = 레벨당 +골드)
        FreeReroll,        // 드래프트 무료 리롤 횟수 (valuePerLevel = 레벨당 +1회)

        // ── 해금 (3-2, 2026-08-19) ──
        // 레벨 개념이 없다 — costLadder 길이 1로 두고 레벨 1이면 해금이다.
        // 대상은 requiresMetaUnlock=true인 에셋뿐이고, 지금은 그런 에셋이 0개다(시스템만 선다).
        UnlockForm,        // unlockTargetId의 폼 해금
        UnlockSkill,       // unlockTargetId의 스킬을 드래프트 풀에 편입
    }

    /// <summary>
    /// 심연의 제단 영구 업그레이드 정의 SO. abyss_shards로 레벨업하며 런에 영구 반영된다.
    /// Resources/Data/MetaUpgrades/ 하위에 배치 → MetaUpgrades 정적 헬퍼가 로드.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaUpgrade", menuName = "Abyss/Data/Meta Upgrade")]
    public sealed class MetaUpgradeData : ScriptableObject
    {
        [Tooltip("고유 ID. MetaSave 레벨 저장 키.")]
        public string upgradeId;

        [Tooltip("업그레이드 이름 StringKey (GameText.csv).")]
        public string nameKey;

        [Tooltip("업그레이드 설명 StringKey (GameText.csv).")]
        public string descKey;

        [Tooltip("효과 종류.")]
        public MetaUpgradeType type = MetaUpgradeType.MaxHp;

        [Tooltip("레벨당 효과 증분. MaxHp=HP량, AttackMultiplier=배율(0.08=+8%), StartingGold=골드, FreeReroll=횟수.")]
        public float valuePerLevel = 1f;

        [Tooltip("UnlockForm/UnlockSkill 전용 — 해금할 formId 또는 skillId. 다른 타입에서는 무시.")]
        public string unlockTargetId;

        /// <summary>해금 항목인가(레벨업이 아니라 1회 구매).</summary>
        public bool IsUnlock => type == MetaUpgradeType.UnlockForm || type == MetaUpgradeType.UnlockSkill;

        [Tooltip("레벨업 비용 사다리(abyss_shards). 길이=최대 레벨. index i = (i→i+1) 비용.")]
        public int[] costLadder = { 30, 60, 100, 150, 220 };

        /// <summary>최대 레벨(비용 사다리 길이).</summary>
        public int MaxLevel => costLadder != null ? costLadder.Length : 0;

        /// <summary>현재 레벨에서 다음 레벨업 비용. 최대치면 -1.</summary>
        public int CostForNextLevel(int currentLevel)
        {
            if (costLadder == null || currentLevel < 0 || currentLevel >= costLadder.Length) return -1;
            return costLadder[currentLevel];
        }

        /// <summary>지정 레벨에서의 누적 효과값(valuePerLevel × level).</summary>
        public float EffectAtLevel(int level) => valuePerLevel * Mathf.Max(0, level);
    }
}
