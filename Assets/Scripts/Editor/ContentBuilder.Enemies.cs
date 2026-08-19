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

            // ── 원거리 3종 (M2-Q1 잔여) ──
            // 셋 다 전용 AI 클래스 없이 데이터만으로 만든다. EnemyData의 isRanged·burstCount·
            // arcProjectilePrefab 조합이 행동을 정하므로, 다음 원거리 적은 이 블록에 항목만 늘리면 된다.

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/BoneArcher.asset", so =>
            {
                so.enemyId = "bone_archer";
                so.displayName = "뼈 궁수";
                // RangedArcher(25/12/사거리5)보다 멀리서 세게 쏘고 대신 더 느리고 무르다.
                // "더 센 궁수"가 아니라 "다른 거리에서 싸우는 궁수"가 되게 사거리를 벌렸다.
                so.baseHp = 22;
                so.baseDamage = 16;
                so.moveSpeed = 1.8f;
                so.detectionRange = 10f;
                so.attackRange = 7.5f;
                so.attackCooldown = 2.4f;
                so.expReward = 22;
                so.goldReward = 5;
                so.isRanged = true;
                so.projectileSpeed = 13f;      // 빠른 탄 — 멀어서 느리면 걸어서 피해진다
                so.projectileLifetime = 3.5f;
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/VoidCaster.asset", so =>
            {
                so.enemyId = "void_caster";
                so.displayName = "공허 술사";
                so.baseHp = 28;
                so.baseDamage = 7;             // 3연사라 발당 피해는 낮다(총 21)
                so.moveSpeed = 2.2f;
                so.detectionRange = 8f;
                so.attackRange = 5.5f;
                so.attackCooldown = 2.6f;      // 연사 뒤 긴 휴식 — 이 틈이 접근 기회다
                so.expReward = 24;
                so.goldReward = 5;
                so.isRanged = true;
                so.projectileSpeed = 7f;       // 느린 탄 — 세 발이 흩어져 날아오는 게 보여야 한다
                so.projectileLifetime = 3f;
                so.burstCount = 3;
                so.burstInterval = 0.18f;
            });

            CreateOrSkip<EnemyData>($"{AbyssPaths.Enemies}/FlameMortar.asset", so =>
            {
                so.enemyId = "flame_mortar";
                so.displayName = "화염 박격포";
                // 사거리가 가장 길고 가장 무르다. 곡사는 근접하면 무력해지므로
                // "먼저 붙어야 하는 적"이라는 역할이 스탯으로도 읽히게 했다.
                so.baseHp = 34;
                so.baseDamage = 20;
                so.moveSpeed = 1.4f;
                so.detectionRange = 12f;
                so.attackRange = 9f;
                so.attackCooldown = 3.2f;
                so.expReward = 28;
                so.goldReward = 6;
                so.isRanged = true;
                so.usesArcProjectile = true;
                so.arcFlightTime = 1.15f;      // 예고 링이 떠 있는 시간 = 회피 창
                so.arcExplosionRadius = 2f;
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
                so.tier = EnemyTier.Elite;
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
                so.tier = EnemyTier.Boss;
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
                so.tier = EnemyTier.MidBoss;   // 보상은 엘리트와 같고(같은 트리거) 표시는 보스 탭
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
                so.tier = EnemyTier.Boss;
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
                so.tier = EnemyTier.MidBoss;   // Sentinel과 동일
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
                so.tier = EnemyTier.Boss;
                so.patrolRadius = 0f; // 보스는 Patrol 정지
            });
        }
    }
}
#endif
