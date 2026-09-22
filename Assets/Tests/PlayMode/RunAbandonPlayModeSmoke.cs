using System.Collections;
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
        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();

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
            Assert.AreEqual(0, Run.GoldShards, "포기 후 재입장인데 이전 런의 골드가 남았다.");
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
    }
}
