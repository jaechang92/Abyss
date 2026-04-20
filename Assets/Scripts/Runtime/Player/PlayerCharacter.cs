using Abyss.Runtime.Form;
using UnityEngine;

namespace Abyss.Runtime.Player
{
    /// <summary>
    /// 플레이어 캐릭터 루트. 책임 분리를 위해 4 partial로 분할:
    /// Movement / Combat / Health / FormProxy.
    /// Unity 6: Rigidbody2D.linearVelocity 사용, FindAnyObjectByType 권장.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed partial class PlayerCharacter : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private FormController formController;

        public Rigidbody2D Body => body;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (formController == null) formController = GetComponent<FormController>();
            InitializeHealth();
        }

        private void Update()
        {
            UpdateGrounded();
            UpdateDashTimers();
        }

        private void FixedUpdate()
        {
            FixedUpdateMovement();
        }

        private void OnDrawGizmosSelected()
        {
            DrawMovementGizmos();
        }
    }
}
