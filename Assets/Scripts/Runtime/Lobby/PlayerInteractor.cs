using System.Collections.Generic;
using Abyss.Runtime.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Abyss.Runtime.Lobby
{
    /// <summary>
    /// 플레이어 근접 IInteractable을 추적하고 Interact 입력 시 최근접 대상의 Interact를 호출한다.
    /// 자식 트리거 콜라이더로 후보를 감지(이벤트 구동) — 매 프레임 OverlapCircle 폴링 회피.
    /// H2에서 NPC가 늘어나도 같은 파이프라인으로 동작한다(architect 자문).
    /// 입력은 PlayerInput SendMessages(OnInteract). g 키 바인딩, Hold 제거(탭).
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("현재 상호작용 대상 프롬프트 표시(없어도 동작).")]
        [SerializeField] private Text promptLabel;

        private readonly List<IInteractable> candidates = new List<IInteractable>();
        private IInteractable current;

        // 같은 오브젝트의 컨트롤러. 패널이 화면을 점유하는 동안 상호작용을 막는 게이트로 쓴다.
        private LobbyPlayerController player;

        private void Awake()
        {
            player = GetComponent<LobbyPlayerController>();
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

            bool show = current != null;
            if (promptLabel.gameObject.activeSelf != show) promptLabel.gameObject.SetActive(show);
            if (show) promptLabel.text = current.InteractionPrompt;
        }

        // PlayerInput SendMessages
        private void OnInteract(InputValue value)
        {
            if (!value.isPressed) return;

            // 메뉴·대화·폼 선택 등이 화면을 점유한 동안에는 상호작용을 받지 않는다.
            // 이 게이트가 없으면 로비 메뉴를 띄운 채 G로 던전에 입장할 수 있다
            // (대화 중 다른 NPC에게 말을 거는 기존 구멍도 같이 막힌다. 대사 진행은 Space/Enter라 영향 없음).
            if (player != null && player.InputLocked) return;

            if (current != null && current.CanInteract) current.Interact(gameObject);
        }
    }
}
