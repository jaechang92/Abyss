using Abyss.Runtime.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 첫 보스 에셋 배선(C2). 🔴 <b>프리팹 항목은 에디터 진입점 <c>FirstBossWiring.Apply</c> 실행 뒤에만 통과한다</b> —
    /// 스크립트 .meta 를 Unity 가 만들어야 프리팹이 새 클래스를 가리킬 수 있어서, 코드만으로는 교체할 수 없다.
    /// 실행 전 실패는 「배선 미적용」이지 코드 결함이 아니다(C2-build-validation.md 절차 순서 참조).
    /// </summary>
    public sealed class FirstBossAssetTests
    {
        private const string DataPath = "Assets/Resources/Data/Enemies/BossAbyssKeeper.asset";
        private const string PrefabPath = "Assets/Prefabs/Enemies/BossAbyssKeeper.prefab";
        private const string TelegraphSfxPath = "Assets/Audio/SFX/boss_telegraph.wav";

        [Test]
        public void 데이터는_예고와_회복만_바뀌고_피해_HP_쿨다운은_그대로다()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            Assert.IsNotNull(data, DataPath);

            Assert.AreEqual(0.7f, data.attackWindup, 1e-5f, "근접 예고(결정 2)");
            Assert.AreEqual(0.5f, data.attackRecovery, 1e-5f, "근접 회복(결정 2)");

            Assert.AreEqual(400, data.baseHp);
            Assert.AreEqual(30, data.baseDamage);
            Assert.AreEqual(1.8f, data.attackCooldown, 1e-5f);
            Assert.AreEqual(2.5f, data.attackRange, 1e-5f);
            Assert.AreEqual(0f, data.staggerDuration, 1e-5f);
            Assert.Less(data.attackWindup + data.attackRecovery, data.attackCooldown, "동작이 쿨다운보다 길면 공격 상태에 갇힌다");
        }

        [Test]
        public void 프리팹은_AbyssKeeperBoss이고_기존_예고음을_쓴다()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);

            var enemies = prefab.GetComponents<EnemyBase>();
            Assert.AreEqual(1, enemies.Length, "적 컴포넌트는 하나");
            Assert.IsInstanceOf<AbyssKeeperBoss>(enemies[0], "FirstBossWiring.Apply 미실행이면 여기서 실패한다");

            var so = new SerializedObject(enemies[0]);
            Assert.AreEqual(TelegraphSfxPath, AssetDatabase.GetAssetPath(so.FindProperty("telegraphSfx").objectReferenceValue));
            Assert.AreEqual(3f, so.FindProperty("volleyInterval").floatValue, 1e-5f, "볼리 주기 불변");
            Assert.AreEqual(3, so.FindProperty("baseVolleyCount").intValue, "볼리 발 수 불변");
        }

        [TestCase("Assets/Prefabs/Enemies/MidbossSentinel.prefab", typeof(MidBossSentinelBoss))]
        [TestCase("Assets/Prefabs/Enemies/MidbossThroneWarden.prefab", typeof(MidBossSentinelBoss))]
        [TestCase("Assets/Prefabs/Enemies/BossFlameSerpent.prefab", typeof(FlameSerpentBoss))]
        [TestCase("Assets/Prefabs/Enemies/BossThronebound.prefab", typeof(ThroneboundBoss))]
        public void 다른_보스는_자기_클래스_그대로다(string path, System.Type expected)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var enemies = prefab.GetComponents<EnemyBase>();
            Assert.AreEqual(1, enemies.Length, path);
            Assert.AreEqual(expected, enemies[0].GetType(), "감시자·파수관·다른 보스 불변(결정 4)");
        }
    }
}
