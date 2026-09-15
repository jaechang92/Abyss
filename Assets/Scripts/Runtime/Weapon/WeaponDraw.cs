using System.Collections.Generic;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 무기 추첨. <b>등급 확률이 항목 개수에 끌려가지 않게 정규화한다</b>
    /// (<c>14-weapon-equipment-system.md</c> §3-2).
    ///
    /// 🔴 <b>왜 그냥 가중 추첨을 못 쓰나.</b> <c>RelicGacha</c> 는 항목마다 가중치를 그대로 준다.
    /// 유물은 <b>개수가 고정</b>이라 그게 감당됐지만, 무기는 「언제든 추가·삭제」가 요구사항이다(§4) —
    /// 에셋 하나 넣을 때마다 <b>등급 확률이 조용히 바뀐다.</b>
    ///
    /// 🔑 <b>해법</b>: <c>항목 가중치 = 등급 가중치 / 그 등급의 항목 수</c>
    /// <list type="bullet">
    /// <item>등급 확률은 <b>고정</b>되고 등급 안에서는 균등해진다</item>
    /// <item>여전히 <b>단일 롤</b>이라 「뽑힌 등급에 후보 0개」 실패가 없다 —
    /// 항목이 없는 등급은 애초에 롤에 안 들어간다</item>
    /// <item>에셋을 넣고 빼도 등급 분포가 안 흔들린다</item>
    /// </list>
    ///
    /// ⚠️ 등급 가중치는 <b>여기 하드코딩하지 않는다.</b> <c>RunConfig</c> 가 갖고 호출자가 넘긴다
    /// (<c>09</c> §1-2 SoT 원칙).
    /// </summary>
    public static class WeaponDraw
    {
        /// <summary>
        /// 항목별 정규화 가중치. <b>Unity 타입을 안 쓰는 순수 함수</b>라 에셋 없이 테스트한다.
        /// </summary>
        /// <param name="rarities">후보들의 등급. 순서가 곧 항목 순서다.</param>
        /// <param name="rarityWeights">등급별 가중치. 색인은 <see cref="WeaponRarity"/> 값.</param>
        public static float[] NormalizedWeights(IReadOnlyList<WeaponRarity> rarities,
                                                IReadOnlyList<float> rarityWeights)
        {
            int n = rarities?.Count ?? 0;
            var weights = new float[n];
            if (n == 0 || rarityWeights == null) return weights;

            // 등급마다 후보가 몇 개인지 먼저 센다 — 그 수로 나눠야 등급 확률이 고정된다.
            var counts = new int[rarityWeights.Count];
            for (int i = 0; i < n; i++)
            {
                int r = (int)rarities[i];
                if (r >= 0 && r < counts.Length) counts[r]++;
            }

            for (int i = 0; i < n; i++)
            {
                int r = (int)rarities[i];
                if (r < 0 || r >= rarityWeights.Count || counts[r] == 0) continue;
                weights[i] = rarityWeights[r] / counts[r];
            }
            return weights;
        }

        /// <summary>
        /// <paramref name="roll01"/> (0 이상 1 미만) 로 항목 하나를 고른다. 후보가 없으면 -1.
        ///
        /// 🔑 <b>난수를 인자로 받는다</b> — 그래야 분포를 테스트로 고정할 수 있다
        /// (<c>WeaponAnchorSet.FrameIndexOf</c>·<c>ResolveMovementState</c> 와 같은 태도).
        /// </summary>
        public static int DrawIndex(IReadOnlyList<WeaponRarity> rarities,
                                    IReadOnlyList<float> rarityWeights, double roll01)
        {
            float[] weights = NormalizedWeights(rarities, rarityWeights);

            double total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total <= 0) return -1;

            double roll = roll01 * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            // 부동소수 누적 오차로 끝을 넘길 수 있다 — 가중치가 있는 마지막 항목으로 물러난다.
            for (int i = weights.Length - 1; i >= 0; i--)
            {
                if (weights[i] > 0) return i;
            }
            return -1;
        }

        /// <summary>후보 목록에서 하나를 뽑는다. 후보가 없으면 null.</summary>
        public static WeaponData Draw(IReadOnlyList<WeaponData> candidates,
                                      IReadOnlyList<float> rarityWeights, System.Random rng = null)
        {
            int n = candidates?.Count ?? 0;
            if (n == 0) return null;

            var rarities = new WeaponRarity[n];
            for (int i = 0; i < n; i++)
            {
                rarities[i] = candidates[i] != null ? candidates[i].rarity : WeaponRarity.Common;
            }

            int index = DrawIndex(rarities, rarityWeights, (rng ?? new System.Random()).NextDouble());
            return index >= 0 ? candidates[index] : null;
        }

        /// <summary>
        /// <paramref name="formId"/> 가 쓸 수 있는 무기만 고른다.
        ///
        /// ⚠️ <b>드래프트 필터와 공유하지 않는다</b>(§6). 같은 「폼으로 거른다」라도
        /// 스킬은 <c>formBound</c> 가 비면 공용이고 무기는 <b>비면 못 쓴다</b> — 규칙이 반대다.
        /// 공유하면 한쪽을 고칠 때 다른 쪽이 조용히 바뀐다.
        /// </summary>
        public static List<WeaponData> CandidatesFor(IReadOnlyList<WeaponData> catalog, string formId)
        {
            var result = new List<WeaponData>();
            if (catalog == null || string.IsNullOrEmpty(formId)) return result;

            for (int i = 0; i < catalog.Count; i++)
            {
                WeaponData weapon = catalog[i];
                if (weapon != null && weapon.formBound == formId) result.Add(weapon);
            }
            return result;
        }
    }
}
