using System;

namespace Abyss.Runtime.Meta
{
    public sealed partial class MetaSaveService
    {
        public bool TrySaveMenuTextScale(float scale)
        {
            if (scale != 1f && scale != 1.25f && scale != 1.5f) return false;
            EnsureLoaded();
            if (IsSaveBlocked) return false;
            var previous = current.settings;
            var settings = previous ?? new MetaSettings();
            float oldScale = settings.menuTextScale;
            string oldTime = current.saveTimeUtc;
            int oldVersion = current.version;
            current.settings = settings;
            settings.menuTextScale = scale;
            bool saved = false;
            try { saved = Save(); return saved; }
            catch (Exception) { return false; }
            finally
            {
                if (!saved)
                {
                    settings.menuTextScale = oldScale;
                    current.settings = previous;
                    current.saveTimeUtc = oldTime;
                    current.version = oldVersion;
                }
            }
        }
    }
}
