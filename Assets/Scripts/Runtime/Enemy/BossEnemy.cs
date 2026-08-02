using System;
using Abyss.Runtime.Audio;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Player;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// EnemyBase를 상속한 보스 기반 클래스. HP 임계값 기반 3페이즈 전환.
    /// 페이즈별 데미지 배율 + 부채꼴 탄막 볼리(발사체 재사용). 페이즈 상승 시 발사 수↑·주기↓.
    /// 처치 시 EnemyBase.Die가 RunManager.NotifyBossKilled() 호출 (EnemyData.isBoss=true).
    ///
    /// 보스별 고유 패턴은 <see cref="TickPattern"/>을 override해 구현한다(파생 클래스).
    /// 기본 동작은 부채꼴 탄막 볼리로, 별도 파생 없이도 동작한다(BossAbyssKeeper 하위호환).
    /// 공용 패턴 빌딩블록 <see cref="FireFan"/>(부채꼴 탄막)·<see cref="MeleeAreaStrike"/>(근접 광역)를
    /// protected로 제공해 파생 클래스가 조합한다.
    /// </summary>
    public class BossEnemy : EnemyBase
    {
        [Header("페이즈 전환 HP 비율")]
        [Range(0f, 1f), SerializeField] private float phase2HpThreshold = 0.66f;
        [Range(0f, 1f), SerializeField] private float phase3HpThreshold = 0.33f;

        [Header("페이즈별 보정")]
        [SerializeField] private float damageMultiplierPhase2 = 1.2f;
        [SerializeField] private float damageMultiplierPhase3 = 1.5f;

        [Header("페이즈 탄막 (부채꼴 볼리 — 발사체 재사용)")]
        [Tooltip("볼리 발사 주기(초). 실제 주기는 interval/currentPhase로 페이즈 상승 시 단축")]
        [SerializeField, Min(0.5f)] private float volleyInterval = 3f;
        [Tooltip("페이즈 1 기준 발사 수. 페이즈마다 +1발")]
        [SerializeField, Min(1)] private int baseVolleyCount = 3;
        [Tooltip("부채꼴 전체 확산 각도(도)")]
        [SerializeField, Min(0f)] private float spreadAngle = 40f;

        [Header("연출 — 페이즈 전환")]
        [Tooltip("페이즈 전환 시 재생할 효과음")]
        [SerializeField] private AudioClip phaseChangeSfx;
        [Tooltip("페이즈 전환 시 잠깐 입히는 강조 색")]
        [SerializeField] private Color phaseFlashColor = new Color(1f, 0.85f, 0.3f);

        private int currentPhase = 1;
        private float lastVolleyTime = -999f;
        private CameraShake cameraShake;

        public int CurrentPhase => currentPhase;
        public event Action<int> OnPhaseChanged;

        public float CurrentDamageMultiplier => currentPhase switch
        {
            1 => 1f,
            2 => damageMultiplierPhase2,
            3 => damageMultiplierPhase3,
            _ => 1f
        };

        protected override void Update()
        {
            base.Update();
            CheckPhaseTransition();
            TickPattern();
        }

        /// <summary>
        /// 매 프레임 호출되는 보스 고유 패턴 훅. 기본 구현은 부채꼴 탄막 볼리.
        /// 파생 보스는 이를 override해 회전베기·화염브레스 등 고유 패턴으로 교체한다.
        /// </summary>
        protected virtual void TickPattern()
        {
            TryFireVolley();
        }

        /// <summary>
        /// 타겟이 감지 범위 안일 때 주기적으로 부채꼴 탄막을 발사한다(근접 공격과 병행).
        /// 페이즈가 오를수록 주기가 짧아지고(interval/phase) 발사 수가 늘어난다.
        /// projectilePrefab 미연결 보스는 무동작(근접만 수행).
        /// </summary>
        private void TryFireVolley()
        {
            if (IsDead || Target == null || Data == null || Data.projectilePrefab == null) return;

            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > Data.detectionRange) return;

            float interval = volleyInterval / currentPhase;
            if (Time.time < lastVolleyTime + interval) return;
            lastVolleyTime = Time.time;

            FireFan(baseVolleyCount + (currentPhase - 1), spreadAngle);
        }

        /// <summary>
        /// 타겟 방향을 중심으로 count발을 spreadDeg 부채꼴로 발사하는 공용 헬퍼.
        /// 데미지·속도·수명은 SpawnProjectile이 EnemyData/페이즈 배율을 반영한다.
        /// projectilePrefab 미연결 시 SpawnProjectile이 무동작. (화염브레스·탄막 볼리 공용 빌딩블록)
        /// </summary>
        protected void FireFan(int count, float spreadDeg)
        {
            if (count <= 0 || Target == null) return;

            Vector2 toTarget = (Vector2)Target.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector2.right;
            float baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

            float start = count > 1 ? baseAngle - spreadDeg * 0.5f : baseAngle;
            float step = count > 1 ? spreadDeg / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angRad = (start + step * i) * Mathf.Deg2Rad;
                SpawnProjectile(new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad)));
            }
        }

        /// <summary>
        /// 보스 주변 radius 원형 범위 안의 플레이어에게 근접 광역 데미지를 1회 가한다(회전베기·꼬리치기 공용).
        /// 데미지는 GetAttackDamage()(페이즈 배율 반영) × damageMultiplier(패턴 고유 배율).
        /// 범위 밖이거나 타겟이 없으면 무동작. 타격 성공 시 true 반환.
        /// </summary>
        protected bool MeleeAreaStrike(float radius, float damageMultiplier = 1f)
        {
            if (Target == null) return false;

            float distance = Vector2.Distance(transform.position, Target.position);
            if (distance > radius) return false;

            var player = Target.GetComponent<PlayerCharacter>();
            if (player == null || player.IsDead) return false;

            int damage = Mathf.RoundToInt(GetAttackDamage() * Mathf.Max(0f, damageMultiplier));
            if (damage > 0) player.TakeDamage(damage);
            return true;
        }

        /// <summary>
        /// 보스 위치에 반경 radius 원형 링 이펙트를 띄운다(근접 광역 패턴의 타격 범위 시각화).
        /// </summary>
        protected void SpawnAreaEffect(float radius, Color color, float duration = 0.35f,
            BossAreaEffect.Mode mode = BossAreaEffect.Mode.Strike)
        {
            BossAreaEffect.Spawn(transform.position, radius, color, duration, mode);
        }

        /// <summary>
        /// 지정한 위치에 링 이펙트를 띄운다. 보스가 <b>지금 있지 않은 곳</b>을 예고해야 하는 패턴용
        /// (순간이동 착지 지점 등) — 제자리 패턴은 <see cref="SpawnAreaEffect"/>로 충분하다.
        /// </summary>
        protected void SpawnAreaEffectAt(Vector3 position, float radius, Color color, float duration = 0.35f,
            BossAreaEffect.Mode mode = BossAreaEffect.Mode.Strike)
        {
            BossAreaEffect.Spawn(position, radius, color, duration, mode);
        }

        /// <summary>
        /// 카메라 흔들림 트리거(강타 타격감). 씬의 CameraShake를 1회 탐색해 캐시한다.
        /// CameraShake가 없으면 무동작.
        /// </summary>
        protected void ShakeCamera(float magnitude, float duration)
        {
            if (cameraShake == null) cameraShake = FindAnyObjectByType<CameraShake>();
            if (cameraShake != null) cameraShake.Shake(magnitude, duration);
        }

        /// <summary>
        /// 효과음 재생(보스 패턴 연출 공용). 클립 미할당 또는 AudioManager 부재 시 무동작.
        /// AudioManager는 단일 AudioSource라 위치 기반은 아니나 보스전엔 충분.
        /// </summary>
        protected void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (AudioManager.HasInstance) AudioManager.Instance.PlaySfx(clip, volume);
        }

        protected override int GetAttackDamage()
        {
            int baseDamage = base.GetAttackDamage();
            return Mathf.RoundToInt(baseDamage * CurrentDamageMultiplier);
        }

        private void CheckPhaseTransition()
        {
            if (IsDead || Data == null || Data.baseHp <= 0) return;

            float ratio = (float)CurrentHp / Data.baseHp;

            int nextPhase = currentPhase;
            if (ratio <= phase3HpThreshold && currentPhase < 3) nextPhase = 3;
            else if (ratio <= phase2HpThreshold && currentPhase < 2) nextPhase = 2;

            if (nextPhase != currentPhase)
            {
                currentPhase = nextPhase;
                Debug.Log($"[Boss:{Data.enemyId}] Phase {currentPhase} 진입 (HP {ratio:P0})");

                // 페이즈 전환 연출: 효과음 + 스케일 펀치 + 강조 틴트 + 카메라 흔들림.
                PlaySfx(phaseChangeSfx);
                PunchVisual(0.18f, 0.3f);
                TintVisual(phaseFlashColor, 0.4f);
                ShakeCamera(0.25f, 0.3f);

                OnPhaseChanged?.Invoke(currentPhase);
            }
        }
    }
}
