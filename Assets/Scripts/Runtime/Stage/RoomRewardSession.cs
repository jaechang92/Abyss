using UnityEngine;

namespace Abyss.Runtime.Stage
{
    public enum RoomRewardKind { Form, Weapon }

    /// <summary>방·제단·제시 세대를 함께 고정한 보상 소유권. 전역 완료 신호는 이 세션을 해결하지 않는다.</summary>
    public sealed class RoomRewardSession
    {
        public int EntryVersion { get; }
        public RoomData Room { get; }
        public RoomRewardKind Kind { get; }
        public MonoBehaviour Altar { get; }
        public int OfferGeneration { get; private set; }
        public bool IsResolved { get; private set; }
        public bool IsInvalidated { get; private set; }

        public RoomRewardSession(int entryVersion, RoomData room, RoomRewardKind kind, MonoBehaviour altar)
        {
            EntryVersion = entryVersion;
            Room = room;
            Kind = kind;
            Altar = altar;
        }

        public void BindOffer(int generation) => OfferGeneration = generation;

        public bool TryResolve()
        {
            if (IsInvalidated || IsResolved) return false;
            IsResolved = true;
            return true;
        }

        public void Invalidate() => IsInvalidated = true;
    }
}
