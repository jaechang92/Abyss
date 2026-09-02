using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests.PlayMode
{
    /// <summary>
    /// 전투 스모크 — 피해·사망·처치 통지.
    ///
    /// 룸 클리어 판정은 <c>GameEvents.OnEnemyKilled</c> 하나에 걸려 있다. 이 이벤트가 안 오면
    /// 방이 영영 안 넘어가고, 두 번 오면 보상이 겹치고 남은 적 수가 음수로 새어 방이 일찍 끝난다.
    /// <b>발행 횟수 자체가 진행의 전제</b>라 여기서 정확히 1회를 못 박는다.
    ///
    /// 발사체는 진영(<see cref="ProjectileFaction"/>)만 본다. 실제 물리 충돌까지 재현하면
    /// 레이어·중력 설정에 묶여 콘텐츠가 바뀔 때마다 깨지는 테스트가 된다.
    /// </summary>
    public sealed class CombatPlayModeSmoke
    {
        private readonly PlayModeSaveGuard saveGuard = new PlayModeSaveGuard();
        private readonly List<Object> spawned = new List<Object>();

        private int killedCount;
        private EnemyData lastKilledData;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saveGuard.Acquire();

            killedCount = 0;
            lastKilledData = null;
            GameEvents.OnEnemyKilled += HandleEnemyKilled;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.OnEnemyKilled -= HandleEnemyKilled;

            foreach (var obj in spawned)
            {
                if (obj != null) Object.Destroy(obj);
            }
            spawned.Clear();

            yield return null;

            saveGuard.Release();
        }

        private void HandleEnemyKilled(EnemyData data, Vector3 _)
        {
            killedCount += 1;
            lastKilledData = data;
        }

        // ───────────────────────── 테스트 ─────────────────────────

        /// <summary>HP를 다 깎으면 죽고, 처치 통지가 <b>정확히 한 번</b> 나간다.</summary>
        [UnityTest]
        public IEnumerator 적은_HP가_0이_되면_죽고_처치_통지가_한_번_나간다()
        {
            var enemy = SpawnEnemy(baseHp: 30);
            yield return null;  // Awake/Start 진행

            Assert.AreEqual(30, enemy.CurrentHp, "baseHp 가 현재 HP로 들어가지 않았다.");
            Assert.IsFalse(enemy.IsDead);

            enemy.TakeDamage(30);
            yield return null;

            Assert.IsTrue(enemy.IsDead, "HP를 다 깎았는데 사망 처리가 안 됐다.");
            Assert.AreEqual(1, killedCount, "처치 통지는 정확히 1회여야 한다.");
            Assert.IsNotNull(lastKilledData, "처치 통지에 EnemyData 가 실려 있어야 보상·도감이 붙는다.");
        }

        /// <summary>
        /// 죽은 적을 또 때려도 통지가 늘지 않는다.
        ///
        /// 시체에 남은 발사체·장판이 닿는 상황은 실제로 생긴다. 그때 통지가 한 번 더 나가면
        /// 룸의 잔여 적 수가 실제보다 빨리 0이 되어 <b>살아 있는 적을 두고 방이 넘어간다</b>.
        /// </summary>
        [UnityTest]
        public IEnumerator 죽은_적을_다시_때려도_처치_통지는_늘지_않는다()
        {
            var enemy = SpawnEnemy(baseHp: 10);
            yield return null;

            enemy.TakeDamage(10);
            yield return null;
            Assert.AreEqual(1, killedCount);

            enemy.TakeDamage(10);
            enemy.TakeDamage(10);
            yield return null;

            Assert.AreEqual(1, killedCount, "사망 후 추가 피해가 처치를 또 세었다 — 룸 클리어가 앞당겨진다.");
        }

        /// <summary>피해가 HP보다 작으면 죽지 않고 HP만 깎인다(경직 경로 회귀 방지).</summary>
        [UnityTest]
        public IEnumerator 치명타가_아니면_HP만_깎이고_살아_있다()
        {
            var enemy = SpawnEnemy(baseHp: 50);
            yield return null;

            enemy.TakeDamage(20);
            yield return null;

            Assert.AreEqual(30, enemy.CurrentHp);
            Assert.IsFalse(enemy.IsDead);
            Assert.AreEqual(0, killedCount, "죽지 않았는데 처치 통지가 나갔다.");
        }

        /// <summary>
        /// 발사체 진영이 발사 시점에 지정한 값으로 남는가.
        ///
        /// 기본값이 <see cref="ProjectileFaction.HitsPlayer"/>라, 플레이어 스킬이 진영을 안 넘기면
        /// <b>자기 발사체에 자기가 맞는다</b>. 기본값 쪽으로 조용히 넘어가는 실수라 값을 직접 확인한다.
        /// </summary>
        [UnityTest]
        public IEnumerator 발사체는_지정한_진영을_유지한다()
        {
            var projectile = SpawnProjectile();

            // ⚠️ 활성화한 채 프레임을 흘리기 전에 Launch 해야 한다 — 아래 SpawnProjectile 주석 참조.
            projectile.Launch(Vector2.right, dmg: 5, spd: 8f, life: 3f, prefab: null,
                faction: ProjectileFaction.HitsEnemies);

            var field = typeof(Projectile).GetField("faction", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Projectile.faction 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");
            Assert.AreEqual(ProjectileFaction.HitsEnemies, (ProjectileFaction)field.GetValue(projectile),
                "플레이어 발사체로 쐈는데 진영이 적 발사체(기본값)로 남았다.");

            yield return null;
        }

        // ───────────────────────── 헬퍼 ─────────────────────────

        /// <summary>
        /// 최소 구성의 적을 만든다.
        ///
        /// <c>EnemyBase</c>는 <c>StateMachine</c>·<c>Rigidbody2D</c>를 RequireComponent 로 요구하므로
        /// AddComponent 가 알아서 붙인다. <c>data</c>는 SerializeField 라 리플렉션으로 넣고,
        /// <b>비활성 상태에서 넣은 뒤 활성화</b>한다 — Awake 가 data 를 읽어 HP를 채우기 때문에
        /// 순서가 뒤바뀌면 HP 0으로 시작해 첫 타격에 죽는다.
        /// </summary>
        private EnemyBase SpawnEnemy(int baseHp)
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyId = "test_enemy";
            data.displayName = "테스트 적";
            data.baseHp = baseHp;
            data.expReward = 0;   // 레벨업 드래프트가 끼어들지 않게 보상은 0으로
            data.goldReward = 0;
            data.tier = EnemyTier.Normal;
            spawned.Add(data);

            var go = new GameObject("TestEnemy");
            go.SetActive(false);
            var enemy = go.AddComponent<EnemyBase>();
            spawned.Add(go);

            var field = typeof(EnemyBase).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "EnemyBase.data 필드를 찾지 못했다 — 이름이 바뀌었는지 확인할 것.");
            field.SetValue(enemy, data);

            go.SetActive(true);
            return enemy;
        }

        /// <summary>
        /// 발사체를 만든다.
        ///
        /// 🔴 <b>콜라이더를 먼저 붙여야 한다.</b> <c>Projectile</c>은 <c>[RequireComponent(typeof(Collider2D))]</c>
        /// 인데 <c>Collider2D</c>는 추상 클래스라 Unity가 대신 붙여 줄 구체 타입을 고르지 못한다.
        /// 그 상태로 AddComponent 하면 예외가 아니라 <b>null 이 돌아온다</b> — 다음 줄의 NRE 로만 드러나
        /// 원인이 RequireComponent 라는 걸 짐작하기 어렵다.
        ///
        /// 🔴 <b>비활성인 채로 돌려준다.</b> 활성 상태로 한 프레임이라도 흐르면 <c>Update</c>가
        /// <c>lifetime</c> 기본값 0 으로 돌아 <c>aliveTimer >= lifetime</c> 을 즉시 만족시키고,
        /// <c>ReturnToPool</c> → (풀도 prefabRef 도 없으므로) <c>Destroy</c> 로 <b>발사도 하기 전에 자멸한다</b>.
        /// 그 뒤의 접근은 MissingReferenceException 으로 나타나 원인이 수명 계산이라는 걸 가린다.
        /// 실사용에서는 스폰과 <c>Launch</c> 사이에 프레임이 끼지 않아 드러나지 않는 창이다.
        /// </summary>
        private Projectile SpawnProjectile()
        {
            var go = new GameObject("TestProjectile");
            go.SetActive(false);
            go.AddComponent<CircleCollider2D>();  // RequireComponent(Collider2D) 충족 — 반드시 먼저
            var projectile = go.AddComponent<Projectile>();
            spawned.Add(go);

            Assert.IsNotNull(projectile, "Projectile 을 붙이지 못했다 — RequireComponent 요구가 늘었는지 확인할 것.");

            return projectile;  // 활성화는 하지 않는다 — Launch 전 Update 가 돌면 안 된다
        }
    }
}
