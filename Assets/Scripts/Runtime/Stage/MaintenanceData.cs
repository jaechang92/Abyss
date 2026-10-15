using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 정비 공간(R2 — 21-room-reward-flow §정비 공간) 한 곳의 정의. 상인과 불씨를 한 방에 세운다.
    ///
    /// 🔴 가격·재고·효과 원본을 복사하지 않는다 — 기존 <see cref="ShopData"/>와 휴식 <see cref="EventData"/>를 <b>참조</b>만 한다.
    /// 방의 <see cref="RoomData.shopData"/>·<see cref="RoomData.eventData"/>를 함께 채우는 대신 이 SO를 거치는 이유는
    /// 두 필드가 「참조 유무 = 방 종류」 판정이라 동시에 채우면 이벤트가 먼저 잡히기 때문이다.
    ///
    /// 지형은 새로 만들지 않고 <see cref="LayoutSourceRoom"/>(원래 상점 방)의 레이아웃을 빌린다(<see cref="RoomLayoutController"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "MaintenanceData", menuName = "Abyss/Data/Maintenance Data")]
    public sealed class MaintenanceData : ScriptableObject
    {
        [SerializeField] private string maintenanceId;

        [Tooltip("상인이 파는 진열대. 원본 상점 에셋을 그대로 참조한다(복사 금지).")]
        [SerializeField] private ShopData shopData;

        [Tooltip("불씨에서 고르는 휴식 이벤트. 원본 휴식 에셋을 그대로 참조한다(복사 금지).")]
        [SerializeField] private EventData restEventData;

        [Tooltip("지형을 빌릴 방. Run 씬 RoomLayoutController 에 이 방으로 등록된 레이아웃이 정비 공간에서 켜진다.")]
        [SerializeField] private RoomData layoutSourceRoom;

        [Tooltip("상인 x(맵 중심 0 기준). 높이는 그 x 의 실제 바닥이다.")]
        [SerializeField] private float merchantX = -3f;

        [Tooltip("불씨 x(맵 중심 0 기준). 상인과 상호작용 범위가 겹치지 않게 떨어뜨린다.")]
        [SerializeField] private float emberX = 3f;

        public string MaintenanceId => string.IsNullOrEmpty(maintenanceId) ? name : maintenanceId;
        public ShopData Shop => shopData;
        public EventData RestEvent => restEventData;
        public RoomData LayoutSourceRoom => layoutSourceRoom;
        public float MerchantX => merchantX;
        public float EmberX => emberX;

        /// <summary>상인과 불씨를 둘 다 세울 수 있는가. 아니면 정비 공간을 열지 않는다.</summary>
        public bool IsValid =>
            shopData != null && shopData.items != null && shopData.items.Count > 0 &&
            restEventData != null && restEventData.choices != null && restEventData.choices.Count > 0;
    }
}
