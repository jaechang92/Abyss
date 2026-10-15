using UnityEngine;

namespace Abyss.Runtime.Camera
{
    /// <summary>
    /// 배경 레이어의 시차(parallax) 이동. 카메라가 움직인 만큼을 <see cref="depth"/> 비율로 되돌려
    /// "먼 것은 덜 움직인다"를 만든다.
    ///
    /// <b>깊이 규약 — 0이 가장 멀다.</b>
    /// <list type="bullet">
    /// <item><c>depth = 0</c> — 무한히 멀다. 화면에 붙어 절대 안 흐른다(하늘).</item>
    /// <item><c>depth = 1</c> — 놀이 평면과 같다. 월드에 박혀 그대로 흐른다(발판).</item>
    /// <item><c>depth &gt; 1</c> — 놀이 평면보다 <b>앞</b>이다. 카메라와 반대로 밀려 더 빨리 흐른다
    /// (<c>bg_near</c>가 그렇다 — 조립본이 "a foreground overlay"라고 적어 둔 층이다).</item>
    /// </list>
    ///
    /// 🔴 <b>이름을 「속도」가 아니라 「깊이」로 둔 이유</b> — 속도로 부르면 0이 "안 움직임"인지
    /// "가장 빠름"인지가 부르는 사람마다 갈린다. 깊이는 그림에 있는 것이라 안 갈린다.
    /// 배치하는 쪽(<c>ArtTestStageBuilder</c>)이 먼 층부터 0에 가까운 값을 준다.
    ///
    /// <b>무한 반복</b>: <see cref="wrapWidth"/>가 0보다 크면 카메라가 그만큼 멀어질 때마다
    /// 기준점을 한 칸 옮긴다. 레이어는 <b>가로로 3장 이상</b> 이어 붙어 있어야 이음매가 안 보인다
    /// (한 칸 옮기는 순간 반대편이 이미 화면에 들어와 있어야 하기 때문).
    /// C3 부터는 카메라와의 거리를 반 칸 안(|gap| ≤ wrapWidth/2)으로 유지한다 — 3장이면 한 장 폭이 화면 반폭 이상이기만 하면 덮는다.
    /// 반복 그림이라 어느 칸이 보이든 화면은 같다. 칸 수는 나눗셈 한 번으로 구한다(순간이동에도 루프가 길어지지 않는다).
    ///
    /// 최후방 반복 레이어(하늘)는 화면 전체를 덮도록 실행 중 배율·칸 수를 보정한다 — ParallaxLayer.Coverage.
    ///
    /// ⚠️ <b>LateUpdate에서 돈다.</b> 카메라(<see cref="PlayerCameraFollow"/>)가 같은 프레임의
    /// LateUpdate에서 움직이므로, Update에서 읽으면 한 프레임 늦은 위치를 쓰고 배경이 떤다.
    /// 그래서 실행 순서를 카메라보다 뒤로 못박는다.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed partial class ParallaxLayer : MonoBehaviour
    {
        [Header("깊이 — 0이 가장 멀다(화면에 붙는다), 1이 놀이 평면, 1 초과는 앞 레이어")]
        [SerializeField, Range(0f, 2f)] private float depth = 0.5f;

        [Header("가로 무한 반복 — 이어 붙인 한 장의 폭(월드 유닛). 0이면 안 한다")]
        [SerializeField, Min(0f)] private float wrapWidth;

        [Header("세로도 따라갈 것인가 — 끄면 카메라가 위아래로 움직여도 배경은 제자리")]
        [SerializeField] private bool followVertical = true;

        private Transform cam;
        private Vector3 origin;
        private float baseX;
        private float baseY;

        private void Start()
        {
            // 미리 받은 카메라(BindCamera)가 있으면 그것을 쓴다 — Run 카메라는 MainCamera 태그가 없다.
            if (cam == null)
            {
                var main = UnityEngine.Camera.main;
                if (main == null)
                {
                    Debug.LogWarning($"[ParallaxLayer] MainCamera 없음 — '{name}' 시차 정지.");
                    enabled = false;
                    return;
                }
                cam = main.transform;
            }

            origin = transform.position;
            baseX = origin.x;
            baseY = origin.y;
            InitCoverage();
            Apply();
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            Apply();
        }

        private void Apply()
        {
            // 화면 크기(ortho·비율)가 바뀐 프레임에만 배율·wrapWidth·칸 수를 다시 정한다.
            RefreshCoverage();

            float shiftX = cam.position.x * (1f - depth);
            float shiftY = followVertical ? cam.position.y * (1f - depth) : 0f;

            transform.position = new Vector3(baseX + shiftX, baseY + shiftY, origin.z);

            if (wrapWidth > 0f)
            {
                // 카메라가 반 칸 넘게 벌어졌으면 기준점을 그쪽으로 필요한 칸 수만큼 한 번에 옮긴다.
                // 한 칸씩 옮기면 순간이동(방 전환)에서 한 칸밖에 못 따라가고, while 은 먼 이동에서 길게 돈다.
                float gap = cam.position.x - transform.position.x;
                float steps = Mathf.Round(gap / wrapWidth);
                if (steps != 0f)
                {
                    float step = wrapWidth * steps;
                    baseX += step;
                    transform.position += new Vector3(step, 0f, 0f);
                }
            }

            // 최후방 레이어만: 위아래 시차로 가장자리가 화면 안에 들어오면 그만큼만 밀어 넣는다(기준점은 안 바꾼다).
            KeepVerticalCoverage();
        }

        /// <summary>
        /// 따라갈 카메라를 미리 넘긴다(<see cref="Start"/> 전에). 넘기지 않으면 옛 동작대로 <c>Camera.main</c> 을 찾는다.
        /// 📌 Run 씬은 카메라에 MainCamera 태그가 없어 <c>Camera.main</c> 이 null 이다 — 태그를 바꾸면
        /// 그 값을 읽는 다른 코드(플레이어 흔들림 조회)까지 경로가 바뀌므로, 표현 쪽이 카메라를 직접 받는다.
        /// </summary>
        public void BindCamera(Transform cameraTransform)
        {
            if (cameraTransform != null) cam = cameraTransform;
        }

        /// <summary>빌더가 배치 직후 값을 넣는다. 에디터에서 손으로 만질 일이 없게 한다.</summary>
        public void Configure(float layerDepth, float layerWrapWidth, bool vertical = true)
        {
            depth = Mathf.Clamp(layerDepth, 0f, 2f);
            wrapWidth = Mathf.Max(0f, layerWrapWidth);
            followVertical = vertical;
        }
    }
}
