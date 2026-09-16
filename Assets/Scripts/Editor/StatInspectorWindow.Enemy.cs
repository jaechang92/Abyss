#if UNITY_EDITOR
using Abyss.Runtime.Enemy;
using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>적 스탯 그리기. 창의 틀·줄 그리기는 <c>StatInspectorWindow.cs</c> 가 갖는다.</summary>
    internal sealed partial class StatInspectorWindow
    {
        private void DrawEnemy(EnemyBase enemy)
        {
            bool isLive = EditorApplication.isPlaying;
            var data = enemy.Data;

            Section("요약");
            if (data == null)
            {
                Warn("EnemyData 가 비어 있다 — 스탯이 전부 0으로 동작한다.");
                return;
            }

            Row("이름", $"{data.displayName} ({data.enemyId})");
            Row("등급", data.tier.ToString());
            Row("HP", isLive ? $"{enemy.CurrentHp} / {enemy.MaxHp}" : enemy.MaxHp.ToString());
            if (isLive)
            {
                Row("상태", enemy.IsDead ? "사망" : "생존");
                Row("타깃", enemy.Target != null ? enemy.Target.name : "없음");
            }

            Section("공격");
            // 실제 공격력은 GetAttackDamage 가 정한다(보스는 페이즈 배수를 곱한다) — 그 결과를 읽는다.
            Row("공격력", isLive ? $"{enemy.AttackDamage}    (기본 {data.baseDamage})" : data.baseDamage.ToString());
            if (isLive && enemy is BossEnemy boss)
            {
                Row("페이즈", boss.CurrentPhase);
                MultRow("   페이즈 배율", boss.CurrentDamageMultiplier);
            }
            Row("방식", AttackStyleText(data));
            Row("사거리", $"{data.attackRange:0.00}");
            Row("쿨다운", $"{data.attackCooldown:0.00}s");

            Section("이동 · 감지");
            Row("이동속도", $"{data.moveSpeed:0.00}    (순찰 {Mult(data.patrolSpeedMultiplier)})");
            Row("감지 범위", $"{data.detectionRange:0.00}");
            Row("순찰 반경", $"{data.patrolRadius:0.00}");

            if (isLive)
            {
                Section("상태 이상");
                Row("연소", enemy.IsBurning ? $"{enemy.BurnStacks} 스택" : "없음");
            }

            Section("보상");
            Row("경험치", data.expReward);
            Row("골드", data.goldReward);

            EditorGUILayout.Space(6f);
            // 선택을 바꾸면 창이 에셋을 따라가 버리므로 핑만 한다.
            if (GUILayout.Button("EnemyData 에셋 핑", EditorStyles.miniButton))
            {
                EditorGUIUtility.PingObject(data);
            }
        }

        private static string AttackStyleText(EnemyData data)
        {
            if (data.usesArcProjectile) return "곡사";
            if (data.isRanged) return data.burstCount > 1 ? $"원거리 (연사 {data.burstCount})" : "원거리";
            return "근접";
        }
    }
}
#endif
