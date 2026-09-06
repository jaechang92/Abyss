using UnityEngine;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// <see cref="SettingsPanel"/>의 세로 배치 계산. 값이 아니라 <b>관계</b>를 적어 둔 곳이다.
    ///
    /// 이 패널은 행이 늘 때마다 배치가 어긋났다 — 해상도 행을 넣을 때 제목(y=-36)과 효과음 행(y=-40)이
    /// 같은 대역에 겹쳐 있었고, 언어 행에서 또 늘었다. <b>행 겹침도 패널 밖 잘림도 오류가 나지 않고
    /// 로그도 남기지 않는다.</b> 글자가 포개지거나 사라질 뿐이라 사람이 화면을 봐야만 발견된다.
    ///
    /// 그래서 y를 손으로 세는 대신 여기서 파생시킨다. 행이 하나 더 늘 때 고칠 곳은
    /// <see cref="RowCount"/> 하나이고, 나머지(제목·조작 안내·닫기 버튼·패널 높이)는 따라온다.
    /// 별도 클래스인 이유는 패널이 500줄 규약에 닿은 것도 있지만, <b>이 계산만 따로 테스트할 수
    /// 있기 때문</b>이다(SettingsPanelLayoutTests).
    /// </summary>
    public static class SettingsPanelLayout
    {
        public const float PanelWidth = 560f;

        /// <summary>행 간격.</summary>
        public const float RowPitch = 50f;

        /// <summary>첫 행(전체 음량) 중심 y.</summary>
        public const float RowTopY = 173f;

        /// <summary>음량 3 · 전체 화면 · 해상도 · 언어.</summary>
        public const int RowCount = 6;

        public const float TitleHeight = 40f;
        public const float KeyGuideHeight = 60f;
        public const float CloseHeight = 48f;

        /// <summary>제목·행 대역·조작 안내·닫기 버튼 사이의 간격.</summary>
        public const float SectionGap = 24f;

        /// <summary>패널 테두리와 내용 사이 여백.</summary>
        public const float EdgePadding = 28f;

        /// <summary>index번째 행의 중심 y. 0번이 맨 위다.</summary>
        public static float RowY(int index) => RowTopY - index * RowPitch;

        /// <summary>행 대역의 위쪽 끝.</summary>
        public static float RowBandTopY => RowY(0) + RowPitch * 0.5f;

        /// <summary>행 대역의 아래쪽 끝.</summary>
        public static float RowBandBottomY => RowY(RowCount - 1) - RowPitch * 0.5f;

        public static float TitleY => RowBandTopY + SectionGap + TitleHeight * 0.5f;

        public static float KeyGuideY => RowBandBottomY - SectionGap - KeyGuideHeight * 0.5f;

        // 조작 안내와 닫기 버튼은 다른 항목보다 조금 붙여 둔다 — 버튼은 스스로 경계가 보여서
        // 같은 간격을 주면 오히려 떨어져 보인다.
        public static float CloseY
            => KeyGuideY - KeyGuideHeight * 0.5f - (SectionGap - 4f) - CloseHeight * 0.5f;

        /// <summary>내용의 가장 위쪽 끝(제목 상단).</summary>
        public static float ContentTopY => TitleY + TitleHeight * 0.5f;

        /// <summary>내용의 가장 아래쪽 끝(닫기 버튼 하단).</summary>
        public static float ContentBottomY => CloseY - CloseHeight * 0.5f;

        /// <summary>
        /// 위아래 어느 쪽도 잘리지 않는 패널 높이.
        ///
        /// 패널은 화면 중앙에 놓이는데 내용은 위아래로 같은 만큼 자라지 않는다. 그래서 중심에서
        /// <b>먼 쪽 끝</b>에 맞춘다 — 한쪽만 보고 정하면 반대쪽이 조용히 패널 밖으로 나간다.
        /// </summary>
        public static float PanelHeight
            => 2f * (EdgePadding + Mathf.Max(ContentTopY, -ContentBottomY));
    }
}
