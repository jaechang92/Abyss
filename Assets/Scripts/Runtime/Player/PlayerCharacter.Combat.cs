using System.Collections.Generic;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 전투 입력 훅 + 근접 공격 판정. 프로토 범위:
    /// Physics2D.OverlapBox(ContactFilter2D, 버퍼) 로 `attackPoint` 기준 박스 안 EnemyBase 감지 → TakeDamage.
    /// GAS Ability 본격 연결은 후속. 현재는 PlayerCharacter가 직접 데미지 로직 수행.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        [Header("공격 쿨다운")]
        [SerializeField, Min(0f)] private float attackCooldownLight = 0.3f;
        [SerializeField, Min(0f)] private float attackCooldownHeavy = 0.8f;

        [Header("공격 판정")]
        [Tooltip("공격 범위 중심이 될 자식 Transform (플레이어 앞쪽 배치)")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private Vector2 attackBoxSize = new(1.6f, 1.2f);
        [SerializeField, Min(0)] private int lightAttackDamage = 15;
        [SerializeField, Min(0)] private int heavyAttackDamage = 35;

        [Header("공격 피드백")]
        [SerializeField] private AttackEffect attackEffect;
        [SerializeField, Min(0f)] private float lightHitstop = 0.05f;
        [SerializeField, Min(0f)] private float heavyHitstop = 0.10f;
        [SerializeField] private Vector2 lightShake = new(0.12f, 0.1f);
        [SerializeField] private Vector2 heavyShake = new(0.25f, 0.2f);
        [SerializeField] private Color lightFlashColor = new(1f, 1f, 0.6f, 1f);
        [SerializeField] private Color heavyFlashColor = new(1f, 0.6f, 0.4f, 1f);
        [SerializeField, Min(0f)] private float lightFlashDuration = 0.12f;
        [SerializeField, Min(0f)] private float heavyFlashDuration = 0.20f;

        private float lastAttackLightTime = -999f;
        private float lastAttackHeavyTime = -999f;
        private CameraShake cachedCameraShake;
        private bool cameraShakeLookupAttempted;
        private SpriteRenderer cachedPlayerSr;
        private bool playerSrLookupAttempted;
        private Color baseSpriteColor = Color.white;
        private float attackFlashTimer;

        private static readonly List<EnemyBase> reusableHitList = new();

        // OverlapBoxAll의 매 호출 배열 할당(GC)을 피하기 위한 무할당 버퍼. 32개면 실전 동시 히트 수 충분.
        private static readonly Collider2D[] overlapBuffer = new Collider2D[32];
        // useTriggers를 매 호출 갱신해야 하므로 readonly 불가 (struct 필드 직접 대입).
        // Unity 6.6부터 인스턴스 메서드 NoFilter()는 deprecated — 정적 noFilter 프로퍼티를 쓴다.
        private static ContactFilter2D overlapFilter = ContactFilter2D.noFilter;

        public bool CanAttackLight => Time.time >= lastAttackLightTime + attackCooldownLight;
        public bool CanAttackHeavy => Time.time >= lastAttackHeavyTime + attackCooldownHeavy;

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackLight) return;

            lastAttackLightTime = Time.time;
            stateMachine?.TriggerAttackLight();
            PerformAttack(Mathf.RoundToInt(lightAttackDamage * AttackMultiplier * MetaAttackMult), lightHitstop, lightShake, isHeavy: false);
        }

        private void OnAttackHeavy(InputValue value)
        {
            if (!value.isPressed) return;
            if (!CanAttackHeavy) return;

            lastAttackHeavyTime = Time.time;
            stateMachine?.TriggerAttackHeavy();
            PerformAttack(Mathf.RoundToInt(heavyAttackDamage * AttackMultiplier * MetaAttackMult), heavyHitstop, heavyShake, isHeavy: true);
        }

        /// <summary>
        /// 공격 판정 + 시각·촉각 피드백.
        /// attackPoint 기준 OverlapBox 로 적 수집, 중복 히트 방지 후 TakeDamage.
        /// 최소 1히트 시 히트스탑·쉐이크 발생.
        /// </summary>
        private void PerformAttack(int damage, float hitstop, Vector2 shake, bool isHeavy)
        {
            if (isHeavy) attackEffect?.PlayHeavy();
            else attackEffect?.PlayLight();

            // 본체 sprite tint flash — AttackEffect는 옆에 표시되는 검기, 본체 flash는 캐릭터 자체가 공격함을 인지시킴.
            TriggerAttackFlash(isHeavy ? heavyFlashColor : lightFlashColor,
                               isHeavy ? heavyFlashDuration : lightFlashDuration);

            if (attackPoint == null) return;

            int hitCount = CollectAndDamageEnemies(damage);
            if (hitCount <= 0) return;

            if (HitstopController.HasInstance)
            {
                HitstopController.Instance.Trigger(hitstop);
            }
            TriggerShake(shake);
        }

        private void TriggerAttackFlash(Color color, float duration)
        {
            var sr = ResolvePlayerSr();
            if (sr == null) return;
            sr.color = color;
            attackFlashTimer = Mathf.Max(attackFlashTimer, duration);
        }

        /// <summary>
        /// PlayerCharacter.Update에서 매 프레임 호출. 타이머 만료 시 본체 색을 baseSpriteColor로 복귀.
        /// </summary>
        private void UpdateAttackFlash()
        {
            if (attackFlashTimer <= 0f) return;

            attackFlashTimer -= Time.unscaledDeltaTime;
            if (attackFlashTimer <= 0f)
            {
                var sr = ResolvePlayerSr();
                if (sr != null) sr.color = baseSpriteColor;
            }
        }

        private SpriteRenderer ResolvePlayerSr()
        {
            if (cachedPlayerSr != null) return cachedPlayerSr;
            if (playerSrLookupAttempted) return null;

            playerSrLookupAttempted = true;
            // 4-1에서 SpriteRenderer가 루트 -> Visual 자식으로 내려갔다(시각/물리 분리).
            // GetComponentInChildren은 자기 자신도 포함하므로 옛 프리팹(루트에 SR)도 그대로 찾는다.
            cachedPlayerSr = GetComponentInChildren<SpriteRenderer>();
            if (cachedPlayerSr != null) baseSpriteColor = cachedPlayerSr.color;
            return cachedPlayerSr;
        }

        private int CollectAndDamageEnemies(int damage)
        {
            // OverlapBoxAll과 동일하게 전역 트리거 감지 설정을 따르도록 매 호출 동기화(설정이 런타임에 바뀔 수 있음).
            overlapFilter.useTriggers = Physics2D.queriesHitTriggers;
            int count = Physics2D.OverlapBox(attackPoint.position, attackBoxSize, 0f, overlapFilter, overlapBuffer);
            reusableHitList.Clear();

            // count만큼만 순회 — 버퍼에 남은 이전 호출 잔존값은 무시. 버퍼 초과분(32개 이상 동시 히트)은 누락될 수 있음.
            for (int i = 0; i < count; i++)
            {
                var col = overlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;
                if (reusableHitList.Contains(enemy)) continue;
                reusableHitList.Add(enemy);
            }

            if (reusableHitList.Count == 0) return 0;

            // Passive '심연 충전'은 적중이 확정된 뒤에 적용·소비한다 — 헛스윙으로 장전이 풀리지 않게
            // 하기 위함(Passives 파트 참조). 수집이 끝난 이 지점이 "맞았다"가 확정되는 유일한 곳이다.
            damage = ConsumeAbyssCharge(damage);

            foreach (var enemy in reusableHitList)
            {
                enemy.TakeDamage(damage);
            }

            return reusableHitList.Count;
        }

        private void TriggerShake(Vector2 magDuration)
        {
            var shake = ResolveCameraShake();
            if (shake != null) shake.Shake(magDuration.x, magDuration.y);
        }

        private CameraShake ResolveCameraShake()
        {
            if (cachedCameraShake != null) return cachedCameraShake;
            if (cameraShakeLookupAttempted) return null;

            cameraShakeLookupAttempted = true;
            var mainCam = UnityEngine.Camera.main;
            cachedCameraShake = mainCam != null ? mainCam.GetComponent<CameraShake>() : null;
            if (cachedCameraShake == null)
            {
                cachedCameraShake = FindAnyObjectByType<CameraShake>();
            }
            return cachedCameraShake;
        }

        private void OnDrawGizmosSelectedCombat()
        {
            if (attackPoint == null) return;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(attackPoint.position, new Vector3(attackBoxSize.x, attackBoxSize.y, 0f));
        }
    }
}
