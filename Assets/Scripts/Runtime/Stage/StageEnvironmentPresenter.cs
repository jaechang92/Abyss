using Abyss.Runtime.Camera;
using Abyss.Runtime.Events;
using UnityEngine;

namespace Abyss.Runtime.Stage
{
    /// <summary>
    /// 공용 Run 씬에서 한 스테이지의 환경 표현(배경·지면·발판 스킨)을 방 진입에 맞춰 켜고 끈다. <b>표시만 한다</b> —
    /// 지형·충돌·진행은 건드리지 않는다(콜라이더는 그레이박스 오브젝트에 그대로 있고, 여기는 렌더러만 바꾼다).
    ///
    /// 📌 <see cref="GameEvents.OnRoomEntered"/> 만 듣는다 — <see cref="RoomLayoutController"/> 와 같은 방식이라
    /// StageDirector 를 고치지 않는다. 판정은 <see cref="StageEnvironmentRule"/>(EditMode 고정).
    ///
    /// 🔴 <b>자기 자신을 끄지 않는다</b> — 구독자가 SetActive(false) 로 꺼지면 OnDisable 에서 구독이 풀려 영영 안 켜진다
    /// (메모리 feedback_self_deactivate_presenter). 끄고 켜는 것은 아래 루트들뿐이다.
    ///
    /// 배치·배선은 에디터 진입점 <c>Stage1EnvironmentWiring.ApplyToRunScene</c> 가 한다. 손으로 만지지 않는다.
    /// </summary>
    public sealed class StageEnvironmentPresenter : MonoBehaviour
    {
        [Header("어느 스테이지의 표현인가")]
        [SerializeField] private StageData stage;
        [Tooltip("보스 방 배경을 붙일 방. 이 방에서만 bossArenaRoot 가 켜진다")]
        [SerializeField] private RoomData bossRoom;

        [Header("표현 루트 — 자식만 켜고 끈다")]
        [Tooltip("일반 방·보스 방 공통(하늘·지면 스킨·발판 스킨)")]
        [SerializeField] private GameObject[] sharedRoots = new GameObject[0];
        [Tooltip("일반 방 배경(먼 층·가까운 층)")]
        [SerializeField] private GameObject fieldRoot;
        [Tooltip("보스 방 배경")]
        [SerializeField] private GameObject bossArenaRoot;

        [Header("그레이박스 — 스킨이 켜지면 렌더러만 끈다(콜라이더 유지)")]
        [SerializeField] private Renderer[] grayboxRenderers = new Renderer[0];

        [Header("시차 카메라 — Run 카메라는 MainCamera 태그가 없어 Camera.main 이 null 이다")]
        [SerializeField] private Transform parallaxCamera;

        [Tooltip("첫 방 진입 이벤트 전(시퀀스 시작 지연 0.2초)의 모습. 런은 이 스테이지 첫 방에서 시작한다")]
        [SerializeField] private StageEnvironmentLook initialLook = StageEnvironmentLook.Field;

        [Header("방 전용 아트 — 비어 있으면 기존 표시 그대로(Stage1EnvironmentWiring 방 아트 진입점이 채운다)")]
        [Tooltip("이 방(이 스테이지의 일반 방)에서만 roomArtRoot 를 켠다. 보스 방·스테이지 밖에서는 무시된다")]
        [SerializeField] private RoomData roomArtRoom;
        [Tooltip("켜면 roomArtRoom 대신 이 스테이지의 모든 방(보스 포함)에서 roomArtRoot 를 켠다. 스테이지 밖에서는 끈다")]
        [SerializeField] private bool roomArtWholeStage;
        [SerializeField] private GameObject roomArtRoot;
        [Tooltip("방 전용 아트가 켜질 때만 끌 기존 표현(일반 방 배경·지면 스킨·그 방 발판 스킨). 다른 방에서는 원래 규칙으로 돌아간다")]
        [SerializeField] private GameObject[] roomArtReplacedRoots = new GameObject[0];

        private StageEnvironmentLook currentLook;
        private bool isRoomArtShown;

        /// <summary>지금 보이는 모습(검증·디버그용).</summary>
        public StageEnvironmentLook CurrentLook => currentLook;

        /// <summary>지금 방 전용 아트가 켜져 있는가(검증·디버그용).</summary>
        public bool IsRoomArtShown => isRoomArtShown;

        public StageData Stage => stage;
        public RoomData BossRoom => bossRoom;
        public RoomData RoomArtRoom => roomArtRoom;
        public bool IsRoomArtWholeStage => roomArtWholeStage;

        private void Awake()
        {
            BindParallaxCamera();
            Apply(initialLook);
        }

        private void OnEnable()
        {
            GameEvents.OnRoomEntered += HandleRoomEntered;
        }

        private void OnDisable()
        {
            GameEvents.OnRoomEntered -= HandleRoomEntered;
        }

        private void HandleRoomEntered(RoomData room)
        {
            Apply(StageEnvironmentRule.Resolve(stage, bossRoom, room), room);
        }

        /// <summary>모습 하나를 적용한다(방 전용 아트 없음). 같은 값이어도 다시 적용한다(멱등) — 누가 중간에 켰어도 규칙대로 돌린다.</summary>
        public void Apply(StageEnvironmentLook look) => Apply(look, null);

        /// <summary>
        /// 모습 하나를 들어간 방과 함께 적용한다. 방 전용 아트가 켜지는 방이면 교체 대상만 끄고 <see cref="roomArtRoot"/> 를 켠다.
        /// 🔑 교체 대상은 매번 원래 규칙값으로 다시 계산한다 — 다른 방·보스 방·스테이지 밖으로 나가면 그대로 복원된다.
        /// </summary>
        public void Apply(StageEnvironmentLook look, RoomData enteredRoom)
        {
            currentLook = look;
            // 루트가 비었으면(배선 전·복원 후) 방 전용 아트는 없다 — 기존 표시를 그대로 둔다.
            isRoomArtShown = roomArtRoot != null && StageEnvironmentRule.ShowsRoomArt(look, roomArtRoom, enteredRoom, roomArtWholeStage);

            bool isShared = StageEnvironmentRule.ShowsShared(look);
            foreach (var root in sharedRoots)
            {
                SetActive(root, isShared && !IsReplaced(root));
            }

            SetActive(fieldRoot, StageEnvironmentRule.ShowsField(look) && !IsReplaced(fieldRoot));
            SetActive(bossArenaRoot, StageEnvironmentRule.ShowsBossArena(look) && !IsReplaced(bossArenaRoot));

            // 위 목록 밖의 교체 대상(공용 층 안의 지면 스킨 등)은 평소 켜 둔다 — 부모가 꺼지면 같이 안 보인다.
            foreach (var root in roomArtReplacedRoots)
            {
                if (root != null && !IsListedRoot(root)) SetActive(root, !isRoomArtShown);
            }

            SetActive(roomArtRoot, isRoomArtShown);

            bool isGraybox = StageEnvironmentRule.ShowsGraybox(look);
            foreach (var graybox in grayboxRenderers)
            {
                if (graybox != null) graybox.enabled = isGraybox;
            }
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive) target.SetActive(isActive);
        }

        /// <summary>지금 방 전용 아트 때문에 꺼야 하는 루트인가.</summary>
        private bool IsReplaced(GameObject root)
            => isRoomArtShown && root != null && System.Array.IndexOf(roomArtReplacedRoots, root) >= 0;

        /// <summary>모습 규칙이 직접 켜고 끄는 루트인가(공용 층·일반 방 배경·보스 방 배경).</summary>
        private bool IsListedRoot(GameObject root)
            => root == fieldRoot || root == bossArenaRoot || System.Array.IndexOf(sharedRoots, root) >= 0;

        /// <summary>
        /// 꺼진 루트 아래 레이어까지 카메라를 넘긴다. Awake 에 하므로 레이어들의 Start 보다 먼저다
        /// (꺼진 오브젝트의 Start 는 처음 켜질 때 돈다).
        /// </summary>
        private void BindParallaxCamera()
        {
            if (parallaxCamera == null) return;

            foreach (var layer in GetComponentsInChildren<ParallaxLayer>(true))
            {
                layer.BindCamera(parallaxCamera);
            }
        }
    }
}
