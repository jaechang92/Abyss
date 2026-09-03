using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Runtime.Meta
{
    /// <summary>
    /// 유물 가차 추첨. 등급 가중 → 그 등급 안에서 균등이 아니라,
    /// <b>유물 단위 단일 가중 추첨</b>이다(스킬 드래프트 <c>DraftPoolManager</c>와 같은 방식).
    ///
    /// 🔑 2단계(등급 롤 → 그 등급에서 하나)로 하지 않은 이유: 그 방식은 <b>"뽑힌 등급에 후보가
    /// 0개"</b>라는 실패 모드를 갖는다. 초기에는 등급별 유물 수가 고르지 않아 실제로 일어난다.
    /// 단일 롤에서는 후보가 없는 등급이 그냥 안 나올 뿐이라 예외 처리가 필요 없다.
    /// (스킬 15→18 작업에서 "가중치 60이 곧 확률 60%는 아니다"를 확인한 것과 같은 구조다 —
    /// 실효 확률은 등급별 <i>개수</i>에 따라 달라진다.)
    /// </summary>
    public static class RelicGacha
    {
        /// <summary>뽑기 1회 비용(심연 조각).</summary>
        public const int DRAW_COST = 50;

        /// <summary>
        /// 등급별 추첨 가중치. 인덱스가 <see cref="RelicRarity"/> 순서다 —
        /// enum 순서를 바꾸면 확률이 조용히 뒤바뀐다.
        ///
        /// 값이 SO가 아니라 상수인 이유: 가차는 게임에 하나뿐이라 SO로 빼면 "에셋을 안 만들면
        /// 확률이 0"인 실패 경로가 새로 생긴다. 밸런스를 실제로 만지게 되면 그때 승격할 것.
        /// </summary>
        public static readonly int[] RARITY_WEIGHTS = { 60, 30, 10 };

        /// <summary>
        /// 카탈로그에서 유물 하나를 뽑는다. 후보가 없으면 null.
        ///
        /// <b>이미 가진 유물도 후보에 남는다</b> — 중복은 레벨을 올리므로 꽝이 아니다.
        /// 최대 레벨에 닿은 것까지 남기는 이유는, 빼기 시작하면 후반에 풀이 급격히 좁아져
        /// 남은 하나가 연속으로 나오기 때문이다. 그건 '운'이 아니라 고장으로 읽힌다.
        /// </summary>
        public static RelicData Draw(IReadOnlyList<RelicData> catalog, System.Random rng = null)
        {
            if (catalog == null || catalog.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                total += WeightOf(catalog[i]);
            }
            if (total <= 0) return null;

            int roll = rng != null ? rng.Next(total) : Random.Range(0, total);
            for (int i = 0; i < catalog.Count; i++)
            {
                roll -= WeightOf(catalog[i]);
                if (roll < 0) return catalog[i];
            }

            // 부동소수 없이 정수만 쓰므로 여기 도달하지 않는다. 도달했다면 가중치가 도중에 바뀐 것.
            return catalog[catalog.Count - 1];
        }

        /// <summary>유물 하나의 추첨 가중치. 효과로 쓸 수 없는 것(해금 2종)은 0이라 안 뽑힌다.</summary>
        public static int WeightOf(RelicData relic)
        {
            if (relic == null || !relic.IsUsableEffect) return 0;

            int index = (int)relic.rarity;
            return index >= 0 && index < RARITY_WEIGHTS.Length ? RARITY_WEIGHTS[index] : 0;
        }
    }
}
