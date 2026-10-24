#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>What a device's screen is, as far as turning it goes. A closed set.</summary>
    public enum ScreenClass
    {
        /// <summary>A phone, and a foldable's cover display: the game stands upright however the device is held.</summary>
        Phone,

        /// <summary>A tablet, an iPad, an open foldable: the game turns with the device, all four ways.</summary>
        Tablet,
    }

    /// <summary>One of the four ways a screen can stand, by Unity's own names, which the game's root turns them into. A closed set.</summary>
    public enum ScreenTurn
    {
        /// <summary>Upright, the device's foot down.</summary>
        Portrait,

        /// <summary>Upright, the device's foot up.</summary>
        PortraitUpsideDown,

        /// <summary>Sideways, the device's foot to the right.</summary>
        LandscapeLeft,

        /// <summary>Sideways, the device's foot to the left.</summary>
        LandscapeRight,
    }

    /// <summary>
    /// Which screens turn, as the owner decided on 2026-10-24: an iPad always, an Android device when the shorter side
    /// of the largest window it can give the game is at least <see cref="SmallestTabletSideDp"/> (a tablet, the open
    /// foldable), never a phone or a foldable's cover display. A screen that turns turns all four ways, upside down
    /// too: Apple asks every iPad app that runs beside other apps for all four, and an Android tablet is held either
    /// way up as often. While a level is played the screen keeps turning: iOS does not promise a lock, and a window
    /// beside another app changes its shape anyway. No Unity dependency.
    /// </summary>
    public static class ScreenTurns
    {
        /// <summary>Android's own line between a phone and a large screen, in density-independent points.</summary>
        public const float SmallestTabletSideDp = 600f;

        private static readonly ScreenTurn[] Upright = { ScreenTurn.Portrait };

        private static readonly ScreenTurn[] EveryWay =
        {
            ScreenTurn.Portrait, ScreenTurn.PortraitUpsideDown, ScreenTurn.LandscapeLeft, ScreenTurn.LandscapeRight,
        };

        /// <summary>
        /// What an Android screen is, by the shorter side of the largest window the device can give the game: the whole
        /// display it is shown on, not the window it has now, so a tablet split between two apps is not taken for a phone.
        /// </summary>
        /// <param name="smallestSideDp">That side, in Android's density-independent points.</param>
        /// <exception cref="ArgumentOutOfRangeException">The side is no length (<see cref="IsALength"/>).</exception>
        public static ScreenClass OfAnAndroidScreen(float smallestSideDp)
        {
            if (!IsALength(smallestSideDp))
            {
                throw new ArgumentOutOfRangeException(nameof(smallestSideDp), smallestSideDp, "A screen's side is a length of more than nothing.");
            }

            return smallestSideDp >= SmallestTabletSideDp ? ScreenClass.Tablet : ScreenClass.Phone;
        }

        /// <summary>
        /// True when a side read from a device is a length a screen can have: more than nothing and finite. Nothing,
        /// less, no number or no end is what a device that cannot tell its size reads as (no density, no window).
        /// </summary>
        public static bool IsALength(float sideDp) => sideDp > 0f && !float.IsInfinity(sideDp);

        /// <summary>What an Apple device's screen is, by the model iOS names: "iPad13,8" is a tablet, "iPhone15,2" a phone.</summary>
        public static ScreenClass OfAnAppleDevice(string model) =>
            model.StartsWith("iPad", StringComparison.Ordinal) ? ScreenClass.Tablet : ScreenClass.Phone;

        /// <summary>The ways the game lets a screen of a class stand: a phone upright only, a tablet all four ways.</summary>
        public static IReadOnlyList<ScreenTurn> AllowedFor(ScreenClass screen) => screen switch
        {
            ScreenClass.Phone => Upright,
            ScreenClass.Tablet => EveryWay,
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, "A class of screen the rule does not know."),
        };
    }
}
