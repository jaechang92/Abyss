using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Run;
using Abyss.Runtime.Weapon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 런 포기(<see cref="RunManager.AbandonRun"/>) 스모크 — 2026-09-22 결함의 회귀 테스트.
    ///
    /// <b>무엇이 잘못돼 있었나.</b> 일시정지 메뉴의 「타이틀로」가 런을 안 끝냈다. RunManager 는
    /// DontDestroyOnLoad 라 씬을 넘어 살고, isRunActive 가 true 로 남은 채 던전에 재입장하면
    /// <see cref="RunManager.StartNewRun"/>의 첫 줄 가드(<c>if (isRunActive) return;</c>)에 걸려
    /// <b>초기화가 통째로 건너뛰어졌다</b> — 레벨·경험치·골드·무기·통계가 이월됐다.
    ///
    /// 🔑 <b>이 실패 모드는 테스트 쪽에서 이미 알고 있었다.</b> MetaPerkPlayModeSmoke 의 TearDown 이
    /// "런은 전역 상태다 — 켜둔 채 끝내면 다음 테스트의 StartNewRun 이 조용히 무시된다"고 적고
    /// 런을 닫는다. 같은 성질이 게임 쪽에서는 안 닫혀 있었다.
    ///
    /// 🔴 <b>조용히 틀리는 결함이다.</b> 오류도 로그도 안 난다 — 값이 남아 있을 뿐이다.
    /// 그래서 "포기했는가"가 아니라 <b>"다음 런이 실제로 초기화되는가"</b>를 본다.
    /// </summary>
    public sealed class RunAbandonPlayModeSmoke
    {
        private const string StartingGoldId = "meta_starting_gold";

        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private readonly List<Object> spawned = new List<Object>();
        private bool isCatalogInjected;

        private static RunManager Run => RunManager.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();
            // 앞 테스트가 런을 켜둔 채 끝났을 수 있다 — 켜져 있으면 StartNewRun 이 무시된다.
            Run.AbandonRun();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Run.AbandonRun();

            foreach (var obj in spawned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            spawned.Clear();
            if (isCatalogInjected)
            {
                MetaUpgrades.Reload();  // 주입한 합성 카탈로그를 반드시 되돌린다
                isCatalogInjected = false;
            }
            Time.timeScale = 1f;        // 정지 경계 테스트가 실패해도 뒤 테스트를 멈춘 채 두지 않는다

            yield return null;
            saveGuard.Release();
        }

        /// <summary>포기한 뒤 재입장하면 런이 <b>실제로</b> 초기화된다 — 본 결함의 회귀 테스트.</summary>
        [UnityTest]
        public IEnumerator 포기하면_다음_런이_실제로_초기화된다()
        {
            Run.StartNewRun();
            Run.GainGoldShards(120);
            Run.GainExp(Run.ExpToNextLevel, "test");     // 확실히 한 레벨 올린다

            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.weaponId = "test_abandon_weapon";
            Run.Weapons.Grant(weapon);

            yield return null;

            Assert.Greater(Run.CurrentLevel, 1, "기준값 만들기 실패 — 레벨이 안 올랐다.");
            Assert.AreEqual(120, Run.GoldShards, "기준값 만들기 실패 — 골드가 안 들어갔다.");
            Assert.IsTrue(Run.Weapons.Owns(weapon), "기준값 만들기 실패 — 무기가 안 들어갔다.");

            Assert.Greater(Run.Stats.totalElapsedSeconds, 0f, "기준값 만들기 실패 — 런 시간이 안 쌓였다.");

            Run.AbandonRun();
            Assert.IsFalse(Run.IsRunActive, "포기했는데 런이 여전히 진행 중이다 — StartNewRun 이 무시된다.");

            Run.StartNewRun();

            // 🔴 통계는 <b>프레임이 지나기 전에</b> 본다. Update 가 isRunActive 만 보고 매 프레임
            //    totalElapsedSeconds 를 쌓으므로, yield 뒤에 0 을 기대하면 초기화가 멀쩡해도 항상 실패한다.
            //    (2026-09-22 이 테스트를 처음 쓸 때 실제로 그렇게 틀렸다 — 코드가 아니라 단언이 틀렸었다.)
            Assert.AreEqual(0f, Run.Stats.totalElapsedSeconds, 0.0001f,
                "포기 후 재입장인데 이전 런의 통계가 남았다.");

            yield return null;

            Assert.AreEqual(1, Run.CurrentLevel, "포기 후 재입장인데 이전 런의 레벨이 남았다.");
            Assert.AreEqual(0, Run.CurrentExp, "포기 후 재입장인데 이전 런의 경험치가 남았다.");
            // 시작 골드는 0 고정이 아니라 특전 시작값이다(가드가 특전 없는 상태로 만들어 여기서는 보통 0).
            Assert.AreEqual(MetaUpgrades.StartingGoldBonus(), Run.GoldShards, "포기 후 재입장인데 이전 런의 골드가 남았다.");
            Assert.IsFalse(Run.Weapons.Owns(weapon), "포기 후 재입장인데 이전 런의 무기가 남았다.");

            Object.DestroyImmediate(weapon);
        }

        /// <summary>
        /// 포기하면 <b>런 시간이 더 쌓이지 않는다</b> — 타이틀 화면에 켜 두기만 해도 늘던 결함.
        ///
        /// 🔑 <see cref="RunManager"/>는 DontDestroyOnLoad 라 씬을 넘어 살고, Update 는 isRunActive만
        /// 본다. 게다가 <c>Time.unscaledDeltaTime</c>이라 정지·씬 전환도 못 막는다 —
        /// 포기가 isRunActive를 내리는 것이 유일한 차단 지점이다.
        /// </summary>
        [UnityTest]
        public IEnumerator 포기하면_런_시간이_더_쌓이지_않는다()
        {
            Run.StartNewRun();
            yield return null;
            yield return null;

            Assert.Greater(Run.Stats.totalElapsedSeconds, 0f, "기준값 만들기 실패 — 런 중에 시간이 안 쌓였다.");

            Run.AbandonRun();
            float atAbandon = Run.Stats.totalElapsedSeconds;

            yield return null;
            yield return null;

            Assert.AreEqual(atAbandon, Run.Stats.totalElapsedSeconds, 0.0001f,
                "포기했는데 런 시간이 계속 쌓인다 — 타이틀 화면에 켜 두기만 해도 플레이타임이 늘어난다.");
        }

        /// <summary>포기하면 심연 조각을 <b>버린다</b>(사용자 결정 2026-09-22).</summary>
        [UnityTest]
        public IEnumerator 포기하면_심연_조각이_늘지_않는다()
        {
            int before = MetaSaveService.Instance.Current.abyssShardsTotal;

            Run.StartNewRun();
            Run.GainGoldShards(500);
            yield return null;

            Run.AbandonRun();

            Assert.AreEqual(before, MetaSaveService.Instance.Current.abyssShardsTotal,
                "포기했는데 심연 조각이 적립됐다 — 포기는 정산하지 않는다.");
            Assert.AreEqual(0, Run.LastRunAbyssShardsEarned,
                "포기했는데 획득량이 0이 아니다.");
            Assert.AreEqual(RunEndReason.Abandoned, Run.LastRunEndReason,
                "포기 사유가 안 남았다 — 결과 패널이 사망으로 오인할 수 있다.");
        }

        /// <summary>
        /// 포기한 런의 골드가 <b>다음 런 정산에 안 섞인다</b> — 악용 경로 회귀 테스트.
        ///
        /// 🔑 환산율을 테스트가 알 필요 없게 <b>같은 골드의 두 정산을 비교</b>한다.
        /// 환산율이 바뀌어도 이 테스트는 그대로 유효하다.
        /// </summary>
        [UnityTest]
        public IEnumerator 포기한_런의_골드는_다음_런_정산에_안_섞인다()
        {
            // ① 기준 — 골드 100 하나만으로 끝낸 런
            Run.StartNewRun();
            Run.GainGoldShards(100);
            yield return null;
            Run.EndRun();
            int cleanEarned = Run.LastRunAbyssShardsEarned;

            Assert.Greater(cleanEarned, 0, "기준값 만들기 실패 — 골드 100이 환산되지 않았다.");

            // ② 포기한 런(골드 500)을 끼워 넣고 같은 런을 돌린다
            Run.StartNewRun();
            Run.GainGoldShards(500);
            yield return null;
            Run.AbandonRun();

            Run.StartNewRun();
            Run.GainGoldShards(100);
            yield return null;
            Run.EndRun();

            Assert.AreEqual(cleanEarned, Run.LastRunAbyssShardsEarned,
                "포기한 런의 골드가 다음 런 정산에 섞였다 — 포기·재입장·사망을 반복하면 메타 재화가 불어난다.");
        }

        /// <summary>포기는 <b>런 밖에서 불려도 안전</b>하다 — 이중 호출 가드.</summary>
        [UnityTest]
        public IEnumerator 런_밖에서_포기해도_아무_일도_없다()
        {
            Run.StartNewRun();
            Run.GainGoldShards(50);
            Run.AbandonRun();
            yield return null;

            int before = MetaSaveService.Instance.Current.abyssShardsTotal;
            Run.AbandonRun();       // 두 번째 호출
            Run.AbandonRun();       // 세 번째 호출

            Assert.IsFalse(Run.IsRunActive);
            Assert.AreEqual(before, MetaSaveService.Instance.Current.abyssShardsTotal,
                "런 밖에서 부른 포기가 메타를 건드렸다.");
            yield return null;
        }

        /// <summary>
        /// 시작 골드 특전이 <b>실제로 있는</b> 상태에서 골드 획득 → 포기 → 새 런 = 특전 시작값(2026-09-23 A2).
        ///
        /// 🔑 특전이 0이면 "포기한 골드가 안 넘어왔다"와 "특전 시작값으로 시작했다"가 같은 숫자라 구분되지 않는다.
        /// 같은 흐름에서 포기의 나머지 계약(미정산·정상 런 요약 보존·결과 창 미표시)도 함께 본다.
        /// </summary>
        [UnityTest]
        public IEnumerator 시작_골드_특전이_있으면_포기_후_새_런은_특전_시작값으로_시작한다()
        {
            var perk = MakeStartingGoldPerk(valuePerLevel: 25f, levels: 3);
            InjectCatalog(perk);
            BuyLevels(perk, 2);
            int perkGold = MetaUpgrades.StartingGoldBonus();
            Assert.Greater(perkGold, 0, "기준값 만들기 실패 — 시작 골드 특전이 합산되지 않았다.");

            // 정상 종료한 런 하나 — 포기가 이 요약을 덮으면 안 된다.
            Run.StartNewRun();
            yield return null;
            Run.EndRun();
            var summaryBefore = Run.LastRunSummary;
            Assert.IsTrue(summaryBefore.hasRecord, "기준값 만들기 실패 — 정상 종료 런의 요약이 안 생겼다.");

            Run.StartNewRun();
            Assert.AreEqual(perkGold, Run.GoldShards, "기준값 만들기 실패 — 새 런이 특전 시작값으로 시작하지 않았다.");
            Run.GainGoldShards(300);
            yield return null;
            Assert.AreEqual(perkGold + 300, Run.GoldShards, "기준값 만들기 실패 — 골드가 안 들어갔다.");

            int shardsBefore = MetaSaveService.Instance.Current.abyssShardsTotal;
            int endedCount = 0;
            System.Action countEnded = () => endedCount++;
            GameEvents.OnRunEnded += countEnded;
            try
            {
                Run.AbandonRun();
            }
            finally
            {
                GameEvents.OnRunEnded -= countEnded;
            }

            Assert.AreEqual(0, endedCount,
                "포기가 OnRunEnded 를 발행했다 — 흐름 FSM이 Result로 가고 결과 패널이 뜬다.");
            Assert.AreEqual(shardsBefore, MetaSaveService.Instance.Current.abyssShardsTotal,
                "포기했는데 심연 조각이 정산됐다.");
            Assert.AreSame(summaryBefore, Run.LastRunSummary,
                "포기가 직전 정상 런의 요약을 덮었다 — 도감 기록 탭이 버린 런을 보여준다.");

            Run.StartNewRun();
            Assert.AreEqual(perkGold, Run.GoldShards,
                "포기 후 새 런 골드가 특전 시작값이 아니다 — 포기한 런의 골드가 넘어왔거나 특전이 빠졌다.");
            yield return null;
        }

        /// <summary>
        /// 포기 사실 이벤트 계약(A2-event-contract.md) — 1번만, 런 비활성·사유 확정 뒤, 골드 초기화 전.
        /// 애널리틱스가 핸들러 안에서 포기 직전 잔액을 읽는다. 순서가 바뀌면 조용히 0이 기록된다.
        /// </summary>
        [UnityTest]
        public IEnumerator 포기_사실은_골드_초기화_전에_한_번만_발행된다()
        {
            Run.StartNewRun();
            Run.GainGoldShards(120);
            yield return null;
            int goldBeforeAbandon = Run.GoldShards;

            int raisedCount = 0;
            bool isActiveAtRaise = true;
            var reasonAtRaise = RunEndReason.Death;
            int goldAtRaise = -1;
            int earnedAtRaise = -1;
            System.Action record = () =>
            {
                raisedCount++;
                isActiveAtRaise = Run.IsRunActive;
                reasonAtRaise = Run.LastRunEndReason;
                goldAtRaise = Run.GoldShards;
                earnedAtRaise = Run.LastRunAbyssShardsEarned;
            };

            GameEvents.OnRunAbandoned += record;
            try
            {
                Run.AbandonRun();
                Run.AbandonRun();   // 중복 호출은 발행하지 않는다
            }
            finally
            {
                GameEvents.OnRunAbandoned -= record;
            }

            Assert.AreEqual(1, raisedCount, "포기 1회에 사실 이벤트가 정확히 1번 나가야 한다.");
            Assert.IsFalse(isActiveAtRaise, "발행 시점에 런이 아직 활성이다.");
            Assert.AreEqual(RunEndReason.Abandoned, reasonAtRaise, "발행 시점에 사유가 확정되지 않았다.");
            Assert.AreEqual(0, earnedAtRaise, "발행 시점 심연 조각 획득량이 0이 아니다.");
            Assert.AreEqual(goldBeforeAbandon, goldAtRaise,
                "발행 시점에 골드가 이미 초기화됐다 — 로거가 포기 직전 잔액을 못 읽는다.");
            Assert.AreEqual(0, Run.GoldShards, "발행 뒤 골드가 0으로 초기화되지 않았다.");
        }

        /// <summary>
        /// 포기 구독자가 예외를 던져도 골드 초기화는 일어난다(2026-09-23 총괄 검수 보완).
        ///
        /// 🔑 계약 문서에 "구독자는 예외를 던지지 말 것"이라고 적는 것만으로는 <b>기존 불변식</b>
        /// (포기한 런의 골드는 남지 않는다)을 지킬 수 없다. 남으면 다음 런 정산에 섞여 메타 재화가 불어난다.
        /// 예외 자체는 삼키지 않는다 — 구독자 결함이 조용히 묻히면 다음에 또 같은 일이 난다.
        /// </summary>
        [UnityTest]
        public IEnumerator 포기_구독자가_예외를_던져도_골드는_0이_된다()
        {
            Run.StartNewRun();
            Run.GainGoldShards(250);
            yield return null;
            Assert.Greater(Run.GoldShards, 0, "기준값 만들기 실패 — 골드가 안 들어갔다.");

            System.Action thrower = () => throw new System.InvalidOperationException("구독자 예외 주입(테스트)");
            GameEvents.OnRunAbandoned += thrower;
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => Run.AbandonRun(),
                    "주입한 구독자 예외가 호출자에게 전달되지 않았다 — 구독자 결함이 조용히 묻힌다.");
            }
            finally
            {
                GameEvents.OnRunAbandoned -= thrower;
            }

            Assert.AreEqual(0, Run.GoldShards,
                "구독자가 예외를 던졌더니 포기한 런의 골드가 남았다 — 다음 런 정산에 섞인다.");
            Assert.IsFalse(Run.IsRunActive, "구독자 예외 때문에 런이 활성으로 남았다 — StartNewRun 이 무시된다.");
            Assert.AreEqual(RunEndReason.Abandoned, Run.LastRunEndReason, "구독자 예외로 사유가 확정되지 않았다.");
            Assert.AreEqual(0, Run.LastRunAbyssShardsEarned, "구독자 예외로 획득량이 남았다.");

            Run.StartNewRun();
            Assert.AreEqual(MetaUpgrades.StartingGoldBonus(), Run.GoldShards,
                "구독자 예외 뒤의 새 런이 특전 시작값으로 시작하지 않았다.");
            yield return null;
        }

        /// <summary>
        /// 씬 전환 중에는 흐름 FSM이 정지도 해제도 받지 않는다(2026-09-23 A2).
        ///
        /// 「타이틀로」는 정지된 채 페이드·로드한다. 그 사이 ESC(해제)가 받아들여지면 검은 화면 뒤에서 게임이 돌고,
        /// 다시 ESC(정지)가 받아들여지면 정지가 다음 씬까지 따라간다. 실제 씬 로드 없이 전환 플래그만 세워
        /// 게이트를 본다 — 씬 경계의 회수(SceneFlowController)는 이 테스트 범위가 아니다(수동 재현 절차로 확인).
        /// </summary>
        [UnityTest]
        public IEnumerator 씬_전환_중에는_정지도_해제도_받지_않는다()
        {
            bool hadFlow = SceneFlowController.HasInstance;
            var flow = SceneFlowController.Instance;
            var loadingField = typeof(SceneFlowController).GetField("isLoading", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(loadingField, "SceneFlowController.isLoading 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");

            var go = new GameObject("TestGameFlow");
            var controller = go.AddComponent<GameFlowController>();   // StateMachine 은 RequireComponent 로 붙는다
            try
            {
                yield return null;   // Start → RunActive
                yield return null;

                GameEvents.RaisePauseRequested();
                Assert.IsTrue(controller.IsPaused, "기준값 만들기 실패 — 전환 밖에서 정지가 안 됐다.");
                Assert.AreEqual(0f, Time.timeScale, "기준값 만들기 실패 — 정지인데 시간이 흐른다.");

                loadingField.SetValue(flow, true);
                GameEvents.RaiseResumeRequested();
                Assert.IsTrue(controller.IsPaused, "전환 중 해제 요청이 정지를 풀었다 — 검은 화면 뒤에서 게임이 돈다.");
                Assert.AreEqual(0f, Time.timeScale, "전환 중인데 시간이 다시 흐른다.");

                loadingField.SetValue(flow, false);
                GameEvents.RaiseResumeRequested();
                Assert.IsFalse(controller.IsPaused, "전환이 끝났는데 해제가 안 된다 — 게이트가 풀리지 않는다.");
                Assert.AreEqual(1f, Time.timeScale);

                loadingField.SetValue(flow, true);
                GameEvents.RaisePauseRequested();
                Assert.IsFalse(controller.IsPaused, "전환 중 정지 요청이 받아들여졌다 — 정지가 다음 씬까지 따라간다.");
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally
            {
                loadingField.SetValue(flow, false);
                Object.Destroy(go);
                if (!hadFlow) Object.Destroy(flow.gameObject);
                Time.timeScale = 1f;
            }
            yield return null;
        }

        // ───────────────────────── 헬퍼 (MetaPerkPlayModeSmoke 와 같은 패턴) ─────────────────────────

        private MetaUpgradeData MakeStartingGoldPerk(float valuePerLevel, int levels)
        {
            var data = ScriptableObject.CreateInstance<MetaUpgradeData>();
            data.upgradeId = StartingGoldId;
            data.type = MetaUpgradeType.StartingGold;
            data.valuePerLevel = valuePerLevel;

            var ladder = new int[levels];
            for (int i = 0; i < levels; i++) ladder[i] = 10;  // 구매 가능하기만 하면 되므로 균일하게
            data.costLadder = ladder;

            spawned.Add(data);
            return data;
        }

        /// <summary>합성 카탈로그 주입. TearDown 의 <c>Reload()</c> 가 되돌린다.</summary>
        private void InjectCatalog(params MetaUpgradeData[] upgrades)
        {
            var field = typeof(MetaUpgrades).GetField("all", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "MetaUpgrades.all 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");
            field.SetValue(null, upgrades);
            isCatalogInjected = true;
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
    }
}
