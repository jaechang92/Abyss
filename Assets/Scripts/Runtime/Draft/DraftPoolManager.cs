using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Draft
{
    /// <summary>
    /// 드래프트 스킬 풀 관리 + 가중치 기반 N장 추첨.
    /// 풀은 SkillCatalog(Resources/Data/Skills 전량)에서 지연 로드한다 — 새 스킬 에셋만 추가하면
    /// 자동 편입(에디터 메뉴 재실행 불필요). DraftWeightCalculator와 협업: 이 클래스는 풀 데이터·추첨만 책임.
    /// </summary>
    public sealed class DraftPoolManager : MonoBehaviour
    {
        // 런타임 전용(비직렬화). 씬에 굳는 스냅샷을 제거해 카탈로그(폴더)를 SoT로 단일화한다.
        private readonly List<SkillData> pool = new();
        private bool isLoaded;

        public int PoolSize { get { EnsureLoaded(); return pool.Count; } }

        /// <summary>치트/디버그용 풀 조회(런타임에 전체 스킬 열람).</summary>
        public IReadOnlyList<SkillData> Pool { get { EnsureLoaded(); return pool; } }

        /// <summary>
        /// 명시적 풀 주입(테스트/치트). 주입 후엔 카탈로그 자동 로드를 억제해 테스트 격리를 보장한다.
        /// </summary>
        public void SetPool(IEnumerable<SkillData> skills)
        {
            pool.Clear();
            foreach (var s in skills)
            {
                if (s != null) pool.Add(s);
            }
            isLoaded = true;
        }

        /// <summary>
        /// 풀이 비어 있으면 SkillCatalog에서 1회 채운다(지연 로드, 실행순서 무관).
        ///
        /// <b>메타 해금이 필요한 스킬은 해금 전까지 안 들어온다</b>(3-2).
        /// 잠금은 <b>옵트인</b>이라 <c>requiresMetaUnlock</c>이 false인 스킬 — 지금 18종 전부 —
        /// 은 세이브를 보지도 않고 그대로 들어온다. 즉 현행 플레이는 하나도 안 바뀐다.
        /// </summary>
        private void EnsureLoaded()
        {
            if (isLoaded) return;
            isLoaded = true;
            if (pool.Count > 0) return;

            var meta = MetaSaveService.Instance;
            foreach (var s in SkillCatalog.All)
            {
                if (s == null) continue;
                // meta가 없으면(에디터 단독 플레이 등) 잠긴 스킬만 빼고 나머지는 그대로 쓴다.
                if (s.requiresMetaUnlock && (meta == null || !meta.IsSkillUnlocked(s))) continue;
                pool.Add(s);
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
            EnsureLoaded();

            var candidates = new List<SkillData>(pool.Count);
            var weights = new List<float>(pool.Count);

            // 희귀도 분포는 RunConfig(SoT)에서 읽어 계산기에 주입한다. 미로드 시 Analyst 프로토 분포로 폴백.
            var cfg = RunConfigProvider.Current;
            var rarityWeights = cfg != null
                ? new RarityWeights(cfg.commonWeight, cfg.rareWeight, cfg.epicWeight, cfg.legendaryWeight)
                : RarityWeights.Default;

            for (int i = 0; i < pool.Count; i++)
            {
                var skill = pool[i];
                if (skill == null) continue;
                if (excludedSkillIds != null && excludedSkillIds.Contains(skill.skillId)) continue;

                float w = DraftWeightCalculator.CalculateWeight(skill, currentFormId, ownedSynergyTags, rarityWeights);
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
