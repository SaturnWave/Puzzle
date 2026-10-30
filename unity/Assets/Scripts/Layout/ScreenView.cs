#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// A screen measured in points, the unit every screen of the game is laid out in: the narrowest supported screen
    /// is <see cref="DesignWidth"/> points wide and the shortest <see cref="DesignHeight"/> high, so a screen is
    /// either exactly that, or taller, or wider, never smaller. A point is one world unit of the scene; the origin
    /// is the middle of the screen and y grows upwards. This file has no Unity dependency, so the repository's tests
    /// compile it too.
    /// </summary>
    public readonly struct ScreenView
    {
        /// <summary>The design's phone: a 9:16 screen is exactly this many points; taller and wider screens show more wall.</summary>
        public const float DesignWidth = 360f;
        public const float DesignHeight = 640f;

        private ScreenView(float pixelsPerPoint, float width, float height, float usableTop, float usableBottom, float usableLeft, float usableRight)
        {
            PixelsPerPoint = pixelsPerPoint;
            Width = width;
            Height = height;
            UsableTop = usableTop;
            UsableBottom = usableBottom;
            UsableLeft = usableLeft;
            UsableRight = usableRight;
        }

        public float PixelsPerPoint { get; }

        /// <summary>The whole screen, in points.</summary>
        public float Width { get; }

        public float Height { get; }

        /// <summary>The height under which a camera cutout or a system bar at the top no longer covers the screen.</summary>
        public float UsableTop { get; }

        /// <summary>The height above which a system bar at the foot no longer covers the screen.</summary>
        public float UsableBottom { get; }

        /// <summary>
        /// Where, from the left, a camera cutout at the screen's left side no longer covers it: the screen's left
        /// edge but on a device held sideways with its camera at that side. What is anchored to a side keeps inside.
        /// </summary>
        public float UsableLeft { get; }

        /// <summary>Where a camera cutout at the screen's right side begins: the screen's right edge on an upright screen.</summary>
        public float UsableRight { get; }

        public float Left => -Width / 2;

        public float Right => Width / 2;

        public float Top => Height / 2;

        public float Bottom => -Height / 2;

        /// <summary>What a screen looks like in points: the side that fits the design exactly sets the size of a point.</summary>
        public static ScreenView Of(ScreenShape screen)
        {
            float pixelsPerPoint = Math.Min(screen.PixelWidth / DesignWidth, screen.PixelHeight / DesignHeight);
            float width = screen.PixelWidth / pixelsPerPoint;
            float height = screen.PixelHeight / pixelsPerPoint;
            float usableTop = height / 2 - screen.TopInsetPixels / pixelsPerPoint;
            float usableBottom = -height / 2 + screen.BottomInsetPixels / pixelsPerPoint;
            float usableLeft = -width / 2 + screen.LeftInsetPixels / pixelsPerPoint;
            float usableRight = width / 2 - screen.RightInsetPixels / pixelsPerPoint;
            return new ScreenView(pixelsPerPoint, width, height, usableTop, usableBottom, usableLeft, usableRight);
        }

        /// <summary>
        /// Where a pixel of the screen lies in points. Pixels are counted from the screen's lower left corner, as
        /// a device reports a touch.
        /// </summary>
        public (float X, float Y) PointAt(float pixelX, float pixelYFromBottom) =>
            (pixelX / PixelsPerPoint - Width / 2, pixelYFromBottom / PixelsPerPoint - Height / 2);

        /// <summary>The pixel of the screen at which a point lies: what <see cref="PointAt"/> turns back into that point.</summary>
        public (float PixelX, float PixelYFromBottom) PixelOfPoint(float x, float y) =>
            ((x + Width / 2) * PixelsPerPoint, (y + Height / 2) * PixelsPerPoint);

        /// <summary>A length in points, rounded down to a whole number of pixels: what is put there is drawn sharp.</summary>
        public float OnWholePixels(float points) => (float)(Math.Floor(points * PixelsPerPoint) / PixelsPerPoint);
    }
}
