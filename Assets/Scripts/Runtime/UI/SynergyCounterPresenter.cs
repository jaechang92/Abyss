using System.Collections.Generic;
using Abyss.Runtime.Draft;
using Abyss.Runtime.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 시너지 카운터 HUD. 현재 런에서 보유한 스킬을 시너지 축별로 집계해 "[불꽃] 2" 칩으로 상시 노출한다
    /// (08-content-roadmap.md §3 — 축 신규 도입 시 표준 체크리스트의 "HUD [축] N개 카운터").
    ///
    /// 드래프트 중에만 보이는 BuildContextPanel과 달리, 전투 중에도 축 누적을 볼 수 있어야
    /// 시너지 카테고리 스킬(임계 2)을 노릴지 판단할 수 있다.
    ///
    /// 갱신은 이벤트 기반(스킬 획득·런 시작/종료) — 매 프레임 재집계하지 않는다.
    /// 축 표시명·색은 <see cref="SynergyAxis"/>가 SoT다.
    /// </summary>
    public sealed class SynergyCounterPresenter : MonoBehaviour
    {
        [Header("표시 요소")]
        [Tooltip("칩 표시/숨김 대상 루트. 비우면 이 오브젝트를 사용.")]
        [SerializeField] private GameObject root;

        [Tooltip("칩이 배치될 컨테이너(HorizontalLayoutGroup 권장). 비우면 root를 사용.")]
        [SerializeField] private RectTransform chipContainer;

        [Tooltip("복제 원본 칩(비활성). Image 배경 + 자식 Text 구조.")]
        [SerializeField] private GameObject chipTemplate;

        [Header("선택 참조 (미배선 시 런타임 탐색)")]
        [SerializeField] private DraftSessionController draftSession;

        private const string CHIP_FORMAT = "[{0}] {1}";
        private const string ACTIVATED_SUFFIX = " ✦"; // 임계 도달 축 표식

        // 임계 미달 축은 흐리게 — 도달한 축이 한눈에 들어오게 한다.
        private const float INACTIVE_ALPHA = 0.5f;
        private static readonly Color INACTIVE_BACKGROUND = new(0.10f, 0.10f, 0.14f, 0.70f);

        private readonly Dictionary<string, int> counts = new();
        private readonly List<string> orderedTags = new();
        private readonly List<ChipView> chips = new();

        /// <summary>생성된 칩 1개의 구성 요소 묶음.</summary>
        private readonly struct ChipView
        {
            public readonly GameObject Root;
            public readonly Image Background;
            public readonly Text Label;

            public ChipView(GameObject root, Image background, Text label)
            {
                Root = root;
                Background = background;
                Label = label;
            }
        }

        private void Awake()
        {
            // 씬 수동 편집으로 참조가 끊기면 카운터가 조용히 사라진다 — 배선 누락을 표면화한다.
            if (chipTemplate == null)
            {
                Debug.LogWarning($"[SynergyCounterPresenter] chipTemplate 미배선 ({name}) — 시너지 카운터가 표시되지 않는다. Build HUD 재실행 필요.");
            }
        }

        private void OnEnable()
        {
            GameEvents.OnSkillDrafted += HandleSkillDrafted;
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.OnSkillDrafted -= HandleSkillDrafted;
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        // 획득·교체 모두 OnSkillDrafted를 거치므로 이 하나로 보유 변화를 빠짐없이 받는다.
        private void HandleSkillDrafted(SkillData skill, DraftTriggerReason reason) => Refresh();

        // 런이 새로 시작되면 이전 런의 세션 참조가 남아 있을 수 있어 캐시를 버린다.
        private void HandleRunStarted()
        {
            draftSession = null;
            Refresh();
        }

        private void HandleRunEnded() => Refresh();

        /// <summary>현재 보유 스킬로 축 집계를 다시 하고 칩 표시를 갱신한다.</summary>
        public void Refresh()
        {
            var session = ResolveDraftSession();
            SynergyAxis.Tally(session != null ? session.Owned : null, counts);

            if (counts.Count == 0)
            {
                SetVisible(false);
                return;
            }

            BuildOrderedTags();
            EnsureChipCount(orderedTags.Count);

            for (int i = 0; i < chips.Count; i++)
            {
                if (i >= orderedTags.Count)
                {
                    if (chips[i].Root != null) chips[i].Root.SetActive(false);
                    continue;
                }
                ApplyChip(chips[i], orderedTags[i], counts[orderedTags[i]]);
            }

            SetVisible(true);
        }

        /// <summary>
        /// 표시 순서를 결정적으로 만든다(보유 수 내림차순 → 태그 사전순).
        /// Dictionary 순회 순서에 맡기면 갱신할 때마다 칩 위치가 튀어 읽기 어렵다.
        /// </summary>
        private void BuildOrderedTags()
        {
            orderedTags.Clear();
            foreach (var kv in counts) orderedTags.Add(kv.Key);
            orderedTags.Sort((a, b) =>
            {
                int byCount = counts[b].CompareTo(counts[a]);
                return byCount != 0 ? byCount : string.CompareOrdinal(a, b);
            });
        }

        private void ApplyChip(ChipView chip, string tag, int count)
        {
            if (chip.Root == null) return;
            if (!chip.Root.activeSelf) chip.Root.SetActive(true);

            bool activated = SynergyAxis.IsActivated(count);
            var axisColor = SynergyAxis.GetColor(tag);

            if (chip.Label != null)
            {
                string text = string.Format(CHIP_FORMAT, SynergyAxis.GetDisplayName(tag), count);
                chip.Label.text = activated ? text + ACTIVATED_SUFFIX : text;
                chip.Label.color = activated ? axisColor : new Color(axisColor.r, axisColor.g, axisColor.b, INACTIVE_ALPHA);
            }

            if (chip.Background != null)
            {
                // 도달한 축만 배경에 축 색을 저채도로 깔아 눈에 띄게 한다.
                chip.Background.color = activated
                    ? new Color(axisColor.r * 0.30f, axisColor.g * 0.30f, axisColor.b * 0.30f, 0.92f)
                    : INACTIVE_BACKGROUND;
            }
        }

        /// <summary>필요한 개수만큼 칩을 확보한다(템플릿 복제, 기존 칩은 재사용).</summary>
        private void EnsureChipCount(int required)
        {
            if (chipTemplate == null) return;
            var parent = chipContainer != null ? chipContainer : (RectTransform)transform;

            while (chips.Count < required)
            {
                var instance = Instantiate(chipTemplate, parent);
                instance.name = $"SynergyChip{chips.Count}";
                instance.SetActive(true);
                chips.Add(new ChipView(instance, instance.GetComponent<Image>(), instance.GetComponentInChildren<Text>(true)));
            }
        }

        private DraftSessionController ResolveDraftSession()
        {
            if (draftSession == null)
            {
                draftSession = FindAnyObjectByType<DraftSessionController>(FindObjectsInactive.Include);
            }
            return draftSession;
        }

        private void SetVisible(bool visible)
        {
            var target = root != null ? root : gameObject;
            // 자기 자신을 끄면 OnDisable로 구독이 끊겨 다시 켤 주체가 없어진다 — 루트 미지정 시엔 칩만 토글.
            if (target == gameObject)
            {
                for (int i = 0; i < chips.Count; i++)
                {
                    if (chips[i].Root != null && chips[i].Root.activeSelf != visible) chips[i].Root.SetActive(visible);
                }
                return;
            }
            if (target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
