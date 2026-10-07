#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// A screen in pixels, with the strips at its top and bottom that a camera cutout or a system bar takes away.
    /// The game is played upright, so cutouts at the sides are not modelled.
    /// </summary>
    public readonly struct ScreenShape : IEquatable<ScreenShape>
    {
        /// <exception cref="ArgumentOutOfRangeException">The screen has no area, or the strips leave none of it.</exception>
        public ScreenShape(int pixelWidth, int pixelHeight, int topInsetPixels = 0, int bottomInsetPixels = 0)
        {
            if (pixelWidth < 1 || pixelHeight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelWidth), $"A screen of {pixelWidth}x{pixelHeight} px has no area.");
            }

            if (topInsetPixels < 0 || bottomInsetPixels < 0 || topInsetPixels + bottomInsetPixels >= pixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(topInsetPixels), $"Insets of {topInsetPixels} and {bottomInsetPixels} px do not fit a screen {pixelHeight} px high.");
            }

            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            TopInsetPixels = topInsetPixels;
            BottomInsetPixels = bottomInsetPixels;
        }

        public int PixelWidth { get; }

        public int PixelHeight { get; }

        public int TopInsetPixels { get; }

        public int BottomInsetPixels { get; }

        public bool Equals(ScreenShape other) =>
            PixelWidth == other.PixelWidth && PixelHeight == other.PixelHeight
            && TopInsetPixels == other.TopInsetPixels && BottomInsetPixels == other.BottomInsetPixels;

        public override bool Equals(object? obj) => obj is ScreenShape other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(PixelWidth, PixelHeight, TopInsetPixels, BottomInsetPixels);

        /// <summary>As the screenshot command writes a screen: 1080x2340, or 1080x2340+96+48 with its insets.</summary>
        public override string ToString() => TopInsetPixels == 0 && BottomInsetPixels == 0
            ? $"{PixelWidth}x{PixelHeight}"
            : $"{PixelWidth}x{PixelHeight}+{TopInsetPixels}+{BottomInsetPixels}";

        public static bool operator ==(ScreenShape left, ScreenShape right) => left.Equals(right);

        public static bool operator !=(ScreenShape left, ScreenShape right) => !left.Equals(right);
    }

    /// <summary>Where a card hangs in front of the board: the height of its middle (it is centred on the screen's width) and how large it is drawn.</summary>
    public readonly struct CardPlace
    {
        public CardPlace(float middle, float scale)
        {
            Middle = middle;
            Scale = scale;
        }

        /// <summary>The height of the card's middle, in the layout's points.</summary>
        public float Middle { get; }

        /// <summary>1 where the card fits at its full size, less where the screen is too small for it.</summary>
        public float Scale { get; }
    }

    /// <summary>What a board asks of the screen: its grid, and whether exit marks hang under its columns.</summary>
    public readonly struct BoardFootprint
    {
        /// <exception cref="ArgumentOutOfRangeException">The grid has no cell.</exception>
        public BoardFootprint(int columns, int rows, bool hasExits)
        {
            if (columns < 1 || rows < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), $"A board of {columns}x{rows} cells has no cell.");
            }

            Columns = columns;
            Rows = rows;
            HasExits = hasExits;
        }

        public int Columns { get; }

        public int Rows { get; }

        public bool HasExits { get; }
    }

    /// <summary>
    /// Where everything of the board screen lies: the plaques at the top, the shelf at the bottom, the board in its
    /// frame between them. Lengths are in points: the unit of the design, in which the narrowest supported screen is
    /// <see cref="DesignWidth"/> wide and the shortest <see cref="DesignHeight"/> high. A point is one world unit of
    /// the scene; the origin is the middle of the screen and y grows upwards. The board comes first: it takes the
    /// screen's width but for its frame, and where the height is what holds it back, the plaques and the shelf are
    /// drawn smaller (<see cref="HudScale"/>, <see cref="ShelfScale"/>) and stand closer to the edges. A screen has
    /// one of three shapes, or lies between two of them:
    /// <list type="bullet">
    /// <item>roomy, a tall phone: the width decides the cell, and the plaques and the shelf have their full size;</item>
    /// <item>tight, the 9:16 phone of the design: the width still decides most boards, and the rest is drawn a little smaller;</item>
    /// <item>compact, a screen wider than that (an open foldable, a tablet): the height decides every board, so the
    /// plaques and the shelf give up all the height they can. Such a screen is a large one in the hand, so they are
    /// still as large there as on a phone.</item>
    /// </list>
    /// This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class ScreenLayout
    {
        /// <summary>The design's phone: a 9:16 screen is exactly this many points; taller and wider screens show more wall.</summary>
        public const float DesignWidth = ScreenView.DesignWidth;
        public const float DesignHeight = ScreenView.DesignHeight;

        /// <summary>From this much usable height on, the plaques and the shelf have their full size and margins.</summary>
        public const float RoomyHeight = 760f;

        /// <summary>The height of the row of plaques at full size: the moves medallion's, the tallest of them.</summary>
        public const float HudHeight = 98f;

        /// <summary>The shelf's plank at full size.</summary>
        public const float ShelfHeight = 58f;

        /// <summary>From this much width on a screen is compact: even a board as wide as it is high is held back by the height.</summary>
        public const float WideWidth = 480f;

        /// <summary>On the tight screen the medallion is 80 points across instead of 98, and all else shrinks with it.</summary>
        public const float TightChromeScale = 80f / 98f;

        /// <summary>
        /// On a compact screen the plaques are drawn this large. The open foldable shows a point a quarter larger
        /// than a phone does, so its medallion is then as large in the hand as the tight phone's.
        /// </summary>
        public const float CompactHudScale = 0.65f;

        /// <summary>...and the shelf this large: a ledge for one line of lettering, which keeps its size (Shelf).</summary>
        public const float CompactShelfScale = 0.5f;

        /// <summary>The plaques hang at least this far under the top of the usable screen, and the shelf stands as far above its foot.</summary>
        public const float LeastWallAtTheEdges = 8f;

        /// <summary>
        /// The row of plaques is never wider than this at its full size, and less as it is drawn smaller: a plaque
        /// much wider than it is high looks empty. The row keeps this much wall at each side of a narrow screen.
        /// </summary>
        public const float WidestHud = 430f;
        public const float HudSideMargin = 10f;

        public const float WidestShelf = 366f;
        public const float ShelfSideMargin = 12f;

        /// <summary>
        /// The round button that leads out of the level stands on the shelf's left end: as large as most of the
        /// plank's height, never larger than the design's round button nor smaller than a fingertip can hit, its
        /// rim this far in from the plank's end.
        /// </summary>
        public const float LargestLeaveButton = 44f;
        public const float SmallestLeaveButton = 30f;
        public const float LeaveButtonShareOfPlank = 0.8f;
        public const float LeaveButtonInFromEnd = 9f;

        /// <summary>
        /// The gilt frame reaches this far past the board's cells on every side (8.1, and its soft edge). It is
        /// three quarters as wide as the web test page's: on a phone every point of it is taken from the cells.
        /// </summary>
        public const float FrameReach = 8.4f;

        /// <summary>The frame's shadow on the wall shows this far under the frame.</summary>
        public const float FrameShadowDrop = 8f;

        /// <summary>
        /// Room beside the cells: the frame, and a line of wall beyond it, so the frame is seen whole and does not
        /// run into the screen's edge. On a phone this is all the board does not get of the screen's width.
        /// </summary>
        public const float BoardMargin = FrameReach + 1f;

        /// <summary>
        /// Cells do not grow past this, so a small board does not turn into a few giant pieces: a cell this large
        /// is 12 mm on a phone. No board of the game's levels comes near it (9 to 11 columns and 10 to 13 rows: at
        /// most 38 points on a phone, 50 on the open foldable); it is there for a hand-made board of a few cells.
        /// </summary>
        public const float LargestCell = 64f;

        /// <summary>An exit mark hangs this far under the lowest cell of its column, clear of the frame.</summary>
        public const float ExitGap = FrameReach + 1.4f;

        /// <summary>The side of an exit mark, in cells: narrower than a cell, so it also clears the frame's next step.</summary>
        public const float ExitSizeInCells = 0.72f;

        /// <summary>A card that hangs in front of the board keeps this much wall at each side of a narrow screen, and this far from the plaques and the shelf.</summary>
        public const float CardSideMargin = 14f;
        public const float CardGapToChrome = 4f;

        private ScreenLayout(Chrome chrome, float cellSize, float boardLeft, float boardTop)
        {
            View = chrome.View;
            HudScale = chrome.HudScale;
            ShelfScale = chrome.ShelfScale;
            HudMiddle = chrome.HudMiddle;
            ShelfMiddle = chrome.ShelfMiddle;
            CellSize = cellSize;
            BoardLeft = boardLeft;
            BoardTop = boardTop;
        }

        /// <summary>The screen in points, as every screen of the game measures it.</summary>
        public ScreenView View { get; }

        public float PixelsPerPoint => View.PixelsPerPoint;

        /// <summary>The whole screen, in points.</summary>
        public float ViewWidth => View.Width;

        public float ViewHeight => View.Height;

        /// <summary>How large the plaques are drawn: 1 on a roomy screen, <see cref="TightChromeScale"/> on a tight one, <see cref="CompactHudScale"/> on a compact one.</summary>
        public float HudScale { get; }

        /// <summary>How large the shelf is drawn: as the plaques are, but for a compact screen, where it is <see cref="CompactShelfScale"/>.</summary>
        public float ShelfScale { get; }

        /// <summary>The height of the middle of the row of plaques. The row is centred on the screen's width.</summary>
        public float HudMiddle { get; }

        public float HudWidth => Math.Min(WidestHud * HudScale, ViewWidth - 2 * HudSideMargin);

        /// <summary>The lower edge of the row of plaques (their shadows fall a little below it).</summary>
        public float HudBottom => HudMiddle - HudHeight * HudScale / 2;

        /// <summary>The height of the middle of the shelf's plank. The shelf is centred on the screen's width.</summary>
        public float ShelfMiddle { get; }

        public float ShelfWidth => Math.Min(WidestShelf, ViewWidth - 2 * ShelfSideMargin);

        /// <summary>The upper edge of the shelf's plank.</summary>
        public float ShelfTop => ShelfMiddle + ShelfHeight * ShelfScale / 2;

        /// <summary>The disc of the round button that leads out of the level, on the shelf's left end.</summary>
        public Box LeaveButton
        {
            get
            {
                float size = Math.Clamp(LeaveButtonShareOfPlank * ShelfHeight * ShelfScale, SmallestLeaveButton, LargestLeaveButton);
                return Box.Around(-ShelfWidth / 2 + LeaveButtonInFromEnd + size / 2, ShelfMiddle, size, size);
            }
        }

        /// <summary>The side of a cell; a whole number of pixels, so every cell is drawn as wide as its neighbour.</summary>
        public float CellSize { get; }

        /// <summary>The left edge of the board's first column.</summary>
        public float BoardLeft { get; }

        /// <summary>The top edge of the board's first row.</summary>
        public float BoardTop { get; }

        /// <summary>The bare wall between the plaques and the board's cells: where the wall lamps hang, when it is enough.</summary>
        public float WallAboveBoard => HudBottom - BoardTop;

        /// <summary>
        /// The bare wall between a side of the screen and the board's frame, the same at both sides: a line on a
        /// phone, wide on a compact screen, where the wall lamps hang in it.
        /// </summary>
        public float WallBesideBoard => BoardLeft + ViewWidth / 2 - FrameReach;

        /// <summary>
        /// Where a pixel of the screen lies on the board, in cells: (0, 0) is the top left corner of the first cell,
        /// (2.5, 0.5) the middle of the third cell of the top row. Pixels are counted from the screen's lower left
        /// corner, as a device reports a touch. A pixel beside the board gives a place outside its cells.
        /// </summary>
        public (float Column, float Row) BoardPointAt(float pixelX, float pixelYFromBottom)
        {
            (float x, float y) = PointAt(pixelX, pixelYFromBottom);
            return ((x - BoardLeft) / CellSize, (BoardTop - y) / CellSize);
        }

        /// <summary>
        /// Where a pixel of the screen lies in the layout's own points: (0, 0) is the middle of the screen, y grows
        /// upwards. Pixels are counted from the screen's lower left corner, as a device reports a touch.
        /// </summary>
        public (float X, float Y) PointAt(float pixelX, float pixelYFromBottom) => View.PointAt(pixelX, pixelYFromBottom);

        /// <summary>The pixel of the screen at which a point of the layout lies: what <see cref="PointAt"/> turns back into that point.</summary>
        public (float PixelX, float PixelYFromBottom) PixelOfPoint(float x, float y) => View.PixelOfPoint(x, y);

        /// <summary>
        /// Where a card of a given size hangs in front of the board: centred on the screen's width and between the
        /// plaques and the shelf, at its full size where it fits there and smaller where it does not, so that
        /// nothing of it is cut off on any screen.
        /// </summary>
        /// <param name="width">The card's width at full size, in points.</param>
        /// <param name="height">The card's height at full size, with all that rides on its edges.</param>
        public CardPlace PlaceOfACard(float width, float height)
        {
            float roomTop = HudBottom - CardGapToChrome;
            float roomBottom = ShelfTop + CardGapToChrome;
            float widestScale = (ViewWidth - 2 * CardSideMargin) / width;
            float tallestScale = (roomTop - roomBottom) / height;
            return new CardPlace((roomTop + roomBottom) / 2, Math.Min(1f, Math.Min(widestScale, tallestScale)));
        }

        /// <summary>The pixel of the screen at which a place on the board lies: what <see cref="BoardPointAt"/> turns back into that place.</summary>
        public (float PixelX, float PixelYFromBottom) PixelAt(float column, float row) =>
            PixelOfPoint(BoardLeft + column * CellSize, BoardTop - row * CellSize);

        /// <summary>
        /// The largest board that fits between the plaques and the shelf, centred in that room. On a phone the
        /// width decides the cell, on a compact screen the height does.
        /// </summary>
        public static ScreenLayout Of(ScreenShape screen, BoardFootprint board)
        {
            Chrome chrome = Chrome.Of(screen);

            // Exit marks take a fixed gap and a part of a cell under the board, so they count as a little more than no row.
            float exitGap = board.HasExits ? ExitGap : 0f;
            float exitRows = board.HasExits ? ExitSizeInCells : 0f;
            float widestCell = (chrome.ViewWidth - 2 * BoardMargin) / board.Columns;
            float tallestCell = (chrome.RoomTop - chrome.RoomBottom - exitGap) / (board.Rows + exitRows);
            float cellSize = OnWholePixels(Math.Min(LargestCell, Math.Min(widestCell, tallestCell)), chrome.PixelsPerPoint);

            float blockHeight = board.Rows * cellSize + exitGap + exitRows * cellSize;
            float boardLeft = OnWholePixels(-board.Columns * cellSize / 2, chrome.PixelsPerPoint);
            float boardTop = OnWholePixels((chrome.RoomTop + chrome.RoomBottom) / 2 + blockHeight / 2, chrome.PixelsPerPoint);
            return new ScreenLayout(chrome, cellSize, boardLeft, boardTop);
        }

        /// <summary>A length in points, rounded down to a whole number of pixels.</summary>
        private static float OnWholePixels(float points, float pixelsPerPoint) =>
            (float)(Math.Floor(points * pixelsPerPoint) / pixelsPerPoint);

        /// <summary>
        /// What a screen alone decides, before any board: the size of a point, where the plaques and the shelf go, and
        /// the room they leave for a board's cells.
        /// </summary>
        private readonly struct Chrome
        {
            private Chrome(ScreenView view, float hudScale, float shelfScale, float hudMiddle, float shelfMiddle, float roomTop, float roomBottom)
            {
                View = view;
                HudScale = hudScale;
                ShelfScale = shelfScale;
                HudMiddle = hudMiddle;
                ShelfMiddle = shelfMiddle;
                RoomTop = roomTop;
                RoomBottom = roomBottom;
            }

            public ScreenView View { get; }

            public float PixelsPerPoint => View.PixelsPerPoint;

            public float ViewWidth => View.Width;

            public float HudScale { get; }

            public float ShelfScale { get; }

            public float HudMiddle { get; }

            public float ShelfMiddle { get; }

            /// <summary>The highest a board's first row may begin.</summary>
            public float RoomTop { get; }

            /// <summary>The lowest a board's cells, or the exit marks under them, may end.</summary>
            public float RoomBottom { get; }

            public static Chrome Of(ScreenShape screen)
            {
                ScreenView view = ScreenView.Of(screen);
                float viewWidth = view.Width;
                float viewHeight = view.Height;
                float usableTop = view.UsableTop;
                float usableBottom = view.UsableBottom;

                // A screen is taller than the tight one or wider than it, never both: the side that fits the design
                // exactly sets the size of a point. So every size and gap below has three ends, and lies between
                // the tight one and one of the other two.
                float roominess = Math.Clamp((usableTop - usableBottom - DesignHeight) / (RoomyHeight - DesignHeight), 0f, 1f);
                float wideness = Math.Clamp((viewWidth - DesignWidth) / (WideWidth - DesignWidth), 0f, 1f);
                float Between(float compact, float tight, float roomy) =>
                    tight + (roomy - tight) * roominess + (compact - tight) * wideness;

                float hudScale = Between(CompactHudScale, TightChromeScale, 1f);
                float shelfScale = Between(CompactShelfScale, TightChromeScale, 1f);
                float wallAboveHud = Between(LeastWallAtTheEdges, 12f, Math.Clamp(0.055f * viewHeight, 14f, 52f));
                float hudMiddle = usableTop - wallAboveHud - HudHeight * hudScale / 2;
                float shelfMiddle = usableBottom + Between(LeastWallAtTheEdges, 10f, 14f) + ShelfHeight * shelfScale / 2;
                float roomTop = hudMiddle - HudHeight * hudScale / 2 - Between(5f, 6f, 8f) - FrameReach;
                float roomBottom = shelfMiddle + ShelfHeight * shelfScale / 2 + Between(3f, 4f, 6f) + FrameShadowDrop + FrameReach;
                return new Chrome(view, hudScale, shelfScale, hudMiddle, shelfMiddle, roomTop, roomBottom);
            }
        }
    }
}
