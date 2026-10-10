using FSM.Core;
using UnityEngine;

namespace Abyss.Runtime.Enemy
{
    /// <summary>
    /// <b>적 atlas 재생기</b>(A1 · 2026-10-09). 제작 패키지의 투명 atlas 를 기존 적의 그림 렌더러에 직접 튼다.
    /// <see cref="EnemyBase"/> 의 Awake 가 붙인다(<see cref="TryAttach"/>) — 프리팹·씬·메뉴 수정 없이 모든 스폰 경로에 걸린다.
    ///
    /// <list type="bullet">
    /// <item><b>렌더러</b> — <see cref="EnemyVisuals"/> 와 같은 규칙(자신 또는 자식의 첫 SpriteRenderer)으로 찾는다.
    /// 색(피격·틴트)·좌우 반전·스케일 펀치는 계속 EnemyVisuals 가 맡고, 여기는 <c>sprite</c> 만 바꾼다.</item>
    /// <item><b>Animator 와 싸우지 않는다</b> — 같은 렌더러의 Animator 와 <see cref="EnemyAnimationBinder"/> 를 끈다.
    /// 그래도 스프라이트 쓰기는 LateUpdate 에서 한다(애니메이터 평가 뒤).</item>
    /// <item><b>시간</b> — 한 번 재생 행은 <see cref="EnemyData"/> 동작 시간에서 나온다. 공격 = 준비 프레임(0·1)이
    /// 예비동작 동안, 타격 프레임(2)이 타격 시각에, 회복 프레임(3)이 회복 동안. 피격 = 경직, 사망 = 사라지기까지 − 여운.
    /// 예비동작 0 인 적(옛 즉발)은 진입 순간이 타격이라 타격 프레임부터 회복(없으면 manifest fps)으로 튼다.
    /// 판정·피해·타이밍은 건드리지 않는다.</item>
    /// <item><b>보스 패턴</b> — 패턴 시작(예고)·타격·끝 경계에서 보스가 직접 부른다(<see cref="BeginPattern"/> 등).
    /// 우선순위: 사망 &gt; 패턴 &gt; 공격·피격 &gt; 이동.</item>
    /// <item><b>정지</b> — <c>Time.time</c> 기준이라 timeScale 0 에서 프레임도 멈춘다.</item>
    /// </list>
    ///
    /// 📌 atlas·색인이 없으면 붙지 않는다 — 기존 Animator 그림 그대로다(폴백).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAtlasPresenter : MonoBehaviour
    {
        /// <summary>준비 → 타격 → 회복 4프레임 행에서 타격 자세의 위치(제작 명세: ready · anticipation · action · recovery).</summary>
        private const int STRIKE_FRAME = 2;

        /// <summary>사망 행이 끝난 뒤 마지막 그림이 남는 시간. EnemyAnimationBuilder.DeadHoldSeconds 와 같은 값이다.</summary>
        private const float DEAD_HOLD_SECONDS = 0.2f;

        private const float MIN_DEAD_DURATION = 0.1f;

        private EnemyBase owner;
        private EnemyData data;
        private StateMachine fsm;
        private SpriteRenderer body;
        private EnemyAtlasSet set;
        private bool isInitialized;
        private bool isBound;

        // 지금 트는 행.
        private EnemyAtlasClip clip;
        private float startTime;
        private float strikeTime;
        private float prepDuration;
        private float postFrameDuration;
        private int split;
        private bool isDeadClip;

        // 패턴(보스 고유 동작). 활성 동안 FSM 변화는 기록만 한다.
        private bool isPatternActive;
        private bool isPatternEnded;

        // 마지막 FSM 상태 — 패턴이 끝나면 이것으로 돌아간다.
        private string fsmStateId;
        private float fsmStateStart;

        /// <summary>렌더러를 찾고 atlas 가 있으면 붙인다. 없으면 null — 기존 그림을 그대로 쓴다.</summary>
        public static EnemyAtlasPresenter TryAttach(EnemyBase owner, EnemyData data)
        {
            if (owner == null || data == null) return null;

            var renderer = owner.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null) return null;

            float localFootY = ResolveLocalFootY(owner.transform, renderer.transform, owner.GetComponent<BoxCollider2D>());
            var atlas = EnemyAtlasLibrary.TryGet(data.enemyId, localFootY);
            if (atlas == null) return null;

            var presenter = owner.gameObject.AddComponent<EnemyAtlasPresenter>();
            presenter.Initialize(owner, data, renderer, atlas);
            return presenter;
        }

        /// <summary>
        /// 콜라이더 바닥을 렌더러 로컬 좌표로. 그림 자식(Visual)은 이미 발밑에 있어 0, 루트 렌더러는 음수다.
        /// 콜라이더가 없으면 렌더러 원점을 발로 본다.
        /// </summary>
        private static float ResolveLocalFootY(Transform root, Transform rendererTransform, BoxCollider2D collider)
        {
            if (collider == null) return 0f;

            var footLocal = new Vector3(collider.offset.x, collider.offset.y - collider.size.y * 0.5f, 0f);
            Vector3 footWorld = root.TransformPoint(footLocal);
            return rendererTransform.InverseTransformPoint(footWorld).y;
        }

        private void Initialize(EnemyBase enemy, EnemyData enemyData, SpriteRenderer renderer, EnemyAtlasSet atlas)
        {
            owner = enemy;
            data = enemyData;
            body = renderer;
            set = atlas;
            fsm = enemy.GetComponent<StateMachine>();

            // 같은 렌더러를 쓰던 Animator 재생층을 끈다 — 두 재생기가 sprite 를 번갈아 쓰지 않게.
            var animator = renderer.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            var binder = enemy.GetComponent<EnemyAnimationBinder>();
            if (binder != null) binder.enabled = false;

            // 새 그림은 전부 오른쪽을 본다 — 옛 정지 그림 때문에 꺼 두었던 좌우 반전을 켠다(애니메이션 적과 같은 규약).
            var visuals = enemy.GetComponent<EnemyVisuals>();
            if (visuals != null) visuals.EnableFacing();

            isInitialized = true;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable()
        {
            if (isInitialized) Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>구독 + 처음부터 다시(풀에서 다시 꺼낸 경우에도 지난 생의 패턴·사망 상태를 들고 오지 않는다).</summary>
        private void Bind()
        {
            if (isBound) return;
            isBound = true;
            if (fsm != null) fsm.OnStateChanged += HandleStateChanged;

            float now = Time.time;
            isPatternActive = false;
            isPatternEnded = false;
            isDeadClip = false;
            clip = null; // 지난 생의 미완 행(타격 전 꺼진 패턴은 끝 시각이 없다)을 이어 붙잡지 않는다.
            fsmStateId = fsm != null ? fsm.CurrentStateId : null;
            fsmStateStart = now;

            if (owner != null && owner.IsDead) PlayDead(now);
            else PlayForFsm(fsmStateId, now, now);
            Apply(now);
        }

        private void Unbind()
        {
            if (!isBound) return;
            isBound = false;
            if (fsm != null) fsm.OnStateChanged -= HandleStateChanged;
            isPatternActive = false;
        }

        /// <summary>🔴 old == new 도 온다 — 경직 중 다시 맞으면 피격 행을 처음부터 다시 튼다.</summary>
        private void HandleStateChanged(string oldStateId, string newStateId)
        {
            float now = Time.time;
            fsmStateId = newStateId;
            fsmStateStart = now;

            if (isDeadClip) return;
            if (newStateId == EnemyStateIds.Dead)
            {
                PlayDead(now);
                return;
            }
            if (isPatternActive) return;

            PlayForFsm(newStateId, now, now);
        }

        // ───────────────────────────── 보스 패턴 경계

        /// <summary>
        /// 패턴 시작(예고). 준비 프레임(0·1)을 <paramref name="preparation"/> 동안 틀고 <see cref="StrikePattern"/> 까지
        /// 준비 자세를 유지한다. 0 이면(즉발 패턴) 같은 프레임의 Strike 가 곧바로 타격 프레임으로 넘긴다.
        /// 행이 없으면 아무것도 안 한다(지금 그림 유지).
        /// </summary>
        public void BeginPattern(string rowId, float preparation)
        {
            if (!isInitialized || isDeadClip || owner == null || owner.IsDead) return;

            var row = set.Get(rowId);
            if (row == null) return;

            float now = Time.time;
            isPatternActive = true;
            isPatternEnded = false;
            Configure(row, now, Mathf.Min(STRIKE_FRAME, row.FrameCount - 1), Mathf.Max(0f, preparation),
                float.PositiveInfinity, 1f / row.FramesPerSecond);
            Apply(now);
        }

        /// <summary>패턴의 실제 타격 시각. 타격 프레임부터 회복까지 manifest fps 로 튼다. 연속 베기는 매번 부른다.</summary>
        public void StrikePattern()
        {
            if (!isPatternActive || isDeadClip || clip == null) return;

            float now = Time.time;
            split = Mathf.Min(STRIKE_FRAME, clip.FrameCount - 1);
            strikeTime = now;
            Apply(now);
        }

        /// <summary>
        /// 패턴 로직이 끝났다(정상 종료·취소). 타격했으면 회복 프레임을 마저 보여 주고, 타격 전 취소면 바로 FSM 그림으로 돌아간다.
        /// 진행 중인 패턴이 없으면 무동작(매 프레임 불려도 된다).
        /// </summary>
        public void EndPattern()
        {
            if (!isPatternActive) return;

            isPatternEnded = true;
            if (float.IsPositiveInfinity(strikeTime)) ReleasePattern(Time.time);
        }

        private void ReleasePattern(float now)
        {
            isPatternActive = false;
            isPatternEnded = false;
            if (isDeadClip) return;

            // 패턴 행을 비워야 이동 상태에서 「남은 한 번 재생」으로 붙잡지 않는다(타격 전 취소는 끝 시각이 없다).
            clip = null;
            PlayForFsm(fsmStateId, fsmStateStart, now);
        }

        // ───────────────────────────── FSM 상태 → 행

        private void PlayForFsm(string stateId, float stateStart, float now)
        {
            switch (stateId)
            {
                case EnemyStateIds.Attack:
                    PlayAttack(stateStart);
                    break;
                case EnemyStateIds.Stagger:
                    PlayHit(stateStart);
                    break;
                case EnemyStateIds.Dead:
                    PlayDead(now);
                    return;
                default:
                    // 순찰·추적 — 공격·피격 행이 아직 남았으면 끝까지 보여 준다(즉발 적은 상태가 한 판정뿐이다).
                    if (clip != null && !clip.IsLoop && !IsComplete(now)) return;
                    PlayMove(now);
                    return;
            }

            if (IsComplete(now)) PlayMove(now);
        }

        private void PlayMove(float now)
        {
            var row = set.Get(EnemyAtlasRowIds.Move);
            if (clip == row) return;
            Configure(row, now, 0, 0f, now, 1f / row.FramesPerSecond);
        }

        /// <summary>공격 — 준비 프레임은 예비동작 동안, 타격 프레임은 EnemyBase 가 때리는 시각(진입 + 예비동작)에.</summary>
        private void PlayAttack(float start)
        {
            var row = set.Get(EnemyAtlasRowIds.Attack);
            if (row == null)
            {
                PlayMove(start);
                return;
            }

            // 예비동작 0(즉발)은 진입 순간 이미 때렸다 — 준비 프레임 없이 타격 프레임부터 시작한다.
            float windup = data != null ? Mathf.Max(0f, data.attackWindup) : 0f;
            float recovery = data != null ? data.attackRecovery : 0f;
            int strikeFrame = Mathf.Min(STRIKE_FRAME, row.FrameCount - 1);
            int postFrames = row.FrameCount - strikeFrame;
            float post = recovery > 0f ? recovery / postFrames : 1f / row.FramesPerSecond;
            Configure(row, start, strikeFrame, windup, start + windup, post);
        }

        /// <summary>피격 — 경직 시간 동안 행 전체. 경직 0 인 적은 manifest fps.</summary>
        private void PlayHit(float start)
        {
            var row = set.Get(EnemyAtlasRowIds.Hit);
            if (row == null)
            {
                PlayMove(start);
                return;
            }

            float stagger = data != null ? data.staggerDuration : 0f;
            float duration = stagger > 0f ? stagger : row.FrameCount / row.FramesPerSecond;
            Configure(row, start, 0, 0f, start, duration / row.FrameCount);
        }

        /// <summary>사망 — 사라지기까지 − 여운 동안 행 전체, 이후 마지막 그림 유지. 패턴을 끊는다.</summary>
        private void PlayDead(float now)
        {
            isPatternActive = false;
            isPatternEnded = false;

            var row = set.Get(EnemyAtlasRowIds.Dead);
            if (row == null) return;

            float linger = data != null ? data.deathLingerDuration : 0.3f;
            float duration = Mathf.Max(MIN_DEAD_DURATION, linger - DEAD_HOLD_SECONDS);
            Configure(row, now, 0, 0f, now, duration / row.FrameCount);
            isDeadClip = true;
            Apply(now);
        }

        private void Configure(EnemyAtlasClip row, float start, int splitIndex, float preparation, float strike, float postDuration)
        {
            clip = row;
            startTime = start;
            split = splitIndex;
            prepDuration = preparation;
            strikeTime = strike;
            postFrameDuration = Mathf.Max(0.0001f, postDuration);
        }

        // ───────────────────────────── 재생

        private void LateUpdate()
        {
            if (!isInitialized || clip == null) return;

            float now = Time.time;
            if (!isDeadClip && owner != null && owner.IsDead) PlayDead(now);

            if (isPatternActive)
            {
                if (isPatternEnded && IsComplete(now)) ReleasePattern(now);
            }
            else if (!isDeadClip && !clip.IsLoop && IsComplete(now))
            {
                PlayMove(now);
            }

            Apply(now);
        }

        private void Apply(float now)
        {
            if (body == null || clip == null) return;

            var sprite = clip.Sprites[FrameAt(now)];
            if (body.sprite != sprite) body.sprite = sprite;
        }

        private int FrameAt(float now)
        {
            int count = clip.FrameCount;
            if (clip.IsLoop && !isPatternActive)
            {
                int index = Mathf.FloorToInt((now - startTime) * clip.FramesPerSecond);
                return ((index % count) + count) % count;
            }

            if (now < strikeTime)
            {
                if (split <= 0) return 0;
                if (prepDuration <= 0f) return split - 1;
                int prepIndex = Mathf.FloorToInt((now - startTime) / prepDuration * split);
                return Mathf.Clamp(prepIndex, 0, split - 1);
            }

            int postIndex = split + Mathf.FloorToInt((now - strikeTime) / postFrameDuration);
            return Mathf.Clamp(postIndex, 0, count - 1);
        }

        private bool IsComplete(float now)
        {
            if (clip == null) return true;
            if (clip.IsLoop && !isPatternActive) return false;
            return now >= strikeTime + (clip.FrameCount - split) * postFrameDuration;
        }
    }
}
