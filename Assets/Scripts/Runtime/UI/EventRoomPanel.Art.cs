using Abyss.Runtime.ArtIntegration;
using UnityEngine;

namespace Abyss.Runtime.UI
{
    public sealed partial class EventRoomPanel
    {
        private System.Action<bool> worldArtResolved;
        private bool wasArtConsumed;

        private void ApplyEventArtwork(bool spent)
        {
            if (current == null || titleText == null || titleText.transform.parent is not RectTransform panel) return;
            // Outside the choice panel; never covers choice text or consumes navigation.
            WorldArtLibrary.ApplyUi(panel, "EventArtwork", WorldArtLibrary.EventKey(current.eventId, spent),
                new Rect(-PANEL_WIDTH * 0.5f - 202f, PANEL_HALF - 220f, 180f, 200f));
        }
    }
}
