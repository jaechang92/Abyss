using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 방 타입의 표시 규약 SoT(<see cref="Abyss.Runtime.Draft.SynergyAxis"/>와 같은 역할).
    ///
    /// 노드 맵 분기 UI가 첫 소비자지만, 앞으로 HUD 진행 표시·도감·미니맵이 같은 라벨과 색을 써야 한다.
    /// 규약이 여러 곳에 흩어지면 방 타입을 추가할 때마다 누락이 생긴다.
    ///
    /// 미등록 타입도 안전하게 다룬다(열거형 이름 + 중립 색) — 타입이 앞서가고 UI가 따라오는 일이
    /// 생기더라도 화면이 비어서는 안 된다.
    /// </summary>
    public static class RoomTypeDisplay
    {
        private static readonly Color CombatColor = new Color(0.90f, 0.45f, 0.40f);
        private static readonly Color EliteColor = new Color(0.95f, 0.62f, 0.30f);
        private static readonly Color BossColor = new Color(0.85f, 0.30f, 0.55f);
        private static readonly Color EventColor = new Color(0.55f, 0.70f, 0.95f);
        private static readonly Color RestColor = new Color(0.55f, 0.85f, 0.60f);
        private static readonly Color ShopColor = new Color(0.95f, 0.82f, 0.45f);
        private static readonly Color NeutralColor = new Color(0.80f, 0.80f, 0.86f);

        /// <summary>플레이어에게 보여줄 방 타입 이름.</summary>
        public static string Label(RoomType type) => type switch
        {
            RoomType.Combat => "전투",
            RoomType.Elite => "엘리트",
            RoomType.Boss => "보스",
            RoomType.Event => "이벤트",
            RoomType.Rest => "휴식",
            RoomType.Shop => "상점",
            _ => type.ToString()
        };

        /// <summary>타입 구분용 색. 라벨과 함께 써서 한눈에 갈리게 한다.</summary>
        public static Color Color(RoomType type) => type switch
        {
            RoomType.Combat => CombatColor,
            RoomType.Elite => EliteColor,
            RoomType.Boss => BossColor,
            RoomType.Event => EventColor,
            RoomType.Rest => RestColor,
            RoomType.Shop => ShopColor,
            _ => NeutralColor
        };

        /// <summary>
        /// 타입 앞에 붙이는 글리프. 스프라이트 아이콘이 나오기 전까지의 임시 표기이므로
        /// 폰트가 확실히 가진 기호만 쓴다(이모지·전용 아이콘 폰트에 기대지 않는다).
        /// </summary>
        public static string Glyph(RoomType type) => type switch
        {
            RoomType.Combat => "×",
            RoomType.Elite => "※",
            RoomType.Boss => "★",
            RoomType.Event => "?",
            RoomType.Rest => "▲",
            RoomType.Shop => "◆",
            _ => "·"
        };

        /// <summary>글리프 + 라벨. 노드 버튼 제목 한 줄.</summary>
        public static string Headline(RoomType type) => $"{Glyph(type)}  {Label(type)}";
    }
}
