using System;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>첫 플레이 튜토리얼이 확인하는 플레이어 행동. 폼 교체·드래프트는 전역 이벤트로 받는다.</summary>
    public enum PlayerTutorialAction
    {
        Move,
        Jump,
        Dash,
        Attack
    }

    /// <summary>
    /// 첫 플레이 튜토리얼용 행동 알림(인스턴스 로컬). 발행은 각 파트의 <b>성공 지점</b>에서만 한다 —
    /// 버튼을 눌렀다는 사실이 아니라 거부 조건(쿨다운·점프 횟수·가드)을 통과해 실제로 일어난 행동이다.
    ///
    /// 🔑 GameEvents 에 채널을 늘리지 않는다. 구독자는 튜토리얼 하나이고 플레이어를 이미 알고 있다.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // 「걸었다」로 칠 지상 이동 누적 거리(유닛). 방향키를 한 번 톡 친 것은 배운 것이 아니다.
        private const float TUTORIAL_MOVE_DISTANCE = 1.5f;

        // 한 물리 프레임에 이동 속도로 갈 수 있는 거리의 배수. 넘으면 텔레포트·밀려남으로 보고 세지 않는다.
        private const float TUTORIAL_TELEPORT_SLACK = 1.5f;

        /// <summary>성공한 행동 알림. 이 플레이어 인스턴스에서만 발행된다.</summary>
        public event Action<PlayerTutorialAction> OnTutorialAction;

        private float tutorialMoveDistance;
        private float tutorialLastBodyX;
        private bool hasTutorialLastBodyX;

        // 직전 PerformAttack 한 번에서 근접 판정을 실제로 돌렸거나 발사체를 실제로 쐈는가.
        // PerformAttack 첫머리에서 내리고 성공 지점(Combat 근접 판정 · Ranged Launch 직후)에서 올린다 —
        // PerformAttack 의 void 반환 계약은 그대로 둔다.
        private bool isTutorialAttackPerformed;

        private void RaiseTutorialAction(PlayerTutorialAction action) => OnTutorialAction?.Invoke(action);

        /// <summary>
        /// 입력 경로(OnAttack · OnAttackHeavy) 전용 — 방금 PerformAttack 이 실제로 판정·발사했을 때만 공격을 알린다.
        /// attackPoint 가 없거나 풀이 발사체를 못 내준 공격은 세지 않는다.
        /// </summary>
        private void RaiseTutorialAttackIfPerformed()
        {
            if (!isTutorialAttackPerformed) return;
            isTutorialAttackPerformed = false;
            RaiseTutorialAction(PlayerTutorialAction.Attack);
        }

        /// <summary>
        /// 입력으로 실제 지상 이동한 거리를 센다. FixedUpdateMovement 첫머리에서 호출 —
        /// 직전 물리 스텝의 위치 변화를 읽는다.
        ///
        /// 세지 않는 것: 공중(스폰 낙하 포함) · 가드 · 대시 · P04 접근 · 무입력 · 한 스텝 상한을 넘는 순간이동.
        /// </summary>
        private void TrackTutorialGroundMove()
        {
            if (OnTutorialAction == null || body == null)
            {
                hasTutorialLastBodyX = false;
                return;
            }

            float x = body.position.x;
            float delta = hasTutorialLastBodyX ? Mathf.Abs(x - tutorialLastBodyX) : 0f;
            tutorialLastBodyX = x;
            hasTutorialLastBodyX = true;

            if (!isGrounded || isGuarding || IsDashing || isLunging) return;
            if (Mathf.Abs(moveInput.x) <= 0.01f) return;

            float maxStep = EffectiveMoveSpeed * Time.fixedDeltaTime * TUTORIAL_TELEPORT_SLACK;
            if (delta <= 0f || delta > maxStep) return;

            tutorialMoveDistance += delta;
            if (tutorialMoveDistance < TUTORIAL_MOVE_DISTANCE) return;

            tutorialMoveDistance = 0f;
            RaiseTutorialAction(PlayerTutorialAction.Move);
        }
    }
}
