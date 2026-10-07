using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>Run 카메라 비교 프로필(22-skul-camera-character-scale §3). 직렬화 값이 곧 씬 데이터 — 번호를 바꾸지 말 것.</summary>
    public enum RunCameraProfileId
    {
        /// <summary>A 현재 — 세로 360 art px, ortho 5.625. 기본값.</summary>
        A_Current = 0,
        /// <summary>B 중간 — 세로 480 art px, ortho 7.5.</summary>
        B_Middle = 1,
        /// <summary>C 넓은 후보 — 세로 540 art px, ortho 8.4375(1080p에서 art px당 2화면px).</summary>
        C_Wide = 2
    }

    /// <summary>
    /// Run 씬 전용 카메라 확대율 프로필(C1). 같은 오브젝트의 Camera 에 ortho 를 정하고, 추적·경계 계산은
    /// 기존 <see cref="PlayerCameraFollow"/>가 그 크기(<c>camera.orthographicSize</c>)를 읽어 그대로 한다.
    ///
    /// 🔑 <b>ortho 는 여기서만 계산한다(단일 출처).</b> 프로필마다 「화면 세로 art px」 하나만 정하고
    /// ortho = artPx / 2 / <see cref="PixelScale.PixelsPerUnit"/> 로 얻는다. A 는 <see cref="PixelScale.ScreenHeightArtPixels"/>
    /// 그대로라 공용 상수와 같다. 공용 상수(<see cref="PixelScale"/>)는 바꾸지 않는다 — 로비·타이틀 빌더가 그 값을 쓴다.
    ///
    /// 적용 시점: Awake(첫 프레임 전) 1회, 이후 프로필이 바뀐 프레임에만 다시 적용한다(플레이 중 Inspector 변경 반영).
    /// 실행 순서를 Follow(10)보다 앞에 두어 바뀐 프레임에도 Follow 의 LateUpdate 가 새 크기로 경계를 잡는다.
    /// 편집 모드에서는 씬 Camera 값을 건드리지 않는다(씬 저장값 5.625 유지).
    /// 몸·충돌체·물리·추적 offset·smoothTime 은 건드리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class RunCameraProfile : MonoBehaviour
    {
        // 프로필별 화면 세로 art px. ortho 는 이것에서만 파생된다.
        private const int ART_HEIGHT_B = 480;
        private const int ART_HEIGHT_C = 540;

        // 비교 보고용 대표 몸 높이(art px). 22번 §2의 idle 첫 프레임 실측 60~61px.
        private const int REFERENCE_BODY_ART_PX = 60;

        [Tooltip("A 현재(5.625) · B 중간(7.5) · C 넓은 후보(8.4375). 플레이 중 바꾸면 다음 프레임에 적용된다.")]
        [SerializeField] private RunCameraProfileId profile = RunCameraProfileId.A_Current;

        [Tooltip("적용할 때마다 해상도별 배율·대표 몸 점유율을 콘솔에 남긴다(캡처 비교용).")]
        [SerializeField] private bool logOnApply = true;

        private UnityEngine.Camera targetCamera;
        private bool hasApplied;
        private RunCameraProfileId appliedProfile;

        public RunCameraProfileId Profile => profile;

        /// <summary>프로필의 화면 세로 art px.</summary>
        public static int ArtHeightFor(RunCameraProfileId id)
        {
            switch (id)
            {
                case RunCameraProfileId.B_Middle: return ART_HEIGHT_B;
                case RunCameraProfileId.C_Wide: return ART_HEIGHT_C;
                default: return PixelScale.ScreenHeightArtPixels;
            }
        }

        /// <summary>프로필의 ortho(세로 반높이, 유닛). A 5.625 / B 7.5 / C 8.4375.</summary>
        public static float OrthographicSizeFor(RunCameraProfileId id) =>
            ArtHeightFor(id) / 2f / PixelScale.PixelsPerUnit;

        /// <summary>
        /// 화면 세로 <paramref name="screenHeight"/>px 에서 art px 하나가 차지하는 화면 px.
        /// 정수가 아니면 줄마다 굵기가 다르게 그려진다 — 예: C 를 720p 에 채우면 720/540 = 1.333(분수).
        /// </summary>
        public static float ScreenPixelsPerArtPixel(RunCameraProfileId id, int screenHeight) =>
            screenHeight / (float)ArtHeightFor(id);

        /// <summary>해당 해상도에서 정수 배율인가(픽셀 굵기 균일).</summary>
        public static bool IsIntegerUpscale(RunCameraProfileId id, int screenHeight) =>
            screenHeight > 0 && screenHeight % ArtHeightFor(id) == 0;

        /// <summary>몸 높이 <paramref name="bodyArtPx"/>(PPU32·월드 스케일 1 가정)가 화면 세로에서 차지하는 비율. 해상도와 무관하다.</summary>
        public static float BodyScreenRatio(RunCameraProfileId id, float bodyArtPx) =>
            bodyArtPx / ArtHeightFor(id);

        private void Awake()
        {
            targetCamera = GetComponent<UnityEngine.Camera>();
            Apply();
        }

        // Follow(10)의 LateUpdate 보다 먼저 돈다. 바뀐 프레임에만 적용한다.
        private void LateUpdate()
        {
            if (hasApplied && appliedProfile == profile) return;
            Apply();
        }

        private void Apply()
        {
            if (targetCamera == null) return;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = OrthographicSizeFor(profile);
            appliedProfile = profile;
            hasApplied = true;

            if (logOnApply) LogProfile();
        }

        private void LogProfile()
        {
            int screenHeight = Screen.height;
            float ortho = OrthographicSizeFor(profile);
            float visibleHeight = ortho * 2f;
            float visibleWidth = visibleHeight * targetCamera.aspect;
            Debug.Log($"[RunCameraProfile] {profile} — ortho {ortho:0.####}, 가시 {visibleWidth:0.##}×{visibleHeight:0.##}유닛, " +
                      $"세로 {ArtHeightFor(profile)} art px · 1080p ×{ScreenPixelsPerArtPixel(profile, 1080):0.###} · " +
                      $"720p ×{ScreenPixelsPerArtPixel(profile, 720):0.###} · 현재 {screenHeight}p ×{ScreenPixelsPerArtPixel(profile, screenHeight):0.###}" +
                      $"({(IsIntegerUpscale(profile, screenHeight) ? "정수" : "분수")}) · 몸 {REFERENCE_BODY_ART_PX}px 점유 " +
                      $"{BodyScreenRatio(profile, REFERENCE_BODY_ART_PX):P1}");
        }
    }
}
