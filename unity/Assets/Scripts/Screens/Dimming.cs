using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The light a control is drawn in when it is not simply there to be pressed: held down by a finger, or out
    /// of use just now (a button whose price the player's coins do not cover, a booster that is set aside while
    /// another is aimed). The design canvas draws no control that is out of use; what it does draw is how
    /// everything looks that is out of reach, under the dark behind a card (WinTR, LoseTR). A dimmed control is
    /// drawn as if some of that dark lay on it alone, so it reads as out of reach on the cream of a card and on
    /// the wood of the shelf alike, and its words can still be read.
    /// </summary>
    public static class Dimming
    {
        /// <summary>
        /// What every picture and every line of a dimmed control is drawn through: what the dark behind a card
        /// (#04242B) makes of white where it lies at three quarters of the strength it has in a screen's middle.
        /// </summary>
        public static readonly Color Shade = new Color(0.557f, 0.614f, 0.626f, 1f);

        /// <summary>What the face of a control is drawn through while a finger holds it down: it loses a little of its light.</summary>
        public static readonly Color Held = new Color(0.88f, 0.88f, 0.88f, 1f);

        /// <summary>What a control's pictures and lines are drawn through: nothing while it is lit, the shade while it is dimmed.</summary>
        public static Color Of(bool isDimmed) => isDimmed ? Shade : Color.white;

        /// <summary>What a control's face is drawn through: the shade while it is dimmed, whether held or not; a little less light while it is held; nothing otherwise.</summary>
        public static Color Of(bool isDimmed, bool isHeld) => isDimmed ? Shade : isHeld ? Held : Color.white;
    }
}
