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
    /// the scene; the origin is the middle of the screen and y grows upwards. The board comes first: on a short
    /// screen the plaques and the shelf are drawn smaller (<see cref="ChromeScale"/>) and stand closer to the edges,
    /// and the cells take what room is left. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public sealed class ScreenLayout
    {
        /// <summary>The design's phone: a 9:16 screen is exactly this many points; taller and wider screens show more wall.</summary>
        public const float DesignWidth = 360f;
        public const float DesignHeight = 640f;

        /// <summary>From this much usable height on, the plaques and the shelf have their full size and margins.</summary>
        public const float RoomyHeight = 760f;

        /// <summary>The height of the row of plaques at full size: the moves medallion's, the tallest of them.</summary>
        public const float HudHeight = 98f;

        /// <summary>The shelf's plank at full size.</summary>
        public const float ShelfHeight = 58f;

        /// <summary>On the shortest screen the medallion is 80 points across instead of 98, and all else shrinks with it.</summary>
        public const float SmallestChromeScale = 80f / 98f;

        /// <summary>The row of plaques is never wider than this, and keeps this much wall at each side of a narrow screen.</summary>
        public const float WidestHud = 350f;
        public const float HudSideMargin = 10f;

        public const float WidestShelf = 366f;
        public const float ShelfSideMargin = 12f;

        /// <summary>The gilt frame reaches this far past the board's cells on every side (10.6, and its soft edge).</summary>
        public const float FrameReach = 11f;

        /// <summary>The frame's shadow on the wall shows this far under the frame.</summary>
        public const float FrameShadowDrop = 8f;

        /// <summary>Room beside the cells: the frame, and 4 points of wall beyond it.</summary>
        public const float BoardMargin = FrameReach + 4f;

        /// <summary>Cells do not grow past this, so a small board does not turn into a few giant pieces.</summary>
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
            PixelsPerPoint = chrome.PixelsPerPoint;
            ViewWidth = chrome.ViewWidth;
            ViewHeight = chrome.ViewHeight;
            ChromeScale = chrome.Scale;
            HudMiddle = chrome.HudMiddle;
            ShelfMiddle = chrome.ShelfMiddle;
            CellSize = cellSize;
            BoardLeft = boardLeft;
            BoardTop = boardTop;
        }

        public float PixelsPerPoint { get; }

        /// <summary>The whole screen, in points.</summary>
        public float ViewWidth { get; }

        public float ViewHeight { get; }

        /// <summary>How large the plaques and the shelf are drawn: 1 on a roomy screen, down to <see cref="SmallestChromeScale"/>.</summary>
        public float ChromeScale { get; }

        /// <summary>The height of the middle of the row of plaques. The row is centred on the screen's width.</summary>
        public float HudMiddle { get; }

        public float HudWidth => Math.Min(WidestHud, ViewWidth - 2 * HudSideMargin);

        /// <summary>The lower edge of the row of plaques (their shadows fall a little below it).</summary>
        public float HudBottom => HudMiddle - HudHeight * ChromeScale / 2;

        /// <summary>The height of the middle of the shelf's plank. The shelf is centred on the screen's width.</summary>
        public float ShelfMiddle { get; }

        public float ShelfWidth => Math.Min(WidestShelf, ViewWidth - 2 * ShelfSideMargin);

        /// <summary>The upper edge of the shelf's plank.</summary>
        public float ShelfTop => ShelfMiddle + ShelfHeight * ChromeScale / 2;

        /// <summary>The side of a cell; a whole number of pixels, so every cell is drawn as wide as its neighbour.</summary>
        public float CellSize { get; }

        /// <summary>The left edge of the board's first column.</summary>
        public float BoardLeft { get; }

        /// <summary>The top edge of the board's first row.</summary>
        public float BoardTop { get; }

        /// <summary>The bare wall between the plaques and the board's cells: where the wall lamps hang, when it is enough.</summary>
        public float WallAboveBoard => HudBottom - BoardTop;

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
        public (float X, float Y) PointAt(float pixelX, float pixelYFromBottom) =>
            (pixelX / PixelsPerPoint - ViewWidth / 2, pixelYFromBottom / PixelsPerPoint - ViewHeight / 2);

        /// <summary>The pixel of the screen at which a point of the layout lies: what <see cref="PointAt"/> turns back into that point.</summary>
        public (float PixelX, float PixelYFromBottom) PixelOfPoint(float x, float y) =>
            ((x + ViewWidth / 2) * PixelsPerPoint, (y + ViewHeight / 2) * PixelsPerPoint);

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
        /// The largest board that fits between the plaques and the shelf, centred in that room. On a tall phone the
        /// width decides the cell, on a nearly square screen the height does.
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
            private Chrome(float pixelsPerPoint, float viewWidth, float viewHeight, float scale, float hudMiddle, float shelfMiddle, float roomTop, float roomBottom)
            {
                PixelsPerPoint = pixelsPerPoint;
                ViewWidth = viewWidth;
                ViewHeight = viewHeight;
                Scale = scale;
                HudMiddle = hudMiddle;
                ShelfMiddle = shelfMiddle;
                RoomTop = roomTop;
                RoomBottom = roomBottom;
            }

            public float PixelsPerPoint { get; }

            public float ViewWidth { get; }

            public float ViewHeight { get; }

            public float Scale { get; }

            public float HudMiddle { get; }

            public float ShelfMiddle { get; }

            /// <summary>The highest a board's first row may begin.</summary>
            public float RoomTop { get; }

            /// <summary>The lowest a board's cells, or the exit marks under them, may end.</summary>
            public float RoomBottom { get; }

            public static Chrome Of(ScreenShape screen)
            {
                float pixelsPerPoint = Math.Min(screen.PixelWidth / DesignWidth, screen.PixelHeight / DesignHeight);
                float viewWidth = screen.PixelWidth / pixelsPerPoint;
                float viewHeight = screen.PixelHeight / pixelsPerPoint;
                float usableTop = viewHeight / 2 - screen.TopInsetPixels / pixelsPerPoint;
                float usableBottom = -viewHeight / 2 + screen.BottomInsetPixels / pixelsPerPoint;

                // 0 on the shortest screen, 1 from the roomy height on; every size and gap below lies between its two ends.
                float roominess = Math.Clamp((usableTop - usableBottom - DesignHeight) / (RoomyHeight - DesignHeight), 0f, 1f);
                float Between(float tight, float roomy) => tight + (roomy - tight) * roominess;

                float scale = Between(SmallestChromeScale, 1f);
                float wallAboveHud = Between(12f, Math.Clamp(0.055f * viewHeight, 14f, 52f));
                float hudMiddle = usableTop - wallAboveHud - HudHeight * scale / 2;
                float shelfMiddle = usableBottom + Between(10f, 14f) + ShelfHeight * scale / 2;
                float roomTop = hudMiddle - HudHeight * scale / 2 - Between(6f, 8f) - FrameReach;
                float roomBottom = shelfMiddle + ShelfHeight * scale / 2 + Between(4f, 6f) + FrameShadowDrop + FrameReach;
                return new Chrome(pixelsPerPoint, viewWidth, viewHeight, scale, hudMiddle, shelfMiddle, roomTop, roomBottom);
            }
        }
    }
}
