using System.Collections.Generic;
using Abyss.Runtime.ArtIntegration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Interaction
{
    /// <summary>
    /// 플레이어 근접 IInteractable을 추적하고 Interact 입력 시 최근접 대상의 Interact를 호출한다.
    /// 자식 트리거 콜라이더로 후보를 감지(이벤트 구동) — 매 프레임 OverlapCircle 폴링 회피.
    /// H2에서 NPC가 늘어나도 같은 파이프라인으로 동작한다(architect 자문).
    /// 입력은 PlayerInput SendMessages(OnInteract). g 키 바인딩, Hold 제거(탭).
    ///
    /// 🔴 <b>2026-08-26 — 로비 전용에서 공용으로 옮겼다.</b>
    /// 예전에는 <c>Abyss.Runtime.Lobby</c>에 있으면서 <c>LobbyPlayerController</c>를 직접 들었다.
    /// 그래서 <b>런 플레이어에는 붙일 수 없었고</b>, 런 중 폼 제단이 반응하지 않았다.
    /// 잠금 게이트만 <see cref="IInteractionBlocker"/>로 뽑아 의존을 끊었다.
    ///
    /// 📌 <b>Player.prefab에 들어간다</b>(예전에는 <c>FormAltarBuilder</c>가 씬 오버라이드로 붙였다).
    /// 오버라이드는 프리팹이나 씬을 다시 빌드하면 <b>조용히 사라진다</b> — 실제로 그렇게 없어졌다.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("현재 상호작용 대상 프롬프트 표시(없어도 동작).")]
        [SerializeField] private Text promptLabel;

        private readonly List<IInteractable> candidates = new List<IInteractable>();
        private IInteractable current;
        public IInteractable CurrentTarget => current;

        // 같은 오브젝트의 잠금 소스. 패널이 화면을 점유하는 동안 상호작용을 막는 게이트로 쓴다.
        // 없으면(런 플레이어) 막지 않는다 — IInteractionBlocker 주석 참조.
        private IInteractionBlocker blocker;

        // A2 상호작용 표식 — 지금 G 로 반응할 대상 머리 위에만. 플레이어마다 하나, 플레이어와 함께 파괴된다.
        private const float MARKER_HEIGHT = 0.7f;
        private const float MARKER_GAP = 0.2f;
        private const int MARKER_SORTING_ORDER = 500;
        private SpriteRenderer marker;
        private IInteractable markedTarget;
        private Collider2D markedCollider;

        private void Awake()
        {
            blocker = GetComponent<IInteractionBlocker>();
        }

        private void OnDisable()
        {
            if (marker != null) marker.enabled = false;
            markedTarget = null;
            markedCollider = null;
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var it = other.GetComponentInParent<IInteractable>();
            if (it != null && !candidates.Contains(it)) candidates.Add(it);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var it = other.GetComponentInParent<IInteractable>();
            if (it != null) candidates.Remove(it);
        }

        private void Update()
        {
            current = PickNearest();
            UpdatePrompt();
            UpdateMarker();
        }

        private IInteractable PickNearest()
        {
            IInteractable best = null;
            float bestSqr = float.MaxValue;
            Vector2 self = transform.position;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                // 파괴되었거나 MonoBehaviour가 아닌 후보 정리(Unity null 포함).
                if (candidates[i] is not MonoBehaviour mb || mb == null)
                {
                    candidates.RemoveAt(i);
                    continue;
                }
                if (!candidates[i].CanInteract) continue;

                float d = ((Vector2)mb.transform.position - self).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = candidates[i];
                }
            }
            return best;
        }

        private void UpdatePrompt()
        {
            if (promptLabel == null) return;

            bool isShow = current != null;
            if (promptLabel.gameObject.activeSelf != isShow) promptLabel.gameObject.SetActive(isShow);
            if (isShow) promptLabel.text = current.InteractionPrompt;
        }

        /// <summary>
        /// 표식은 실제 상호작용 대상(최근접 · CanInteract)이 있고 잠금이 없을 때만 보인다 — 메뉴·대화가 화면을 쥔 동안은 숨긴다.
        /// 대상이 바뀔 때만 콜라이더를 다시 찾고, 위치는 대상 콜라이더 위 가장자리를 따라간다(대상이 움직여도 맞게).
        /// </summary>
        private void UpdateMarker()
        {
            bool isShown = current != null && (blocker == null || !blocker.BlocksInteraction);
            if (!isShown)
            {
                if (marker != null && marker.enabled) marker.enabled = false;
                return;
            }

            if (!EnsureMarker()) return;
            if (!ReferenceEquals(markedTarget, current))
            {
                markedTarget = current;
                markedCollider = current is MonoBehaviour behaviour ? behaviour.GetComponentInChildren<Collider2D>() : null;
            }

            Vector3 top;
            if (markedCollider != null)
            {
                var bounds = markedCollider.bounds;
                top = new Vector3(bounds.center.x, bounds.max.y, 0f);
            }
            else
            {
                top = ((MonoBehaviour)current).transform.position;
            }

            float halfHeight = marker.sprite.bounds.extents.y * marker.transform.localScale.y;
            marker.transform.position = top + new Vector3(0f, MARKER_GAP + halfHeight, 0f);
            if (!marker.enabled) marker.enabled = true;
        }

        private bool EnsureMarker()
        {
            if (marker != null) return true;
            var sprite = UiArtLibrary.Get(UiArtKeys.UI_INTERACTION_MARKER);
            if (sprite == null) return false;

            var go = new GameObject("InteractionMarker");
            marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = sprite;
            marker.sortingOrder = MARKER_SORTING_ORDER;
            float height = sprite.bounds.size.y;
            float scale = height > 0f ? MARKER_HEIGHT / height : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            marker.enabled = false;
            return true;
        }

        // PlayerInput SendMessages
        private void OnInteract(InputValue value)
        {
            if (!value.isPressed) return;

            // 메뉴·대화·폼 선택 등이 화면을 점유한 동안에는 상호작용을 받지 않는다.
            // 이 게이트가 없으면 로비 메뉴를 띄운 채 G로 던전에 입장할 수 있다
            // (대화 중 다른 NPC에게 말을 거는 기존 구멍도 같이 막힌다. 대사 진행은 Space/Enter라 영향 없음).
            //
            // ⚠️ 런 플레이어에는 아직 잠금 소스가 없다(PlayerCharacter에 입력 잠금 개념 자체가 없음).
            //    그래서 런 중 모달이 떠 있어도 이 게이트는 통과한다 — 알려진 구멍이고 후속 과제다.
            if (blocker != null && blocker.BlocksInteraction) return;

            if (current != null && current.CanInteract) current.Interact(gameObject);
        }
    }
}
