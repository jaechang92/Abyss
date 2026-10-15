using System;

namespace Abyss.Runtime.Meta
{
    public sealed partial class MetaSaveService
    {
        public bool TrySaveReadability(float scale, bool contrast)
        {
            if (scale != 1f && scale != 1.25f && scale != 1.5f) return false;
            EnsureLoaded();
            if (IsSaveBlocked) return false;
            var previous = current.settings;
            var settings = previous ?? new MetaSettings();
            float oldScale = settings.narrativeTextScale;
            bool oldContrast = settings.highContrastNarrative;
            string oldTime = current.saveTimeUtc;
            int oldVersion = current.version;
            current.settings = settings;
            settings.narrativeTextScale = scale;
            settings.highContrastNarrative = contrast;
            bool saved = false;
            try { saved = Save(); return saved; }
            catch (Exception) { return false; }
            finally
            {
                if (!saved)
                {
                    settings.narrativeTextScale = oldScale;
                    settings.highContrastNarrative = oldContrast;
                    current.settings = previous;
                    current.saveTimeUtc = oldTime;
                    current.version = oldVersion;
                }
            }
        }
    }
}
