#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// What a finger on a row of pages means at a moment, as <see cref="PageSwipe"/> reads it: nothing yet, a tap,
    /// a drag of the row, or the row let go to settle. A closed set: the private constructor keeps the kinds inside
    /// this file.
    /// </summary>
    public abstract class SwipeReading
    {
        private SwipeReading()
        {
        }

        /// <summary>
        /// No page moves: no finger is followed, the finger has not gone far enough to say what it does, or what it
        /// does is no swipe (it went up or down, or came down at the screen's edge), or it lifted after such a touch.
        /// </summary>
        public sealed class None : SwipeReading
        {
            public static readonly None Instance = new None();

            private None()
            {
            }
        }

        /// <summary>The finger lifted without ever going further than the slop from where it came down.</summary>
        public sealed class Tap : SwipeReading
        {
            public static readonly Tap Instance = new Tap();

            private Tap()
            {
            }
        }

        /// <summary>
        /// The finger drags the row. The page that is shown stands <see cref="By"/> points to the right of its place
        /// (less than nothing: to the left), and the page at <see cref="Beside"/> comes in next to it; nothing comes
        /// in beyond the row's first and last page, where the shown page only stretches a little way.
        /// </summary>
        public sealed class Drag : SwipeReading
        {
            public Drag(float by, int? beside)
            {
                By = by;
                Beside = beside;
            }

            public float By { get; }

            /// <summary>The place in the row of the page that comes in beside the shown one; nothing while none does.</summary>
            public int? Beside { get; }
        }

        /// <summary>The finger let go of the row: it settles with the page at <see cref="Place"/> shown, from where the drag left it.</summary>
        public sealed class Settle : SwipeReading
        {
            public Settle(int place, float from)
            {
                Place = place;
                From = from;
            }

            public int Place { get; }

            /// <summary>Where the page that was shown stood when the finger let go, as <see cref="Drag.By"/> says it.</summary>
            public float From { get; }
        }
    }

    /// <summary>A row of pages a finger can swipe through: how many there are, which is shown, and how wide the screen is that shows them.</summary>
    public readonly struct SwipeRow
    {
        /// <exception cref="ArgumentOutOfRangeException">The row has no page, the shown page is not one of it, or the screen has no width.</exception>
        public SwipeRow(int count, int shown, float width)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "A row of pages has at least one page.");
            }

            if (shown < 0 || shown >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(shown), shown, $"A row of {count} pages shows one of the places 0 to {count - 1}.");
            }

            if (!(width > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A screen that shows pages has a width.");
            }

            Count = count;
            Shown = shown;
            Width = width;
        }

        public int Count { get; }

        /// <summary>The place in the row of the page that is shown, counted from 0 at the left.</summary>
        public int Shown { get; }

        /// <summary>The width of the screen, in points: a page is that wide, and the next one stands that far beside it.</summary>
        public float Width { get; }
    }

    /// <summary>
    /// What a finger on a row of pages means: the museum's pages as the bar orders them, or the collection's rooms.
    /// A touch is a tap until the finger goes further than <see cref="Slop"/>; it becomes a swipe only when it then
    /// goes clearly sideways (<see cref="SidewaysOverUpright"/>), and one that goes up or down first stays no swipe
    /// until it lifts. While it swipes, the shown page follows the finger and its neighbour comes in beside it;
    /// beyond the row's ends the page resists, never stretching further than <see cref="StretchShare"/> of the
    /// screen. Let go, the row turns to the neighbour when the page was dragged <see cref="TurnShare"/> of the
    /// screen or flicked towards it (<see cref="FlickSpeed"/>), and springs back otherwise.
    /// </summary>
    /// <remarks>
    /// Places are told in the layout's points, from the middle of the screen, x growing to the right; times in the
    /// seconds of the stage's own clock. A touch that comes down within <see cref="BackGestureEdge"/> of the screen's
    /// left or right edge never swipes: on a phone with gesture navigation the system takes a swipe from there as
    /// its way back (the game hears it as the way back, or the touch is cancelled), and on one with buttons a tap
    /// there still presses what lies under it. The reading only says what the finger means; showing it is the
    /// stage's. This file has no Unity dependency, so the repository's tests compile it too.
    /// </remarks>
    public sealed class PageSwipe
    {
        /// <summary>A finger that went no further than this from where it came down has not moved: it taps, as it presses a control (<see cref="TapSlop"/>).</summary>
        public const float Slop = TapSlop.Points;

        /// <summary>A swipe goes at least this many times as far sideways as up or down.</summary>
        public const float SidewaysOverUpright = 2f;

        /// <summary>
        /// A touch that comes down this close to the screen's left or right edge is left to the system's back
        /// gesture. Android's zone is 24 dp, and up to about 40 at its highest sensitivity; a point is a little more
        /// than a dp on a phone, so this covers the whole zone.
        /// </summary>
        public const float BackGestureEdge = 32f;

        /// <summary>A page dragged this share of the screen's width turns to its neighbour when it is let go, however slowly.</summary>
        public const float TurnShare = 1f / 3f;

        /// <summary>A finger that lifts going at least this fast, in points a second, flicks the page on: to the neighbour it moved towards.</summary>
        public const float FlickSpeed = 500f;

        /// <summary>A flick turns the page only once it has dragged it at least this far, so a twitch at the end of a tap turns nothing.</summary>
        public const float FlickDistance = 24f;

        /// <summary>The finger's speed is measured over its last this many seconds; one that rested longer before it lifted has no speed.</summary>
        public const float FlickWindow = 0.1f;

        /// <summary>Beyond the row's ends the page stretches towards this share of the screen's width, and never reaches it.</summary>
        public const float StretchShare = 0.15f;

        /// <summary>No more of the finger's way than this many places is kept: the speed needs only its last tenth of a second.</summary>
        private const int MostPlacesKept = 32;

        private Finger? _finger;

        /// <summary>True from the moment the finger became a swipe until it lifted or was taken away.</summary>
        public bool IsDragging => _finger is { Swipes: true };

        /// <summary>A finger came down on a row of pages; a finger followed before is forgotten.</summary>
        public void Down(float x, float y, float seconds, SwipeRow row)
        {
            bool isAtTheEdge = Math.Abs(x) > row.Width / 2f - BackGestureEdge;
            _finger = new Finger(x, y, seconds, row, maySwipe: !isAtTheEdge);
        }

        /// <summary>The finger moved: the drag it makes, once it is a swipe; nothing before, or when it is no swipe.</summary>
        public SwipeReading MovedTo(float x, float y, float seconds)
        {
            if (_finger is not Finger finger)
            {
                return SwipeReading.None.Instance;
            }

            finger.Keep(x, seconds);
            finger.Touch = finger.Touch.MovedTo(x, y);
            float across = x - finger.DownX;
            float upright = y - finger.DownY;
            if (!finger.Swipes && finger.MaySwipe)
            {
                bool goesSideways = Math.Abs(across) > Slop && Math.Abs(across) > SidewaysOverUpright * Math.Abs(upright);
                if (goesSideways)
                {
                    // The page starts from where the finger left the slop, so it does not jump by the slop.
                    finger.Swipes = true;
                    finger.DragsFromX = finger.DownX + Math.Sign(across) * Slop;
                }
                else if (Math.Abs(upright) > Slop)
                {
                    finger.MaySwipe = false;
                }
            }

            return finger.Swipes ? DragOf(finger) : SwipeReading.None.Instance;
        }

        /// <summary>The finger lifted: where the row settles, after a swipe; a tap, or nothing, after any other touch.</summary>
        public SwipeReading Up(float seconds)
        {
            if (_finger is not Finger finger)
            {
                return SwipeReading.None.Instance;
            }

            _finger = null;
            if (!finger.Swipes)
            {
                return finger.Touch.IsATap ? SwipeReading.Tap.Instance : SwipeReading.None.Instance;
            }

            SwipeReading.Drag drag = DragOf(finger);
            return new SwipeReading.Settle(PlaceToSettleOn(finger.Row, drag, finger.SpeedAt(seconds)), drag.By);
        }

        /// <summary>The touch was taken away: a swipe springs back to the page that was shown; any other touch means nothing.</summary>
        public SwipeReading Cancel()
        {
            Finger? finger = _finger;
            _finger = null;
            return finger is { Swipes: true } ? new SwipeReading.Settle(finger.Row.Shown, DragOf(finger).By) : SwipeReading.None.Instance;
        }

        /// <summary>The drag a swiping finger makes: with a neighbour beside the page, the page follows the finger, no further than one page; without one, it stretches.</summary>
        private static SwipeReading.Drag DragOf(Finger finger)
        {
            SwipeRow row = finger.Row;
            float dragged = finger.LastX - finger.DragsFromX;
            int? beside = dragged > 0f && row.Shown > 0 ? row.Shown - 1
                : dragged < 0f && row.Shown < row.Count - 1 ? row.Shown + 1
                : (int?)null;
            return beside is int
                ? new SwipeReading.Drag(Math.Max(-row.Width, Math.Min(row.Width, dragged)), beside)
                : new SwipeReading.Drag(Math.Sign(dragged) * Stretched(Math.Abs(dragged), row.Width), beside: null);
        }

        /// <summary>A drag beyond the row's ends: the further the finger goes, the less the page follows it.</summary>
        private static float Stretched(float dragged, float width)
        {
            float limit = StretchShare * width;
            return limit * dragged / (dragged + limit);
        }

        /// <summary>
        /// The neighbour the page was dragged towards, when it was flicked on or dragged far enough without being
        /// flicked back; the page that was shown otherwise, and always where nothing stands beside it.
        /// </summary>
        private static int PlaceToSettleOn(SwipeRow row, SwipeReading.Drag drag, float speed)
        {
            if (drag.Beside is not int beside)
            {
                return row.Shown;
            }

            bool isFlicked = Math.Abs(speed) >= FlickSpeed;
            bool goesTowardsIt = Math.Sign(speed) == Math.Sign(drag.By);
            bool isFlickedOn = isFlicked && goesTowardsIt && Math.Abs(drag.By) >= FlickDistance;
            bool isFlickedBack = isFlicked && !goesTowardsIt;
            bool isFarEnough = Math.Abs(drag.By) >= TurnShare * row.Width;
            return isFlickedOn || (isFarEnough && !isFlickedBack) ? beside : row.Shown;
        }

        /// <summary>A finger on the row: where it came down, the way it went since, and what it has turned out to be.</summary>
        private sealed class Finger
        {
            /// <summary>The finger's last places across, with their times, oldest first.</summary>
            private readonly List<(float X, float Seconds)> _way = new List<(float, float)>();

            public Finger(float x, float y, float seconds, SwipeRow row, bool maySwipe)
            {
                DownX = x;
                DownY = y;
                Row = row;
                MaySwipe = maySwipe;
                Touch = TapSlop.Down(x, y, Slop);
                _way.Add((x, seconds));
            }

            public float DownX { get; }

            public float DownY { get; }

            public SwipeRow Row { get; }

            /// <summary>False once the touch can no longer become a swipe: it came down at the edge, or went up or down first.</summary>
            public bool MaySwipe { get; set; }

            /// <summary>Whether the finger still taps: it no longer does once it has gone further than the slop.</summary>
            public TapSlop Touch { get; set; }

            /// <summary>True once the touch has become a swipe; it stays one until the finger lifts.</summary>
            public bool Swipes { get; set; }

            /// <summary>Where across the swipe drags the page from: the point at which the finger left the slop.</summary>
            public float DragsFromX { get; set; }

            public float LastX => _way[_way.Count - 1].X;

            /// <summary>Keeps a place of the finger, and of the older ones only what the speed needs.</summary>
            public void Keep(float x, float seconds)
            {
                _way.Add((x, seconds));
                float windowOpens = seconds - FlickWindow;
                while (_way.Count > 2 && _way[1].Seconds <= windowOpens)
                {
                    _way.RemoveAt(0); // the one left first stays as the last place before the window
                }

                if (_way.Count > MostPlacesKept)
                {
                    _way.RemoveAt(0);
                }
            }

            /// <summary>How fast the finger went across as it lifted, in points a second to the right; nothing when it rested for longer than the window first.</summary>
            public float SpeedAt(float liftedAt)
            {
                (float lastX, float lastSeconds) = _way[_way.Count - 1];
                if (liftedAt - lastSeconds > FlickWindow)
                {
                    return 0f;
                }

                (float firstX, float firstSeconds) = _way[0];
                float seconds = lastSeconds - firstSeconds;
                return seconds > 0f ? (lastX - firstX) / seconds : 0f;
            }
        }
    }
}
