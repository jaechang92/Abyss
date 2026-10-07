using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Player;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>첫 방 지면 접촉만 표현한다. 낙하·이동·적·카메라·입력을 연출을 위해 바꾸지 않는다.</summary>
    [DefaultExecutionOrder(60)]
    public sealed class RunArrivalPresenter : MonoBehaviour
    {
        private PlayerCharacter player;
        private bool hasFirstRoom;
        private bool finished;
        private int transitionVersion;
        private float waiting;

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoom;
            GameEvents.OnPlayerDead += Stop;
            GameEvents.OnRunEnded += Stop;
            GameEvents.OnRunAbandoned += Stop;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoom;
            GameEvents.OnPlayerDead -= Stop;
            GameEvents.OnRunEnded -= Stop;
            GameEvents.OnRunAbandoned -= Stop;
        }

        private void HandleRoom(RoomData room)
        {
            if (hasFirstRoom) { Stop(); return; }
            hasFirstRoom = true;
            transitionVersion = JourneyPresentation.CurrentTransitionVersion;
            player = FindAnyObjectByType<PlayerCharacter>();
        }

        private void LateUpdate()
        {
            if (finished || !hasFirstRoom) return;
            if (JourneyPresentation.CurrentTransitionVersion != transitionVersion ||
                (RunManager.HasInstance && !RunManager.Instance.IsRunActive)) { Stop(); return; }
            if (player == null) player = FindAnyObjectByType<PlayerCharacter>();
            if (player != null && player.IsDead) { Stop(); return; }
            if (Time.timeScale <= 0f || SaveStatusOverlay.IsCapturingInput) return;
            waiting += Time.deltaTime;
            if (waiting >= 3f) { Stop(); return; }
            if (player == null || !player.IsGrounded || player.Velocity.y > 0.1f) return;

            // 초기 페이드 중에도 실제 접지에 맞춘다. 이후 로드 요청은 위의 전환 번호로 중단한다.
            bool first = !JourneyPresentation.HasSeenArrival;
            ArrivalDustFx.Play(player.transform.position, first ? 0.6f : 0.3f, transitionVersion, withSound: true);
            JourneyPresentation.MarkArrivalSeen();
            Stop();
        }

        private void Stop()
        {
            finished = true;
            Destroy(gameObject);
        }
    }
}
