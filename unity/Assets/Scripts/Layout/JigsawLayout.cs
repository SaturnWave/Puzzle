#nullable enable
using System;
using System.Linq;
using Puzzle.Museum;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where everything of a painting's jigsaw lies, as the design canvas draws that screen (JigsawTR), from the top
    /// down: the piece counter's plaque under the cornice with the round button that leads back beside it, the
    /// picture rail, the painting in its frame with its brass plate, the bar of progress pips, the wooden tray with
    /// the pieces that wait, and the button that places them all. The painting comes first: it is as large as its
    /// proportions and the screen allow; what the screen has in height beyond that is shared out between the gaps,
    /// so a tall phone shows a calm wall instead of one large hole. Lengths are in points. No Unity dependency.
    /// </summary>
    public sealed class JigsawLayout
    {
        /// <summary>The largest picture: on the open foldable it may be wider than the hall's, the better to put pieces into.</summary>
        public const float WidestPicture = 400f;
        private const float WallBesideFrame = 9f;

        public const float PlaqueWidth = 148f;

        /// <summary>The round button that leads back, at full size, and how far it keeps from the screen's side.</summary>
        public const float BackButtonSize = 44f;
        private const float BackButtonFromSide = 13f;

        /// <summary>A bar of pips, at full size; a painting of more than <see cref="MostPipsInARow"/> pieces has two of them, this far apart.</summary>
        public const float PipBarHeight = 22f;
        public const int MostPipsInARow = 20;
        private const float GapBetweenPipBars = 4f;
        private const float DesignPipBarWidth = 181f;

        /// <summary>The hint's pill rides on the tray's top edge: it is this high and its middle lies this far under that edge.</summary>
        public const float HintHeight = 24f;
        private const float HintMiddleUnderTrayTop = 1f;

        /// <summary>The tray: its widest, and how far the velvet of its well lies in from its sides, its top and its foot.</summary>
        private const float WidestTray = 430f;
        private const float TraySideMargin = 9f;
        public const float WellInFromSides = 16f;
        public const float WellUnderTop = 24f;
        public const float WellOverFoot = 19f;

        /// <summary>A tray shows no more than this many pieces side by side.</summary>
        public const int MostTrayPlaces = 6;

        /// <summary>
        /// A piece's knobs stand out of its cell by up to about a quarter of the cell on each side: upright the
        /// tray leaves room for a knob at the top and one at the foot; sideways, where neighbours' knobs seldom
        /// meet, for a little less.
        /// </summary>
        private const float PieceOverItsCellUpright = 1.5f;
        private const float PieceOverItsCellAcross = 1.3f;

        /// <summary>A piece in the tray fills at most this share of its place's width and of the well's height.</summary>
        private const float PieceInItsPlace = 0.92f;
        private const float PieceInTheWell = 0.86f;

        /// <summary>The button under the tray: its pill at full size, and the lip under it.</summary>
        public const float ButtonHeight = 72f;
        public const float ButtonWidth = 286f;
        public const float ButtonLip = 7f;

        /// <summary>The wires are at least a little longer than the picture light rises over the frame.</summary>
        private const float LightOverFrame = 20f;

        /// <summary>How the height a painting leaves is shared out: to the wires, between tray and button, and under the button; the rest opens between the pips and the tray.</summary>
        private const float SlackToWires = 0.16f;
        private const float SlackOverButton = 0.24f;
        private const float SlackUnderButton = 0.34f;

        private JigsawLayout(PageLayout page, float pictureAspect, JigsawSize size, PaintingOrientation orientation)
        {
            int pieceCount = size.PieceCount;
            Page = page;
            ScreenView view = page.View;
            Plaque = Box.Around(0f, page.Plaque.MiddleY, PlaqueWidth, page.Plaque.Height);
            BackScale = page.Between(0.86f, 1f);
            float backSize = BackButtonSize * BackScale;
            Back = new Box(view.Left + BackButtonFromSide, Plaque.MiddleY - backSize / 2, backSize, backSize);

            ButtonScale = page.Between(0.78f, 0.9f);
            PipScale = page.Between(0.86f, 1f);
            PipRows = pieceCount > MostPipsInARow ? 2 : 1;
            float pipsHeight = PipRows * PipBarHeight * PipScale + (PipRows - 1) * GapBetweenPipBars;
            float trayHeight = page.Between(106f, 130f);
            float trayWidth = Math.Min(WidestTray, view.Width - 2 * TraySideMargin);
            float hintRise = HintHeight / 2 - HintMiddleUnderTrayTop;

            // The gaps of the tight screen, from the foot of the screen up to the rail.
            float underButton = page.Between(8f, 14f) + ButtonLip * ButtonScale;
            float overButton = page.Between(9f, 14f);
            float pipsToHint = page.Between(9f, 12f);
            float plateToPips = page.Between(6f, 9f);
            float wires = page.Between(LightOverFrame + 2f, LightOverFrame + 6f);
            float plateUnderFrame = HallLayout.PlateHeight - HallLayout.PlateOverFrame;

            float taken = underButton + ButtonHeight * ButtonScale + overButton + trayHeight + hintRise + pipsToHint + pipsHeight
                + plateToPips + plateUnderFrame + 2 * HallLayout.FrameWidth + wires;
            float tallestPicture = Math.Max(1f, page.RailBottom - view.UsableBottom - taken);
            float widestPicture = Math.Min(WidestPicture, view.Width - 2 * (HallLayout.FrameWidth + WallBesideFrame));
            float pictureWidth = view.OnWholePixels(Math.Min(widestPicture, tallestPicture * pictureAspect));
            float pictureHeight = view.OnWholePixels(pictureWidth / pictureAspect);
            float slack = Math.Max(0f, tallestPicture - pictureHeight);

            float buttonBottom = view.UsableBottom + underButton + SlackUnderButton * slack;
            Button = new Box(-ButtonWidth * ButtonScale / 2, buttonBottom, ButtonWidth * ButtonScale, ButtonHeight * ButtonScale);
            Tray = new Box(-trayWidth / 2, Button.Top + overButton + SlackOverButton * slack, trayWidth, trayHeight);
            Hint = Box.Around(0f, Tray.Top - HintMiddleUnderTrayTop, 0f, HintHeight);

            float pictureTop = view.OnWholePixels(page.RailBottom - wires - SlackToWires * slack - HallLayout.FrameWidth);
            Hanging = new FramePlace(new Box(view.OnWholePixels(-pictureWidth / 2), pictureTop - pictureHeight, pictureWidth, pictureHeight), page.RailBottom);
            Pips = new Box(-PipBarWidth / 2, Hanging.PlateBottom - plateToPips - pipsHeight, PipBarWidth, pipsHeight);

            // The pieces lie in the tray as large as the well's height lets the tallest of them; the tray shows as
            // many of them side by side as then fit its width.
            LargestCell = LargestCellOf(size, orientation, pictureWidth, pictureHeight);
            float fitsTheWell = Math.Min(1f, PieceInTheWell * Well.Height / (PieceOverItsCellUpright * LargestCell.Height));
            float widthInTheTray = PieceOverItsCellAcross * LargestCell.Width * fitsTheWell;
            TrayPlaces = Math.Clamp((int)(PieceInItsPlace * Well.Width / widthInTheTray), 1, MostTrayPlaces);
            float fitsItsPlace = PieceInItsPlace * (Well.Width / TrayPlaces) / (PieceOverItsCellAcross * LargestCell.Width);
            TrayPieceScale = Math.Min(fitsTheWell, fitsItsPlace);
        }

        public PageLayout Page { get; }

        /// <summary>The piece counter's plaque, hung from the cornice.</summary>
        public Box Plaque { get; }

        /// <summary>The round button that leads back, and how large it is drawn.</summary>
        public Box Back { get; }

        public float BackScale { get; }

        /// <summary>Where the painting hangs: its picture, and the rail its wires come down from.</summary>
        public FramePlace Hanging { get; }

        /// <summary>The bars of progress pips under the painting's plate: one, or two above each other, each <see cref="PipBarWidth"/> wide.</summary>
        public Box Pips { get; }

        public int PipRows { get; }

        /// <summary>How large the bars and their pips are drawn.</summary>
        public float PipScale { get; }

        /// <summary>The width of a bar of pips: the design's 196 points on its 390-point canvas.</summary>
        public float PipBarWidth => DesignPipBarWidth * PipScale;

        /// <summary>The wooden tray's block.</summary>
        public Box Tray { get; }

        /// <summary>The velvet of the tray's well: where the waiting pieces lie.</summary>
        public Box Well => new Box(
            Tray.Left + WellInFromSides,
            Tray.Bottom + WellOverFoot,
            Tray.Width - 2 * WellInFromSides,
            Tray.Height - WellUnderTop - WellOverFoot);

        /// <summary>How many pieces the tray shows side by side.</summary>
        public int TrayPlaces { get; }

        /// <summary>The width of the painting's widest piece and the height of its tallest, knobs aside, at the painting's size on this screen.</summary>
        public (float Width, float Height) LargestCell { get; }

        /// <summary>
        /// How large a piece lies in the tray, as a share of its size in the painting: small enough to lie in its
        /// place with its knobs, and never larger than it is in the painting.
        /// </summary>
        public float TrayPieceScale { get; }

        /// <summary>The middle and the height of the hint's pill; it is as wide as its words.</summary>
        public Box Hint { get; }

        /// <summary>The pill of the button under the tray; its lip shows under it.</summary>
        public Box Button { get; }

        public float ButtonScale { get; }

        /// <param name="pictureAspect">The painting's width over its height.</param>
        /// <param name="size">How the painting is cut into pieces.</param>
        /// <param name="orientation">Which way the painting lies: the rows of an upright painting's cut run as columns.</param>
        /// <exception cref="ArgumentOutOfRangeException">The proportions are not those of a picture.</exception>
        public static JigsawLayout Of(ScreenShape screen, float pictureAspect, JigsawSize size, PaintingOrientation orientation)
        {
            if (!(pictureAspect > 0f) || float.IsInfinity(pictureAspect))
            {
                throw new ArgumentOutOfRangeException(nameof(pictureAspect), pictureAspect, "A picture has a width and a height.");
            }

            return new JigsawLayout(PageLayout.Of(screen, PageTop.PlaqueUnderTheCornice), pictureAspect, size, orientation);
        }

        /// <summary>The middle of a place in the tray, the places that are used being centred in the well as a row.</summary>
        /// <param name="place">Which place, counted from 0 at the left.</param>
        /// <param name="used">How many places the row has.</param>
        /// <exception cref="ArgumentOutOfRangeException">The row has no such place, or more places than the tray.</exception>
        public (float X, float Y) TrayPlaceMiddle(int place, int used)
        {
            if (used < 1 || used > TrayPlaces || place < 0 || place >= used)
            {
                throw new ArgumentOutOfRangeException(nameof(place), place, $"A row of {used} places in a tray of {TrayPlaces} has no place {place}.");
            }

            float placeWidth = Well.Width / TrayPlaces;
            return (Well.MiddleX + (place - (used - 1) / 2f) * placeWidth, Well.MiddleY);
        }

        /// <summary>
        /// The cut's bands run across a landscape painting as rows and down an upright one as columns; every band
        /// is as thick as the others, and the pieces of the band with the fewest are the longest.
        /// </summary>
        private static (float Width, float Height) LargestCellOf(JigsawSize size, PaintingOrientation orientation, float pictureWidth, float pictureHeight)
        {
            int bands = size.RowsOfALandscapePicture.Count;
            int fewestInABand = size.RowsOfALandscapePicture.Min();
            return orientation == PaintingOrientation.Portrait
                ? (pictureWidth / bands, pictureHeight / fewestInABand)
                : (pictureWidth / fewestInABand, pictureHeight / bands);
        }
    }
}
