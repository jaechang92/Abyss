#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 상점 방 콘텐츠(ShopData SO) 생성. 완주 루프 계획 1-2.
    ///
    /// <see cref="StageBuilder"/>가 상점 룸을 만들 때 여기서 ShopData를 가져온다.
    /// 이미 존재하는 에셋은 덮어쓰지 않는다 — 가격은 밸런스 조정 대상이라
    /// 인스펙터에서 만진 수치를 빌더 재실행이 되돌리면 안 된다(<see cref="EventContentBuilder"/>와 같은 규약).
    ///
    /// 스테이지마다 하나씩, 가격을 다르게 잡는다. 예산 기준:
    ///   · Stage1 상점 도달 시 누적 골드 <b>142</b> (2026-08-02 완주 플레이테스트 실측)
    ///   · Stage2 ≈ 300 / Stage3 ≈ 480 (추정 — 실측은 Stage1만 있다)
    /// 진열을 전부 사려면 예산을 넘기게 잡아 <b>무엇을 포기할지</b>가 선택이 되게 한다.
    /// </summary>
    public static class ShopContentBuilder
    {
        public const string PeddlerFile = "Shop_WanderingPeddler";
        public const string AshTraderFile = "Shop_AshTrader";
        public const string GraveRobberFile = "Shop_GraveRobber";

        /// <summary>
        /// 상점 SO 3종만 따로 손본다. <see cref="EnsureAllShops"/>는 스테이지 빌더 안에 묻혀 있어
        /// 품목 하나를 넣으려고 스테이지 셋을 통째로 다시 돌려야 했다 — 필요한 것보다 넓게 건드리면
        /// 무엇이 바뀐 diff 인지 판별하는 비용이 변경 자체보다 커진다.
        /// </summary>
        [MenuItem(AbyssMenu.GenerateShopContent)]
        public static void GenerateShopContentMenu()
        {
            EnsureAllShops(out _, out _, out _);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ShopContentBuilder] 상점 3종 점검 완료 — 위 로그에서 추가된 품목 수를 확인할 것.");
        }

        /// <summary>상점 SO 3종을 생성하고(없으면) 반환한다. 순서는 스테이지 순.</summary>
        public static void EnsureAllShops(out ShopData peddler, out ShopData ashTrader, out ShopData graveRobber)
        {
            EnsureDir(AbyssPaths.Shops);

            peddler = CreateOrLoad(PeddlerFile, "shop_wandering_peddler", "떠돌이 상인",
                "구멍 난 외투를 걸친 자가 좌판을 펼쳐 놓았다. 값은 부르는 대로다.",
                new[]
                {
                    // 드래프트는 상점을 떠날 때 열린다 — 설명에 반드시 적어 둔다(적지 않으면 산 게 사라진 것처럼 보인다).
                    Item("잊힌 기술서", "떠날 때 스킬 하나를 고른다", 2,
                        Effect(EventEffectType.GoldSpend, 45),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Item("응급 붕대", "체력을 25% 회복한다", 2,
                        Effect(EventEffectType.GoldSpend, 25),
                        Effect(EventEffectType.HealPercent, 25)),

                    Item("심연의 정수", "체력을 60% 회복한다", 1,
                        Effect(EventEffectType.GoldSpend, 60),
                        Effect(EventEffectType.HealPercent, 60)),

                    // 골드가 마른 플레이어에게도 살 방법을 준다 — 상점 앞에서 할 일이 없는 상황을 막는다.
                    Item("피의 거래", "체력 20%를 팔아 골드를 받는다", 1,
                        Effect(EventEffectType.HpCostPercent, 20),
                        Effect(EventEffectType.GoldGain, 60)),

                    Item("닳은 산가지", "드래프트 리롤 횟수 +1 (이번 런)", 1,
                        Effect(EventEffectType.GoldSpend, 30),
                        Effect(EventEffectType.RerollTicket, 1)),
                });

            ashTrader = CreateOrLoad(AshTraderFile, "shop_ash_trader", "잿더미 행상",
                "타다 만 수레 위에 물건이 쌓여 있다. 아래로 내려온 값은 위와 다르다.",
                new[]
                {
                    Item("불탄 기술서", "떠날 때 스킬 하나를 고른다", 2,
                        Effect(EventEffectType.GoldSpend, 70),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Item("지혈대", "체력을 25% 회복한다", 2,
                        Effect(EventEffectType.GoldSpend, 40),
                        Effect(EventEffectType.HealPercent, 25)),

                    Item("잿불 강장제", "체력을 60% 회복한다", 1,
                        Effect(EventEffectType.GoldSpend, 95),
                        Effect(EventEffectType.HealPercent, 60)),

                    Item("피의 거래", "체력 20%를 팔아 골드를 받는다", 1,
                        Effect(EventEffectType.HpCostPercent, 20),
                        Effect(EventEffectType.GoldGain, 90)),

                    Item("그을린 산가지", "드래프트 리롤 횟수 +1 (이번 런)", 2,
                        Effect(EventEffectType.GoldSpend, 50),
                        Effect(EventEffectType.RerollTicket, 1)),
                });

            graveRobber = CreateOrLoad(GraveRobberFile, "shop_grave_robber", "무덤 도굴꾼",
                "왕들의 부장품을 자루째 끌고 다닌다. 마지막 손님일 거라는 걸 아는 눈치다.",
                new[]
                {
                    // 마지막 상점이라 남은 골드를 쓸 곳이 여기뿐이다 — 기술서 재고를 3으로 늘려
                    // "다 털고 보스로 간다"는 선택을 실제로 가능하게 둔다.
                    Item("왕의 유고", "떠날 때 스킬 하나를 고른다", 3,
                        Effect(EventEffectType.GoldSpend, 95),
                        Effect(EventEffectType.SkillDraft, 0)),

                    Item("장례용 향유", "체력을 25% 회복한다", 2,
                        Effect(EventEffectType.GoldSpend, 55),
                        Effect(EventEffectType.HealPercent, 25)),

                    Item("부장 성수", "체력을 60% 회복한다", 1,
                        Effect(EventEffectType.GoldSpend, 130),
                        Effect(EventEffectType.HealPercent, 60)),

                    Item("피의 거래", "체력 20%를 팔아 골드를 받는다", 1,
                        Effect(EventEffectType.HpCostPercent, 20),
                        Effect(EventEffectType.GoldGain, 120)),

                    // 마지막 상점이라 남는 골드를 흘려보낼 곳이 필요하다 — 재고 2로 둔다.
                    Item("부장 산가지", "드래프트 리롤 횟수 +1 (이번 런)", 2,
                        Effect(EventEffectType.GoldSpend, 70),
                        Effect(EventEffectType.RerollTicket, 1)),
                });
        }

        private static ShopData CreateOrLoad(
            string fileName, string shopId, string title, string description, ShopItem[] items)
        {
            string path = $"{AbyssPaths.Shops}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ShopData>(path);
            if (existing != null)
            {
                int added = AddMissingItems(existing, items);
                Debug.Log(added > 0
                    ? $"[ShopContentBuilder] 품목 {added}개 추가 (기존 유지): {path}"
                    : $"[ShopContentBuilder] 건너뜀 (존재): {path}");
                return existing;
            }

            var so = ScriptableObject.CreateInstance<ShopData>();
            so.shopId = shopId;
            so.title = title;
            so.description = description;
            so.items = new List<ShopItem>(items);
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[ShopContentBuilder] 생성: {path}");
            return so;
        }

        /// <summary>
        /// 코드에는 있는데 에셋에는 없는 품목만 <b>덧붙인다</b>. 추가한 개수를 돌려준다.
        ///
        /// <see cref="CreateOrLoad"/>가 기존 에셋을 통째로 건너뛰는 규약은 <i>가격을 지키기 위한 것</i>이지
        /// 진열을 영원히 얼려 두려던 것이 아니다. 실제로 리롤권을 추가했을 때, 건너뛰기만 하는 코드로는
        /// 빌더를 몇 번을 돌려도 이미 만들어진 상점 3곳에 물건이 나타나지 않았다 — 코드에는 있는데
        /// 게임에는 없는 상태가 조용히 성립한다(EnemyTier·적 SFX 때 같은 함정).
        ///
        /// 라벨로 대조하므로 <b>인스펙터에서 이름을 바꾼 품목은 다른 것으로 보여 다시 들어온다</b>.
        /// 가격·재고·설명은 절대 건드리지 않는다 — 손으로 맞춘 수치를 되돌리지 않는 것이 원래 규약이다.
        /// </summary>
        private static int AddMissingItems(ShopData shop, ShopItem[] items)
        {
            if (shop == null || items == null) return 0;
            shop.items ??= new List<ShopItem>();

            int added = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (shop.items.Exists(existing => existing != null && existing.label == items[i].label)) continue;
                shop.items.Add(items[i]);
                added += 1;
            }

            if (added > 0) EditorUtility.SetDirty(shop);
            return added;
        }

        private static ShopItem Item(string label, string description, int stock, params EventEffect[] effects)
        {
            return new ShopItem
            {
                label = label,
                description = description,
                stock = stock,
                effects = new List<EventEffect>(effects),
            };
        }

        private static EventEffect Effect(EventEffectType type, int amount)
        {
            return new EventEffect { type = type, amount = amount };
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
#endif
