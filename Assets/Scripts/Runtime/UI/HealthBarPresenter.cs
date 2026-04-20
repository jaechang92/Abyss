using Abyss.Runtime.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.UI
{
    /// <summary>
    /// 플레이어 HP를 Fill Image + Text로 표시.
    /// PlayerCharacter.OnHpChanged를 구독해 즉시 갱신.
    /// </summary>
    public sealed class HealthBarPresenter : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Text hpText;

        private PlayerCharacter player;

        public void Bind(PlayerCharacter target)
        {
            if (player != null) player.OnHpChanged -= HandleHpChanged;
            player = target;
            if (player != null)
            {
                player.OnHpChanged += HandleHpChanged;
                Refresh(player.CurrentHp, player.BaseHp);
            }
        }

        private void OnDisable()
        {
            if (player != null) player.OnHpChanged -= HandleHpChanged;
        }

        private void HandleHpChanged(int previousHp, int currentHp)
        {
            if (player != null) Refresh(currentHp, player.BaseHp);
        }

        private void Refresh(int current, int max)
        {
            if (fillImage != null && max > 0) fillImage.fillAmount = (float)current / max;
            if (hpText != null) hpText.text = $"{current}/{max}";
        }
    }
}
