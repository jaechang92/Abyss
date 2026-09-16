using System.Collections.Generic;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 「이 방이 어떤 무기를 내놓는가」를 정하는 규칙. 획득 3창구 중 <b>제단</b>이 쓴다
    /// (<c>14-weapon-equipment-system.md</c> §3-B).
    ///
    /// 🔑 <b>순수 함수로 떼어 둔다.</b> <c>StageDirector</c> 안에 두면 씬 없이는 못 재현하는데,
    /// 「보상이 안 나온다」는 <b>오류가 아니라 게이트가 조용히 스킵되는</b> 모양으로 드러난다.
    /// <see cref="WeaponDraw"/>·<c>WeaponAnchorSet.FrameIndexOf</c>와 같은 태도다.
    /// </summary>
    public static class WeaponReward
    {
        /// <summary>
        /// 제단이 제시할 무기를 정한다. 제시할 것이 없으면 <c>null</c>(호출자는 게이트를 걸지 않는다).
        ///
        /// <list type="number">
        /// <item>고정 지정이 있으면 그대로 — 보스 방처럼 <b>무엇이 나올지 정해 둔</b> 자리용</item>
        /// <item>아니면 현재 폼이 쓸 수 있는 무기 중에서 등급 가중 추첨</item>
        /// </list>
        ///
        /// 🔑 <b>이미 가진 무기도 후보에 남긴다.</b> 중복은 강화라 꽝이 아니고(§7),
        /// 빼기 시작하면 후반에 풀이 급격히 좁아져 남은 하나가 연속으로 나온다 —
        /// 그건 '운'이 아니라 고장으로 읽힌다(<c>RelicGacha</c>가 같은 이유로 같은 선택을 했다).
        ///
        /// ⚠️ <b>폼 보상과 규칙이 일부러 다르다.</b> 폼은 <b>미보유 우선</b>으로 뽑는데,
        /// 폼에는 중복 개념이 없어 이미 가진 폼을 주면 정말로 빈손이기 때문이다.
        /// 같은 「보상 추첨」이라고 한 함수로 묶으면 한쪽을 고칠 때 다른 쪽이 조용히 바뀐다.
        /// </summary>
        /// <param name="fixedReward">룸이 고정 지정한 무기(없으면 <c>null</c>).</param>
        /// <param name="catalog">전체 무기 카탈로그.</param>
        /// <param name="formId">지금 폼의 ID. 무기는 폼 전용이라 이게 없으면 뽑을 수 없다.</param>
        /// <param name="rarityWeights">등급별 가중치. <c>RunConfig</c>가 갖는다 — 여기 하드코딩하지 않는다.</param>
        public static WeaponData Resolve(WeaponData fixedReward, IReadOnlyList<WeaponData> catalog,
                                         string formId, IReadOnlyList<float> rarityWeights,
                                         System.Random rng = null)
        {
            if (fixedReward != null) return fixedReward;

            List<WeaponData> candidates = WeaponDraw.CandidatesFor(catalog, formId);
            return candidates.Count > 0 ? WeaponDraw.Draw(candidates, rarityWeights, rng) : null;
        }
    }
}
