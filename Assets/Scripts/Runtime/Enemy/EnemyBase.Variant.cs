using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 엘리트 변종(2026-10-06 · 광폭화 <see cref="EliteBerserkerEnemy"/> · 소환사 <see cref="EliteSummonerEnemy"/>)이
    /// 쓰는 파생 훅과 소환 졸개 규약. 기본값은 전부 옛 동작이다 — 기존 적은 아무것도 바뀌지 않는다.
    /// </summary>
    public partial class EnemyBase
    {
        /// <summary>추적 이동 속도 배율. 기본 1. 광폭화가 올린다.</summary>
        protected virtual float MoveSpeedMultiplier => 1f;

        /// <summary>공격 쿨다운 배율(&lt;1 = 더 자주). 기본 1. 광폭화가 내린다.</summary>
        protected virtual float AttackCooldownMultiplier => 1f;

        /// <summary>
        /// 사망 처리 직후·처치 이벤트 직전 1회. 기본 무동작. <see cref="Dismiss"/>로 거둘 때는 부르지 않는다.
        /// </summary>
        protected virtual void OnDying() { }

        /// <summary>다른 적이 소환한 졸개인가. 보상(경험치·골드·엘리트 보너스)이 없다.</summary>
        public bool IsSummoned { get; private set; }

        /// <summary>소환 직후 소환자가 부른다. 되돌릴 수 없다.</summary>
        public void MarkSummoned() => IsSummoned = true;

        /// <summary>몸 기본색 교체(변종의 상태 표현 — 광폭화 시 더 붉게).</summary>
        protected void SetVisualBaseColor(Color color)
        {
            visuals?.SetBaseColor(color);
        }

        /// <summary>
        /// 처치가 아니라 <b>거두기</b> — 소환자가 죽을 때 졸개를 함께 지운다. 보상·처치 이벤트·사망음이 없다.
        ///
        /// 🔴 반드시 <b>소환자의 처치 이벤트보다 먼저</b> 불러야 한다(<see cref="OnDying"/> 안). StageDirector는
        /// 처치 이벤트를 받을 때 <c>IsDead</c>인 적을 한꺼번에 목록에서 빼므로, 그 전에 죽은 표시만 해 두면
        /// 소환자의 처치 한 번으로 졸개까지 정리되고 방 클리어 판정이 한 번에 맞는다.
        /// </summary>
        public void Dismiss()
        {
            if (isDead) return;
            isDead = true;
            if (body != null) StopHorizontal();
            visuals?.Tint(new Color(0.35f, 0.3f, 0.45f, 1f), 1f);
            Destroy(gameObject, data != null ? data.deathLingerDuration : 0.3f);
        }
    }
}
