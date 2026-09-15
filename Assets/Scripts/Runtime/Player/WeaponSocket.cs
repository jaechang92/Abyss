using Anim.Core;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 몸 애니메이션 위에 무기를 얹는다. 몸 그림을 담는 <c>Visual</c>의 자식으로 둔다 —
    /// 앵커 좌표가 <b>스프라이트 피벗(발밑) 기준</b>이라 부모가 그 원점이어야 한다.
    ///
    /// 🔑 <b>좌우 반전을 여기서 안 한다.</b> 캐릭터 방향은 루트의 <c>localScale.x</c> 부호로
    /// 뒤집히므로(<c>PlayerCharacter.Movement</c>) 자식인 이 소켓이 위치·회전째 같이 뒤집힌다.
    /// 여기서 또 뒤집으면 두 번 뒤집혀 제자리로 돌아온다.
    ///
    /// 🔴 <b>앵커가 없으면 숨긴다.</b> 틀린 자리에 박힌 무기보다 없는 편이 낫고, 그래야
    /// 결손이 화면에서 바로 읽힌다. 애니메이션 폴백 사슬이 「클립이 없으면 정지 그림으로
    /// 물러난다」로 하는 것과 같은 태도다.
    ///
    /// 🔴 <b>폼보다 뒤에 돈다.</b> <c>FormVisualPresenter</c>도 <c>LateUpdate</c>라
    /// 순서를 안 정하면 <b>폼이 바뀐 프레임에 이전 폼 앵커로 한 번 그린다</b>.
    /// 실행 순서를 <see cref="Form.FormVisualPresenter.VisualOrder"/> 뒤로 못박아 막는다.
    ///
    /// 규약은 <c>Docs/technical/weapon-attachment.md</c> 에 있다.
    /// </summary>
    [DefaultExecutionOrder(SocketOrder)]
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WeaponSocket : MonoBehaviour
    {
        /// <summary>폼이 앵커를 꽂은 뒤에 무기를 놓는 차례. 폼보다 커야 한다.</summary>
        public const int SocketOrder = 40;

        [Header("배선 (비우면 자동으로 찾는다)")]
        [SerializeField] private SpriteRenderer weaponRenderer;

        [Tooltip("정렬 기준이 되는 몸 렌더러. 무기는 이보다 한 칸 앞이나 뒤에 놓인다.")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Tooltip("재생 진행도를 읽을 재생기.")]
        [SerializeField] private AnimatorDriver animatorDriver;

        [Header("에디터 미리보기")]
        [Tooltip("플레이 중이 아닐 때 쓸 앵커. 런타임에는 폼이 꽂아 주므로 비워도 된다.")]
        [SerializeField] private WeaponAnchorSet previewAnchors;

        [Tooltip("플레이 중이 아닐 때 볼 애니메이션. PlayerAnimationIds 의 이름.")]
        [SerializeField] private string previewAnimationId = PlayerAnimationIds.Idle;

        [Tooltip("그 애니메이션의 어디를 볼까. 0 = 첫 프레임, 1 = 끝.")]
        [SerializeField, Range(0f, 1f)] private float previewNormalizedTime;

        private IAnimationDriver driver;
        private WeaponAnchorSet anchors;
        private Sprite weapon;

        private void Awake()
        {
            if (weaponRenderer == null) weaponRenderer = GetComponent<SpriteRenderer>();
            if (animatorDriver == null) animatorDriver = GetComponentInParent<AnimatorDriver>(true);
            if (bodyRenderer == null && transform.parent != null)
            {
                bodyRenderer = transform.parent.GetComponent<SpriteRenderer>();
            }
            driver = animatorDriver;
        }

        /// <summary>
        /// 폼이 바뀌면 앵커 한 벌도 같이 바뀐다.
        /// <c>FormVisualApplier</c>가 스프라이트·애니메이션과 <b>한 진입점</b>에서 부른다.
        /// </summary>
        public void SetAnchors(WeaponAnchorSet set)
        {
            anchors = set;
        }

        /// <summary>
        /// 장착한 무기 그림. <b>스프라이트의 피벗이 곧 손이 쥐는 지점</b>이라
        /// (<c>weapon_grips.py</c> 규약) 여기서 따로 보정하지 않는다.
        /// </summary>
        public void SetWeapon(Sprite sprite)
        {
            weapon = sprite;
            if (weaponRenderer != null) weaponRenderer.sprite = sprite;
        }

        /// <summary>
        /// 🔴 <b><c>LateUpdate</c>여야 한다.</b> 한 번 재생 클립은 <c>AnimatorDriver.Play</c>가
        /// <c>Animator.Update(0f)</c>로 그 프레임 안에 반영하는데, 그건 게임 FSM이 도는
        /// <c>Update</c> 단계다. <c>Update</c>에서 읽으면 <b>전이 이전 값</b>을 읽어
        /// 무기만 한 프레임 뒤처진다 — 오류가 아니라 화면으로만 드러나는 종류다.
        /// </summary>
        private void LateUpdate()
        {
            if (weaponRenderer == null) return;

            Sprite active = ResolveWeapon();
            WeaponAnchorSet set = ResolveAnchors();
            ResolvePlayhead(out string animationId, out float normalizedTime);

            if (active == null || set == null || string.IsNullOrEmpty(animationId) ||
                !set.TrySample(animationId, normalizedTime, out WeaponAnchorFrame frame))
            {
                Hide();
                return;
            }

            weaponRenderer.enabled = true;
            weaponRenderer.sprite = active;
            transform.localPosition = frame.position;

            // 각도를 '값'으로 받아 여기서 실현한다. 각도별 그림으로 바꾸더라도
            // 바뀌는 곳은 이 줄이지 앵커 데이터가 아니다(weapon-attachment.md §3).
            transform.localRotation = Quaternion.Euler(0f, 0f, frame.angle);

            if (bodyRenderer != null)
            {
                weaponRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                weaponRenderer.sortingOrder = bodyRenderer.sortingOrder + (frame.isInFront ? 1 : -1);
            }
        }

        /// <summary>
        /// 🔑 <b>비어 있으면 렌더러에 물린 것을 쓴다.</b> 장비 시스템(<c>WeaponData</c>)이 아직 없어
        /// <see cref="SetWeapon"/>을 부르는 곳이 없다. 필드만 보면 항상 비어서
        /// <b>무기가 숨어 버린다</b> — 2026-09-14 에 실제로 그랬다.
        /// </summary>
        private Sprite ResolveWeapon()
        {
            if (weapon != null) return weapon;
            return weaponRenderer != null ? weaponRenderer.sprite : null;
        }

        /// <summary>런타임에는 폼이 꽂아 준 것, 에디터에서는 인스펙터에 물린 것.</summary>
        private WeaponAnchorSet ResolveAnchors()
        {
            return anchors != null ? anchors : previewAnchors;
        }

        /// <summary>
        /// 지금 어느 애니메이션의 어디를 그릴까.
        ///
        /// 🔴 <b>에디터에서는 재생기가 답을 못 준다</b> — 게임 FSM이 안 돌아
        /// <c>CurrentAnimationId</c>가 빈 문자열이다. 그대로 두면 앵커를 눈으로 맞출 수가 없어서
        /// (그게 이 값들을 다듬는 유일한 방법인데) 미리보기 값으로 물러난다.
        /// </summary>
        private void ResolvePlayhead(out string animationId, out float normalizedTime)
        {
            if (Application.isPlaying && driver != null &&
                !string.IsNullOrEmpty(driver.CurrentAnimationId))
            {
                animationId = driver.CurrentAnimationId;
                normalizedTime = driver.NormalizedTime;
                return;
            }

            animationId = previewAnimationId;
            normalizedTime = previewNormalizedTime;
        }

        private void Hide()
        {
            if (weaponRenderer != null) weaponRenderer.enabled = false;
        }
    }
}
