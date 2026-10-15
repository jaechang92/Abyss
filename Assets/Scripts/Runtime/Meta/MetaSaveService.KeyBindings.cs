using System;

namespace Abyss.Runtime.Meta
{
    public sealed partial class MetaSaveService
    {
        /// <summary>실패 시 설정과 저장 시각을 되돌린다. 다른 메타 진행은 보존한다.</summary>
        public bool TrySaveKeyboardBindings(string json)
        {
            EnsureLoaded();
            if (IsSaveBlocked) return false;
            var previousSettings = current.settings;
            var settings = current.settings ?? new MetaSettings();
            string previous = settings.keyboardBindingOverrides;
            string previousTime = current.saveTimeUtc;
            int previousVersion = current.version;
            current.settings = settings;
            settings.keyboardBindingOverrides = json ?? string.Empty;
            bool success = false;
            try { success = Save(); return success; }
            catch (Exception) { return false; }
            finally
            {
                if (!success)
                {
                    settings.keyboardBindingOverrides = previous;
                    current.settings = previousSettings;
                    current.saveTimeUtc = previousTime;
                    current.version = previousVersion;
                }
            }
        }
    }
}
