using Abyss.Runtime.UI;
using NUnit.Framework;

namespace Abyss.Tests.EditMode
{
    /// <summary>
    /// 설정 패널의 세로 배치 불변식.
    ///
    /// 이 테스트가 생긴 계기: 설정 패널은 행이 늘 때마다 배치가 어긋났다. 해상도 행을 넣을 때
    /// 제목(y=-36)과 효과음 행(y=-40)이 같은 대역에 겹쳐 있었고, 언어 행에서 또 한 칸 늘었다.
    ///
    /// 🔴 <b>겹침도 잘림도 오류가 아니다.</b> uGUI는 글자가 포개져도, 패널 밖으로 나가도 아무 말을
    /// 하지 않는다. 로그도 예외도 없이 <b>화면에서만</b> 드러나므로, 사람이 그 화면을 열어 보기
    /// 전까지는 발견되지 않는다 — 상점 5번째 품목·도감 6번째 탭이 같은 방식으로 잘렸다.
    ///
    /// 그래서 좌표를 눈으로 확인하는 대신 <b>항목 사이의 관계</b>를 여기에 고정한다.
    /// 행을 하나 더 늘리려면 <see cref="SettingsPanelLayout.RowCount"/>만 올리면 되고,
    /// 그 결과가 패널을 넘치면 이 테스트가 먼저 잡는다.
    /// </summary>
    public sealed class SettingsPanelLayoutTests
    {
        // 행 안의 위젯 중 가장 높은 것(해상도·언어의 ◀ ▶ 버튼)의 높이.
        private const float ROW_WIDGET_HEIGHT = 34f;

        private static float PanelTopY => SettingsPanelLayout.PanelHeight * 0.5f;
        private static float PanelBottomY => -SettingsPanelLayout.PanelHeight * 0.5f;

        [Test]
        public void 행_간격이_행_안의_위젯보다_넓다()
        {
            Assert.Greater(SettingsPanelLayout.RowPitch, ROW_WIDGET_HEIGHT,
                "행 간격이 위젯 높이보다 좁으면 이웃한 두 행이 서로 파고든다.");
        }

        [Test]
        public void 행이_위에서_아래로_내려간다()
        {
            for (int i = 1; i < SettingsPanelLayout.RowCount; i++)
            {
                Assert.Less(SettingsPanelLayout.RowY(i), SettingsPanelLayout.RowY(i - 1),
                    $"{i}번 행이 {i - 1}번 행보다 위에 있다 — RowY의 부호가 뒤집혔다.");
            }
        }

        [Test]
        public void 제목이_첫_행과_겹치지_않는다()
        {
            float titleBottom = SettingsPanelLayout.TitleY - SettingsPanelLayout.TitleHeight * 0.5f;

            Assert.GreaterOrEqual(titleBottom, SettingsPanelLayout.RowBandTopY,
                "제목 아래끝이 첫 행 대역으로 내려왔다 — 이 패널이 실제로 한 번 겪은 겹침이다.");
        }

        [Test]
        public void 조작_안내가_마지막_행과_겹치지_않는다()
        {
            float guideTop = SettingsPanelLayout.KeyGuideY + SettingsPanelLayout.KeyGuideHeight * 0.5f;

            Assert.LessOrEqual(guideTop, SettingsPanelLayout.RowBandBottomY,
                "조작 안내가 마지막 행 위로 올라왔다 — 행을 늘렸는데 안내가 따라 내려가지 않은 것이다.");
        }

        [Test]
        public void 닫기_버튼이_조작_안내와_겹치지_않는다()
        {
            float guideBottom = SettingsPanelLayout.KeyGuideY - SettingsPanelLayout.KeyGuideHeight * 0.5f;
            float closeTop = SettingsPanelLayout.CloseY + SettingsPanelLayout.CloseHeight * 0.5f;

            Assert.LessOrEqual(closeTop, guideBottom,
                "닫기 버튼이 조작 안내를 덮는다.");
        }

        /// <summary>
        /// 🔑 이 테스트가 이 파일의 이유다. 패널은 화면 중앙에 놓이는데 내용은 위아래로 같은 만큼
        /// 자라지 않는다 — 한쪽 끝만 보고 높이를 정하면 <b>반대쪽이 조용히 밖으로 나간다.</b>
        /// </summary>
        [Test]
        public void 내용이_패널_위아래_안에_들어온다()
        {
            Assert.LessOrEqual(SettingsPanelLayout.ContentTopY, PanelTopY,
                "제목이 패널 위로 삐져나갔다.");
            Assert.GreaterOrEqual(SettingsPanelLayout.ContentBottomY, PanelBottomY,
                "닫기 버튼이 패널 아래로 잘렸다.");
        }

        [Test]
        public void 내용_바깥에_최소_여백이_남는다()
        {
            Assert.GreaterOrEqual(PanelTopY - SettingsPanelLayout.ContentTopY, SettingsPanelLayout.EdgePadding,
                "위쪽 여백이 규정보다 좁다.");
            Assert.GreaterOrEqual(SettingsPanelLayout.ContentBottomY - PanelBottomY, SettingsPanelLayout.EdgePadding,
                "아래쪽 여백이 규정보다 좁다.");
        }
    }
}
