using System;

namespace Abyss.Runtime.Meta
{
    public sealed partial class MetaSaveService
    {
        /// <summary>화면 효과 설정만 저장한다. 실패하면 메모리 설정과 저장 식별값도 원복한다.</summary>
        public bool TrySaveScreenEffects(float shakeScale, bool reduceBossFlashes)
        {
            if (float.IsNaN(shakeScale) || float.IsInfinity(shakeScale) || shakeScale < 0f || shakeScale > 1f) return false;
            EnsureLoaded();
            if (IsSaveBlocked) return false;
            var previousSettings = current.settings;
            var settings = current.settings ?? new MetaSettings();
            float previousShake = settings.cameraShakeScale;
            bool previousFlashes = settings.reduceBossScreenFlashes;
            string previousTime = current.saveTimeUtc;
            int previousVersion = current.version;
            current.settings = settings;
            settings.cameraShakeScale = shakeScale;
            settings.reduceBossScreenFlashes = reduceBossFlashes;
            bool success = false;
            try { success = Save(); return success; }
            catch (Exception) { return false; }
            finally
            {
                if (!success)
                {
                    settings.cameraShakeScale = previousShake;
                    settings.reduceBossScreenFlashes = previousFlashes;
                    current.settings = previousSettings;
                    current.saveTimeUtc = previousTime;
                    current.version = previousVersion;
                }
            }
        }
    }
}
