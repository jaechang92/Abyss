using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 드래프트 스킬 풀 관리. 9개 프로토 스킬 보유 + 가중치 기반 N장 추첨.
    /// DraftWeightCalculator와 협업: 이 클래스는 풀 데이터·랜덤 추첨만 책임.
    /// </summary>
    public sealed class DraftPoolManager : MonoBehaviour
    {
        [SerializeField] private List<SkillData> pool = new();

        public int PoolSize => pool.Count;

        public void SetPool(IEnumerable<SkillData> skills)
        {
            pool.Clear();
            foreach (var s in skills)
            {
                if (s != null) pool.Add(s);
            }
        }

        /// <summary>
        /// 중복 없이 count장 추첨. excludedSkillIds에 포함된 스킬은 후보에서 제외.
        /// </summary>
        public IReadOnlyList<SkillData> DrawOptions(
            int count,
            string currentFormId,
            IReadOnlyCollection<string> ownedSynergyTags,
            IReadOnlyCollection<string> excludedSkillIds = null)
        {
            var candidates = new List<SkillData>(pool.Count);
            var weights = new List<float>(pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                var skill = pool[i];
                if (skill == null) continue;
                if (excludedSkillIds != null && excludedSkillIds.Contains(skill.skillId)) continue;

                float w = DraftWeightCalculator.CalculateWeight(skill, currentFormId, ownedSynergyTags);
                if (w > 0f)
                {
                    candidates.Add(skill);
                    weights.Add(w);
                }
            }

            var result = new List<SkillData>(count);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int picked = PickWeightedIndex(weights);
                result.Add(candidates[picked]);
                candidates.RemoveAt(picked);
                weights.RemoveAt(picked);
            }

            return result;
        }

        private static int PickWeightedIndex(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0f) return 0;

            float roll = Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += weights[i];
                if (roll <= acc) return i;
            }
            return weights.Count - 1;
        }
    }
}
