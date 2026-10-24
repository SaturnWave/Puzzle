#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where the world map's page lies (MapWorldTR, #4): under the page's plaque and rail hangs the map, a sheet in
    /// the gilt frame the paintings have, over the navigation bar. The sheet is the sea the map is drawn on; across
    /// its foot runs the strip of the twenty eras, and over that, the part the map's camera frames. What stands on
    /// the sheet is placed in the sheet's own points, from its top left corner, x to the right and y downwards, as
    /// the design canvas places it on its 316 by 456 map (<see cref="PageOf"/> turns them into the page's points).
    /// The design's phone has the sheet as wide as its 316; a taller screen gives it more height, a wider one, as
    /// high as the tight phone, more width up to the widest frame. No Unity dependency.
    /// </summary>
    public sealed class MapLayout
    {
        /// <summary>The frame is this wide on every side of the sheet (the frame's own drawing).</summary>
        public const float FrameWidth = HallLayout.FrameWidth;

        /// <summary>The widest sheet with its frame, and the wall it keeps at each side of a phone's screen.</summary>
        public const float WidestFrame = 384f;
        private const float WallBesideFrame = 10f;

        /// <summary>
        /// On a screen held sideways (<see cref="PageLayout.IsSideways"/>) the frame widens: its sheet is at most this
        /// many times as wide as the map over the strip is high, about the shape of the world the camera frames from
        /// California to Japan (the map's camera, MapCamera), and it keeps this much wall at each side of the screen.
        /// </summary>
        public const float WorldShape = 2.1f;
        private const float SidewaysWallBesideFrame = 28f;

        /// <summary>The strip of eras across the sheet's foot (MapWorldTR: 78 of the map's 456 points).</summary>
        public const float StripHeight = 78f;

        /// <summary>The wall over the frame and under it, on a tight screen and a roomy one.</summary>
        private const float TightWallOverTheFrame = 14f;
        private const float RoomyWallOverTheFrame = 22f;
        private const float TightWallUnderTheFrame = 12f;
        private const float RoomyWallUnderTheFrame = 20f;

        /// <summary>
        /// Below this the part of the sheet over the strip leaves the camera no room to frame an era's five pins with
        /// the card under them; the wall around the frame then narrows, as far as <see cref="LeastWallAroundTheFrame"/>.
        /// </summary>
        public const float LeastMapHeight = 280f;
        private const float LeastWallAroundTheFrame = 6f;

        private MapLayout(PageLayout page, Box sheet)
        {
            Page = page;
            Sheet = sheet;
        }

        public PageLayout Page { get; }

        /// <summary>The sheet the map is drawn on, inside its frame, in the page's points.</summary>
        public Box Sheet { get; }

        /// <summary>The frame's outer edge.</summary>
        public Box Frame => new Box(Sheet.Left - FrameWidth, Sheet.Bottom - FrameWidth, Sheet.Width + 2 * FrameWidth, Sheet.Height + 2 * FrameWidth);

        /// <summary>How wide the map is, in the sheet's points: the camera frames this width.</summary>
        public float MapWidth => Sheet.Width;

        /// <summary>How high the part of the sheet over the strip is: the camera frames this height.</summary>
        public float MapHeight => Sheet.Height - StripHeight;

        /// <summary>The strip of eras, in the page's points.</summary>
        public Box Strip => new Box(Sheet.Left, Sheet.Bottom, Sheet.Width, StripHeight);

        /// <summary>A point of the sheet, in its own points from its top left corner (y downwards), as the page has it.</summary>
        public (float X, float Y) PageOf(float sheetX, float sheetY) => (Sheet.Left + sheetX, Sheet.Top - sheetY);

        /// <summary>A point of the page as the sheet has it: what <see cref="PageOf"/> turns back.</summary>
        public (float X, float Y) SheetOf(float pageX, float pageY) => (pageX - Sheet.Left, Sheet.Top - pageY);

        public static MapLayout Of(ScreenShape screen)
        {
            PageLayout page = PageLayout.Of(screen);
            float wallOver = page.Between(TightWallOverTheFrame, RoomyWallOverTheFrame);
            float wallUnder = page.Between(TightWallUnderTheFrame, RoomyWallUnderTheFrame);
            float SheetHeightBetween(float over, float under) => SheetTopUnder(page, over) - (page.BarReach + under + FrameWidth);

            // Where the map would be too low for an era's pins, the wall around the frame narrows as far as it must.
            float missing = StripHeight + LeastMapHeight - SheetHeightBetween(wallOver, wallUnder);
            float canNarrow = wallOver + wallUnder - 2 * LeastWallAroundTheFrame;
            if (missing > 0f && canNarrow > 0f)
            {
                float narrowed = Math.Min(1f, missing / canNarrow);
                wallOver -= narrowed * (wallOver - LeastWallAroundTheFrame);
                wallUnder -= narrowed * (wallUnder - LeastWallAroundTheFrame);
            }

            float sheetTop = SheetTopUnder(page, wallOver);
            float sheetBottom = page.View.OnWholePixels(page.BarReach + wallUnder + FrameWidth);
            float frameWidth = FrameWidthOn(page, mapHeight: sheetTop - sheetBottom - StripHeight);
            float sheetWidth = page.View.OnWholePixels(frameWidth - 2 * FrameWidth);
            return new MapLayout(page, new Box(-sheetWidth / 2, sheetBottom, sheetWidth, sheetTop - sheetBottom));
        }

        /// <summary>
        /// How wide the frame is: as wide as the widest frame where the screen allows, and on a screen held sideways as
        /// wide as the whole world needs at the map's height, from California to Japan (<see cref="WorldShape"/>), as far
        /// as the usable screen leaves a wider wall beside it.
        /// </summary>
        private static float FrameWidthOn(PageLayout page, float mapHeight)
        {
            ScreenView view = page.View;
            float upright = Math.Min(WidestFrame, view.Width - 2 * WallBesideFrame);
            if (!page.IsSideways)
            {
                return upright;
            }

            float usableWidth = 2 * Math.Min(-view.UsableLeft, view.UsableRight);
            float theWorld = WorldShape * mapHeight + 2 * FrameWidth;
            return Math.Max(upright, Math.Min(theWorld, usableWidth - 2 * SidewaysWallBesideFrame));
        }

        /// <summary>The top of the sheet, inside the frame that hangs this far under the picture rail.</summary>
        private static float SheetTopUnder(PageLayout page, float wallOverTheFrame) =>
            page.View.OnWholePixels(page.RailBottom - wallOverTheFrame - FrameWidth);
    }
}
