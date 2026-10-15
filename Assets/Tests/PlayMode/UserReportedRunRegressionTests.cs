using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Run;
using FSM.Core;
using NUnit.Framework;
using ObjectPool_Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Abyss.Tests.PlayMode
{
    public sealed class UserReportedRunRegressionTests
    {
        private readonly PlayModeSaveGuard guard = new();
        private readonly List<Object> owned = new();
        private readonly List<string> poolKeys = new();
        private bool ownsRun, ownsPool;
        private float previousTime;
        private UnityEngine.Random.State previousRandom;
        private RunManager run;
        private Projectile prefab;
        private ArcProjectile arcPrefab;
        private Action<BossEnemy> previousSpawned;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static FieldInfo SpawnedField => typeof(BossEnemy).GetField("Spawned", BindingFlags.Static | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            owned.Clear(); poolKeys.Clear(); prefab = null; arcPrefab = null;
            guard.Acquire(); previousTime = Time.timeScale; previousRandom = UnityEngine.Random.state;
            // Exercise the real AI/hold without starting unrelated global HUD and intro sequences.
            previousSpawned = (Action<BossEnemy>)SpawnedField.GetValue(null);
            SpawnedField.SetValue(null, null);
            Time.timeScale = 1f; ownsRun = !RunManager.HasInstance; ownsPool = !PoolManager.HasInstance;
            run = RunManager.Instance; Assert.False(run.IsRunActive, "Do not interrupt an existing run.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (run != null && run.IsRunActive) run.AbandonRun();
                RunTransientCleanup.Clear();
                if (PoolManager.HasInstance)
                {
                    var pool = PoolManager.Instance;
                    var dictionary = (IDictionary)typeof(PoolManager).GetField("pools", Private).GetValue(pool);
                    foreach (string key in poolKeys)
                    {
                        dictionary.Remove(key);
                        var parent = pool.transform.Find("Pool_" + key.Substring(key.IndexOf('_') + 1));
                        if (parent != null) Object.Destroy(parent.gameObject);
                    }
                    if (ownsPool) Object.Destroy(pool.gameObject);
                }
                foreach (var item in owned) if (item != null) Object.Destroy(item);
                if (ownsRun && run != null) Object.Destroy(run.gameObject);
                SpawnedField.SetValue(null, previousSpawned);
                Time.timeScale = previousTime; UnityEngine.Random.state = previousRandom;
                yield return null; yield return null;
            }
            finally { guard.Release(); }
        }

        [UnityTest]
        public IEnumerator EndAndAbandonReturnBothFactionsAndExplosiveArcsWhileTimeIsStopped()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                run.StartNewRun();
                var enemy = Shot(ProjectileFaction.HitsPlayer);
                var friendly = Shot(ProjectileFaction.HitsEnemies);
                var shell = Shell();
                Assert.Greater(Object.FindObjectsByType<BossAreaEffect>().Length, 0);
                Time.timeScale = 0f;
                if (attempt == 0) run.EndRun(); else run.AbandonRun();
                Assert.False(enemy.gameObject.activeSelf); Assert.False(friendly.gameObject.activeSelf);
                Assert.False(shell.gameObject.activeSelf);
                Assert.Zero(Object.FindObjectsByType<BossAreaEffect>().Length);
                Assert.Zero(RunTransientCleanup.Clear(), "Repeated boundary cleanup must not release twice.");
                run.EndRun(); run.AbandonRun();
                var next = Shot(ProjectileFaction.HitsPlayer);
                Assert.True(next.gameObject.activeSelf); Assert.True(next.DiscardSilently());
                Assert.False(next.DiscardSilently());
                Time.timeScale = 1f; yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NewRunDiscardsLeftoverObjectsBeforeNewRunSubscribersExecute()
        {
            var old = Shot(ProjectileFaction.HitsPlayer); var arc = Shell();
            bool observedClean = false;
            void Observe() => observedClean = !old.gameObject.activeSelf && !arc.gameObject.activeSelf;
            Abyss.Runtime.Events.GameEvents.OnRunStarted += Observe;
            try { run.StartNewRun(); }
            finally { Abyss.Runtime.Events.GameEvents.OnRunStarted -= Observe; }
            Assert.True(observedClean);
            var fresh = Shot(ProjectileFaction.HitsEnemies);
            Assert.True(fresh.gameObject.activeSelf); Assert.True(fresh.DiscardSilently());
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonPooledCleanupDisablesImmediatelyWithoutCreatingPoolManager()
        {
            bool before = PoolManager.HasInstance;
            var go = Own(new GameObject("NonPooledShot", typeof(BoxCollider2D)));
            var shot = go.AddComponent<Projectile>();
            shot.Launch(Vector2.right, 20, 2, 20, null);
            Time.timeScale = 0f;
            Assert.True(shot.DiscardSilently()); Assert.False(go.activeSelf);
            Assert.False(shot.DiscardSilently()); Assert.AreEqual(before, PoolManager.HasInstance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistantDamagedBossPursuesButNormalEnemyAndUntouchedBossKeepDetectionRange()
        {
            var target = Own(new GameObject("DistantTarget")); target.transform.position = new Vector3(40030, 40000);
            var boss = CreateEnemy<BossEnemy>(target.transform);
            var normal = CreateEnemy<EnemyBase>(target.transform);
            yield return null;
            Evaluate(boss); Evaluate(normal);
            Assert.AreEqual(EnemyStateIds.Patrol, boss.GetComponent<StateMachine>().CurrentStateId);
            boss.TakeDamage(0); Assert.False(boss.IsEngagedByDamage);
            boss.TakeDamage(10); normal.TakeDamage(10);
            Set(boss, "staggerQueued", false); Set(normal, "staggerQueued", false);
            Evaluate(boss); Evaluate(normal);
            Assert.True(boss.IsEngagedByDamage);
            Assert.AreEqual(EnemyStateIds.Chase, boss.GetComponent<StateMachine>().CurrentStateId);
            Assert.AreEqual(EnemyStateIds.Patrol, normal.GetComponent<StateMachine>().CurrentStateId);
            typeof(EnemyBase).GetMethod("FixedUpdate", Private).Invoke(boss, null);
            Assert.Greater(boss.Body.linearVelocity.x, 0f);
            Assert.AreEqual(0, Object.FindObjectsByType<Projectile>().Length, "Pursuit must not grant unlimited range attacks.");
            target.SetActive(false); Evaluate(boss); Assert.False(boss.IsEngagedByDamage);
        }

        [UnityTest]
        public IEnumerator KeeperHoldPreventsMovementUntilReleasedAfterDistantDamage()
        {
            var target = Own(new GameObject("KeeperTarget")); target.transform.position = new Vector3(40030, 40000);
            var keeper = CreateEnemy<AbyssKeeperBoss>(target.transform);
            void Claim(BossEnemy boss) { if (boss == keeper) Assert.True(keeper.TryClaimEncounter()); }
            BossEnemy.Spawned += Claim;
            try { yield return null; }
            finally { BossEnemy.Spawned -= Claim; }
            keeper.TakeDamage(10); Set(keeper, "staggerQueued", false); Evaluate(keeper);
            typeof(AbyssKeeperBoss).GetMethod("FixedUpdate", Private).Invoke(keeper, null);
            Assert.True(keeper.IsHoldingForEncounter); Assert.Zero(keeper.Body.linearVelocity.x);
            keeper.ReleaseEncounterHold(); Evaluate(keeper);
            typeof(AbyssKeeperBoss).GetMethod("FixedUpdate", Private).Invoke(keeper, null);
            Assert.Greater(keeper.Body.linearVelocity.x, 0f);
        }

        [UnityTest]
        public IEnumerator NormalHitStillDamagesAndNotifiesExactlyOnceThenClearsBorrower()
        {
            var target = Own(new GameObject("NormalHitTarget")); target.transform.position = new Vector3(40030, 40000);
            var enemy = CreateEnemy<EnemyBase>(target.transform); yield return null;
            var listener = new Listener(); var shot = Shot(ProjectileFaction.HitsEnemies, listener);
            Trigger(shot, enemy.GetComponent<BoxCollider2D>());
            Assert.AreEqual(95, enemy.CurrentHp); Assert.AreEqual(1, listener.afterHits);
            Assert.False(shot.gameObject.activeSelf);
            Assert.IsNull(typeof(Projectile).GetField("hitListener", Private).GetValue(shot));
        }

        [UnityTest]
        public IEnumerator SynchronousRunEndCancelsOldHitFeedbackAndSameInstanceReborrowIsNotReturned()
        {
            run.StartNewRun();
            var target = Own(new GameObject("HitTarget")); target.transform.position = new Vector3(40030, 40000);
            var enemy = CreateEnemy<EnemyBase>(target.transform); yield return null;
            var listener = new Listener(); var shot = Shot(ProjectileFaction.HitsEnemies, listener);
            enemy.OnHpChanged += (_, _) => run.EndRun();
            Trigger(shot, enemy.GetComponent<BoxCollider2D>());
            Assert.False(shot.gameObject.activeSelf); Assert.Zero(listener.afterHits);
            Assert.Zero(Object.FindObjectsByType<WorldArtFx>().Length);
            // The first hit callback can return/reborrow the same pooled object synchronously.
            run.StartNewRun();
            var reusable = Shot(ProjectileFaction.HitsEnemies);
            Projectile borrowed = null;
            listener = new Listener { before = () =>
            {
                reusable.DiscardSilently();
                borrowed = Shot(ProjectileFaction.HitsEnemies);
            }};
            reusable.Launch(Vector2.right, 5, 2, 30, prefab, ProjectileFaction.HitsEnemies, hitListener: listener);
            int hp = enemy.CurrentHp;
            Trigger(reusable, enemy.GetComponent<BoxCollider2D>());
            Assert.AreSame(reusable, borrowed); Assert.True(borrowed.gameObject.activeSelf);
            Assert.AreEqual(hp, enemy.CurrentHp); Assert.Zero(listener.afterHits);
        }

        private Projectile Shot(ProjectileFaction faction, Listener listener = null)
        {
            if (prefab == null)
            {
                var go = Own(new GameObject("RegressionShot_" + Guid.NewGuid().ToString("N"), typeof(BoxCollider2D)));
                go.SetActive(false); prefab = go.AddComponent<Projectile>();
                PoolManager.Instance.GetOrCreatePool(prefab, 1); poolKeys.Add("Projectile_" + prefab.name);
            }
            var shot = PoolManager.Instance.Get(prefab, new Vector3(45000, 45000));
            shot.Launch(Vector2.right, 5, 2, 30, prefab, faction, hitListener: listener); return shot;
        }

        private ArcProjectile Shell()
        {
            if (arcPrefab == null)
            {
                var go = Own(new GameObject("RegressionArc_" + Guid.NewGuid().ToString("N"), typeof(BoxCollider2D)));
                go.SetActive(false); arcPrefab = go.AddComponent<ArcProjectile>();
                PoolManager.Instance.GetOrCreatePool(arcPrefab, 1); poolKeys.Add("ArcProjectile_" + arcPrefab.name);
            }
            var arc = PoolManager.Instance.Get(arcPrefab, new Vector3(45000, 45000));
            arc.Launch(new Vector2(45010, 45000), 999, 1f, 3f, arcPrefab); return arc;
        }

        private T CreateEnemy<T>(Transform target) where T : EnemyBase
        {
            var data = Own(ScriptableObject.CreateInstance<EnemyData>());
            data.enemyId = "regression_enemy";
            data.baseHp = 100; data.detectionRange = 5; data.attackRange = 1; data.moveSpeed = 3;
            data.patrolRadius = 0; data.staggerDuration = 0;
            var go = Own(new GameObject("RegressionEnemy", typeof(BoxCollider2D))); go.SetActive(false);
            go.transform.position = new Vector3(40000, 40000); var enemy = go.AddComponent<T>();
            Set(enemy, "data", data); Set(enemy, "target", target);
            go.GetComponent<Rigidbody2D>().gravityScale = 0; go.SetActive(true); return enemy;
        }

        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static void Set(object obj, string name, object value) => typeof(EnemyBase).GetField(name, Private).SetValue(obj, value);
        private static void Evaluate(EnemyBase enemy) => typeof(EnemyBase).GetMethod("EvaluateTransitions", Private).Invoke(enemy, null);
        private static void Trigger(Projectile shot, Collider2D col) => typeof(Projectile).GetMethod("OnTriggerEnter2D", Private).Invoke(shot, new object[] { col });
        private sealed class Listener : IProjectileHitListener, IProjectileEnemyHitListener, IProjectileCriticalFeedback
        {
            public Action before;
            public int afterHits;
            public bool LastHitWasCritical => true;
            public int OnProjectileHit(int damage, bool first) { before?.Invoke(); return damage; }
            public void OnProjectileEnemyHit(EnemyBase enemy, bool first) => afterHits++;
        }
    }
}
