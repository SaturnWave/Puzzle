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

    /// <summary>What a page has between its cornice and its plaque. A closed set.</summary>
    public enum PageTop
    {
        /// <summary>The row of the player's coins and lives: the museum's hall, map and collection.</summary>
        WalletRow,

        /// <summary>Nothing: the plaque hangs right under the cornice (the jigsaw, whose plaque counts its pieces).</summary>
        PlaqueUnderTheCornice,
    }

    /// <summary>
    /// What every page of the museum has in common, from the top of the screen down: the cornice under the ceiling,
    /// the row of the player's coins and lives, the page's plaque, the picture rail that what hangs on the page
    /// hangs from, and at the foot the navigation bar. Lengths are in points (<see cref="ScreenView"/>). Like the
    /// board screen, a page is laid out for the tight 9:16 phone and gets more air on a taller one; a wider screen
    /// is as high as the tight phone and shows more wall at its sides. What the row takes of the height comes out
    /// of what hangs on each page, which gives it up in the order its own layout states. No Unity dependency.
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

        /// <summary>
        /// The wallet row is drawn as wide as the plaque on a roomy screen and as much smaller on a tight one as the
        /// bar is; it keeps this much wall under the cornice and over the plaque, on a tight screen and a roomy one.
        /// The canvas leaves 39 points over its row for a phone's status bar, which the game's screen does not
        /// count (<see cref="ScreenView.UsableTop"/>), and 9 under it.
        /// </summary>
        public const float WalletRowTightScale = TightBarScale;
        private const float TightGapOverTheRow = 5f;
        private const float RoomyGapOverTheRow = 12f;
        private const float TightGapUnderTheRow = 6f;
        private const float RoomyGapUnderTheRow = 10f;

        /// <summary>A page without the row hangs its plaque this far under the cornice, on a tight screen and a roomy one.</summary>
        private const float TightGapUnderTheCornice = 9f;
        private const float RoomyGapUnderTheCornice = 15f;

        /// <summary>The picture rail: a narrow moulding right under the plaque.</summary>
        public const float RailHeight = 7f;
        private const float GapPlaqueToRail = 8f;

        /// <summary>The bar's own measures at full size: its height above the usable foot of the screen, and how far the chosen destination's mark rises over its top edge.</summary>
        public const float BarHeight = 84f;
        public const float TabRise = 26f;
        public const float TightBarScale = 0.86f;

        /// <summary>A destination's place on the bar is at most this wide.</summary>
        public const float WidestTab = 104f;

        /// <summary>
        /// A card that hangs in front of a page has the whole usable screen for itself: the dark it hangs in
        /// front of lies over the plaque and over the bar too. It keeps this much wall at each side of a narrow
        /// screen, as a card in front of the board does, and this far from the usable screen's top and foot.
        /// </summary>
        public const float CardSideMargin = ScreenLayout.CardSideMargin;
        public const float CardMarginAboveAndBelow = 8f;

        /// <summary>
        /// A card's middle lies this far above the middle of the usable screen where there is room for that, as
        /// the design hangs its cards (LevelStartTR and SettingsTR: what hangs there has its middle 13.5 points
        /// above the middle of the canvas).
        /// </summary>
        public const float CardAboveTheMiddle = 13.5f;

        private PageLayout(ScreenView view, float roominess, PageTop top)
        {
            View = view;
            Roominess = roominess;
            BarScale = Between(TightBarScale, 1f);
            CorniceBottom = view.UsableTop - CorniceHeight;
            float plaqueWidth = Math.Min(WidestPlaque, view.Width - 2 * PlaqueSideMargin);
            float plaqueTop;
            switch (top)
            {
                case PageTop.WalletRow:
                    WalletRowScale = plaqueWidth / WalletRowLayout.Width * Between(WalletRowTightScale, 1f);
                    float rowWidth = WalletRowLayout.Width * WalletRowScale;
                    float rowHeight = WalletRowLayout.Height * WalletRowScale;
                    WalletRow = new Box(-rowWidth / 2, CorniceBottom - Between(TightGapOverTheRow, RoomyGapOverTheRow) - rowHeight, rowWidth, rowHeight);
                    plaqueTop = WalletRow.Bottom - Between(TightGapUnderTheRow, RoomyGapUnderTheRow);
                    break;
                case PageTop.PlaqueUnderTheCornice:
                    WalletRow = new Box(0f, CorniceBottom, 0f, 0f);
                    plaqueTop = CorniceBottom - Between(TightGapUnderTheCornice, RoomyGapUnderTheCornice);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(top), top, "A page's top that the layout does not know.");
            }

            Plaque = new Box(-plaqueWidth / 2, plaqueTop - PlaqueHeight, plaqueWidth, PlaqueHeight);
            RailTop = Plaque.Bottom - GapPlaqueToRail;
            BarTop = view.UsableBottom + BarHeight * BarScale;
        }

        public ScreenView View { get; }

        /// <summary>0 on the tight phone and on every wider screen, 1 from <see cref="RoomyHeight"/> on.</summary>
        public float Roominess { get; }

        /// <summary>The lower edge of the cornice.</summary>
        public float CorniceBottom { get; }

        /// <summary>
        /// The row of the player's coins and lives, centred over the plaque: its box is the coin's and the heart's
        /// height and the canvas' row's width (<see cref="WalletRowLayout"/>) at <see cref="WalletRowScale"/>. A
        /// page without the row has an empty box at the cornice's lower edge.
        /// </summary>
        public Box WalletRow { get; }

        /// <summary>How large the wallet row is drawn: a share of the canvas' points; nothing on a page without the row.</summary>
        public float WalletRowScale { get; }

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

        /// <param name="top">What stands between the cornice and the plaque: the wallet row on the museum's pages.</param>
        public static PageLayout Of(ScreenShape screen, PageTop top = PageTop.WalletRow)
        {
            ScreenView view = ScreenView.Of(screen);
            float usableHeight = view.UsableTop - view.UsableBottom;
            float roominess = Math.Clamp((usableHeight - ScreenView.DesignHeight) / (RoomyHeight - ScreenView.DesignHeight), 0f, 1f);
            return new PageLayout(view, roominess, top);
        }

        /// <summary>A length that is one thing on the tight screen and another on a roomy one, and between them in between.</summary>
        public float Between(float tight, float roomy) => tight + (roomy - tight) * Roominess;

        /// <summary>
        /// True for a screen held sideways with room beside the upright column: one at least <see cref="SidewaysShape"/>
        /// times as wide as it is high, as a tablet held sideways is (the 12.9" iPad 854 by 640 points, a 16:10 tablet
        /// 1024). The pages that gain most from the width lay themselves out for it there: the map's frame widens and
        /// the jigsaw puts its tray beside the painting. The open foldable held sideways (663 by 640) is nearly square
        /// and keeps the upright pages, as every page keeps them on a phone.
        /// </summary>
        public bool IsSideways => View.Width >= SidewaysShape * View.Height;

        /// <summary>A screen at least this many times as wide as it is high counts as one held sideways (<see cref="IsSideways"/>).</summary>
        public const float SidewaysShape = 1.25f;

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

        /// <summary>
        /// Where a card of a given size hangs in front of the page: centred on the screen's width, a little above
        /// the middle of the usable screen, at its full size where it fits there and smaller where it does not. A
        /// card is made smaller before anything of it is cut off, and it never leaves the usable screen: where
        /// the room is short, it gives up its place above the middle first.
        /// </summary>
        /// <param name="width">The card's width at full size, in points.</param>
        /// <param name="height">The card's height at full size, with all that rides on its edges.</param>
        /// <exception cref="ArgumentOutOfRangeException">The card has no size.</exception>
        public CardPlace PlaceOfACard(float width, float height)
        {
            if (!(width > 0f && height > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"A card of {width} x {height} points has no size.");
            }

            float roomTop = View.UsableTop - CardMarginAboveAndBelow;
            float roomBottom = View.UsableBottom + CardMarginAboveAndBelow;
            float widestScale = (View.Width - 2 * CardSideMargin) / width;
            float tallestScale = (roomTop - roomBottom) / height;
            float scale = Math.Min(1f, Math.Min(widestScale, tallestScale));

            // A card that fills the room has one place only; rounding must not turn that into none.
            float halfHeight = scale * height / 2;
            float lowest = roomBottom + halfHeight;
            float highest = Math.Max(lowest, roomTop - halfHeight);
            float wished = (roomTop + roomBottom) / 2 + CardAboveTheMiddle;
            return new CardPlace(Math.Clamp(wished, lowest, highest), scale);
        }
    }
}
