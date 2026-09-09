namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 화면 픽셀 배율의 단일 출처(SoT). <b>카메라 ortho 값은 취향이 아니라 산수다.</b>
    ///
    /// 목표 해상도 1080 세로를 정수 배로 나눠 아트 픽셀 세로를 정하고, 그것을 PPU 로 나눠
    /// 카메라 반높이를 얻는다. 어느 값도 손으로 적지 않는다 — 하나를 바꾸면 나머지가 따라온다.
    ///
    /// <code>
    /// 1080 / 3배 = 360 아트px  ->  360 / 2 / PPU 32 = ortho 5.625
    /// </code>
    ///
    /// 🔴 <b>정수가 아니면 도트가 고르지 않게 그려진다.</b> 예전 값 ortho 5 는 세로 320 아트px 라
    /// 1080/320 = 3.375 배였고, <b>어떤 줄은 3배 어떤 줄은 4배</b>로 확대돼 같은 굵기의 선이
    /// 자리마다 달라 보였다. 그림을 아무리 고쳐도 안 잡히는 종류의 지저분함이다.
    ///
    /// 💡 배율을 4 로 올리면 화면이 480x270 아트px 로 <b>좁아진다</b>(캐릭터가 커진다).
    ///    3 을 고른 것은 옛 320px 기준에서 3배가 +12.5%, 4배가 -15.6% 라 3배가 덜 움직이고,
    ///    패럴랙스 배경 규약(160x96 을 x4 = 640x384)이 이미 640x360 화면을 전제하기 때문이다.
    /// </summary>
    public static class PixelScale
    {
        /// <summary>기준 세로 해상도. FHD 1920x1080 확정(2026-09-09).</summary>
        public const int ReferenceScreenHeight = 1080;

        /// <summary>아트 픽셀 하나가 화면에 그려질 배율. <b>정수여야 한다.</b></summary>
        public const int ScreenUpscale = 3;

        /// <summary>
        /// 월드 픽셀 밀도. 32px 타일 = 1유닛. <b>이 값을 보는 것은 「Point 필터 픽셀 아트로
        /// 월드에 놓이는 것」뿐이다</b> — 환경 타일·배경(`EnvironmentArtImporter`)과
        /// 제단(`FormAltarBuilder`).
        ///
        /// 🔴 <b>2026-09-10 전수 조사 — 나머지 PPU 는 안 묶는다. 다른 축이다.</b>
        /// (같은 조사를 다시 하지 않도록 결론을 여기 적는다)
        /// <code>
        /// 대상             PPU   아트1px→화면px   왜 다른가
        /// 환경·제단         32       3.00        ← 이 상수
        /// 폼 스프라이트    128       0.75        손그림 + Bilinear (ADR-008). 세로 256px 가
        ///                                        플레이어 콜라이더 2유닛에 맞는 값이라 근거가 따로 있다
        /// 적 스프라이트     16       6.00        ⚠️ 도트인데 격자가 환경의 2배다 — 아래 참조
        /// 흰 사각형(유틸)    4      24.00        4x4 단색. 그림이 아니라 스케일 조절용 도구다
        /// UI 아이콘         32        —          Canvas 안이라 월드 배율과 무관. 값이 같은 것은 우연
        /// </code>
        ///
        /// ⚠️ <b>적 16 은 미결이다.</b> 환경과 같은 화면에 놓이는 Point 필터 도트인데
        /// 아트 1px 이 화면 6px 로 그려져 <b>환경(3px)보다 도트가 정확히 2배 굵다.</b>
        /// 의도(적을 크게 읽히게)인지 결함인지는 아트 방향 결정이라 코드가 못 정한다.
        /// 눈으로 판정할 것 — `NEXT_TASKS` 에 올려 뒀다.
        /// </summary>
        public const float PixelsPerUnit = 32f;

        /// <summary>화면에 담기는 아트 픽셀 세로. 1080 / 3 = 360.</summary>
        public const int ScreenHeightArtPixels = ReferenceScreenHeight / ScreenUpscale;

        /// <summary>
        /// 카메라 세로 반높이. 360 / 2 / 32 = 5.625.
        /// <b>씬에 이 숫자를 손으로 적지 않는다</b> — 씬 값과 여기가 갈리면 씬이 이긴다.
        /// </summary>
        public const float OrthographicSize = ScreenHeightArtPixels / 2f / PixelsPerUnit;
    }
}
