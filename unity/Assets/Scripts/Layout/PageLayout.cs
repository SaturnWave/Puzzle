#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>An upright rectangle of a screen, in points, y growing upwards. No Unity dependency.</summary>
    public readonly struct Box
    {
        /// <exception cref="ArgumentOutOfRangeException">A side is negative.</exception>
        public Box(float left, float bottom, float width, float height)
        {
            if (width < 0f || height < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"A box of {width} x {height} points has a side less than nothing.");
            }

            Left = left;
            Bottom = bottom;
            Width = width;
            Height = height;
        }

        public float Left { get; }

        public float Bottom { get; }

        public float Width { get; }

        public float Height { get; }

        public float Right => Left + Width;

        public float Top => Bottom + Height;

        public float MiddleX => Left + Width / 2;

        public float MiddleY => Bottom + Height / 2;

        /// <summary>The box of a given size around a middle.</summary>
        public static Box Around(float middleX, float middleY, float width, float height) =>
            new Box(middleX - width / 2, middleY - height / 2, width, height);

        public bool Contains(float x, float y) => x >= Left && x <= Right && y >= Bottom && y <= Top;

        public override string ToString() => $"{Left:0.#}..{Right:0.#} x {Bottom:0.#}..{Top:0.#}";
    }

    /// <summary>
    /// What every page of the museum has in common, from the top of the screen down: the cornice under the ceiling,
    /// the page's plaque, the picture rail that what hangs on the page hangs from, and at the foot the navigation
    /// bar. Lengths are in points (<see cref="ScreenView"/>). Like the board screen, a page is laid out for the
    /// tight 9:16 phone and gets more air on a taller one; a wider screen is as high as the tight phone and shows
    /// more wall at its sides. No Unity dependency.
    /// </summary>
    public sealed class PageLayout
    {
        /// <summary>From this much usable height on, a page has its full sizes and gaps (the board screen's rule).</summary>
        public const float RoomyHeight = ScreenLayout.RoomyHeight;

        /// <summary>The cornice shows this far under the top of the usable screen; above that it runs up to the screen's edge.</summary>
        public const float CorniceHeight = 16f;

        public const float PlaqueHeight = 54f;
        public const float WidestPlaque = 336f;
        public const float PlaqueSideMargin = 12f;

        /// <summary>The picture rail: a narrow moulding right under the plaque.</summary>
        public const float RailHeight = 7f;
        private const float GapPlaqueToRail = 8f;

        /// <summary>The bar's own measures at full size: its height above the usable foot of the screen, and how far the chosen destination's mark rises over its top edge.</summary>
        public const float BarHeight = 84f;
        public const float TabRise = 26f;
        public const float TightBarScale = 0.86f;

        /// <summary>A destination's place on the bar is at most this wide.</summary>
        public const float WidestTab = 104f;

        private PageLayout(ScreenView view, float roominess)
        {
            View = view;
            Roominess = roominess;
            BarScale = Between(TightBarScale, 1f);
            CorniceBottom = view.UsableTop - CorniceHeight;
            float plaqueTop = CorniceBottom - Between(9f, 15f);
            float plaqueWidth = Math.Min(WidestPlaque, view.Width - 2 * PlaqueSideMargin);
            Plaque = new Box(-plaqueWidth / 2, plaqueTop - PlaqueHeight, plaqueWidth, PlaqueHeight);
            RailTop = Plaque.Bottom - GapPlaqueToRail;
            BarTop = view.UsableBottom + BarHeight * BarScale;
        }

        public ScreenView View { get; }

        /// <summary>0 on the tight phone and on every wider screen, 1 from <see cref="RoomyHeight"/> on.</summary>
        public float Roominess { get; }

        /// <summary>The lower edge of the cornice.</summary>
        public float CorniceBottom { get; }

        /// <summary>The page's plaque: the room's name on the home, the museum's summary on the map.</summary>
        public Box Plaque { get; }

        public float RailTop { get; }

        public float RailBottom => RailTop - RailHeight;

        /// <summary>How large the bar and what stands on it are drawn.</summary>
        public float BarScale { get; }

        /// <summary>The bar's upper edge; the bar runs from there to the foot of the screen.</summary>
        public float BarTop { get; }

        /// <summary>The highest point of the bar: the top of the chosen destination's mark. A page keeps clear of it.</summary>
        public float BarReach => BarTop + TabRise * BarScale;

        public static PageLayout Of(ScreenShape screen)
        {
            ScreenView view = ScreenView.Of(screen);
            float usableHeight = view.UsableTop - view.UsableBottom;
            float roominess = Math.Clamp((usableHeight - ScreenView.DesignHeight) / (RoomyHeight - ScreenView.DesignHeight), 0f, 1f);
            return new PageLayout(view, roominess);
        }

        /// <summary>A length that is one thing on the tight screen and another on a roomy one, and between them in between.</summary>
        public float Between(float tight, float roomy) => tight + (roomy - tight) * Roominess;

        /// <summary>The middle of a destination's place on the bar, the places being centred as a row.</summary>
        /// <param name="index">Which place, counted from 0 at the left.</param>
        /// <param name="count">How many places the bar has.</param>
        /// <exception cref="ArgumentOutOfRangeException">The bar has no such place.</exception>
        public float TabMiddle(int index, int count)
        {
            if (count < 1 || index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"A bar of {count} places has no place {index}.");
            }

            return (index - (count - 1) / 2f) * TabWidth(count);
        }

        /// <summary>How wide each of the bar's places is: what a finger may hit.</summary>
        public float TabWidth(int count) => Math.Min(WidestTab, View.Width / count);
    }
}
