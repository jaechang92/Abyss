using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 첫 보스 조우 대기(E1). 스폰 직후부터 소개가 끝날 때까지 <b>선공하지 않는다</b> —
    /// 근접 공격 진입(<see cref="CanBeginAttack"/>)·탄막 예고(<see cref="TickPattern"/>)·추적 이동(<see cref="FixedUpdate"/>)을 막는다.
    ///
    /// <list type="bullet">
    /// <item>피해·경직·HP·사망·보상 경로는 그대로다. 무적·방벽을 만들지 않는다 — 대기 중 맞아 죽으면 정상 처치로 끝난다.</item>
    /// <item>대기를 <b>쥐는 쪽</b>은 보스 연출(<c>BossPresenter</c>)이다. 스폰 이벤트 안에서 아무도 쥐지 않으면
    /// (연출이 다른 보스를 추적 중 등) 곧바로 풀어 옛 동작으로 돌아간다 — 영원히 서 있는 보스를 만들지 않는다.</item>
    /// <item>timeScale 0 에서는 Update를 막아 이미 도달한 공격 시각과 상태 전이도 실행하지 않는다.
    /// 대기는 그 밖(소개 전 자유 조작 구간)의 선공을 막는다.</item>
    /// </list>
    /// </summary>
    public sealed partial class AbyssKeeperBoss
    {
        private const float ENCOUNTER_CUE_VOLUME = 0.5f;

        private bool isHoldingForEncounter;
        private bool isEncounterClaimed;
        private SpriteRenderer bodyRenderer;

        /// <summary>소개 전 대기 중인가. 죽었으면 거짓.</summary>
        public bool IsHoldingForEncounter => isHoldingForEncounter && !IsDead;

        /// <summary>보스 그림 렌더러(루트). 연출은 이것을 <b>복제</b>해 쓰고 원본 트랜스폼·콜라이더는 건드리지 않는다.</summary>
        public SpriteRenderer BodyRenderer
        {
            get
            {
                if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
                return bodyRenderer;
            }
        }

        protected override void Start()
        {
            isHoldingForEncounter = true;
            isEncounterClaimed = false;

            // base.Start 가 BossEnemy.Spawned 를 발행한다 — 연출이 그 안에서 TryClaimEncounter 로 대기를 쥔다.
            base.Start();

            if (!isEncounterClaimed) isHoldingForEncounter = false;
        }

        /// <summary>대기를 쥔다. 한 번만 성공한다. 쥔 쪽이 소개를 마치거나 중단할 때 <see cref="ReleaseEncounterHold"/> 를 부른다.</summary>
        public bool TryClaimEncounter()
        {
            if (!isHoldingForEncounter || isEncounterClaimed || IsDead) return false;
            isEncounterClaimed = true;
            return true;
        }

        /// <summary>대기를 푼다(멱등). 이후 공격·예고는 정상 규칙(예고부터)으로 시작한다.</summary>
        public void ReleaseEncounterHold()
        {
            isHoldingForEncounter = false;
        }

        /// <summary>조우 반응음 — 기존 예고음(boss_telegraph)을 낮게. 신규 음원 아님.</summary>
        public void PlayEncounterCue()
        {
            PlaySfx(telegraphSfx, ENCOUNTER_CUE_VOLUME);
        }

        protected override void Update()
        {
            if (Time.timeScale <= 0f) return;
            base.Update();
        }

        protected override void FixedUpdate()
        {
            // 대기 중에는 다가오지 않는다(수평 속도만 끊는다 — 낙하는 그대로).
            if (IsHoldingForEncounter && Body != null)
            {
                Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
                return;
            }
            base.FixedUpdate();
        }
    }
}
