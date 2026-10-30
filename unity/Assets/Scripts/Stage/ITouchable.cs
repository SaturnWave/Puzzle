#nullable enable
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>
    /// What the device's touches and its way back are told to: the player's session, or before it the entry page the
    /// game opens on. Pixels are counted from the screen's lower left corner, as a device reports a touch.
    /// </summary>
    public interface ITouchable
    {
        /// <summary>A finger came down on the screen.</summary>
        void PointerDown(Vector2 pixel);

        /// <summary>The finger moved while it was down.</summary>
        void PointerMoved(Vector2 pixel);

        /// <summary>The finger lifted.</summary>
        void PointerUp();

        /// <summary>The touch was taken away by the device, or the game lost the screen while a finger was down.</summary>
        void PointerCancelled();

        /// <summary>One more finger came down while the finger the game follows is still on the screen.</summary>
        void AnotherFingerDown();

        /// <summary>The player asked for the way back, by the device's back button or gesture.</summary>
        void GoBack();
    }
}
