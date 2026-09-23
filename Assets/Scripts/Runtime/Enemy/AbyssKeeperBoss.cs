using Abyss.Runtime.Feedback;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// Stage1 첫 보스 「심연의 수호자」(boss_abyss_keeper) 전용 — <b>예고 → 발동 → 회복</b>을 보이게 한다(D2 · 총괄 C2 결정 2~5).
    ///
    /// 🔴 <b>왜 파생 클래스인가</b> — 예고 규칙을 <see cref="BossEnemy"/> 나 <see cref="EnemyBase"/> 에 넣으면
    /// 감시자·파수관·뱀·왕좌까지 같이 바뀐다. 결정 4가 「첫 보스 한정」이라 공용 쪽에는 <b>기본값이 옛 동작인 훅</b>만 두고
    /// 규칙은 전부 여기 있다(다른 보스 셋이 파생 클래스로 고유 패턴을 갖는 것과 같은 구조).
    ///
    /// <list type="bullet">
    /// <item><b>근접</b> — 시간은 EnemyData(<c>attackWindup 0.7 · attackRecovery 0.5</c>)가 갖는다. 여기는 표현만:
    /// 예고 시작에 붉은 틴트 + 발밑 반경 <c>attackRange</c> 예고 링 + <c>boss_telegraph</c>, 타격 시각에 링 확장 + 플래시.
    /// 링 반경 = 타격 판정 반경(<c>EnemyBase.Strike</c> 가 같은 <c>attackRange</c> 로 다시 잰다) — 예고와 실제 범위가 같다.</item>
    /// <item><b>탄막</b> — 일정은 <see cref="AbyssKeeperVolleySchedule"/>. 예고는 차가운 흰 틴트 + 스케일 펀치 + 같은 소리를 낮게.
    /// <b>링을 쓰지 않는다</b> — 링은 근접이라는 약속을 지킨다(D2 §2-2).</item>
    /// <item><b>겹침 금지</b> — 근접 예비동작 중엔 탄막 예고를 미루고, 탄막 예고 중엔 근접 상태에 안 들어간다.</item>
    /// <item><b>경직</b> — 공격 동작(예비동작+회복) 중 경직을 버린다. 안 그러면 1.8초에 한 번 이상 때리는 것만으로
    /// 근접이 영원히 안 나간다(D2 §1-3). 공격 밖 경직·피해·HP 는 그대로다.</item>
    /// </list>
    ///
    /// 📌 피해·HP·쿨다운·볼리 발 수·주기는 건드리지 않는다. 볼리 직렬값은 <see cref="BossEnemy"/> 쪽 필드가 SoT.
    /// </summary>
    public sealed class AbyssKeeperBoss : BossEnemy
    {
        [Header("첫 보스 예고 (C2 · 기존 boss_telegraph 재사용)")]
        [Tooltip("근접·탄막 예고 시작에 재생. 기존 boss_telegraph.wav — 신규 음원 아님")]
        [SerializeField] private AudioClip telegraphSfx;
        [Tooltip("탄막 예고음 볼륨 배율. 근접(1.0)보다 낮게 둬 소리로도 둘을 가른다(D2 §2-2 제안 0.6)")]
        [SerializeField, Range(0f, 1f)] private float volleyTelegraphVolume = 0.6f;
        [Tooltip("탄막 예고 스케일 펀치 크기 — 부풀었다 쏜다")]
        [SerializeField, Min(0f)] private float volleyPunchMagnitude = 0.12f;

        // 근접 예고 = 붉은 경고(감시자와 같은 어휘). 탄막 예고 = 차가운 흰빛 — 둘이 색으로 갈려야 한다.
        public static readonly Color MeleeTelegraphColor = new Color(1f, 0.3f, 0.3f);
        public static readonly Color VolleyTelegraphColor = new Color(0.8f, 0.92f, 1f);

        private readonly AbyssKeeperVolleySchedule volley = new();

        /// <summary>탄막 예고 중인가(검증·디버그 표시용).</summary>
        public bool IsVolleyTelegraphing => volley.IsTelegraphing;

        /// <summary>근접 예비동작 중인가(검증·디버그 표시용).</summary>
        public bool IsMeleeTelegraphing => IsAttackWindupActive;

        protected override bool ResistsStaggerWhileAttacking => true;

        protected override bool CanBeginAttack() => !volley.IsTelegraphing;

        protected override void OnAttackWindupStarted(float windup)
        {
            if (Data == null) return;

            PlaySfx(telegraphSfx);
            TintVisual(MeleeTelegraphColor, windup);
            SpawnAreaEffect(Data.attackRange, MeleeTelegraphColor, windup, BossAreaEffect.Mode.Telegraph);
        }

        protected override void OnAttackStrike()
        {
            if (Data == null) return;

            FlashVisual();
            SpawnAreaEffect(Data.attackRange, MeleeTelegraphColor);
        }

        /// <summary>기본 볼리(<c>BossEnemy.TryFireVolley</c>) 대신 예고가 붙은 볼리를 돈다. 발 수·확산·주기는 같은 값.</summary>
        protected override void TickPattern()
        {
            if (IsDead || Target == null || Data == null || Data.projectilePrefab == null)
            {
                volley.Cancel();
                return;
            }

            float distance = Vector2.Distance(transform.position, Target.position);
            bool isInRange = distance <= Data.detectionRange;

            var step = volley.Tick(Time.time, CurrentPhase, VolleyInterval, isInRange, IsAttackWindupActive);
            switch (step)
            {
                case AbyssKeeperVolleySchedule.Step.BeginTelegraph:
                    float telegraph = volley.FireTime - Time.time;
                    PlaySfx(telegraphSfx, volleyTelegraphVolume);
                    TintVisual(VolleyTelegraphColor, telegraph);
                    PunchVisual(volleyPunchMagnitude, telegraph);
                    break;

                case AbyssKeeperVolleySchedule.Step.Fire:
                    FireFan(CurrentVolleyCount, VolleySpreadAngle);
                    break;
            }
        }
    }
}
