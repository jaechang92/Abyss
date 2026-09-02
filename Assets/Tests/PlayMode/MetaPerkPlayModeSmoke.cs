using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 시작 특전(3-2) 스모크 — 제단에서 산 것이 실제 런에 도착하는가.
    ///
    /// 특전은 <b>타입·배선이 먼저 서고 에셋이 나중에 붙은</b> 기능이라, 오랫동안 코드가 다 있는데
    /// 아무 일도 일어나지 않는 상태였다. 그런 기능은 "구현됐다"와 "동작한다" 사이가 벌어져도
    /// 아무도 모른다 — 화면에 항목이 없으니 눌러 볼 수도 없다.
    ///
    /// 그래서 두 층을 따로 본다.
    /// ① <b>합성 업그레이드</b>로 소비 경로(RunManager·DraftSessionController)를 검증 — 에셋과 무관.
    /// ② <b>실제 카탈로그</b>에 특전 에셋이 있는지 검증 — 빌더 메뉴 재실행이 끝났는지 여기서 걸린다.
    /// </summary>
    public sealed class MetaPerkPlayModeSmoke
    {
        private const string StartingGoldId = "meta_starting_gold";
        private const string FreeRerollId = "meta_free_reroll";

        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private readonly List<Object> spawned = new List<Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in spawned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            spawned.Clear();

            MetaUpgrades.Reload();  // 주입한 합성 카탈로그를 반드시 되돌린다
            yield return null;

            // 런은 전역 상태다 — 켜둔 채 끝내면 다음 테스트의 StartNewRun 이 조용히 무시된다.
            if (RunManager.HasInstance && RunManager.Instance.IsRunActive) RunManager.Instance.EndRun();

            saveGuard.Release();
        }

        // ───────────────────── ① 소비 경로 (에셋 무관) ─────────────────────

        /// <summary>시작 골드 특전 레벨이 런 시작 골드로 들어온다.</summary>
        [UnityTest]
        public IEnumerator 시작_골드_특전은_런_시작_골드에_반영된다()
        {
            var perk = MakeUpgrade(StartingGoldId, MetaUpgradeType.StartingGold, valuePerLevel: 25f, levels: 3);
            InjectCatalog(perk);
            BuyLevels(perk, 2);   // 2레벨 = +50

            RunManager.Instance.EndRun();       // 진행 중인 런이 있으면 StartNewRun 이 무시된다
            RunManager.Instance.StartNewRun();
            yield return null;

            Assert.AreEqual(50, RunManager.Instance.GoldShards,
                "시작 골드 특전 2레벨(25×2)이 런 시작 골드에 반영되지 않았다.");
        }

        /// <summary>특전이 하나도 없으면 시작 골드는 0 — 지금까지의 동작 그대로여야 한다.</summary>
        [UnityTest]
        public IEnumerator 특전이_없으면_시작_골드는_0이다()
        {
            InjectCatalog();  // 빈 카탈로그

            RunManager.Instance.EndRun();
            RunManager.Instance.StartNewRun();
            yield return null;

            Assert.AreEqual(0, RunManager.Instance.GoldShards,
                "특전이 없는데 시작 골드가 붙었다 — 기존 밸런스가 바뀐다.");
        }

        /// <summary>
        /// 무료 리롤 특전이 첫 리롤 비용을 0으로 만든다.
        ///
        /// <b>리롤 가능 횟수는 안 늘린다</b>는 것이 이 특전의 규약이다. 상한을 건드리면 드래프트
        /// 한 번에 볼 수 있는 카드 수가 바뀌어 밸런스가 흔들린다 — 그래서 비용만 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator 무료_리롤_특전은_첫_리롤_비용을_0으로_만든다()
        {
            var perk = MakeUpgrade(FreeRerollId, MetaUpgradeType.FreeReroll, valuePerLevel: 1f, levels: 2);

            InjectCatalog();  // 특전 없는 상태의 기준값부터 잡는다
            var draft = SpawnDraftController();
            yield return null;

            int baseCost = draft.GetRerollCost();
            Assert.Greater(baseCost, 0, "특전이 없으면 첫 리롤은 유료여야 한다(기준값).");

            InjectCatalog(perk);
            BuyLevels(perk, 1);

            Assert.AreEqual(1, MetaUpgrades.FreeRerollCount(), "무료 리롤 횟수 합산이 레벨을 따라오지 않았다.");
            Assert.AreEqual(0, draft.GetRerollCost(),
                "무료 리롤 1레벨인데 첫 리롤이 여전히 유료다.");
        }

        // ───────────────────── ② 실제 카탈로그 (에셋 검증) ─────────────────────

        /// <summary>
        /// 특전 에셋이 실제로 Resources 에 있는가.
        ///
        /// 🔴 <b>이 테스트는 빌더 메뉴를 재실행해야 통과한다.</b> 코드에 Upsert 를 적어 두는 것만으로는
        /// <c>.asset</c> 이 생기지 않는다 — 에디터가 한 번 돌아야 한다. 실패하면 코드가 아니라
        /// 절차가 덜 끝난 것이니 LobbySceneBuilder 메뉴부터 실행할 것.
        /// </summary>
        [UnityTest]
        public IEnumerator 특전_에셋_두_종이_카탈로그에_있다()
        {
            MetaUpgrades.Reload();
            yield return null;

            var ids = new List<string>();
            foreach (var up in MetaUpgrades.All)
            {
                if (up != null) ids.Add(up.upgradeId);
            }

            Assert.Contains(StartingGoldId, ids,
                "시작 골드 특전 에셋이 없다 — LobbySceneBuilder 메뉴를 재실행할 것. 현재 목록: " + string.Join(",", ids));
            Assert.Contains(FreeRerollId, ids,
                "무료 리롤 특전 에셋이 없다 — LobbySceneBuilder 메뉴를 재실행할 것. 현재 목록: " + string.Join(",", ids));
        }

        /// <summary>
        /// 무료 리롤 사다리가 실제 리롤 상한을 넘지 않는가.
        ///
        /// <c>RunConfig.rerollCostLadder</c> 가 리롤 가능 횟수를 정한다. 그보다 긴 사다리를 팔면
        /// <b>값을 받고 아무것도 주지 않는 레벨</b>이 생긴다 — 환불도 안 되는 영구 구매라
        /// 사람이 눈치채기 전에 파편이 먼저 사라진다.
        /// </summary>
        [UnityTest]
        public IEnumerator 무료_리롤_사다리는_리롤_상한을_넘지_않는다()
        {
            MetaUpgrades.Reload();
            yield return null;

            MetaUpgradeData perk = null;
            foreach (var up in MetaUpgrades.All)
            {
                if (up != null && up.upgradeId == FreeRerollId) perk = up;
            }

            if (perk == null) Assert.Ignore("무료 리롤 에셋이 아직 없다 — 빌더 메뉴 재실행 후 이 테스트가 의미를 갖는다.");

            var config = ScriptableObject.CreateInstance<RunConfig>();
            spawned.Add(config);
            int rerollCap = config.rerollCostLadder != null ? config.rerollCostLadder.Length : 0;

            Assert.LessOrEqual(perk.MaxLevel, rerollCap,
                "무료 리롤 최대 레벨(" + perk.MaxLevel + ")이 리롤 가능 횟수(" + rerollCap + ")를 넘는다 " +
                "— 초과 레벨은 파편만 받고 효과가 없다.");
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        private MetaUpgradeData MakeUpgrade(string id, MetaUpgradeType type, float valuePerLevel, int levels)
        {
            var data = ScriptableObject.CreateInstance<MetaUpgradeData>();
            data.upgradeId = id;
            data.type = type;
            data.valuePerLevel = valuePerLevel;

            var ladder = new int[levels];
            for (int i = 0; i < levels; i++) ladder[i] = 10;  // 구매 가능하기만 하면 되므로 균일하게
            data.costLadder = ladder;

            spawned.Add(data);
            return data;
        }

        /// <summary>
        /// 합성 카탈로그를 주입한다.
        ///
        /// <c>MetaUpgrades.All</c> 은 Resources 를 훑는 지연 로드라, 에셋이 아직 없는 상태에서도
        /// 소비 경로를 검증하려면 캐시를 직접 채워야 한다. TearDown 의 <c>Reload()</c> 가
        /// 이 주입을 되돌린다 — 안 되돌리면 뒤따르는 테스트가 합성 카탈로그를 보게 된다.
        /// </summary>
        private static void InjectCatalog(params MetaUpgradeData[] upgrades)
        {
            var field = typeof(MetaUpgrades).GetField("all", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "MetaUpgrades.all 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");
            field.SetValue(null, upgrades);
        }

        /// <summary>파편을 채워 넣고 정규 구매 경로로 레벨을 올린다(레벨을 직접 쓰지 않는다).</summary>
        private static void BuyLevels(MetaUpgradeData data, int levels)
        {
            var meta = MetaSaveService.Instance;
            for (int i = 0; i < levels; i++)
            {
                int cost = data.CostForNextLevel(meta.GetUpgradeLevel(data.upgradeId));
                meta.AddAbyssShards(cost, autoSave: false);
                Assert.IsTrue(meta.TryPurchaseUpgrade(data, autoSave: false),
                    "특전 구매에 실패했다(레벨 " + (i + 1) + ") — 파편이 모자라거나 최대 레벨이다.");
            }
        }

        private DraftSessionController SpawnDraftController()
        {
            var go = new GameObject("TestDraftSession");
            var controller = go.AddComponent<DraftSessionController>();
            spawned.Add(go);
            return controller;
        }
    }
}
