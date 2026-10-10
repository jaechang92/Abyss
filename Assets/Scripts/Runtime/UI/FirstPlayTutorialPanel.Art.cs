using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// A2(2026-10-09) — 튜토리얼 띠 테두리 · 실제 키 안내 틀 · 행동 완료 표식.
    ///
    /// <list type="bullet">
    /// <item>테두리: 큰 띠(안내 중)에만. 개구부가 띠를 감싸고, 작은 대기 띠(우하단)에서는 끈다.</item>
    /// <item>키 틀: 월드 힌트의 바인딩 글자 칸(Keycap)을 9-slice 로 감싼다 — 글자는 실제 바인딩(장치별) 그대로.</item>
    /// <item>완료 표식: 월드 힌트의 기존 완료 피드백(목적지 근거를 새로 얻은 직후 0.6초)에서만 켠다. 상시 표시하지 않는다.</item>
    /// </list>
    /// 모두 표시 전용(raycastTarget=false)이라 건너뛰기 버튼·내비게이션과 무관하다.
    /// </summary>
    public sealed partial class FirstPlayTutorialPanel
    {
        private const string FRAME_NAME = "ArtTutorialFrame";
        private const string KEY_FRAME_NAME = "ArtKeyFrame";
        private const string COMPLETE_NAME = "ArtObjectiveComplete";

        private const float KEY_FRAME_BORDER = 10f;
        private const float COMPLETE_SIZE = 48f;
        private static readonly Vector2 CompletePosition = new(-190f, 23f); // 행동 그림 자리(BuildSpatialUI)

        // 큰 띠 프레임(그림 포함) 위 가장자리와 화면 위 가장자리 사이 최소 여백 — 프레임이 없을 때의 기존 띠 여백과 같다.
        private const float FRAME_SAFE_TOP = 24f;

        private Image frameArt;
        private Image completeMark;

        /// <summary>
        /// 큰 띠 위 가장자리보다 프레임 그림이 위로 나오는 높이(0 이상). 그림이 없으면 0 → 기존 위치 그대로.
        /// 개구부가 띠를 감싸는 AroundInner 라 프레임 높이는 그림 비율로 정해진다(띠 112 → 약 300).
        /// </summary>
        private float frameTopOverflow;

        /// <summary>
        /// 큰 띠 위치(위 가운데 기준). 프레임 전체가 화면 위 여백 안에 들도록 띠를 넘친 만큼 내린다.
        /// 상수와 BuildUI 때 한 번 계산한 값으로만 정해서 normal/compact 를 몇 번 오가도 같은 자리다.
        /// </summary>
        private Vector2 NormalBodyPosition => new(0f, -(FRAME_SAFE_TOP + frameTopOverflow));

        /// <summary>BuildUI 끝에서 한 번 — 큰 띠 테두리. 큰 띠 상태(BuildUI 직후)에서 계산해 띠 위치도 맞춘다.</summary>
        private void ApplyFrameArt()
        {
            if (body == null || body.transform is not RectTransform bodyRect) return;
            frameArt = UiArtDecor.ApplyFitted(bodyRect, FRAME_NAME, UiArtKeys.UI_TUTORIAL_FRAME, UiArtFit.AroundInner, out var placement);
            if (frameArt != null) frameArt.transform.SetAsFirstSibling();

            // 배치 좌표는 띠 중심 기준(크기 = PanelSize). 프레임 위 가장자리 − 띠 위 가장자리.
            frameTopOverflow = frameArt != null
                ? Mathf.Max(0f, placement.Center.y + placement.Size.y * 0.5f - PanelSize.y * 0.5f)
                : 0f;
            bodyRect.anchoredPosition = NormalBodyPosition;
        }

        /// <summary>작은 대기 띠에서는 테두리를 숨긴다(크기가 큰 띠 기준으로 맞춰져 있다).</summary>
        private void SetFrameArtVisible(bool isVisible)
        {
            if (frameArt != null) frameArt.enabled = isVisible;
        }

        /// <summary>월드 힌트의 키 칸을 감싸는 키 틀(9-slice). 개구부 = 키 칸.</summary>
        private static void ApplyKeyFrame(RectTransform keycap)
        {
            var sprite = UiArtLibrary.GetSliced(UiArtKeys.UI_INPUT_KEY_FRAME);
            if (keycap == null || sprite == null) return;

            var image = UiArtDecor.EnsureImage(keycap, KEY_FRAME_NAME);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.color = Color.white;
            // 테두리 화면 두께 = 테두리 픽셀 / 배율 — 좌우 중 두꺼운 쪽이 KEY_FRAME_BORDER 가 되게.
            float borderPixels = Mathf.Max(sprite.border.x, sprite.border.z, 1f);
            image.pixelsPerUnitMultiplier = borderPixels / KEY_FRAME_BORDER;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-KEY_FRAME_BORDER, -KEY_FRAME_BORDER);
            rect.offsetMax = new Vector2(KEY_FRAME_BORDER, KEY_FRAME_BORDER);
            image.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// 월드 힌트의 완료 피드백(컨트롤러가 목적지 근거를 새로 얻은 직후 잠깐 띄우는 「✓」 상태)에서만 완료 표식을 켠다.
        /// 행동 그림 자리에 둔다 — 완료 상태에는 행동 그림이 모두 꺼져 있어 겹치지 않는다. 그 밖의 상태에서는 끈다.
        /// </summary>
        private void SetCompleteMark(bool isComplete)
        {
            if (spatialBody == null || spatialBody.transform is not RectTransform spatialRect) return;
            if (!isComplete)
            {
                if (completeMark != null) completeMark.enabled = false;
                return;
            }

            var box = new Rect(CompletePosition.x - COMPLETE_SIZE * 0.5f, CompletePosition.y - COMPLETE_SIZE * 0.5f, COMPLETE_SIZE, COMPLETE_SIZE);
            completeMark = UiArtDecor.ApplyIconInRect(spatialRect, COMPLETE_NAME, UiArtKeys.UI_OBJECTIVE_COMPLETE, box, Color.white);
        }
    }
}
