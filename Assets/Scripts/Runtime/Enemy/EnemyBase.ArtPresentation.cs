namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// 적 atlas 연결 이음매(A1 · 2026-10-09). Awake 에서 <see cref="EnemyAtlasPresenter"/> 를 붙이고,
    /// 보스 패턴이 시작·타격·끝 경계를 알리는 창구를 준다. atlas 가 없는 적은 전부 무동작 — 옛 그림·동작 그대로다.
    /// 📌 판정·피해·타이밍은 여기서 아무것도 바꾸지 않는다. 그림만 따라간다.
    /// </summary>
    public partial class EnemyBase
    {
        private EnemyAtlasPresenter atlasPresenter;

        /// <summary>자기 enemyId 의 전용 atlas 로 그려지는가. 참이면 변종 몸 색(빌린 그림 구별용)을 입히지 않는다.</summary>
        private bool UsesDedicatedAtlasArt => atlasPresenter != null;

        private void AttachAtlasPresenter()
        {
            if (atlasPresenter != null) return;
            atlasPresenter = EnemyAtlasPresenter.TryAttach(this, data);
        }

        /// <summary>패턴 시작(예고 시작). <paramref name="preparation"/> 은 실제 예고 대기 시간 — 0 이면 즉발 패턴.</summary>
        protected void BeginPatternPresentation(string rowId, float preparation)
        {
            if (atlasPresenter == null) return;
            FaceTargetForPresentation();
            atlasPresenter.BeginPattern(rowId, preparation);
        }

        /// <summary>패턴의 실제 타격 시각(판정 직전). 순간이동 뒤라면 새 자리에서 대상을 본다.</summary>
        protected void StrikePatternPresentation()
        {
            if (atlasPresenter == null) return;
            FaceTargetForPresentation();
            atlasPresenter.StrikePattern();
        }

        /// <summary>패턴 종료·취소. 진행 중인 패턴이 없으면 무동작이라 매 프레임 불려도 된다.</summary>
        protected void EndPatternPresentation()
        {
            if (atlasPresenter == null) return;
            atlasPresenter.EndPattern();
        }

        private void FaceTargetForPresentation()
        {
            if (target == null || visuals == null) return;
            visuals.Face(target.position.x - transform.position.x);
        }
    }
}
