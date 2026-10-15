using Abyss.Runtime.Events;
using System;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Localization;
using UnityEngine;

namespace Abyss.Runtime.Form
{
    /// <summary>
    /// 런 도중 배치되는 폼 보상 제단. 상호작용 시 직렬화된 <see cref="rewardForm"/>을 폼 보상으로 제시한다.
    /// GameEvents.OnFormRewardOffered → HUDPresenter → FormReplacementModal(슬롯 선택)로 이어진다(정식 트리거).
    /// 이전까지 폼 보상은 CheatMenu에서만 발동됐으나, 이제 이 제단이 실플레이 진입점이다.
    /// 로비 DungeonPortal과 동일한 IInteractable 패턴 — 근접 감지·발동은 PlayerInteractor가 담당한다.
    /// 1회성: 발동 후 소비되어 재상호작용 불가(스프라이트를 흐리게 표시).
    /// </summary>
    public sealed class FormAltar : MonoBehaviour, IInteractable
    {
        [Tooltip("이 제단이 제시하는 폼 보상.")]
        [SerializeField] private FormData rewardForm;

        [Tooltip("프롬프트 StringKey(비우면 기본 문구).")]
        [SerializeField] private string promptKey;

        [Tooltip("소비 시 흐려질 스프라이트(비우면 자기 SpriteRenderer 자동 사용).")]
        [SerializeField] private SpriteRenderer visual;

        [Tooltip("소비 후 스프라이트 알파 배율(시각적 비활성 표시).")]
        [SerializeField] private float consumedAlphaScale = 0.35f;

        private bool consumed;
        private float baseAlpha = 1f;
        private static FormAltar offeringAltar;
        private GameObject offeringInteractor;
        private Feedback.RewardAltarFeedback feedback;
        private Func<int, bool> isOwnerValid;
        private Action<int> onResolved;
        private Action<bool> cancelModal;
        private bool isOfferResolved;
        public int OfferGeneration { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOffer() => offeringAltar = null;

        // 제시 이벤트를 처리하는 동안만 유효하다. 치트/다른 보상을 근처 제단으로 오인하지 않는다.
        public static Vector3? CaptureOfferOrigin(FormData form, FormController controller)
        {
            var altar = CaptureOffer(form, controller);
            if (altar == null) return null;
            return altar.visual != null ? altar.visual.bounds.center : altar.transform.position;
        }

        public static FormAltar CaptureOffer(FormData form, FormController controller)
        {
            var altar = offeringAltar;
            if (altar == null || altar.rewardForm != form || altar.offeringInteractor == null || controller == null) return null;
            if (controller.gameObject != altar.offeringInteractor &&
                !controller.transform.IsChildOf(altar.offeringInteractor.transform)) return null;
            return altar;
        }

        public string InteractionPrompt => Loc.Get(string.IsNullOrEmpty(promptKey) ? StringKey.Prompt_FormAltar : promptKey);

        // 이미 소비했거나 보상이 비어 있으면 상호작용 불가 — PlayerInteractor의 후보 선정에서 자동 제외된다.
        public bool CanInteract => !consumed && rewardForm != null && IsOfferOwned(OfferGeneration);

        private bool IsOfferOwned(int generation) => isActiveAndEnabled && generation == OfferGeneration &&
            !isOfferResolved && (isOwnerValid == null || isOwnerValid(generation));

        public bool CanResolveOffer(int generation) => consumed && IsOfferOwned(generation);

        public void BindModalCancellation(int generation, Action<bool> cancel)
        {
            if (CanResolveOffer(generation)) cancelModal = cancel;
        }

        public void InvalidateOffer(bool canReleaseModalPause = false)
        {
            var cancel = cancelModal;
            cancelModal = null;
            isOwnerValid = null;
            onResolved = null;
            rewardForm = null;
            consumed = true;
            OfferGeneration++;
            cancel?.Invoke(canReleaseModalPause);
        }

        private void OnDestroy() => InvalidateOffer();

        private void Awake()
        {
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            if (visual != null) baseAlpha = visual.color.a;
            feedback = Feedback.RewardAltarFeedback.Ensure(this, visual);
            feedback.Configure(rewardForm != null ? rewardForm.icon : null, rewardForm != null);
        }

        /// <summary>
        /// 런타임 재설정. 보상 폼을 갈아끼우고 재장전(소비 해제·시각 복원)한 뒤 활성화한다.
        /// 보상 룸 클리어 시 StageDirector가 호출 — 제단 하나를 룸마다 재사용한다.
        /// </summary>
        public void Configure(FormData reward, Func<int, bool> ownerValid = null, Action<int> resolved = null)
        {
            InvalidateOffer();
            // 비활성 상태면 먼저 켜서 Awake(visual/baseAlpha 확보)를 유발한다.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            rewardForm = reward;
            OfferGeneration++;
            consumed = false;
            isOfferResolved = false;
            isOwnerValid = ownerValid;
            onResolved = resolved;
            if (feedback != null) feedback.Configure(reward != null ? reward.icon : null, reward != null);
            if (visual != null)
            {
                var c = visual.color;
                c.a = baseAlpha;
                visual.color = c;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            consumed = true;
            int generation = OfferGeneration;
            if (feedback != null) feedback.SetPending();
            var previousOffer = offeringAltar;
            offeringAltar = this;
            offeringInteractor = interactor;
            try { GameEvents.RaiseFormRewardOffered(rewardForm); }
            finally
            {
                offeringInteractor = null;
                offeringAltar = previousOffer;
            }
            if (generation == OfferGeneration && consumed && !isOfferResolved) ApplyConsumedVisual();
        }

        // 표현이 미배선이어도 같은 제시의 진행 완료는 전달한다. 외부 비활성은 여기로 오지 않는다.
        public bool CompletePresentation(bool acquired, int generation)
        {
            if (!CanResolveOffer(generation)) return false;
            isOfferResolved = true;
            cancelModal = null;
            var resolved = onResolved;
            onResolved = null;
            if (feedback != null) feedback.Complete(acquired);
            resolved?.Invoke(generation);
            return true;
        }

        // 파괴/비활성 대신 흐리게 남겨 "이미 받은 제단"임을 알린다. 재상호작용은 CanInteract가 차단한다.
        private void ApplyConsumedVisual()
        {
            if (visual == null) return;
            var c = visual.color;
            c.a *= consumedAlphaScale;
            visual.color = c;
        }
    }
}
