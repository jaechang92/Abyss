using Abyss.Runtime.Events;
using Abyss.Runtime.Flow;
using Abyss.Runtime.Interaction;
using Abyss.Runtime.Run;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>제단 표현만 소유한다. 대기/접근/선택/획득/거절로 보상·문·취소 상태를 바꾸지 않는다.</summary>
    public sealed class RewardAltarFeedback : MonoBehaviour
    {
        private enum DisplayState { Available, Pending, Resolved }
        private MonoBehaviour owner;
        private SpriteRenderer pedestal;
        private SpriteRenderer icon;
        private SpriteRenderer ring;
        private PlayerInteractor interactor;
        private DisplayState state;
        private bool isStopped;

        public static RewardAltarFeedback Ensure(MonoBehaviour owner, SpriteRenderer visual)
        {
            var feedback = owner.GetComponent<RewardAltarFeedback>();
            if (feedback == null) feedback = owner.gameObject.AddComponent<RewardAltarFeedback>();
            feedback.owner = owner;
            feedback.pedestal = visual;
            if (feedback.icon == null)
            {
                feedback.icon = feedback.CreatePart("AvailableReward");
                feedback.ring = feedback.CreatePart("RewardFocus");
                feedback.ring.sprite = FormComboSprites.Ring;
            }
            return feedback;
        }

        private SpriteRenderer CreatePart(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            renderer.sortingLayerID = pedestal != null ? pedestal.sortingLayerID : 0;
            renderer.sortingOrder = pedestal != null ? pedestal.sortingOrder + 2 : 5;
            return renderer;
        }

        public void Configure(Sprite rewardIcon, bool hasReward)
        {
            state = hasReward ? DisplayState.Available : DisplayState.Resolved;
            isStopped = RunManager.HasInstance && !RunManager.Instance.IsRunActive;
            if (icon != null) icon.sprite = rewardIcon;
            Apply();
        }

        public void SetPending()
        {
            state = DisplayState.Pending;
            Apply();
        }

        public void Complete(bool acquired)
        {
            if (state == DisplayState.Resolved) return;
            state = DisplayState.Resolved;
            if (CanDisplay) RewardClaimFx.Play(pedestal, acquired);
            Hide();
        }

        private bool CanDisplay => !isStopped && isActiveAndEnabled && owner != null && owner.isActiveAndEnabled &&
            (!RunManager.HasInstance || RunManager.Instance.IsRunActive) &&
            (!SceneFlowController.HasInstance || !SceneFlowController.Instance.IsLoading);

        private void OnEnable()
        {
            GameEvents.OnPlayerDead += Stop;
            GameEvents.OnRunEnded += Stop;
            GameEvents.OnRunAbandoned += Stop;
            GameEvents.OnRunStarted += Resume;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerDead -= Stop;
            GameEvents.OnRunEnded -= Stop;
            GameEvents.OnRunAbandoned -= Stop;
            GameEvents.OnRunStarted -= Resume;
            Hide();
        }

        private void Stop() { isStopped = true; Hide(); }
        private void Resume() => isStopped = false;

        private void Update() => Apply();

        private void Apply()
        {
            if (icon == null || ring == null) return;
            if (!CanDisplay || state == DisplayState.Resolved) { Hide(); return; }
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            bool focused = state == DisplayState.Available && interactor != null &&
                ReferenceEquals(interactor.CurrentTarget, owner);
            Vector3 center = pedestal != null ? pedestal.bounds.center : transform.position;
            float top = pedestal != null ? pedestal.bounds.max.y : center.y + 0.5f;
            float bob = state == DisplayState.Available ? Mathf.Sin(Time.time * 2f) * 0.06f : 0f;
            icon.transform.position = new Vector3(center.x, top + 0.45f + bob, center.z);
            float extent = icon.sprite != null ? Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y) : 1f;
            SetWorldScale(icon.transform, 0.65f / Mathf.Max(0.01f, extent));
            icon.color = state == DisplayState.Pending ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            icon.enabled = icon.sprite != null;
            ring.transform.position = icon.transform.position;
            SetWorldScale(ring.transform, focused ? 0.95f : 0.8f);
            ring.color = state == DisplayState.Pending ? new Color(0.65f, 0.65f, 0.7f, 0.14f)
                : new Color(1f, 0.85f, 0.45f, focused ? 0.5f : 0.15f);
            ring.enabled = true;
        }

        private static void SetWorldScale(Transform target, float size)
        {
            var scale = target.parent.lossyScale;
            target.localScale = new Vector3(size / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
                size / Mathf.Max(0.01f, Mathf.Abs(scale.y)), 1f);
        }

        private void Hide()
        {
            if (icon != null) icon.enabled = false;
            if (ring != null) ring.enabled = false;
        }
    }
}
