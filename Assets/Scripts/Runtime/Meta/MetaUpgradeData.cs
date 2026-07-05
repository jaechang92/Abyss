using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>메타 영구 업그레이드 효과 종류.</summary>
    public enum MetaUpgradeType
    {
        MaxHp,             // 최대 HP 가산 (valuePerLevel = 레벨당 +HP)
        AttackMultiplier,  // 공격 배율 가산 (valuePerLevel = 레벨당 +비율, 0.08 = +8%)
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

        [Tooltip("레벨당 효과 증분. MaxHp=HP량, AttackMultiplier=배율(0.08=+8%).")]
        public float valuePerLevel = 1f;

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
