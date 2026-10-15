using System;
using Abyss.Runtime.Meta;
using UnityEngine;

namespace Abyss.Runtime.Feedback
{
    /// <summary>카메라·보스 화면 효과의 저장된 선호. 전투 판정이나 히트스탑은 변경하지 않는다.</summary>
    public static class ScreenEffectPreferences
    {
        public static event Action OnChanged;

        public static float ShakeScale => NormalizeShakeScale(Current?.cameraShakeScale ?? 1f);
        public static bool ReduceBossFlashes => Current?.reduceBossScreenFlashes ?? false;
        private static MetaSettings Current => MetaSaveService.GetInstanceSafe()?.Current?.settings;

        public static float NormalizeShakeScale(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);

        public static bool TrySet(float shakeScale, bool reduceBossFlashes)
        {
            if (!MetaSaveService.Instance.TrySaveScreenEffects(shakeScale, reduceBossFlashes)) return false;
            OnChanged?.Invoke();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OnChanged = null;
    }
}
