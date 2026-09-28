using Abyss.Runtime.Feedback;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 방 클리어 처치 연출(A2) — 맵 전투·엘리트 방에서 마지막 적을 잡아 <b>실제로 클리어될 때 1회</b>
    /// 짧은 슬로(<see cref="HitstopController.TriggerSlow"/> 재사용)와 CLEAR 표시(<see cref="UI.RoomClearBanner"/>).
    ///
    /// 1회 보장은 호출 자리(<see cref="HandleRoomCleared"/>의 <c>isRoomClearing</c> 가드)가 한다.
    /// 보스 방은 기존 처치 연출(BossPresenter 슬로·대사)이 있어 겹치지 않게 뺀다.
    /// 이벤트·상점·휴식·빈 방은 처치가 없으니(<see cref="mapKillCount"/> 0) 연출도 없다.
    ///
    /// 🔴 <b>스폰 실패가 한 번이라도 있었던 방은 연출하지 않는다</b>(<see cref="hasRoomSpawnFailure"/>).
    /// 처치가 있었더라도 그 방의 클리어는 「다 잡아서」가 아니라 「못 나온 적이 빠져서」일 수 있다 — 성공 연출은 거짓이 된다.
    /// 진행·보상은 기존 실패 폴백 그대로 이어진다(연출만 뺀다).
    /// 모달이 timeScale 0을 쥐고 있으면 TriggerSlow가 스스로 무시한다 — 정지 소유권을 침범하지 않는다.
    /// </summary>
    public sealed partial class StageDirector
    {
        private const float CLEAR_SLOW_SCALE = 0.35f;
        private const float CLEAR_SLOW_SECONDS = 0.18f;   // 실시간

        // 이 방에서 스폰 실패가 있었는가 — 최초 배치·예고 시작(유효성)·등장(프리팹 없음·EnemyBase 없음) 모두.
        // 새 방 진입(OpenRoomSession)에서만 지운다.
        private bool hasRoomSpawnFailure;

        private void MarkRoomSpawnFailure() => hasRoomSpawnFailure = true;

        private void PlayRoomClearFeedback(RoomData room)
        {
            if (room == null || !room.IsMapRoom || mapKillCount <= 0) return;
            if (room.roomType != RoomType.Combat && room.roomType != RoomType.Elite) return;
            if (room.IsEventRoom || room.IsShopRoom) return;
            if (hasRoomSpawnFailure)
            {
                Debug.Log($"[StageDirector] {room.roomId} 스폰 실패가 있었던 방 — 클리어 연출 생략");
                return;
            }

            if (HitstopController.HasInstance) HitstopController.Instance.TriggerSlow(CLEAR_SLOW_SCALE, CLEAR_SLOW_SECONDS);
            UI.RoomClearBanner.Show(Loc.Get(StringKey.Hud_RoomClear));
            Debug.Log($"[StageDirector] {room.roomId} 클리어 연출 — 처치 {mapKillCount}");
        }
    }
}
