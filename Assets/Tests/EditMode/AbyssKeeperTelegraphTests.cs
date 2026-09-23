using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 첫 보스(C2) 예고 규칙 — 탄막 일정표 · 두 예고 겹침 금지 · 예고 중 경직 규칙.
    ///
    /// 🔑 시간축 시뮬레이션은 <c>EnemyBase.EvaluateTransitions</c> 의 근접 규칙(쿨다운 1.8 · 예비동작 0.7 · 회복 0.5 ·
    /// <c>CanBeginAttack</c> = 탄막 예고 중 아님)을 그대로 옮긴 모형 위에서 <see cref="AbyssKeeperVolleySchedule"/> 를 돌린다.
    /// 프레임은 1/64초(2진수로 정확) — 경계 단언은 한 프레임 허용 오차를 둔다(메모리 feedback_float_boundary_tests).
    /// </summary>
    public sealed class AbyssKeeperTelegraphTests
    {
        private const float Dt = 1f / 64f;
        private const float BaseInterval = 3f;
        private const float MeleeCooldown = 1.8f;
        private const float MeleeWindup = 0.7f;
        private const float MeleeRecovery = 0.5f;

        // ───────────────────────────── 예고 시간

        [Test]
        public void 페이즈별_탄막_예고는_06_05_04초다()
        {
            Assert.AreEqual(0.6f, AbyssKeeperVolleySchedule.TelegraphTime(1, 3f), 1e-6f);
            Assert.AreEqual(0.5f, AbyssKeeperVolleySchedule.TelegraphTime(2, 1.5f), 1e-6f);
            Assert.AreEqual(0.4f, AbyssKeeperVolleySchedule.TelegraphTime(3, 1f), 1e-6f);
        }

        [Test]
        public void 범위_밖_페이즈는_끝값으로_붙고_예고는_주기를_넘지_않는다()
        {
            Assert.AreEqual(0.6f, AbyssKeeperVolleySchedule.TelegraphTime(0, 3f), 1e-6f);
            Assert.AreEqual(0.4f, AbyssKeeperVolleySchedule.TelegraphTime(7, 3f), 1e-6f);
            // 주기가 예고보다 짧으면 주기로 자른다 — 「주기 불변」이 먼저다.
            Assert.AreEqual(0.25f, AbyssKeeperVolleySchedule.TelegraphTime(1, 0.25f), 1e-6f);
        }

        // ───────────────────────────── 일정표 단위 동작

        [Test]
        public void 첫_예고는_감지_즉시_시작하고_예고_시간_뒤에_쏜다()
        {
            var schedule = new AbyssKeeperVolleySchedule();
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.BeginTelegraph, schedule.Tick(10f, 2, BaseInterval, true, false));
            Assert.IsTrue(schedule.IsTelegraphing);
            Assert.AreEqual(10.5f, schedule.FireTime, 1e-6f);

            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.None, schedule.Tick(10.25f, 2, BaseInterval, true, false));
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.Fire, schedule.Tick(10.5f, 2, BaseInterval, true, false));
            Assert.IsFalse(schedule.IsTelegraphing);
            Assert.AreEqual(10.5f, schedule.LastFireTime, 1e-6f);
        }

        [Test]
        public void 다음_예고는_주기에서_예고_시간을_뺀_시각에_시작한다()
        {
            // P2: 주기 1.5 · 예고 0.5 → 발사 10.5 다음 예고는 11.5, 발사는 12.0 (간격 1.5 그대로).
            var schedule = new AbyssKeeperVolleySchedule();
            schedule.Tick(10f, 2, BaseInterval, true, false);
            schedule.Tick(10.5f, 2, BaseInterval, true, false);

            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.None, schedule.Tick(11.25f, 2, BaseInterval, true, false));
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.BeginTelegraph, schedule.Tick(11.5f, 2, BaseInterval, true, false));
            Assert.AreEqual(12f, schedule.FireTime, 1e-6f);
        }

        [Test]
        public void 감지_범위_밖이면_예고를_시작하지_않는다()
        {
            var schedule = new AbyssKeeperVolleySchedule();
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.None, schedule.Tick(10f, 1, BaseInterval, false, false));
            Assert.IsFalse(schedule.IsTelegraphing);
        }

        [Test]
        public void 예고를_시작했으면_사거리를_벗어나도_쏜다()
        {
            // 예고했는데 안 오면 예고를 믿지 않게 된다.
            var schedule = new AbyssKeeperVolleySchedule();
            schedule.Tick(10f, 3, BaseInterval, true, false);
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.Fire, schedule.Tick(10.5f, 3, BaseInterval, false, false));
        }

        [Test]
        public void 근접_예비동작_중에는_탄막_예고를_미룬다()
        {
            var schedule = new AbyssKeeperVolleySchedule();
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.None, schedule.Tick(10f, 1, BaseInterval, true, true));
            Assert.IsFalse(schedule.IsTelegraphing, "근접 예고 중 탄막 예고가 시작되면 두 틴트가 덮어쓴다");
            Assert.AreEqual(AbyssKeeperVolleySchedule.Step.BeginTelegraph, schedule.Tick(10.75f, 1, BaseInterval, true, false));
        }

        [Test]
        public void 취소하면_예고가_풀리고_발사하지_않는다()
        {
            var schedule = new AbyssKeeperVolleySchedule();
            schedule.Tick(10f, 1, BaseInterval, true, false);
            schedule.Cancel();
            Assert.IsFalse(schedule.IsTelegraphing);
            Assert.AreNotEqual(AbyssKeeperVolleySchedule.Step.Fire, schedule.Tick(10.75f, 1, BaseInterval, false, false));
        }

        // ───────────────────────────── 경직 규칙

        [Test]
        public void 기본값인_적은_공격_중에도_경직을_버리지_않는다()
        {
            // 첫 보스 외 11종·다른 보스의 옛 규칙 — 예비동작 중에 맞으면 끊긴다.
            foreach (string state in new[] { EnemyStateIds.Attack, EnemyStateIds.Chase, EnemyStateIds.Stagger, EnemyStateIds.Patrol })
            {
                Assert.IsFalse(EnemyTimedState.IsStaggerIgnored(false, state, 5f, 10f), state);
            }
        }

        [Test]
        public void 첫_보스는_공격_동작_중에만_경직을_버린다()
        {
            Assert.IsTrue(EnemyTimedState.IsStaggerIgnored(true, EnemyStateIds.Attack, 9.75f, 10f), "예비동작·회복 중");
            Assert.IsFalse(EnemyTimedState.IsStaggerIgnored(true, EnemyStateIds.Attack, 10f, 10f), "동작이 끝난 뒤");
            Assert.IsFalse(EnemyTimedState.IsStaggerIgnored(true, EnemyStateIds.Chase, 5f, 10f), "공격 밖");
            Assert.IsFalse(EnemyTimedState.IsStaggerIgnored(true, EnemyStateIds.Stagger, 5f, 10f), "경직 중 재피격");
        }

        // ───────────────────────────── 시간축 시뮬레이션

        [Test]
        public void 근접만_있을_때_연타해도_근접이_쿨다운마다_나간다()
        {
            // D2 §1-3 결손의 모양: 1.8초보다 자주 때리면 옛 규칙에서는 근접이 영원히 안 나간다.
            var run = Simulate(phase: 1, seconds: 20f, hitEvery: 0.25f, resistsStagger: true, hasVolley: false);
            Assert.GreaterOrEqual(run.MeleeStrikes, 10, "20초에 1.8초 쿨다운이면 10회 이상 타격해야 한다");

            var old = Simulate(phase: 1, seconds: 20f, hitEvery: 0.25f, resistsStagger: false, hasVolley: false);
            Assert.AreEqual(0, old.MeleeStrikes, "대조군 — 옛 규칙은 연타에 근접이 봉인된다(이 테스트가 무엇을 막는지 고정)");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 두_예고는_한_프레임도_겹치지_않는다(int phase)
        {
            var run = Simulate(phase, seconds: 60f, hitEvery: 0.25f, resistsStagger: true, hasVolley: true);
            Assert.AreEqual(0, run.OverlapFrames, "근접 예비동작과 탄막 예고가 같은 프레임에 있으면 틴트를 읽을 수 없다");
            Assert.Greater(run.MeleeStrikes, 0, "근접이 굶으면 안 된다");
            Assert.Greater(run.VolleyFires, 0, "탄막이 굶으면 안 된다");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 쿨다운과_탄막_주기는_짧아지지_않는다(int phase)
        {
            var run = Simulate(phase, seconds: 60f, hitEvery: 0.25f, resistsStagger: true, hasVolley: true);
            float interval = BaseInterval / phase;

            foreach (float gap in Gaps(run.MeleeStartTimes))
                Assert.GreaterOrEqual(gap, MeleeCooldown - Dt, "근접 쿨다운 1.8 보존");
            foreach (float gap in Gaps(run.VolleyFireTimes))
                Assert.GreaterOrEqual(gap, interval - Dt, "탄막 주기 보존(겹침 금지는 미룰 뿐 당기지 않는다)");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 근접이_없으면_탄막_주기는_옛_볼리와_같다(int phase)
        {
            var run = Simulate(phase, seconds: 30f, hitEvery: 0f, resistsStagger: true, hasVolley: true, meleeInRange: false);
            float interval = BaseInterval / phase;

            foreach (float gap in Gaps(run.VolleyFireTimes))
                Assert.AreEqual(interval, gap, Dt * 1.5f, "예고는 주기 안에 들어간다(주기 불변)");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 모든_발사_앞에는_페이즈_예고_시간만큼의_예고가_있다(int phase)
        {
            var run = Simulate(phase, seconds: 60f, hitEvery: 0.25f, resistsStagger: true, hasVolley: true);
            float expected = AbyssKeeperVolleySchedule.TelegraphTime(phase, BaseInterval / phase);

            Assert.AreEqual(run.VolleyFireTimes.Count, run.TelegraphDurations.Count);
            foreach (float duration in run.TelegraphDurations)
                Assert.AreEqual(expected, duration, Dt * 1.5f);
        }

        [Test]
        public void 근접_예고부터_타격까지는_예비동작_시간이다()
        {
            var run = Simulate(phase: 1, seconds: 20f, hitEvery: 0.25f, resistsStagger: true, hasVolley: true);
            Assert.Greater(run.WindupDurations.Count, 0);
            foreach (float windup in run.WindupDurations)
                Assert.AreEqual(MeleeWindup, windup, Dt * 1.5f, "예고 시작 → 타격 = 0.7초");
        }

        // ───────────────────────────── 모형

        private sealed class Result
        {
            public int MeleeStrikes;
            public int VolleyFires;
            public int OverlapFrames;
            public readonly List<float> MeleeStartTimes = new();
            public readonly List<float> VolleyFireTimes = new();
            public readonly List<float> TelegraphDurations = new();
            public readonly List<float> WindupDurations = new();
        }

        /// <summary>
        /// EnemyBase 의 근접 판정 순서(경직 → 타격 → 붙잡힘 → 공격 진입)를 옮긴 모형 + 일정표.
        /// <paramref name="hitEvery"/> 초마다 플레이어가 때린다(0 이면 안 때림). 첫 보스 경직 시간은 0 이다.
        /// </summary>
        private static Result Simulate(int phase, float seconds, float hitEvery, bool resistsStagger, bool hasVolley,
                                       bool meleeInRange = true)
        {
            var result = new Result();
            var schedule = new AbyssKeeperVolleySchedule();

            string state = EnemyStateIds.Chase;
            float lastAttack = float.NegativeInfinity;
            float strikeTime = 0f, exitTime = 0f, attackStart = 0f;
            bool hasStruck = true;
            float nextHit = hitEvery > 0f ? 0f : float.PositiveInfinity;
            float telegraphStart = 0f;

            int frames = (int)(seconds / Dt);
            for (int f = 0; f < frames; f++)
            {
                float now = f * Dt;

                // 플레이어 타격 → 경직 요청(TakeDamage 가 staggerQueued 를 세운다).
                bool staggerQueued = false;
                if (now >= nextHit)
                {
                    staggerQueued = true;
                    nextHit += hitEvery;
                }

                // ── EnemyBase.EvaluateTransitions
                bool skipRest = false;
                if (staggerQueued && !EnemyTimedState.IsStaggerIgnored(resistsStagger, state, now, exitTime))
                {
                    exitTime = now;          // staggerDuration 0
                    state = EnemyStateIds.Stagger;
                    skipRest = true;
                }

                if (!skipRest)
                {
                    if (state == EnemyStateIds.Attack && EnemyTimedState.IsStrikeDue(hasStruck, now, strikeTime))
                    {
                        hasStruck = true;
                        result.MeleeStrikes++;
                        result.WindupDurations.Add(now - attackStart);
                    }

                    if (!EnemyTimedState.IsHeld(state, now, exitTime))
                    {
                        bool canAttack = meleeInRange && now >= lastAttack + MeleeCooldown && !schedule.IsTelegraphing;
                        if (canAttack)
                        {
                            lastAttack = now;
                            state = EnemyStateIds.Attack;
                            attackStart = now;
                            strikeTime = now + MeleeWindup;
                            exitTime = now + MeleeWindup + MeleeRecovery;
                            hasStruck = false;
                            result.MeleeStartTimes.Add(now);
                        }
                        else
                        {
                            state = EnemyStateIds.Chase;
                        }
                    }
                }

                bool isWindup = state == EnemyStateIds.Attack && !hasStruck;

                // ── BossEnemy.Update → TickPattern (같은 프레임, 근접 판정 뒤)
                if (hasVolley)
                {
                    var step = schedule.Tick(now, phase, BaseInterval, true, isWindup);
                    if (step == AbyssKeeperVolleySchedule.Step.BeginTelegraph) telegraphStart = now;
                    if (step == AbyssKeeperVolleySchedule.Step.Fire)
                    {
                        result.VolleyFires++;
                        result.VolleyFireTimes.Add(now);
                        result.TelegraphDurations.Add(now - telegraphStart);
                    }
                }

                if (isWindup && schedule.IsTelegraphing) result.OverlapFrames++;
            }

            // 끝에 걸린 예고(발사 전 종료)는 짝이 없으므로 센 것만 비교한다.
            return result;
        }

        private static IEnumerable<float> Gaps(IReadOnlyList<float> times)
        {
            for (int i = 1; i < times.Count; i++) yield return times[i] - times[i - 1];
        }
    }
}
