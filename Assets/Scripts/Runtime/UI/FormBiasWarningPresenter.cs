using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Run;
using Abyss.Runtime.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 폼 편향 경고 HUD. 한 런에서 단일 폼 사용 비율이 RunConfig.formBiasThreshold(기본 60%)를 넘으면
    /// "폼 교체를 활용해 보세요" 안내를 노출한다(MF-5). 폼 교체 설계 의도를 잃은 플레이를 부드럽게 환기하는 용도로,
    /// 페널티는 없다.
    ///
    /// 갱신은 이벤트 기반(룸 클리어·폼 교체) — 매 프레임 비율을 재계산하지 않는다.
    /// 초반 표본이 적을 때(런 시작 직후 1폼 100%) 오탐하지 않도록 최소 누적 시간 게이트를 둔다.
    /// </summary>
    public sealed class FormBiasWarningPresenter : MonoBehaviour
    {
        [Header("표시 요소")]
        [Tooltip("경고 표시/숨김 대상 루트. 비우면 이 오브젝트를 사용.")]
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;

        [Header("설정")]
        [Tooltip("폼 플레이타임이 이만큼 누적된 뒤부터 판정한다(초반 표본 부족 오탐 방지).")]
        [SerializeField, Min(0f)] private float minElapsedSeconds = 45f;

        [Tooltip("비워두면 Resources/Data/RunConfig를 자동 로드")]
        [SerializeField] private RunConfig config;

        // config·에셋 부재 시 폴백 임계값(RunConfig.formBiasThreshold 기본값과 동일)
        private const float DEFAULT_BIAS_THRESHOLD = 0.6f;
        private const string WARNING_FORMAT = "폼 편향 경고 — {0} {1:P0} 사용 중\n폼 교체를 활용해 보세요";

        private float Threshold
        {
            get
            {
                var resolved = RunConfigProvider.Resolve(config);
                return resolved != null ? resolved.formBiasThreshold : DEFAULT_BIAS_THRESHOLD;
            }
        }

        private void Awake()
        {
            // 씬 수동 편집으로 참조가 끊기면 경고가 조용히 사라진다 — 배선 누락을 표면화한다.
            if (root == null && label == null)
            {
                Debug.LogWarning($"[FormBiasWarningPresenter] root·label 모두 미배선 ({name}) — 경고가 표시되지 않는다. Build HUD 재실행 필요.");
            }
        }

        private void OnEnable()
        {
            GameEvents.OnRoomCleared += HandleRoomCleared;
            GameEvents.OnFormSwapped += HandleFormSwapped;
            SetVisible(false);
        }

        private void OnDisable()
        {
            GameEvents.OnRoomCleared -= HandleRoomCleared;
            GameEvents.OnFormSwapped -= HandleFormSwapped;
        }

        private void HandleRoomCleared(RoomData room) => Refresh();

        // 교체 직후엔 편향이 해소되는 방향이므로 즉시 재평가해 경고를 걷어준다.
        private void HandleFormSwapped(FormData previous, FormData next) => Refresh();

        /// <summary>현재 런 통계로 편향 여부를 재판정하고 표시를 갱신한다.</summary>
        public void Refresh()
        {
            var run = RunManager.GetInstanceSafe();
            if (run == null || !run.IsRunActive)
            {
                SetVisible(false);
                return;
            }

            // 게이트·판정 모두 폼 플레이타임 축을 쓴다. 런 총 경과시간(unscaled)은 모달·일시정지를 포함해
            // 폼 누적(scaled)과 시간축이 어긋나므로 편향 비율의 분모로 쓰지 않는다.
            var stats = run.Stats;
            if (stats == null || stats.FormPlaytimeTotalSeconds < minElapsedSeconds)
            {
                SetVisible(false);
                return;
            }

            string dominantId = stats.GetDominantFormId();
            float ratio = stats.GetFormPlaytimeRatio(dominantId);

            if (string.IsNullOrEmpty(dominantId) || ratio < Threshold)
            {
                SetVisible(false);
                return;
            }

            if (label != null)
            {
                var form = FormCatalog.GetById(dominantId);
                string name = form != null && !string.IsNullOrEmpty(form.displayName) ? form.displayName : dominantId;
                label.text = string.Format(WARNING_FORMAT, name, ratio);
            }
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            // 자기 자신을 끄면 OnDisable로 구독이 끊겨 다시 켤 주체가 없어진다 — 루트 미지정 시엔 라벨만 토글.
            if (target == gameObject)
            {
                if (label != null) label.enabled = visible;
                return;
            }
            if (target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
