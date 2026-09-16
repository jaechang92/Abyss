using System.Collections.Generic;

namespace Abyss.Runtime.Weapon
{
    /// <summary>
    /// 상점 무기 좌판의 규칙. 획득 3창구 중 <b>둘째</b>다(§3-B).
    ///
    /// 🔑 <b>제단과 같은 문제, 다른 답.</b> 둘 다 「어느 무기냐」를 에셋에 못 적는다(무기는 폼 전용이라
    /// 여는 시점에야 정해진다). 다른 점은 <b>값을 치른다</b>는 것이고, 그래서 여기에는
    /// <see cref="PriceOf"/> 가 있다.
    ///
    /// 🔴 <b>가격은 한 번만 계산한다.</b> 진열·잔액 판정·차감이 각자 계산하면 어긋나고,
    /// 그건 「살 수 있다고 떴는데 안 사진다」로 드러난다. 호출자는 <see cref="Offer"/> 를 받아
    /// <b>그 안의 값만</b> 쓴다 — <c>ShopItem.GoldCost</c>(효과에서 파생)와 섞지 말 것.
    /// </summary>
    public static class WeaponShop
    {
        /// <summary>좌판 한 자리. 무엇을 파는지와 값이 <b>한 벌로</b> 온다.</summary>
        public readonly struct Offer
        {
            public readonly WeaponData Weapon;
            public readonly int Price;

            public Offer(WeaponData weapon, int price)
            {
                Weapon = weapon;
                Price = price;
            }

            /// <summary>진열할 것이 있는지. 후보가 없으면 그 자리는 아예 감춘다(품절이 아니다).</summary>
            public bool IsValid => Weapon != null;
        }

        /// <summary>
        /// 등급에 매긴 값. 표가 짧아 등급이 없으면 0이고, 0은 「무료」로 보이므로
        /// 호출자는 <see cref="Build"/> 를 쓴다 — 거기서 값이 0 이하인 자리는 걸러진다.
        /// </summary>
        public static int PriceOf(WeaponRarity rarity, IReadOnlyList<int> priceByRarity)
        {
            int index = (int)rarity;
            if (priceByRarity == null || index < 0 || index >= priceByRarity.Count) return 0;
            return priceByRarity[index];
        }

        /// <summary>
        /// 좌판을 채운다. <paramref name="slotCount"/> 자리를 <b>서로 다른 무기</b>로 메운다 —
        /// 같은 무기가 두 자리에 놓이면 값만 두 번 보이고 살 이유는 한 번뿐이라 고장으로 읽힌다.
        ///
        /// 후보가 모자라면 채운 만큼만 돌려준다. 빈 자리는 호출자가 감춘다.
        ///
        /// 🔑 <b>이미 가진 무기도 후보다</b> — 중복은 강화라 꽝이 아니다(제단과 같은 판단).
        /// </summary>
        public static List<Offer> Build(IReadOnlyList<WeaponData> catalog, string formId,
                                        IReadOnlyList<float> rarityWeights,
                                        IReadOnlyList<int> priceByRarity,
                                        int slotCount, System.Random rng = null)
        {
            var offers = new List<Offer>();
            if (slotCount <= 0) return offers;

            List<WeaponData> pool = WeaponDraw.CandidatesFor(catalog, formId);
            rng ??= new System.Random();

            while (offers.Count < slotCount && pool.Count > 0)
            {
                WeaponData picked = WeaponDraw.Draw(pool, rarityWeights, rng);
                if (picked == null) break;   // 가중치가 전부 0 — 더 돌려도 같은 결과다

                pool.Remove(picked);         // 같은 무기를 두 자리에 놓지 않는다

                int price = PriceOf(picked.rarity, priceByRarity);
                if (price <= 0) continue;    // 값을 안 매긴 등급 — 공짜로 내놓지 않는다

                offers.Add(new Offer(picked, price));
            }
            return offers;
        }
    }
}
