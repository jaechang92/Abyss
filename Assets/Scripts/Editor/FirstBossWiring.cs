#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Abyss.Runtime.Enemy;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// C2 — 첫 보스(BossAbyssKeeper) 프리팹을 <see cref="AbyssKeeperBoss"/> 로 바꾸고 기존 <c>boss_telegraph.wav</c> 를 잇는 제한적 진입점.
    /// <c>-executeMethod Abyss.EditorTools.FirstBossWiring.ApplyBatch</c> / <c>ValidateBatch</c>(실패 시 종료 코드 1).
    ///
    /// 🔴 <b>PrefabBuilder · EnemyAnimationBuilder 를 돌리지 않는다</b> — 그쪽은 적 여러 종을 다시 굽는다(C-integration §2).
    /// 여기는 프리팹 <b>한 개</b>의 스크립트 참조만 바꾼다. 방법은 <c>m_Script</c> 교체라 컴포넌트의 파일 ID 와
    /// 직렬값(데이터 참조·페이즈·볼리·효과음)이 그대로 남는다 — 적용 전후 직렬값을 전부 비교해 하나라도 다르면 저장하지 않는다.
    ///
    /// 📌 스크립트 .meta 는 Unity 가 만든다 — 그래서 GUID 를 코드에 적지 않고 경로로 찾는다.
    /// 🔑 데이터 에셋의 windup 0.7 · recovery 0.5 는 이미 YAML 로 들어가 있다. <see cref="Apply"/> 는 값이 다를 때만 맞춘다(멱등).
    /// </summary>
    public static class FirstBossWiring
    {
        public const string PrefabPath = AbyssPaths.EnemyPrefabs + "/BossAbyssKeeper.prefab";
        public const string DataPath = AbyssPaths.Enemies + "/BossAbyssKeeper.asset";
        private const string ScriptPath = "Assets/Scripts/Runtime/Enemy/AbyssKeeperBoss.cs";
        private const string TelegraphSfxPath = AbyssPaths.Sfx + "/boss_telegraph.wav";
        private const string PhaseSfxPath = AbyssPaths.Sfx + "/boss_phase.wav";

        // 총괄 결정 2 — 검증용 값. 나머지는 「바뀌지 않았다」를 확인하는 기준값(현재 에셋에서 읽은 사실).
        public const float ExpectedWindup = 0.7f;
        public const float ExpectedRecovery = 0.5f;
        private const int ExpectedHp = 400;
        private const int ExpectedDamage = 30;
        private const float ExpectedCooldown = 1.8f;
        private const float ExpectedRange = 2.5f;
        private const float ExpectedStagger = 0f;

        // 다른 보스는 이 타입 그대로여야 한다(결정 4 — 감시자·파수관·다른 보스 불변).
        private static readonly (string prefab, System.Type type)[] OtherBosses =
        {
            (AbyssPaths.EnemyPrefabs + "/MidbossSentinel.prefab", typeof(MidBossSentinelBoss)),
            (AbyssPaths.EnemyPrefabs + "/MidbossThroneWarden.prefab", typeof(MidBossSentinelBoss)),
            (AbyssPaths.EnemyPrefabs + "/BossFlameSerpent.prefab", typeof(FlameSerpentBoss)),
            (AbyssPaths.EnemyPrefabs + "/BossThronebound.prefab", typeof(ThroneboundBoss)),
        };

        public static void ApplyBatch()
        {
            bool isOk = Apply();
            if (Application.isBatchMode) EditorApplication.Exit(isOk ? 0 : 1);
        }

        public static void ValidateBatch()
        {
            bool isOk = Validate();
            if (Application.isBatchMode) EditorApplication.Exit(isOk ? 0 : 1);
        }

        /// <summary>데이터의 예고·회복 값을 맞추고 프리팹 스크립트를 교체한다. 실패하면 저장하지 않고 false.</summary>
        public static bool Apply()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
            var telegraphSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(TelegraphSfxPath);
            if (data == null || script == null || script.GetClass() != typeof(AbyssKeeperBoss) || telegraphSfx == null)
            {
                Debug.LogError($"[FirstBossWiring] 준비물 없음 — data {data != null} · script {script != null} · sfx {telegraphSfx != null}");
                return false;
            }

            ApplyTiming(data);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var bosses = root.GetComponents<EnemyBase>();
                if (bosses.Length != 1 || bosses[0] is not BossEnemy)
                {
                    Debug.LogError($"[FirstBossWiring] 루트의 적 컴포넌트가 BossEnemy 1개가 아니다({bosses.Length}개) — 손대지 않는다.");
                    return false;
                }

                var before = Snapshot(bosses[0]);
                if (bosses[0].GetType() != typeof(AbyssKeeperBoss))
                {
                    var so = new SerializedObject(bosses[0]);
                    so.FindProperty("m_Script").objectReferenceValue = script;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var keeper = root.GetComponent<AbyssKeeperBoss>();
                if (keeper == null)
                {
                    Debug.LogError("[FirstBossWiring] m_Script 교체 후 AbyssKeeperBoss 를 찾지 못했다 — 저장하지 않는다.");
                    return false;
                }

                var keeperSo = new SerializedObject(keeper);
                keeperSo.FindProperty("telegraphSfx").objectReferenceValue = telegraphSfx;
                keeperSo.ApplyModifiedPropertiesWithoutUndo();

                var after = Snapshot(keeper);
                var changed = before.Where(pair => !after.TryGetValue(pair.Key, out string now) || now != pair.Value)
                                    .Select(pair => $"  {pair.Key}: {pair.Value} → {(after.TryGetValue(pair.Key, out string v) ? v : "(없음)")}")
                                    .ToList();
                if (changed.Count > 0)
                {
                    Debug.LogError("[FirstBossWiring] 기존 직렬값이 바뀌었다 — 저장하지 않는다.\n" + string.Join("\n", changed));
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[FirstBossWiring] 완료 — {PrefabPath} → AbyssKeeperBoss · 기존 직렬값 {before.Count}개 보존 · telegraphSfx = boss_telegraph");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>예고·회복 두 값만 맞춘다. 이미 맞으면 저장하지 않는다. 피해·HP·쿨다운은 건드리지 않는다.</summary>
        private static void ApplyTiming(EnemyData data)
        {
            if (Mathf.Approximately(data.attackWindup, ExpectedWindup) && Mathf.Approximately(data.attackRecovery, ExpectedRecovery)) return;

            Debug.Log($"[FirstBossWiring] {DataPath} windup {data.attackWindup} → {ExpectedWindup} · recovery {data.attackRecovery} → {ExpectedRecovery}");
            data.attackWindup = ExpectedWindup;
            data.attackRecovery = ExpectedRecovery;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>읽기 전용 검증. 결과는 <c>[C2-BOSS]</c> 로그.</summary>
        public static bool Validate()
        {
            var report = new C2Report("C2-BOSS");

            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
            report.Check("데이터 로드", data != null, DataPath);
            if (data == null) return report.Finish();

            report.Check("근접 예고·회복", Mathf.Approximately(data.attackWindup, ExpectedWindup) && Mathf.Approximately(data.attackRecovery, ExpectedRecovery),
                         $"windup {data.attackWindup} (기대 {ExpectedWindup}) · recovery {data.attackRecovery} (기대 {ExpectedRecovery})");
            report.Check("HP·피해·쿨다운·사거리·경직 불변",
                         data.baseHp == ExpectedHp && data.baseDamage == ExpectedDamage && Mathf.Approximately(data.attackCooldown, ExpectedCooldown)
                         && Mathf.Approximately(data.attackRange, ExpectedRange) && Mathf.Approximately(data.staggerDuration, ExpectedStagger),
                         $"HP {data.baseHp} · 피해 {data.baseDamage} · 쿨다운 {data.attackCooldown} · 사거리 {data.attackRange} · 경직 {data.staggerDuration}");
            report.Check("동작 합 < 쿨다운", data.attackWindup + data.attackRecovery < data.attackCooldown,
                         $"{data.attackWindup} + {data.attackRecovery} = {data.attackWindup + data.attackRecovery} < {data.attackCooldown}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var keeper = prefab != null ? prefab.GetComponent<AbyssKeeperBoss>() : null;
            var enemies = prefab != null ? prefab.GetComponents<EnemyBase>() : new EnemyBase[0];
            report.Check("프리팹 = AbyssKeeperBoss 1개", keeper != null && enemies.Length == 1, $"{PrefabPath} 적 컴포넌트 {enemies.Length}개");
            report.Check("스폰 프리팹 연결", data.spawnPrefab != null && data.spawnPrefab == prefab, "EnemyData.spawnPrefab → BossAbyssKeeper.prefab");

            if (keeper != null)
            {
                var so = new SerializedObject(keeper);
                string telegraph = AssetDatabase.GetAssetPath(so.FindProperty("telegraphSfx").objectReferenceValue);
                string phase = AssetDatabase.GetAssetPath(so.FindProperty("phaseChangeSfx").objectReferenceValue);
                report.Check("예고음 = 기존 boss_telegraph", telegraph == TelegraphSfxPath, telegraph);
                report.Check("페이즈음 유지", phase == PhaseSfxPath, phase);
                report.Check("데이터 참조 유지", so.FindProperty("data").objectReferenceValue == data, "data → BossAbyssKeeper.asset");
                report.Check("볼리 직렬값 유지",
                             Mathf.Approximately(so.FindProperty("volleyInterval").floatValue, 3f)
                             && so.FindProperty("baseVolleyCount").intValue == 3
                             && Mathf.Approximately(so.FindProperty("spreadAngle").floatValue, 40f),
                             $"주기 {so.FindProperty("volleyInterval").floatValue} · 발 수 {so.FindProperty("baseVolleyCount").intValue} · 확산 {so.FindProperty("spreadAngle").floatValue}");
            }

            foreach (var (path, type) in OtherBosses)
            {
                var other = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var components = other != null ? other.GetComponents<EnemyBase>() : new EnemyBase[0];
                bool isSame = components.Length == 1 && components[0].GetType() == type;
                report.Check($"다른 보스 불변 {System.IO.Path.GetFileNameWithoutExtension(path)}", isSame,
                             $"{type.Name} 기대 · 실제 {string.Join(",", components.Select(c => c.GetType().Name))}");
            }

            for (int phase = 1; phase <= 3; phase++)
            {
                float interval = 3f / phase;
                float telegraph = AbyssKeeperVolleySchedule.TelegraphTime(phase, interval);
                report.Check($"탄막 예고 P{phase}", telegraph > 0f && telegraph < interval,
                             $"예고 {telegraph:0.##}초 · 주기 {interval:0.##}초(불변) · 예고 없는 구간 {interval - telegraph:0.##}초");
            }

            return report.Finish();
        }

        /// <summary>컴포넌트의 직렬값 전부(경로 → 값 문자열). <c>m_Script</c> 는 뺀다 — 바꾸는 대상이다.</summary>
        private static Dictionary<string, string> Snapshot(Object target)
        {
            var result = new Dictionary<string, string>();
            var so = new SerializedObject(target);
            var it = so.GetIterator();
            bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = true;
                // 클래스 정체 메타데이터는 교체 대상 그 자체라 비교에서 뺀다.
                if (it.propertyPath == "m_Script" || it.propertyPath.StartsWith("m_Script.", System.StringComparison.Ordinal)
                    || it.propertyPath == "m_EditorClassIdentifier") continue;
                string value = Describe(it);
                if (value != null) result[it.propertyPath] = value;
            }
            return result;
        }

        private static string Describe(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.doubleValue.ToString("R");
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Color: return p.colorValue.ToString("F4");
                case SerializedPropertyType.Enum: return p.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString("F4");
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("F4");
                case SerializedPropertyType.ObjectReference:
                    return p.objectReferenceValue != null
                        ? AssetDatabase.GetAssetPath(p.objectReferenceValue) + ":" + p.objectReferenceValue.name
                        : "null";
                default: return null;
            }
        }
    }
}
#endif
