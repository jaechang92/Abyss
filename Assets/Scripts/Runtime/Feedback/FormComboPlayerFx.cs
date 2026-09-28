using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// P04 폼 연계 A/B 의 플레이어 주변 도형 피드백(프로토타입). 문구(<see cref="FormComboLabel"/>)와 짝으로 쓴다 —
    /// 색 + 모양 + 문구로 알린다.
    /// <list type="bullet">
    /// <item>A 입력 가능(예약 전): 몸 둘레 옅은 노랑 고리 하나(움직이지 않는다)</item>
    /// <item>A 예약 유지: 몸 둘레 노랑 이중 고리(서로 반대 위상으로 약하게 숨쉰다)</item>
    /// <item>A 예약 교체 성공: 고리 하나가 <see cref="SUCCESS_DURATION"/> 동안 퍼지며 사라진다</item>
    /// <item>B 준비: 진행 방향을 가리키는 하늘색 쐐기 두 개</item>
    /// <item>B 실제 이동: 발밑 뒤쪽 짧은 속도선 3개. 정상 종료 뒤 <see cref="TRAIL_FADE_DURATION"/> 페이드</item>
    /// </list>
    ///
    /// 🔑 <b>대상의 자식이 아니고 따라간다</b>(<see cref="FormComboLabel"/> 과 같은 이유 — 플레이어는 localScale.x 부호로
    /// 좌우를 바꾸므로 자식이면 도형이 같이 뒤집힌다). 좌우는 위치 부호와 <c>flipX</c> 로 직접 정한다.
    /// 🔑 오브젝트 · 렌더러는 <see cref="Create"/> 에서 한 번만 만들고, 이후에는 <c>enabled</c> 와 변환만 바꾼다.
    /// 파괴는 소유자(PlayerCharacter)가 한다. 대상이 사라지면 스스로 모두 숨긴다.
    /// </summary>
    public sealed class FormComboPlayerFx : MonoBehaviour
    {
        // ── 공통 ──
        private const int SORTING_ORDER_BACK = 4;   // 고리 · 속도선
        private const int SORTING_ORDER_FRONT = 6;  // 쐐기

        // ── A 예약 이중 고리 ──
        private static readonly Color ReserveColor = new(1f, 0.9f, 0.45f, 1f);
        private const float RESERVE_OUTER_DIAMETER_RATIO = 1.2f;   // 몸 높이 대비
        private const float RESERVE_INNER_DIAMETER_RATIO = 0.95f;
        private const float RESERVE_OUTER_ALPHA = 0.8f;
        private const float RESERVE_INNER_ALPHA = 0.5f;
        private const float RESERVE_BREATH_SCALE = 0.05f;          // ±5%
        private const float RESERVE_BREATH_SPEED = 7f;             // rad/s

        // ── A 입력 가능(예약 전) 약한 고리 ──
        private const float HINT_ALPHA = 0.3f;

        // ── A 성공 확장 고리 ──
        private static readonly Color SuccessColor = new(1f, 0.97f, 0.7f, 1f);
        public const float SUCCESS_DURATION = 0.3f;
        private const float SUCCESS_END_SCALE = 1.7f;              // 바깥 고리 지름 대비
        private const float SUCCESS_START_ALPHA = 0.95f;

        // ── B 준비 쐐기 ──
        private static readonly Color WedgeColor = new(0.55f, 0.95f, 1f, 1f);
        private const float WEDGE_SIZE = 0.3f;
        private const float WEDGE_GAP = 0.25f;                     // 몸 옆면에서 첫 쐐기까지
        private const float WEDGE_SPACING = 0.2f;                  // 첫 쐐기 → 둘째 쐐기
        private const float WEDGE_SECOND_ALPHA = 0.55f;
        private const float WEDGE_BOB_DISTANCE = 0.06f;
        private const float WEDGE_BOB_SPEED = 10f;                 // rad/s

        // ── B 이동 속도선 ──
        private static readonly Color TrailColor = new(0.55f, 0.95f, 1f, 0.85f);
        private static readonly float[] TrailHeights = { 0.08f, 0.22f, 0.36f };   // 발밑 원점 기준
        private static readonly float[] TrailLengths = { 0.55f, 0.4f, 0.3f };
        private const float TRAIL_THICKNESS = 0.045f;
        private const float TRAIL_BACK_GAP = 0.05f;                // 몸 뒷면에서 선 시작까지
        private const float TRAIL_SCROLL_DISTANCE = 0.15f;
        private const float TRAIL_SCROLL_SPEED = 6f;               // 주기/초
        public const float TRAIL_FADE_DURATION = 0.2f;

        private Transform target;
        private Vector3 bodyCenterOffset;
        private float bodyHeight;
        private float bodyHalfWidth;

        private SpriteRenderer hintRing;
        private SpriteRenderer reserveOuter;
        private SpriteRenderer reserveInner;
        private SpriteRenderer successRing;
        private SpriteRenderer wedgeNear;
        private SpriteRenderer wedgeFar;
        private SpriteRenderer[] trailLines;

        private bool isHintShown;
        private bool isReserveShown;
        private float successTimer = -1f;   // 음수 = 꺼짐
        private bool isWedgeShown;
        private int wedgeDirection = 1;
        private bool isTrailShown;
        private int trailDirection = 1;
        private float trailFadeTimer = -1f; // 음수 = 페이드 중 아님

        /// <summary>
        /// 도형 묶음을 만든다(처음에는 모두 숨김). <paramref name="bodyCenterOffset"/> 은 대상 원점(발밑) 기준 몸 중심,
        /// <paramref name="bodyHeight"/> · <paramref name="bodyHalfWidth"/> 는 콜라이더 크기다.
        /// </summary>
        public static FormComboPlayerFx Create(string name, Transform target, Vector3 bodyCenterOffset,
            float bodyHeight, float bodyHalfWidth)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<FormComboPlayerFx>();
            fx.target = target;
            fx.bodyCenterOffset = bodyCenterOffset;
            fx.bodyHeight = Mathf.Max(0.5f, bodyHeight);
            fx.bodyHalfWidth = Mathf.Max(0.1f, bodyHalfWidth);

            fx.hintRing = CreatePart(go.transform, "HintRing", FormComboSprites.Ring, SORTING_ORDER_BACK);
            fx.reserveOuter = CreatePart(go.transform, "ReserveOuter", FormComboSprites.Ring, SORTING_ORDER_BACK);
            fx.reserveInner = CreatePart(go.transform, "ReserveInner", FormComboSprites.Ring, SORTING_ORDER_BACK);
            fx.successRing = CreatePart(go.transform, "SuccessRing", FormComboSprites.Ring, SORTING_ORDER_BACK);
            fx.wedgeNear = CreatePart(go.transform, "WedgeNear", FormComboSprites.Wedge, SORTING_ORDER_FRONT);
            fx.wedgeFar = CreatePart(go.transform, "WedgeFar", FormComboSprites.Wedge, SORTING_ORDER_FRONT);
            fx.trailLines = new SpriteRenderer[TrailHeights.Length];
            for (int i = 0; i < fx.trailLines.Length; i++)
                fx.trailLines[i] = CreatePart(go.transform, "TrailLine", FormComboSprites.White, SORTING_ORDER_BACK);

            fx.Follow();
            return fx;
        }

        private static SpriteRenderer CreatePart(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        // ====== A ======

        /// <summary>입력 가능(예약 전) 약한 고리를 켜고 끈다. 예약 고리와 동시에 켜지 않는 것은 호출자가 맞춘다.</summary>
        public void SetHint(bool shown)
        {
            isHintShown = shown;
            if (!shown) hintRing.enabled = false;
        }

        /// <summary>예약 유지 이중 고리를 켜고 끈다.</summary>
        public void SetReserve(bool shown)
        {
            isReserveShown = shown;
            if (!shown)
            {
                reserveOuter.enabled = false;
                reserveInner.enabled = false;
            }
        }

        /// <summary>예약 교체가 실제로 성공했다 — 예약 고리를 끄고 확장 고리를 한 번 재생한다.</summary>
        public void PlaySuccess()
        {
            SetHint(false);
            SetReserve(false);
            successTimer = 0f;
        }

        /// <summary>확장 고리를 즉시 끈다(정지 · 룸 이동 · 사망 · 비활성).</summary>
        public void StopSuccess()
        {
            successTimer = -1f;
            successRing.enabled = false;
        }

        // ====== B ======

        /// <summary>준비 쐐기를 켜고 끈다. <paramref name="direction"/> 은 +1(오른쪽) / -1(왼쪽).</summary>
        public void SetReady(bool shown, int direction)
        {
            isWedgeShown = shown;
            if (direction != 0) wedgeDirection = direction;
            if (!shown)
            {
                wedgeNear.enabled = false;
                wedgeFar.enabled = false;
            }
        }

        /// <summary>
        /// 실제로 움직이는 동안만 속도선을 켠다(호출자가 매 프레임 실제 이동 여부로 부른다).
        /// 켜면 진행 중이던 페이드는 취소된다.
        /// </summary>
        public void SetMoving(bool moving, int direction)
        {
            if (direction != 0) trailDirection = direction;
            if (moving)
            {
                trailFadeTimer = -1f;
                isTrailShown = true;
                return;
            }
            if (trailFadeTimer < 0f) HideTrail();
        }

        /// <summary>
        /// 실제 이동을 마쳤다 — 속도선을 <see cref="TRAIL_FADE_DURATION"/> 동안 흐려지며 끝낸다.
        /// 이동이 너무 짧아 한 번도 켜지지 않았어도 이 페이드는 보인다(호출자가 이동 거리로 성공을 판단한 뒤 부른다).
        /// </summary>
        public void FadeTrail(int direction)
        {
            if (direction != 0) trailDirection = direction;
            isTrailShown = true;
            trailFadeTimer = 0f;
        }

        /// <summary>B 도형(쐐기 · 속도선 · 페이드)을 즉시 모두 끈다.</summary>
        public void ClearLunge()
        {
            SetReady(false, 0);
            HideTrail();
        }

        /// <summary>A/B 도형을 즉시 모두 끈다.</summary>
        public void ClearAll()
        {
            SetHint(false);
            SetReserve(false);
            StopSuccess();
            ClearLunge();
        }

        private void HideTrail()
        {
            isTrailShown = false;
            trailFadeTimer = -1f;
            for (int i = 0; i < trailLines.Length; i++) trailLines[i].enabled = false;
        }

        // ====== 매 프레임 ======

        private void LateUpdate()
        {
            if (target == null)
            {
                ClearAll();
                return;
            }
            Follow();

            if (isHintShown) PlaceRing(hintRing, bodyHeight * RESERVE_OUTER_DIAMETER_RATIO, HINT_ALPHA, ReserveColor);
            if (isReserveShown) UpdateReserve();
            if (successTimer >= 0f) UpdateSuccess();
            if (isWedgeShown) UpdateWedge();
            if (isTrailShown) UpdateTrail();
        }

        private void Follow()
        {
            if (target != null) transform.position = target.position;
        }

        private void UpdateReserve()
        {
            float wave = Mathf.Sin(Time.time * RESERVE_BREATH_SPEED);
            float outer = bodyHeight * RESERVE_OUTER_DIAMETER_RATIO * (1f + RESERVE_BREATH_SCALE * wave);
            float inner = bodyHeight * RESERVE_INNER_DIAMETER_RATIO * (1f - RESERVE_BREATH_SCALE * wave);
            PlaceRing(reserveOuter, outer, RESERVE_OUTER_ALPHA, ReserveColor);
            PlaceRing(reserveInner, inner, RESERVE_INNER_ALPHA, ReserveColor);
        }

        private void UpdateSuccess()
        {
            successTimer += Time.deltaTime;
            float t = Mathf.Clamp01(successTimer / SUCCESS_DURATION);
            if (t >= 1f)
            {
                StopSuccess();
                return;
            }
            float start = bodyHeight * RESERVE_OUTER_DIAMETER_RATIO;
            float diameter = Mathf.Lerp(start, start * SUCCESS_END_SCALE, 1f - (1f - t) * (1f - t));  // 빠르게 퍼지고 느려짐
            PlaceRing(successRing, diameter, SUCCESS_START_ALPHA * (1f - t), SuccessColor);
        }

        private void PlaceRing(SpriteRenderer ring, float diameter, float alpha, Color color)
        {
            ring.transform.localPosition = bodyCenterOffset;
            ring.transform.localScale = new Vector3(diameter, diameter, 1f);
            color.a = alpha;
            ring.color = color;
            ring.enabled = true;
        }

        private void UpdateWedge()
        {
            float bob = (0.5f + 0.5f * Mathf.Sin(Time.time * WEDGE_BOB_SPEED)) * WEDGE_BOB_DISTANCE;
            float nearX = wedgeDirection * (bodyHalfWidth + WEDGE_GAP + WEDGE_SIZE * 0.5f + bob);
            float farX = nearX + wedgeDirection * WEDGE_SPACING;
            PlaceWedge(wedgeNear, nearX, 1f);
            PlaceWedge(wedgeFar, farX, WEDGE_SECOND_ALPHA);
        }

        private void PlaceWedge(SpriteRenderer wedge, float localX, float alpha)
        {
            wedge.transform.localPosition = new Vector3(localX, bodyCenterOffset.y, 0f);
            wedge.transform.localScale = new Vector3(WEDGE_SIZE, WEDGE_SIZE, 1f);
            wedge.flipX = wedgeDirection < 0;
            Color c = WedgeColor;
            c.a = alpha;
            wedge.color = c;
            wedge.enabled = true;
        }

        private void UpdateTrail()
        {
            float fade = 1f;
            if (trailFadeTimer >= 0f)
            {
                trailFadeTimer += Time.deltaTime;
                fade = 1f - Mathf.Clamp01(trailFadeTimer / TRAIL_FADE_DURATION);
                if (fade <= 0f)
                {
                    HideTrail();
                    return;
                }
            }

            float scroll = Mathf.Repeat(Time.time * TRAIL_SCROLL_SPEED, 1f) * TRAIL_SCROLL_DISTANCE;
            float back = -trailDirection;
            for (int i = 0; i < trailLines.Length; i++)
            {
                float length = TrailLengths[i];
                // 선마다 흐르는 위상을 조금씩 어긋낸다 — 셋이 같이 움직이면 한 덩어리로 보인다.
                float phase = Mathf.Repeat(scroll + i * TRAIL_SCROLL_DISTANCE / trailLines.Length, TRAIL_SCROLL_DISTANCE);
                float x = back * (bodyHalfWidth + TRAIL_BACK_GAP + length * 0.5f + phase);
                var line = trailLines[i];
                line.transform.localPosition = new Vector3(x, TrailHeights[i], 0f);
                line.transform.localScale = new Vector3(length, TRAIL_THICKNESS, 1f);
                Color c = TrailColor;
                c.a *= fade;
                line.color = c;
                line.enabled = true;
            }
        }
    }
}
