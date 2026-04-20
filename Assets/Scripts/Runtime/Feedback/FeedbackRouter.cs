using Abyss.Runtime.Enemy;
using Abyss.Runtime.Events;
using Abyss.Runtime.Form;
using Abyss.Runtime.Player;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>
    /// GameEvents 구독 → HitstopController·CameraShake 자동 트리거.
    /// 히트스탑 지속: 폼 교체 120ms, 피격 80ms, 보스 처치 250ms, 사망 300ms (stage-d-analyst §2).
    /// </summary>
    public sealed class FeedbackRouter : MonoBehaviour
    {
        [SerializeField] private CameraShake cameraShake;

        [Header("히트스탑 (초)")]
        [SerializeField, Min(0f)] private float formSwapHitstop = 0.12f;
        [SerializeField, Min(0f)] private float playerHitHitstop = 0.08f;
        [SerializeField, Min(0f)] private float bossKillHitstop = 0.25f;
        [SerializeField, Min(0f)] private float playerDeathHitstop = 0.3f;

        [Header("쉐이크 (magnitude, duration)")]
        [SerializeField] private Vector2 formSwapShake = new(0.15f, 0.2f);
        [SerializeField] private Vector2 playerHitShake = new(0.2f, 0.15f);
        [SerializeField] private Vector2 bossKillShake = new(0.3f, 0.3f);
        [SerializeField] private Vector2 playerDeathShake = new(0.4f, 0.4f);

        private PlayerCharacter player;

        private void OnEnable()
        {
            GameEvents.OnFormSwapped += HandleFormSwap;
            GameEvents.OnBossKilled += HandleBossKill;
            GameEvents.OnPlayerDead += HandlePlayerDead;

            BindPlayer();
        }

        private void OnDisable()
        {
            GameEvents.OnFormSwapped -= HandleFormSwap;
            GameEvents.OnBossKilled -= HandleBossKill;
            GameEvents.OnPlayerDead -= HandlePlayerDead;

            if (player != null) player.OnHpChanged -= HandleHpChanged;
        }

        private void Start()
        {
            BindPlayer();
        }

        private void BindPlayer()
        {
            if (player != null) return;
            player = FindAnyObjectByType<PlayerCharacter>();
            if (player != null) player.OnHpChanged += HandleHpChanged;
        }

        private void HandleFormSwap(FormData previous, FormData next)
        {
            TriggerHitstop(formSwapHitstop);
            TriggerShake(formSwapShake);
        }

        private void HandleHpChanged(int previousHp, int currentHp)
        {
            if (currentHp >= previousHp) return;
            TriggerHitstop(playerHitHitstop);
            TriggerShake(playerHitShake);
        }

        private void HandleBossKill()
        {
            TriggerHitstop(bossKillHitstop);
            TriggerShake(bossKillShake);
        }

        private void HandlePlayerDead()
        {
            TriggerHitstop(playerDeathHitstop);
            TriggerShake(playerDeathShake);
        }

        private static void TriggerHitstop(float seconds)
        {
            if (HitstopController.HasInstance)
            {
                HitstopController.Instance.Trigger(seconds);
            }
        }

        private void TriggerShake(Vector2 magDuration)
        {
            if (cameraShake == null) return;
            cameraShake.Shake(magDuration.x, magDuration.y);
        }
    }
}
