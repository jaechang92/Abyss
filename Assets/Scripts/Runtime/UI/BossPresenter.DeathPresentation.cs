using Abyss.Runtime.Feedback;
using Abyss.Runtime.Stage;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    public sealed partial class BossPresenter
    {
        /// <summary>특정 보스·방 세대의 실제 연출 완료. 새 보스의 ticket으로 이전 방을 완료하지 않는다.</summary>
        public sealed class DeathPresentationTicket
        {
            internal StageDirector Director;
            internal RoomData Room;
            internal int RoomGeneration;
            public int BossInstanceId { get; internal set; }
            public bool IsCompleted { get; internal set; }
            public bool IsCancelled { get; internal set; }
            public bool IsPresenterDeactivated { get; internal set; }
        }

        private StageDirector deathDirector;
        private RoomData deathRoom;
        private int deathRoomGeneration;
        private int deathBossInstanceId;
        private DeathPresentationTicket deathTicket;
        private KeeperEncounterFx deathAftermath;

        private void CaptureDeathOwner()
        {
            CancelDeathPresentation();
            deathDirector = FindAnyObjectByType<StageDirector>();
            deathRoom = deathDirector != null ? deathDirector.CurrentRoom : null;
            deathRoomGeneration = deathDirector != null ? deathDirector.RoomGeneration : -1;
            deathBossInstanceId = boss != null ? boss.GetEntityId().GetHashCode() : 0;
            deathAftermath = null;
        }

        private void BeginDeathPresentation()
        {
            deathTicket = new DeathPresentationTicket
            {
                Director = deathDirector, Room = deathRoom, RoomGeneration = deathRoomGeneration,
                BossInstanceId = deathBossInstanceId,
                IsCancelled = !isActiveAndEnabled,
                IsPresenterDeactivated = !isActiveAndEnabled
            };
        }

        public static DeathPresentationTicket CaptureDeathPresentation(StageDirector director, RoomData room, int generation)
        {
            var ticket = instance != null ? instance.deathTicket : null;
            return ticket != null && ticket.Director == director && ticket.Room == room && ticket.RoomGeneration == generation
                ? ticket : null;
        }

        private void TickDeathPresentation()
        {
            var ticket = deathTicket;
            if (ticket == null || ticket.IsCancelled || ticket.IsCompleted) return;
            if (ticket.Director == null || !ticket.Director.OwnsRoomGeneration(ticket.Room, ticket.RoomGeneration))
            {
                ticket.IsCancelled = true;
                return;
            }
            // 값은 연출 소유자의 실제 상태다. StageDirector에는 연출 시간 상수를 복제하지 않는다.
            if (barHideAt >= 0f || subtitleShownAt >= 0f || flashStartedAt >= 0f || deathAftermath != null) return;
            if (HitstopController.HasInstance && HitstopController.Instance.IsPresentationActive) return;
            ticket.IsCompleted = true;
        }

        private void CancelDeathPresentation()
        {
            // 정상 완료는 과거 사실이다. 완료 뒤 disable로 이미 연 보상/귀환을 소급 잠그지 않는다.
            if (deathTicket == null || deathTicket.IsCompleted || deathTicket.IsCancelled) return;
            deathTicket.IsPresenterDeactivated = !isActiveAndEnabled;
            deathTicket.IsCancelled = true;
        }

        private void CancelDeathForRoom(RoomData room) => CancelDeathPresentation();

        private void SubscribeDeathPresentation(bool subscribe)
        {
            if (subscribe)
            {
                Events.GameEvents.OnRoomEntered += CancelDeathForRoom;
                Events.GameEvents.OnRunEnded += CancelDeathPresentation;
                Events.GameEvents.OnRunAbandoned += CancelDeathPresentation;
                Events.GameEvents.OnPlayerDead += CancelDeathPresentation;
                return;
            }
            Events.GameEvents.OnRoomEntered -= CancelDeathForRoom;
            Events.GameEvents.OnRunEnded -= CancelDeathPresentation;
            Events.GameEvents.OnRunAbandoned -= CancelDeathPresentation;
            Events.GameEvents.OnPlayerDead -= CancelDeathPresentation;
        }
    }
}
