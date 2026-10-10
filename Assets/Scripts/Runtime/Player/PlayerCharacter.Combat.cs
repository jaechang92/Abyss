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
        /// <summary>
        /// 장착한 무기가 주는 공격 배율. 무기가 없으면 1.
        ///
        /// 🔑 <b>기존 배율 둘과 같은 모양으로 곱한다</b>(<c>AttackMultiplier</c> 버프 ·
        /// <c>MetaAttackMult</c> 메타). 셋을 더하기로 섞으면 어느 층이 얼마를 줬는지
        /// 화면에서 못 읽는다 — 곱셈이면 층이 독립이다.
        ///
        /// ⚠️ 강화 단계는 <c>WeaponData.MultiplierAt</c> 이 계산한다. 식을 여기 복제하지 않는다.
        /// </summary>
        public float WeaponAttackMult { get; private set; } = 1f;

        /// <summary>
        /// 배율을 준 무기와 그 강화 단계. <b>전투가 실제로 쓰는 쪽</b>이다(스탯 창 조회용).
        /// 손에 그려진 무기는 <c>WeaponSocket.EquippedWeapon</c> 이 따로 갖는다 — 둘이 갈리면 결함이다.
        /// </summary>
        public Weapon.WeaponData CurrentWeapon { get; private set; }
        public int WeaponUpgradeLevel { get; private set; }

        /// <summary>무기를 갈아 끼운다. 강화 단계가 오르면 같은 무기로 다시 부른다.</summary>
        public void SetWeapon(Weapon.WeaponData weapon, int upgradeLevel = 0)
        {
            CurrentWeapon = weapon;
            WeaponUpgradeLevel = weapon != null ? upgradeLevel : 0;
            WeaponAttackMult = weapon != null ? weapon.MultiplierAt(upgradeLevel) : 1f;
        }

        /// <summary>
        /// 공격 배율 세 층(버프 · 메타 · 무기)의 곱.
        ///
        /// 🔑 <b>대미지 식을 여기 한 곳에 둔다.</b> 공격 입력과 스탯 창이 각자 곱하면
        /// 층을 하나 더할 때 한쪽만 고쳐져 <b>창에 보이는 값과 실제로 들어가는 값이 갈린다</b> — 오류가 안 난다.
        /// ⚠️ 적중 시점에만 붙는 것(심연 충전 2배 · 신중한 시선 치명타)은 여기 없다. 헛스윙에는 안 붙기 때문이다.
        /// 최후의 일격(피의 서약 시너지, 저HP 2배)은 휘두르는 순간의 HP로 정해지므로 이 곱의 한 층이다.
        /// </summary>
        public float TotalAttackMult => AttackMultiplier * MetaAttackMult * WeaponAttackMult * FinalStandAttackMult;

        /// <summary>
        /// 실제 기본 공격 쿨다운 배율 — 버프(시간 왜곡)와 피의 분노의 곱. 두 층은 서로 독립이다.
        /// </summary>
        private float AttackCooldownScale => BuffAttackCooldownMultiplier * BloodRageCooldownMultiplier;

        public int BaseLightAttackDamage => lightAttackDamage;
        public int BaseHeavyAttackDamage => heavyAttackDamage;
        public int LightAttackDamage => Mathf.RoundToInt(lightAttackDamage * TotalAttackMult);
        public int HeavyAttackDamage => Mathf.RoundToInt(heavyAttackDamage * TotalAttackMult);
        public float AttackCooldownLight => attackCooldownLight;
        public float AttackCooldownHeavy => attackCooldownHeavy;

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

        public bool CanAttackLight => Time.time >= lastAttackLightTime + attackCooldownLight * AttackCooldownScale;
        public bool CanAttackHeavy => Time.time >= lastAttackHeavyTime + attackCooldownHeavy * AttackCooldownScale;

        private void OnAttack(InputValue value)
        {
            if (!value.isPressed) return;
            if (isGuarding) return;  // 방패를 든 채로는 약공격을 안 한다
            if (!CanAttackLight) return;

            lastAttackLightTime = Time.time;
            stateMachine?.TriggerAttackLight();
            PerformAttack(LightAttackDamage, lightHitstop, lightShake, isHeavy: false);
            // 입력 경로에서만, 실제 판정·발사가 일어났을 때만 알린다 — 가드 자동 반격은 PerformAttack 을 직접 불러 여기를 지나지 않는다.
            RaiseTutorialAttackIfPerformed();
        }

        private void OnAttackHeavy(InputValue value)
        {
            if (!value.isPressed) return;
            // 가드 폼(방패병)의 X 는 가드다 — 강공격은 가드 성공 시 자동 반격으로만 나간다(PlayerCharacter.Guard).
            if (UsesGuard) return;
            if (!CanAttackHeavy) return;

            lastAttackHeavyTime = Time.time;
            stateMachine?.TriggerAttackHeavy();
            PerformAttack(HeavyAttackDamage, heavyHitstop, heavyShake, isHeavy: true);
            RaiseTutorialAttackIfPerformed();
        }

        /// <summary>
        /// 공격 판정 + 시각·촉각 피드백.
        /// attackPoint 기준 OverlapBox 로 적 수집, 중복 히트 방지 후 TakeDamage.
        /// 최소 1히트 시 히트스탑·쉐이크 발생.
        ///
        /// 원거리 폼(<see cref="Form.FormAttackStyle.Ranged"/>)은 박스 대신 발사체를 쏜다(<c>PlayerCharacter.Ranged</c>).
        /// </summary>
        private void PerformAttack(int damage, float hitstop, Vector2 shake, bool isHeavy)
        {
            CancelLunge("공격");  // P04 B — 공격(자동 반격 포함)이 접근을 끊는다
            isTutorialAttackPerformed = false;

            // 본체 sprite tint flash — AttackEffect는 옆에 표시되는 검기, 본체 flash는 캐릭터 자체가 공격함을 인지시킴.
            TriggerAttackFlash(isHeavy ? heavyFlashColor : lightFlashColor,
                               isHeavy ? heavyFlashDuration : lightFlashDuration);

            // 🔴 원거리는 박스 이펙트를 켜기 전에 갈린다 — 그 이펙트는 「판정 박스 크기를 그린다」가 계약이라
            // 원거리에서 켜면 코앞에 닿을 것 같은 그림이 다시 생긴다.
            if (TryPerformRangedAttack(damage, hitstop, shake, isHeavy)) return;

            // 이펙트 크기는 판정 박스에서 파생된다 - 화면이 사거리를 부풀리지 않게 값을 넘긴다.
            bool hasSlash = attackPoint != null && WorldArtFx.Play("slash-arc", attackPoint.position,
                attackBoxSize.y, isHeavy ? 0.24f : 0.16f, facingSign, transform, attackBoxSize.x);
            if (!hasSlash)
            {
                if (isHeavy) attackEffect?.PlayHeavy(attackBoxSize);
                else attackEffect?.PlayLight(attackBoxSize);
            }

            if (attackPoint == null) return;

            isTutorialAttackPerformed = true;  // 근접 판정을 실제로 돌렸다(빗나감 포함)
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
            // 다른 일시 색(플래시 · 가드 틴트)이 없을 때만 지금 색을 원래 색으로 잡는다 — 덮인 색을 원래 색으로 착각하지 않게.
            if (attackFlashTimer <= 0f && !isGuardTinted) CaptureBaseSpriteColor(sr);
            sr.color = color;
            attackFlashTimer = Mathf.Max(attackFlashTimer, duration);
        }

        /// <summary>
        /// 일시 색 효과가 끝나면 돌아갈 색을 <b>효과가 시작되는 순간</b> 잡는다.
        ///
        /// 🔴 예전에는 SpriteRenderer 를 처음 찾을 때 한 번만 잡았다. 그 시점이 폼 시각(<c>FormVisualApplier</c>, LateUpdate)이
        /// 색을 정하기 전이면 프리팹에 남은 옛 틴트가 영구히 「원래 색」이 된다. 폼 교체로 색이 바뀌어도 못 따라갔다.
        /// </summary>
        private void CaptureBaseSpriteColor(SpriteRenderer sr)
        {
            baseSpriteColor = sr.color;
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

            // 신중한 시선(무축) 치명타 — 같은 자리(적중 확정 뒤). 휘두름 한 번에 한 번 굴린다.
            int beforeCritical = damage;
            damage = RollKeenEye(damage);
            bool isCritical = damage > beforeCritical;

            // P04 C — 표식 소비도 적중 확정 뒤 · 피해 전. 피해 식은 바꾸지 않는다(경직만).
            TryConsumeRangedMark(reusableHitList);

            foreach (var enemy in reusableHitList)
            {
                int previousHp = enemy.CurrentHp;
                enemy.TakeDamage(damage);
                if (isCritical && enemy != null && enemy.CurrentHp < previousHp)
                    WorldArtFx.Play("critical-impact", enemy.transform.position, 1.25f, owner: transform);
            }

            int totalDamage = damage * reusableHitList.Count;
            ApplyMeleeLifeSteal(totalDamage);
            ApplyVampiricSeal(totalDamage);   // 흡혈 인장(피의 서약) — 폼 흡수와 별개 층
            return reusableHitList.Count;
        }

        /// <summary>
        /// 폼의 근접 흡수(<see cref="Form.FormData.meleeLifeSteal"/>, 암흑 검사). 적중이 확정된 뒤
        /// 실제로 준 피해 합계에서만 회복한다 — 헛스윙에는 없고, 여러 마리를 베면 그만큼 더 찬다.
        /// </summary>
        private void ApplyMeleeLifeSteal(int totalDamage)
        {
            Form.FormData form = formController != null ? formController.CurrentForm : null;
            if (form == null || form.meleeLifeSteal <= 0f) return;

            int amount = Mathf.RoundToInt(totalDamage * form.meleeLifeSteal);
            if (amount > 0) Heal(amount);
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
