#if UNITY_EDITOR
using Abyss.Runtime.Enemy;
using UnityEditor;
using UnityEngine;

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
                // 2026-09-18 애니메이션 — 검을 들어 올리는 동안(0.6초)이 피할 틈이다. 공격 클립 9장 = 0.8초.
                so.attackWindup = 0.6f;
                so.attackRecovery = 0.2f;
                so.staggerDuration = 0.3f;
                so.deathLingerDuration = 1f;
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
                // 2026-09-18 애니메이션 — 무거운 적이라 근접 병사(0.6)보다 길게 든다. 공격 9장 1.2초 → f6(창끝이 땅)이 0.8초.
                so.attackWindup = 0.8f;
                so.attackRecovery = 0.4f;
                so.staggerDuration = 0.3f;
                so.deathLingerDuration = 1.2f;
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
                so.projectileSpeed = 9f;       // 곡사 배선이 빠졌을 때의 폴백(직진 탄)
                so.projectileLifetime = 3f;

                // 🔑 위를 향해 쏘던 자세가 굳은 적이라 화살이 포물선으로 떨어진다(사용자 결정 2026-09-18).
                // 조준점이 「발사 시점의 플레이어 자리」가 되므로 서 있으면 맞고 움직이면 피한다 —
                // 「표적을 보지 않고 쏘는 것」이라는 정체와 맞는다.
                so.usesArcProjectile = true;
                so.arcExplodes = false;        // 화살은 터지지 않는다 — 맞은 대상만. 예고 링도 없다
                so.arcFlightTime = 0.75f;      // 박격포(1.15)보다 빠르다. 사거리 5 에서 피할 수 있는 최소치
                so.arcExplosionRadius = 0.1f;  // 안 터지므로 쓰이지 않는다(Min 하한)
                so.projectileOrigin = new Vector2(0.9f, 0.6f); // 활이 머리 위다 — 공격 f4 화살촉 실측
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

                // 🔑 애니메이션이 붙은 네 번째 적(2026-09-19). 동작 시간은 그림에서 뽑았다 —
                // 공격 9프레임 중 f5 가 시위를 놓는 프레임이고(f3~f5 는 활이 수직으로 선다),
                // 5/9 지점이 windup 과 맞으려면 windup = 1.25 x recovery 여야 한다.
                // 0.75 + 0.6 = 1.35초로 f5 가 정확히 0.75초다.
                so.attackWindup = 0.75f;
                so.attackRecovery = 0.6f;      // 합 1.35 — 쿨다운 2.4 안에 넉넉히 들어간다
                so.staggerDuration = 0.3f;     // 앞 3종과 같다
                so.deathLingerDuration = 1f;   // 0.3 으로는 「위에서부터 풀려 내린다」가 안 담긴다

                // 🔴 사거리에서 파생시키지 않는다(Bug-048). 🔴 그리고 **쏘는 프레임에서** 재야 한다 —
                // 이 적은 평소 활을 45° 아래로 내리고 있다가 공격 때만 수직으로 세운다(attack v3).
                // 내린 자세에서 재면 (0.95, 0.15)가 나오는데 그건 화살이 안 나가는 자세다.
                // 값은 공격 f5 의 화살촉(칸 좌표 104, 54)을 피벗(62, 92)·PPU 32·콜라이더 높이 1.9 로 환산했다.
                so.projectileOrigin = new Vector2(1.3f, 0.24f);
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

                // 🔑 애니메이션이 붙은 다섯 번째 적(2026-09-20). 동작 시간은 그림에서 뽑았다 —
                // 공격 v2 9프레임에서 가리키는 팔이 뻗었다 접기를 되풀이하는데, 최대로 뻗는 칸이 f0·f4·f8 이다.
                // 접었다가 다시 뻗는 **첫 내지르기가 f4** 이므로 4/9 지점이 windup 과 맞아야 한다 —
                // windup = 0.8 x recovery 를 풀면 0.6 + 0.75 = 1.35초로 f4 가 정확히 0.6초다.
                so.attackWindup = 0.6f;
                so.attackRecovery = 0.75f;     // 합 1.35 — 쿨다운 2.6 안에 넉넉하다
                so.staggerDuration = 0.3f;     // 앞 4종과 같다
                so.deathLingerDuration = 1f;   // 0.3 으로는 「위에서부터 지워진다」가 안 담긴다

                // 🔴 연사 0.36초(3 x 0.18)가 windup 뒤에 시작해 0.96초에 끝난다 — 팔이 뻗어 있는
                // f4~f8 구간 안이다. 앞 4종에 없던 제약이라 windup 을 바꾸면 이것부터 다시 본다.

                // 🔴 사거리에서 파생시키지 않는다(Bug-048) · 🔴 쏘는 프레임에서 잰다(뼈 궁수 ⓚ).
                // 값은 공격 v2 f4 의 손끝(칸 좌표 92, 51)을 피벗(62, 92)·PPU 32·콜라이더 높이 2.0 으로 환산했다.
                // 🔑 원거리 3종이 전부 다르다 — 사수 (0.9, 0.6) 활이 머리 위 · 뼈 궁수 (1.3, 0.24) 활이 몸 앞 아래 ·
                // 이 적 (0.94, 0.28) 가리키는 손끝. 한 값을 돌려쓸 수 없다.
                so.projectileOrigin = new Vector2(0.94f, 0.28f);
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

                // 🔑 애니메이션이 붙은 여섯 번째 적(2026-09-20). 동작 시간은 그림에서 뽑았다 —
                // 공격 9프레임 중 f5 에서 기와 한 장이 더미 꼭대기를 떠난다(32프레임 중 유일하게 분리된 프레임).
                // 5/9 지점이 windup 과 맞으려면 windup = 1.25 x recovery 여야 한다(뼈 궁수와 같은 식).
                // 🔴 12종 중 가장 느린 적이라 합을 크게 잡았다 — 예비동작이 길어야 「피할 수 있는 적」이 된다.
                so.attackWindup = 1f;          // 앞 5종(0.5~0.8) 중 가장 길다
                so.attackRecovery = 0.8f;      // 합 1.8 — 쿨다운 3.2 안에 1.4 가 남는다
                so.staggerDuration = 0.3f;     // 앞 5종과 같다
                so.deathLingerDuration = 1.2f; // 「쏟아져 쌓인다」라 중장 강적과 같은 1.2 쪽

                // 🔴 사거리에서 파생시키지 않는다(Bug-048) · 🔴 쏘는 프레임에서 잰다(뼈 궁수 ⓚ).
                // 값은 공격 f5 에서 떠난 조각의 중심(칸 좌표 67, 31)을 피벗(62, 92)·PPU 32·콜라이더 높이 1.5 로 환산했다.
                // 🔑 원거리 4종이 전부 다르다 — 사수 (0.9, 0.6) 활이 머리 위 · 뼈 궁수 (1.3, 0.24) 몸 앞 아래 ·
                // 공허 술사 (0.94, 0.28) 가리키는 손끝 · 이 적 (0.16, 1.16) **몸 한가운데 바로 위**.
                // x 가 거의 0 이고 y 가 가장 높은 것이 「위로 올려 던지는 것」이라는 정체 그대로다.
                // 🔴 곡사의 옛 폴백(ArcFallbackOffset = up x 0.7)보다 훨씬 높다 — 발밑 폭발 안전장치가 필요 없어진다.
                so.projectileOrigin = new Vector2(0.16f, 1.16f);
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

        /// <summary>
        /// 적 피격/사망 효과음을 <b>등급별로</b> 연결한다(4-2).
        ///
        /// <see cref="CreateOrSkip"/>는 기존 에셋을 건너뛰므로 생성 시점의 값만으로는
        /// 이미 있는 12종에 소리가 안 붙는다 — 그래서 <b>매번 도는 별도 패스</b>로 뒀다.
        /// 이미 같은 클립이면 아무것도 안 하므로 몇 번 돌려도 안전하다(발동음 연결과 같은 형태).
        ///
        /// 🔑 <b>적마다가 아니라 등급마다 나눈다.</b> 피격음은 한 런에서 가장 많이 듣는 소리라
        /// 전부 같으면 잡몹과 보스를 때리는 감각이 구분되지 않고, 12종을 다 다르게 하면 피로해진다.
        /// 중간보스는 <see cref="EnemyTier.MidBoss"/>지만 <b>소리는 중량 쪽</b>이다 —
        /// 보스음은 스테이지 보스에게만 남겨 둬야 그 등장이 무거워진다.
        /// </summary>
        private static void LinkEnemySfx()
        {
            var normalHit = LoadSfx("enemy_hit");
            var normalDeath = LoadSfx("enemy_death");
            var heavyHit = LoadSfx("enemy_hit_heavy");
            var heavyDeath = LoadSfx("enemy_death_heavy");
            var bossHit = LoadSfx("boss_hit");
            var bossDeath = LoadSfx("boss_death");

            if (normalHit == null)
            {
                Debug.LogWarning(
                    "[ContentBuilder] 적 효과음이 없다 — _enemy_sfx_generator.py를 먼저 실행할 것. " +
                    "(파일이 없어도 게임은 조용히 돌아간다)");
                return;
            }

            int linked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyData", new[] { AbyssPaths.Enemies }))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;

                // 중장 강적은 등급이 Normal이지만 덩치로는 중량이다 — id로 예외를 둔다.
                bool heavy = data.tier == EnemyTier.Elite
                             || data.tier == EnemyTier.MidBoss
                             || data.enemyId == "melee_brute";

                var hit = data.tier == EnemyTier.Boss ? bossHit : heavy ? heavyHit : normalHit;
                var death = data.tier == EnemyTier.Boss ? bossDeath : heavy ? heavyDeath : normalDeath;

                if (data.hitSfx == hit && data.deathSfx == death) continue;
                data.hitSfx = hit;
                data.deathSfx = death;
                EditorUtility.SetDirty(data);
                linked += 1;
            }

            if (linked > 0) AssetDatabase.SaveAssets();
            Debug.Log($"[ContentBuilder] 적 효과음 연결: {linked}종 갱신.");
        }

        /// <summary>Assets/Audio/SFX/{name}.wav 로드. 없으면 null(런타임이 무음 가드).</summary>
        private static AudioClip LoadSfx(string name)
            => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AbyssPaths.Sfx}/{name}.wav");
    }
}
#endif
