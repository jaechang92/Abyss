#if UNITY_EDITOR
using Abyss.Runtime.Enemy;

namespace Abyss.EditorTools
{
    public static partial class ContentBuilder
    {
        private static void CreateEnemies()
        {
            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/MeleeGrunt.asset", so =>
            {
                so.enemyId = "melee_grunt";
                so.displayName = "근접 병사";
                so.baseHp = 30;
                so.baseDamage = 10;
                so.moveSpeed = 3f;
                so.detectionRange = 6f;
                so.attackRange = 1.2f;
                so.attackCooldown = 1.5f;
                so.expReward = 15;
                so.goldReward = 3;
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/MeleeBrute.asset", so =>
            {
                so.enemyId = "melee_brute";
                so.displayName = "중장 강적";
                so.baseHp = 60;
                so.baseDamage = 18;
                so.moveSpeed = 2.5f;
                so.detectionRange = 6f;
                so.attackRange = 1.5f;
                so.attackCooldown = 2f;
                so.expReward = 25;
                so.goldReward = 5;
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/RangedArcher.asset", so =>
            {
                so.enemyId = "ranged_archer";
                so.displayName = "원거리 사수";
                so.baseHp = 25;
                so.baseDamage = 12;
                so.moveSpeed = 2f;
                so.detectionRange = 8f;
                so.attackRange = 5f;
                so.attackCooldown = 1.8f;
                so.expReward = 20;
                so.goldReward = 4;
                so.isRanged = true;
                so.projectileSpeed = 9f;       // 발사체 속도(근접 즉발 대신 직진 탄)
                so.projectileLifetime = 3f;    // 미명중 시 소멸 시간
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/EliteHunter.asset", so =>
            {
                so.enemyId = "elite_hunter";
                so.displayName = "엘리트 사냥꾼";
                so.baseHp = 120;
                so.baseDamage = 25;
                so.moveSpeed = 3.5f;
                so.detectionRange = 8f;
                so.attackRange = 1.8f;
                so.attackCooldown = 1.3f;
                so.expReward = 60;
                so.goldReward = 15;
                so.isElite = true;
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/BossAbyssKeeper.asset", so =>
            {
                so.enemyId = "boss_abyss_keeper";
                so.displayName = "심연의 수호자";
                so.baseHp = 400;
                so.baseDamage = 30;
                so.moveSpeed = 2.5f;
                so.detectionRange = 12f;
                so.attackRange = 2.5f;
                so.attackCooldown = 1.8f;
                so.expReward = 200;
                so.goldReward = 50;
                so.isBoss = true;
                so.patrolRadius = 0f; // 보스는 Patrol 정지 (수동 페이즈 스크립트로 제어)
            });

            // Stage 2 "불꽃의 회랑" 신규 적 2종. AI·페이즈 패턴은 기존 재활용(스탯만 차별화),
            // 정식 회전베기/화염브레스 패턴은 M2 본작업으로 연기(08-content-roadmap.md).
            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/MidBossSentinel.asset", so =>
            {
                so.enemyId = "midboss_sentinel";
                so.displayName = "감시자 거인";
                so.baseHp = 220;
                so.baseDamage = 28;
                so.moveSpeed = 2.8f;
                so.detectionRange = 8f;
                so.attackRange = 2f;
                so.attackCooldown = 1.4f;
                so.expReward = 90;
                so.goldReward = 20;
                so.isElite = true; // Stage2 중간보스 — EliteBonus 드래프트 트리거 재활용
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/BossFlameSerpent.asset", so =>
            {
                so.enemyId = "boss_flame_serpent";
                so.displayName = "화염 뱀";
                so.baseHp = 520;
                so.baseDamage = 38;
                so.moveSpeed = 3f;
                so.detectionRange = 12f;
                so.attackRange = 2.8f;
                so.attackCooldown = 1.6f;
                so.expReward = 260;
                so.goldReward = 65;
                so.isBoss = true;
                so.patrolRadius = 0f; // 보스는 Patrol 정지
            });

            // Stage 3 "왕좌의 잔해" 신규 2종.
            // 중간보스는 AI를 새로 만들지 않고 MidBossSentinelBoss(회전베기)를 스탯·이름만 바꿔 재사용한다
            // — Stage2 신규 적이 세운 선례. 최종보스만 전용 패턴(ThroneboundBoss)을 갖는다.
            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/MidBossThroneWarden.asset", so =>
            {
                so.enemyId = "midboss_throne_warden";
                so.displayName = "왕좌의 파수관";
                so.baseHp = 300;
                so.baseDamage = 34;
                so.moveSpeed = 3f;
                so.detectionRange = 9f;
                so.attackRange = 2.2f;
                so.attackCooldown = 1.3f;
                so.expReward = 120;
                so.goldReward = 26;
                so.isElite = true; // Stage3 중간보스 — EliteBonus 드래프트 트리거 재활용(Sentinel과 동일)
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/BossThronebound.asset", so =>
            {
                so.enemyId = "boss_thronebound";
                so.displayName = "왕좌의 영혼";
                so.baseHp = 680;
                so.baseDamage = 44;
                so.moveSpeed = 3.2f;
                so.detectionRange = 14f; // 순간이동으로 거리를 지우는 보스라 감지 범위가 넓어야 패턴이 돈다
                so.attackRange = 2.6f;
                so.attackCooldown = 1.5f;
                so.expReward = 340;
                so.goldReward = 85;
                so.isBoss = true;
                so.patrolRadius = 0f; // 보스는 Patrol 정지
            });
        }
    }
}
#endif
