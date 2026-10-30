#nullable enable
using System;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// Whether a touch is still a tap: a finger taps while it has gone no further than the slop from where it came
    /// down, across or up and down; once it has gone further it drags, and it stays a drag until it lifts, even if it
    /// comes back. A control is pressed only by a tap (<see cref="PressSet{TAction}"/>), so a sideways swipe across a
    /// card's row presses nothing; and a page is swiped only by a finger that has left the slop (<see cref="PageSwipe"/>).
    /// </summary>
    /// <remarks>
    /// Places are told in the points of what the finger is on, and the slop in the same points: the layout's for a
    /// page, a card's own for a card drawn smaller or larger than the layout. No Unity dependency, so the
    /// repository's tests compile it too.
    /// </remarks>
    public readonly struct TapSlop
    {
        /// <summary>A finger that went no further than this many of the layout's points from where it came down has not moved. About Android's own touch slop.</summary>
        public const float Points = 10f;

        private readonly float _downX;
        private readonly float _downY;
        private readonly float _slop;

        private TapSlop(float downX, float downY, float slop, bool isATap)
        {
            _downX = downX;
            _downY = downY;
            _slop = slop;
            IsATap = isATap;
        }

        /// <summary>True while the finger has gone no further than the slop from where it came down.</summary>
        public bool IsATap { get; }

        /// <summary>A finger came down at a place: it taps.</summary>
        /// <param name="slop">How far the finger may go and still tap, in the points its places are told in.</param>
        /// <exception cref="ArgumentOutOfRangeException">The slop is less than nothing, or is no number.</exception>
        public static TapSlop Down(float x, float y, float slop = Points) =>
            slop >= 0f
                ? new TapSlop(x, y, slop, isATap: true)
                : throw new ArgumentOutOfRangeException(nameof(slop), slop, "A finger may go no distance or some distance and still tap, never less than none.");

        /// <summary>The finger moved to a place: from the first place further than the slop on, it drags.</summary>
        public TapSlop MovedTo(float x, float y)
        {
            bool isWithinTheSlop = Math.Max(Math.Abs(x - _downX), Math.Abs(y - _downY)) <= _slop;
            return IsATap && isWithinTheSlop ? this : new TapSlop(_downX, _downY, _slop, isATap: false);
        }
    }
}
