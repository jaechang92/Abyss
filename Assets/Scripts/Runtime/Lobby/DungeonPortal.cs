using Abyss.Runtime.Flow;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using Abyss.Runtime.Meta;
using Abyss.Runtime.Stage;
using Abyss.Runtime.Story;
using Abyss.Runtime.UI;
using UnityEngine;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 던전 입구. 상호작용 시 즉시 Run 씬으로 전환한다(입장 전용).
    /// 시작 폼 선택은 정비 NPC(ServiceNpc)가 담당하고, 그 결과는 RunStartContext로 전달된다.
    ///
    /// 입구 위에 탐사 목적 한 줄을 띄운다(26-expedition-discovery E1) — 수락 창·정지·필수 대화 없이 읽기만 한다.
    /// 미발견이면 목적, 발견·반응 미열람이면 기록자 안내, 반응을 들었으면 띄우지 않는다(기본 입구 표시).
    /// 런 씬 <c>StageDirector</c>와 같은 시퀀스 에셋을 직렬화로 받아, 기록이 전제하는 구성일 때만 띄운다.
    /// </summary>
    public sealed class DungeonPortal : MonoBehaviour, IInteractable
    {
        private const float PURPOSE_GAP = 0.45f;            // 입구 판정 윗면 위 여백
        private const float PURPOSE_CHARACTER_SIZE = 0.07f;
        private const int ORDER_PURPOSE = 5;

        private static readonly Color PurposeColor = new Color(0.92f, 0.86f, 0.72f);

        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        [Tooltip("Run 씬 StageDirector와 같은 시퀀스(MainRunSequence). 비우거나 다른 구성이면 목적을 띄우지 않는다.")]
        [SerializeField] private StageSequenceData previewSequence;

        private bool busy;

        private bool isPurposeAvailable;
        private TextMesh purposeLabel;
        private string shownPurposeKey;
        private LocalizationLanguage shownLanguage;

        public string InteractionPrompt => Loc.Get(string.IsNullOrEmpty(promptKey) ? StringKey.Portal_Prompt : promptKey);
        public bool CanInteract => !busy;

        private void Start()
        {
            isPurposeAvailable = PassageDiscovery.IsCompatible(previewSequence);
            if (!isPurposeAvailable) Debug.Log("[DungeonPortal] 미리보기 시퀀스가 없거나 통행 기록 구성이 아니다 — 입구 목적 생략");
        }

        public void Interact(GameObject interactor)
        {
            if (busy) return;
            busy = true;
            HidePurpose();
            // 씬이 전환되므로 busy 해제는 불필요(로비 언로드).
            _ = SceneFlowController.Instance.LoadRunAsync();
        }

        /// <summary>발견 상태·언어가 바뀌면 다시 쓴다. 같으면 아무것도 하지 않는다(매 프레임 재기록 없음).</summary>
        private void LateUpdate()
        {
            string key = ResolvePurposeKey();
            if (key == null)
            {
                HidePurpose();
                return;
            }

            var language = Loc.CurrentLanguage;
            if (purposeLabel != null && purposeLabel.gameObject.activeSelf && key == shownPurposeKey && language == shownLanguage) return;

            var label = EnsurePurposeLabel();
            label.text = Loc.Get(key);
            label.gameObject.SetActive(true);
            shownPurposeKey = key;
            shownLanguage = language;
        }

        private void OnDisable() => HidePurpose();

        private string ResolvePurposeKey()
        {
            if (!isPurposeAvailable || busy) return null;
            // 조회 전용 — 세이브 서비스를 새로 만들지 않는다(없으면 띄우지 않는다).
            var meta = MetaSaveService.GetInstanceSafe();
            if (meta == null) return null;
            return PassageDiscovery.GetState(meta) switch
            {
                PassageDiscoveryState.Unknown => ExpeditionTextKeys.PortalPurpose,
                PassageDiscoveryState.DiscoveredUnviewed => ExpeditionTextKeys.PortalRecorderHint,
                _ => null,
            };
        }

        private void HidePurpose()
        {
            shownPurposeKey = null;
            if (purposeLabel != null && purposeLabel.gameObject.activeSelf) purposeLabel.gameObject.SetActive(false);
        }

        /// <summary>입구 판정(트리거) 윗면 위에 한 줄. 판정이 없으면 입구 위치 위에 둔다.</summary>
        private TextMesh EnsurePurposeLabel()
        {
            if (purposeLabel != null) return purposeLabel;

            var trigger = GetComponent<Collider2D>();
            float top = trigger != null ? trigger.bounds.max.y : transform.position.y + 1f;

            var go = new GameObject("PurposeLabel");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(transform.position.x, top + PURPOSE_GAP, transform.position.z);
            // 입구 오브젝트는 판정 크기만큼 늘려 둔다(씬 배율 1.5×2) — 글자가 따라 늘지 않게 역배율을 건다.
            var scale = transform.lossyScale;
            go.transform.localScale = new Vector3(SafeInverse(scale.x), SafeInverse(scale.y), 1f);

            purposeLabel = go.AddComponent<TextMesh>();
            purposeLabel.font = UiFactory.GetDefaultFont();
            purposeLabel.fontSize = 48;
            purposeLabel.characterSize = PURPOSE_CHARACTER_SIZE;
            purposeLabel.anchor = TextAnchor.LowerCenter;
            purposeLabel.alignment = TextAlignment.Center;
            purposeLabel.color = PurposeColor;

            var renderer = go.GetComponent<MeshRenderer>();
            if (purposeLabel.font != null) renderer.sharedMaterial = purposeLabel.font.material;
            renderer.sortingOrder = ORDER_PURPOSE;
            go.SetActive(false);
            return purposeLabel;
        }

        private static float SafeInverse(float value) => Mathf.Approximately(value, 0f) ? 1f : 1f / Mathf.Abs(value);
    }
}
