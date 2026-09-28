using System.Collections.Generic;
using Abyss.Runtime.Combat;
using Abyss.Runtime.Enemy;
using Abyss.Runtime.Feedback;
using Abyss.Runtime.Form;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// P04 <b>C 원거리 표식</b>. 원거리 기본 발사체의 첫 실제 적중이 적 1명에게 표식을 남기고(플레이어당 1명),
    /// <b>다른 폼</b>의 근접 기본 공격이 실제로 맞히면 표식을 1회 소비해 일반 적을 기존 경직 경로로 짧게 끊는다. 추가 피해 없음.
    ///
    /// 🔑 <b>발사 순간을 캡처한다</b>(<see cref="RangedMarkShot"/>): 발사자 · 발사한 폼 · 세대. 교체 뒤 늦게 맞아도
    /// 표식의 폼은 「쏜 폼」이다 — 현재 폼으로 오인하지 않는다. 세대가 바뀌었으면(새 방 · 새 런) 표식을 남기지 않는다.
    /// 🔴 보스 · 중간보스는 표식 대상이 아니다(<see cref="EnemyBase.CanReceiveComboMark"/>) — 경직 면역을 우회하지 않는다.
    /// 스킬 발사체 · 적 탄은 이 청취자를 거치지 않으므로 표식이 생기지 않는다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        private static readonly Color MarkColor = new(1f, 0.45f, 0.85f, 1f);
        private const float MARK_RING_RADIUS = 0.55f;
        private const float MARK_RING_DURATION = 0.2f;

        private EnemyBase markedEnemy;
        private FormData markSourceForm;
        private float markExpireTime;
        private int markEpoch;
        private FormComboLabel markLabel;

        /// <summary>지금 표식이 붙은 적(없으면 null). 디버그 · 표시 조회용.</summary>
        public EnemyBase MarkedEnemy => markedEnemy;

        private static bool IsRangedMarkOn
        {
            get { var cfg = ComboConfig; return cfg != null && cfg.isRangedMarkEnabled; }
        }

        /// <summary>표식이 설정돼 있는가 — 적이 파괴돼 Unity 가 null 로 보이는 경우도 「정리할 것이 있다」로 센다.</summary>
        private bool HasMarkSlot => !ReferenceEquals(markedEnemy, null);

        /// <summary>
        /// 원거리 발사 때 청취자를 고른다. 🔑 C 가 꺼져 있으면 기존 청취자를 <b>그대로</b> 돌려준다(기존 동작 · 할당 없음).
        /// 켜져 있으면 발사마다 작은 캡처 객체를 하나 만든다 — 발사 시점의 폼 · 세대를 발사체별로 들고 있어야 해서다.
        /// </summary>
        private IProjectileHitListener WrapRangedListenerForMark(IProjectileHitListener inner, FormData firingForm)
        {
            if (!IsRangedMarkOn) return inner;
            return new RangedMarkShot(this, inner, firingForm, comboEpoch);
        }

        /// <summary>발사체 첫 적중이 부른다. 검증을 통과하면 표식을 이 적으로 옮긴다(이전 표식은 해제).</summary>
        private void TryPlaceRangedMark(EnemyBase enemy, FormData sourceForm, int shotEpoch)
        {
            if (!IsRangedMarkOn) return;
            if (!isActiveAndEnabled || isDead)
            {
                LogCombo("C", "표식 안 남김 — 발사자 비활성/사망");
                return;
            }
            if (shotEpoch != comboEpoch)
            {
                LogCombo("C", "표식 안 남김 — 발사 뒤 룸/런이 바뀜");
                return;
            }
            if (enemy == null || enemy.IsDead) return;  // 직격으로 처치 — 남길 대상이 없다
            if (!enemy.CanReceiveComboMark)
            {
                LogCombo("C", $"표식 제외 — {DescribeEnemy(enemy)} (보스/중간보스/경직 저항)");
                return;
            }

            if (markedEnemy != enemy) ClearRangedMark(HasMarkSlot ? "다른 적에 새 표식" : null);

            float duration = ComboConfig.rangedMarkDuration;
            markedEnemy = enemy;
            markSourceForm = sourceForm;
            markExpireTime = Time.time + duration;
            markEpoch = comboEpoch;

            ShowMarkLabel(enemy);
            BossAreaEffect.Spawn(enemy.transform.position, MARK_RING_RADIUS, MarkColor, MARK_RING_DURATION);
            LogCombo("C", $"표식 {DescribeEnemy(enemy)} ← {(sourceForm != null ? sourceForm.formId : "?")} ({duration:F2}s)");
        }

        /// <summary>
        /// 근접 기본 공격의 적중이 확정된 직후(<c>CollectAndDamageEnemies</c>, 피해 적용 전) 부른다.
        /// 맞은 목록에 표식 대상이 있고 <b>표식을 남긴 폼과 다른 근접 폼</b>이면 1회 소비한다.
        /// 자동 반격도 같은 경로(강공격 판정)를 타므로 같은 규칙이 적용된다.
        /// 🔑 소비하면 표식을 <b>먼저</b> 비운다 — 여러 콜라이더 · 같은 프레임의 두 번째 판정이 다시 소비하지 못한다.
        /// </summary>
        private void TryConsumeRangedMark(List<EnemyBase> hits)
        {
            if (!HasMarkSlot || markedEnemy == null || !IsRangedMarkOn) return;
            if (!hits.Contains(markedEnemy)) return;
            if (markEpoch != comboEpoch)
            {
                ClearRangedMark("룸/런 변경");
                return;
            }

            FormData current = formController != null ? formController.CurrentForm : null;
            if (current == null || current.attackStyle != FormAttackStyle.Melee)
            {
                LogCombo("C", "소비 안 함 — 현재 폼이 근접 기본 공격 폼이 아님");
                return;
            }
            if (IsSameForm(current, markSourceForm))
            {
                LogCombo("C", "소비 안 함 — 표식을 남긴 폼과 같은 폼");
                return;
            }

            EnemyBase enemy = markedEnemy;
            string enemyName = DescribeEnemy(enemy);
            ClearRangedMark(null);

            float interrupt = ComboConfig.rangedMarkInterruptDuration;
            bool isInterrupted = enemy.RequestMarkInterrupt(interrupt);
            BossAreaEffect.Spawn(enemy.transform.position, MARK_RING_RADIUS * 1.6f, MarkColor, MARK_RING_DURATION);
            LogCombo("C", isInterrupted
                ? $"표식 소비 — {enemyName} 경직 최소 {interrupt:F2}s ({current.formId})"
                : $"표식 소비 실패 — {enemyName} 경직 대상 아님");
        }

        /// <summary>매 프레임 — 대상 제거 · 사망 · 비활성 · 세대 변경 · 만료 · 스위치 꺼짐이면 표식과 문구를 거둔다.</summary>
        private void UpdateRangedMark()
        {
            if (!HasMarkSlot) return;

            string reason = null;
            if (!IsRangedMarkOn) reason = "스위치 꺼짐";
            else if (markedEnemy == null) reason = "적 제거";
            else if (markedEnemy.IsDead) reason = "적 사망";
            else if (!markedEnemy.isActiveAndEnabled) reason = "적 비활성";
            else if (markEpoch != comboEpoch) reason = "룸/런 변경";
            else if (Time.time >= markExpireTime) reason = "만료";

            if (reason != null) ClearRangedMark(reason);
        }

        /// <summary>표식과 문구를 거둔다. <paramref name="reason"/> 이 null 이면 로그 없이 조용히.</summary>
        private void ClearRangedMark(string reason)
        {
            if (!HasMarkSlot) return;

            markedEnemy = null;
            markSourceForm = null;
            if (markLabel != null)
            {
                markLabel.SetTarget(null);
                markLabel.Hide();
            }

            if (reason != null) LogCombo("C", $"표식 해제 — {reason}");
        }

        private void ShowMarkLabel(EnemyBase enemy)
        {
            if (markLabel == null)
            {
                markLabel = FormComboLabel.Create("FormComboMarkLabel", enemy.transform,
                    ComputeComboLabelOffset(enemy.transform), COMBO_LABEL_CHARACTER_SIZE);
            }
            else
            {
                markLabel.SetTarget(enemy.transform, ComputeComboLabelOffset(enemy.transform));
            }
            markLabel.Show(Loc.Get(StringKey.Combo_Mark), MarkColor);
        }

        private static string DescribeEnemy(EnemyBase enemy)
        {
            if (enemy == null) return "(제거됨)";
            return enemy.Data != null ? enemy.Data.enemyId : enemy.name;
        }

        /// <summary>
        /// 발사 1회분 캡처. 기존 적중 청취자(심연 충전 · 히트스탑)는 <see cref="inner"/> 에 그대로 넘긴다.
        /// 🔑 플레이어가 파괴된 뒤 늦게 맞으면 <c>owner == null</c>(Unity null 비교)로 걸러진다.
        /// </summary>
        private sealed class RangedMarkShot : IProjectileHitListener, IProjectileEnemyHitListener
        {
            private readonly PlayerCharacter owner;
            private readonly IProjectileHitListener inner;
            private readonly FormData firingForm;
            private readonly int epoch;

            public RangedMarkShot(PlayerCharacter owner, IProjectileHitListener inner, FormData firingForm, int epoch)
            {
                this.owner = owner;
                this.inner = inner;
                this.firingForm = firingForm;
                this.epoch = epoch;
            }

            public int OnProjectileHit(int damage, bool isFirstHit)
            {
                return inner != null ? inner.OnProjectileHit(damage, isFirstHit) : damage;
            }

            public void OnProjectileEnemyHit(EnemyBase enemy, bool isFirstHit)
            {
                // 관통 후속 타격은 새 표식을 남기지 않는다.
                if (!isFirstHit || owner == null) return;
                owner.TryPlaceRangedMark(enemy, firingForm, epoch);
            }
        }
    }
}
