using System;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss.Runtime.Dialogue
{
    /// <summary>Portrait identity follows the localization key, never the translated speaker name.</summary>
    public sealed class DialoguePortraitPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string speakerKey;
            public Sprite portrait;
        }

        [SerializeField] private Image target;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public void Configure(Image image, Entry[] portraits)
        {
            target = image;
            entries = portraits ?? Array.Empty<Entry>();
            if (target != null)
            {
                target.preserveAspect = true;
                target.raycastTarget = false;
            }
            Clear();
        }

        public void ShowSpeaker(string speakerKey)
        {
            if (target == null) return;
            Sprite portrait = null;
            if (!string.IsNullOrEmpty(speakerKey) && entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || !string.Equals(entry.speakerKey, speakerKey, StringComparison.Ordinal)) continue;
                    portrait = entry.portrait;
                    break;
                }
            }
            target.sprite = portrait;
            target.enabled = portrait != null;
        }

        public void Clear()
        {
            if (target == null) return;
            target.sprite = null;
            target.enabled = false;
        }
    }
}
