#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where everything of the entry page lies on a screen (EntryTR, drawn on a canvas 390 points wide): the palette of
    /// the app icon with the light behind it and the gems that float beside it, under it the title, the line under the
    /// title and the badge that says the title is a working name; lower down the bar that fills while the game starts,
    /// with its line, where "Başlamak için dokun" stands once it is full; at the foot the studio's name and the version.
    /// Lengths are in points (<see cref="ScreenView"/>). No Unity dependency.
    /// </summary>
    /// <remarks>
    /// The page is the canvas' drawing at a scale: the canvas' own on a phone as wide as it, less on the narrow phone,
    /// and at most a little more on a wide screen, so the inner screen of a fold shows the same page and more ground
    /// around it. A short screen draws it smaller still, until it fits the usable height (the inner screen of the
    /// Pixel Fold is 640 points high). Whatever height is left over lies between the parts, most of it over the
    /// palette and between the title's lines and the bar, as the canvas spreads its page over a tall phone; the
    /// footer keeps to the usable foot. Nothing is placed where a camera cutout or a system bar covers the screen.
    /// </remarks>
    public sealed class EntryLayout
    {
        /// <summary>The design canvas draws the entry page this many points wide.</summary>
        public const float CanvasWidth = 390f;

        /// <summary>
        /// On a wide screen the page is drawn no larger than the canvas draws it: larger, the logo's picture would be
        /// drawn with more pixels than it has.
        /// </summary>
        public const float LargestScale = 1f;

        /// <summary>
        /// The palette (with its brush) as it is seen in the logo's picture, the icon's art at the whole icon's size
        /// (art/icon/entry-logo-1024.png): its box in that picture's pixels, where they are more than a quarter opaque
        /// (the soft shades around reach further). Measure it again when the icon is drawn anew.
        /// </summary>
        public const float PictureSide = 1024f;
        private const float PaletteLeftInThePicture = 159f;
        private const float PaletteTopInThePicture = 196f;
        private const float PaletteRightInThePicture = 894f;
        private const float PaletteFootInThePicture = 917f;

        /// <summary>
        /// How wide the palette is drawn, in the canvas' points: a little wider than EntryTR's (208 of its picture
        /// of 380), as the page's one picture; 735 of the picture's pixels, so a phone draws it with no more pixels than it has.
        /// </summary>
        public const float PaletteWidth = 236f;

        /// <summary>The type sizes of the canvas, in its points.</summary>
        public const float TitleSize = 44f;
        public const float SubtitleSize = 15f;
        public const float BadgeSize = 10.5f;
        public const float LoadingLineSize = 13f;
        public const float TapSize = 24f;
        public const float FooterSize = 11f;

        /// <summary>From the palette's foot to the title's middle, and between the middles of the lines under it.</summary>
        private const float TitleUnderThePalette = 36f;
        private const float SubtitleUnderTheTitle = 38f;
        private const float BadgeUnderTheSubtitle = 27f;

        /// <summary>The badge around "çalışma adı": a pill this high, its line this far from its ends.</summary>
        public const float BadgeHeight = 18f;
        public const float BadgeEnds = 10f;

        /// <summary>The bar's gilt body, as wide and high as the canvas draws it; its line's middle this far under the bar's.</summary>
        public const float BarWidth = 260f;
        public const float BarHeight = 22f;
        private const float LoadingLineUnderTheBar = 25f;

        /// <summary>What the bar's part of the page reaches over the bar's middle (its rim) and under its line's middle.</summary>
        private const float OverTheBar = 13f;
        private const float UnderTheLoadingLine = 8f;

        /// <summary>The footer's middle over the usable foot, and its ends in from the screen's sides (EntryTR: bottom 22, padding 18).</summary>
        private const float FooterOverTheFoot = 28f;
        private const float FooterSideMargin = 18f;

        /// <summary>A line of the page keeps this much of the screen at each side.</summary>
        private const float LineSideMargin = 20f;

        /// <summary>Each part keeps at least this much air from the next, in the canvas' points at the page's scale.</summary>
        private const float SmallestGap = 12f;

        /// <summary>How the height left over is shared: over the palette, between the badge and the bar; the rest (a quarter) between the bar's line and the footer.</summary>
        private const float ShareOverThePalette = 0.48f;
        private const float ShareBetween = 0.27f;

        /// <summary>
        /// The gems that float beside the palette, from its middle in the canvas' points, and their sides: two beside
        /// its upper half, two beside its lower half, as EntryTR has four, moved clear of a long title.
        /// </summary>
        private static readonly (float X, float Y, float Side)[] GemsFromThePalette =
        {
            (-140f, 95f, 62f),
            (142f, 120f, 56f),
            (150f, -55f, 50f),
            (-148f, -80f, 46f),
        };

        private EntryLayout(ScreenView view, float scale, float paletteTop)
        {
            View = view;
            Scale = scale;
            float paletteHigh = PaletteHigh * scale;
            float paletteWide = PaletteWidth * scale;
            Palette = new Box(-paletteWide / 2f, paletteTop - paletteHigh, paletteWide, paletteHigh);

            float side = PaletteWidth / (PaletteRightInThePicture - PaletteLeftInThePicture) * PictureSide * scale;
            float pixel = side / PictureSide;
            Picture = new Box(Palette.Left - PaletteLeftInThePicture * pixel, Palette.Top + PaletteTopInThePicture * pixel - side, side, side);

            TitleMiddle = Palette.Bottom - TitleUnderThePalette * scale;
            SubtitleMiddle = TitleMiddle - SubtitleUnderTheTitle * scale;
            BadgeMiddle = SubtitleMiddle - BadgeUnderTheSubtitle * scale;

            float free = view.UsableTop - view.UsableBottom - scale * (GroupAHeight + GroupBHeight + FooterRoom);
            float barTop = BadgeMiddle - BadgeHeight / 2f * scale - free * ShareBetween;
            BarMiddle = barTop - OverTheBar * scale;
            LoadingLineMiddle = BarMiddle - LoadingLineUnderTheBar * scale;
            TapMiddle = (barTop + LoadingLineMiddle - UnderTheLoadingLine * scale) / 2f;
            FooterMiddle = view.UsableBottom + FooterOverTheFoot * scale;
            FooterLeft = view.UsableLeft + FooterSideMargin * scale;
            FooterRight = view.UsableRight - FooterSideMargin * scale;
            LineRoom = view.Width - 2f * LineSideMargin;

            var gems = new List<Box>();
            foreach ((float x, float y, float gemSide) in GemsFromThePalette)
            {
                gems.Add(Box.Around(Palette.MiddleX + x * scale, Palette.MiddleY + y * scale, gemSide * scale, gemSide * scale));
            }

            Gems = gems;
        }

        public ScreenView View { get; }

        /// <summary>How large the page is drawn against the canvas: a point of the canvas is this many points of the screen.</summary>
        public float Scale { get; }

        /// <summary>The palette as it is seen: the box of its opaque pixels.</summary>
        public Box Palette { get; }

        /// <summary>The square the logo's whole picture is drawn in: the palette, and see-through around it.</summary>
        public Box Picture { get; }

        /// <summary>The heights of the middles of the lines (each line is centred on its capitals, Lettering.Write).</summary>
        public float TitleMiddle { get; }

        public float SubtitleMiddle { get; }

        public float BadgeMiddle { get; }

        public float BarMiddle { get; }

        public float LoadingLineMiddle { get; }

        /// <summary>"Başlamak için dokun" stands in the middle of the place the bar and its line took.</summary>
        public float TapMiddle { get; }

        public float FooterMiddle { get; }

        /// <summary>Where the studio's name begins and where the version line ends.</summary>
        public float FooterLeft { get; }

        public float FooterRight { get; }

        /// <summary>How wide a line of the page may be: one that would be wider is set smaller.</summary>
        public float LineRoom { get; }

        /// <summary>The places of the gems that float beside the palette, in the order of the canvas' four.</summary>
        public IReadOnlyList<Box> Gems { get; }

        /// <summary>The palette's height in the canvas' points: its width, in the proportions of its box in the picture.</summary>
        private static float PaletteHigh =>
            PaletteWidth * (PaletteFootInThePicture - PaletteTopInThePicture) / (PaletteRightInThePicture - PaletteLeftInThePicture);

        /// <summary>The palette and the lines under it, from the palette's top to the badge's foot, in the canvas' points.</summary>
        private static float GroupAHeight => PaletteHigh + TitleUnderThePalette + SubtitleUnderTheTitle + BadgeUnderTheSubtitle + BadgeHeight / 2f;

        /// <summary>The bar and its line, from the bar's rim to under the line.</summary>
        private static float GroupBHeight => OverTheBar + LoadingLineUnderTheBar + UnderTheLoadingLine;

        /// <summary>What the footer takes over the usable foot, its line included.</summary>
        private static float FooterRoom => FooterOverTheFoot + FooterSize;

        /// <summary>The page on a screen.</summary>
        public static EntryLayout Of(ScreenShape screen)
        {
            ScreenView view = ScreenView.Of(screen);
            float usable = view.UsableTop - view.UsableBottom;
            float tallest = GroupAHeight + GroupBHeight + FooterRoom + 3f * SmallestGap;
            float scale = Math.Min(LargestScale, Math.Min(view.Width / CanvasWidth, usable / tallest));
            float free = usable - scale * (GroupAHeight + GroupBHeight + FooterRoom);
            float paletteTop = view.UsableTop - free * ShareOverThePalette;
            return new EntryLayout(view, scale, paletteTop);
        }
    }
}
