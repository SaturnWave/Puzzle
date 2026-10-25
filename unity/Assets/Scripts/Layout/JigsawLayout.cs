#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Avatar;
using Puzzle.Museum;

namespace Puzzle.Client.Layout
{
    /// <summary>How the jigsaw's screen is laid out. A closed set.</summary>
    public enum JigsawArrangement
    {
        /// <summary>Upright, as the design canvas draws it (JigsawTR): the painting, its pips, the tray and the button from the top down.</summary>
        Stacked,

        /// <summary>
        /// On a screen held sideways (<see cref="PageLayout.IsSideways"/>): the painting at the left as high as the
        /// screen allows, and at its right the pips, the tray with its places in rows and columns, and the button.
        /// </summary>
        SideBySide,
    }

    /// <summary>
    /// Where everything of a painting's jigsaw lies, as the design canvas draws that screen (JigsawTR), from the top
    /// down: the piece counter's plaque under the cornice with the round button that leads back beside it, the
    /// picture rail, the painting in its frame with its brass plate, the bar of progress pips, the wooden tray with
    /// the pieces that wait, and the button that places them all. The painting comes first: it is as large as its
    /// proportions and the screen allow; what the screen has in height beyond that is shared out between the gaps,
    /// so a tall phone shows a calm wall instead of one large hole. On a screen held sideways the same parts stand in
    /// two columns instead (<see cref="JigsawArrangement.SideBySide"/>), and the painting is drawn as large as the
    /// screen's height allows. Lengths are in points. No Unity dependency.
    /// </summary>
    public sealed class JigsawLayout
    {
        /// <summary>The largest picture: on the open foldable it may be wider than the hall's, the better to put pieces into.</summary>
        public const float WidestPicture = 400f;
        private const float WallBesideFrame = 9f;

        public const float PlaqueWidth = 148f;

        /// <summary>The round button that leads back, at full size, and how far it keeps from the usable screen's side.</summary>
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

        /// <summary>A tray shows no more than this many pieces at once.</summary>
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

        /// <summary>
        /// Side by side: the column of the pips, the tray and the button is this wide, this far from the painting's
        /// frame, and the two columns keep this much wall at the screen's sides. The tray is at most this high; its
        /// places stand in two columns of three or three columns of two, whichever lays the pieces larger. The pips
        /// hang from the picture rail on wires this long; the painting's plate keeps this much wall under it.
        /// </summary>
        public const float SidewaysColumnWidth = 300f;
        private const float GapBetweenColumns = 32f;
        private const float SidewaysWallAtTheSides = 16f;
        private const float TallestSidewaysTray = 300f;
        private const float SidewaysPipWires = 18f;
        private const float GapPipsToTray = 22f;
        private const float WallUnderThePlate = 14f;
        private static readonly (int Columns, int Rows)[] SidewaysGrids = { (2, 3), (3, 2) };

        /// <summary>
        /// The painter who cheers at a painting made whole: his feet this far over the tray's foot, on its lower rim;
        /// his reach this far in from the tray's right side; his raised hands this far under the label over the tray;
        /// his whole height, raised arms and all, this much at the most (the hall's painter with his palette is 150
        /// points on the design phone); the reward this far from his lowered body.
        /// </summary>
        private const float PainterOnTheTrayRim = 8f;
        private const float PainterInFromTheTraySide = 10f;
        private const float PainterUnderTheLabel = 4f;
        private const float PainterAtTheMost = 166f;
        private const float PainterBesideTheReward = 8f;

        private JigsawLayout(PageLayout page, float pictureAspect, JigsawSize size, PaintingOrientation orientation)
        {
            int pieceCount = size.PieceCount;
            Page = page;
            ScreenView view = page.View;
            Arrangement = page.IsSideways ? JigsawArrangement.SideBySide : JigsawArrangement.Stacked;
            BackScale = page.Between(0.86f, 1f);
            ButtonScale = page.Between(0.78f, 0.9f);
            PipScale = page.Between(0.86f, 1f);
            PipRows = pieceCount > MostPipsInARow ? 2 : 1;
            float pipsHeight = PipRows * PipBarHeight * PipScale + (PipRows - 1) * GapBetweenPipBars;

            Parts parts = Arrangement == JigsawArrangement.SideBySide
                ? SideBySide(page, pictureAspect, pipsHeight, PipBarWidth, ButtonScale)
                : Stacked(page, pictureAspect, pipsHeight, PipBarWidth, ButtonScale);
            Plaque = parts.Plaque;
            Hanging = parts.Hanging;
            Pips = parts.Pips;
            PipsHangFrom = parts.PipsHangFrom;
            Tray = parts.Tray;
            Hint = Box.Around(Tray.MiddleX, Tray.Top - HintMiddleUnderTrayTop, 0f, HintHeight);
            Button = parts.Button;
            float backSize = BackButtonSize * BackScale;
            Back = new Box(view.UsableLeft + BackButtonFromSide, Plaque.MiddleY - backSize / 2, backSize, backSize);

            // The pieces lie in the tray as large as their places let the largest of them, never larger than in the
            // painting. Upright the tray is one row: as many places as then fit its width. Sideways it has rows and
            // columns, as many places as the most a tray shows.
            LargestCell = LargestCellOf(size, orientation, Hanging.Picture.Width, Hanging.Picture.Height);
            (TrayColumns, TrayRows, TrayPieceScale) = Arrangement == JigsawArrangement.SideBySide
                ? LargestGridIn(Well, LargestCell)
                : OneRowIn(Well, LargestCell);
        }

        public PageLayout Page { get; }

        /// <summary>Whether the screen is laid out upright or in two columns.</summary>
        public JigsawArrangement Arrangement { get; }

        /// <summary>The piece counter's plaque, hung from the cornice: in the middle, or over the painting when it stands at the left.</summary>
        public Box Plaque { get; }

        /// <summary>The round button that leads back, and how large it is drawn: at the usable screen's left, beside the plaque.</summary>
        public Box Back { get; }

        public float BackScale { get; }

        /// <summary>Where the painting hangs: its picture, and the rail its wires come down from.</summary>
        public FramePlace Hanging { get; }

        /// <summary>The bars of progress pips: one, or two above each other, each <see cref="PipBarWidth"/> wide.</summary>
        public Box Pips { get; }

        /// <summary>The height the pips' wires come down from: the painting's plate upright, the picture rail side by side.</summary>
        public float PipsHangFrom { get; }

        /// <summary>The pips' wires are at most this far from the bars' middle: within the plate they hang from, or within the bars.</summary>
        public float PipWiresReach => Arrangement == JigsawArrangement.Stacked ? Hanging.WidestPlate / 2 : PipBarWidth / 2;

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

        /// <summary>How many places the tray has across, and how many rows of them: one row upright.</summary>
        public int TrayColumns { get; }

        public int TrayRows { get; }

        /// <summary>How many pieces the tray shows at once.</summary>
        public int TrayPlaces => TrayColumns * TrayRows;

        /// <summary>How wide and how high each place of the tray is: the well shared out between the places.</summary>
        public (float Width, float Height) TrayPlaceSize => (Well.Width / TrayColumns, Well.Height / TrayRows);

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

        /// <summary>
        /// Where the painter cheers once the painting is whole (§22): standing in front of the tray's right end, his
        /// feet on its lower rim, as tall as the hall's painter on the design phone at the most, and never so tall that
        /// his raised hands reach the label over the tray.
        /// </summary>
        public FigurePlace CheeringPainter
        {
            get
            {
                StandingMeasure cheering = PainterPictures.MeasureOf(PainterPicture.StandingCheering);
                float feetY = Tray.Bottom + PainterOnTheTrayRim;
                float tallest = Math.Min(PainterAtTheMost, Hint.Bottom - PainterUnderTheLabel - feetY);
                float pointsPerMetre = Math.Max(1f, tallest / cheering.Top);
                return new FigurePlace(Tray.Right - PainterInFromTheTraySide - cheering.Right * pointsPerMetre, feetY, pointsPerMetre);
            }
        }

        /// <summary>
        /// The part of the tray's well left of the painter who cheers in front of it, his lowered body and a little
        /// room around it: where the painting's reward is shown while he stands there.
        /// </summary>
        public Box WellBesideThePainter
        {
            get
            {
                float right = CheeringPainter.LowBounds(PainterPictures.MeasureOf(PainterPicture.StandingCheering)).Left - PainterBesideTheReward;
                return new Box(Well.Left, Well.Bottom, Math.Max(1f, right - Well.Left), Well.Height);
            }
        }

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

        /// <summary>
        /// The middle of a place in the tray. The places that are used are centred in the well: upright as a row,
        /// side by side as rows of <see cref="TrayColumns"/> from the top, the last row centred if it is not full.
        /// </summary>
        /// <param name="place">Which place, counted from 0 at the left of the top row.</param>
        /// <param name="used">How many places are used.</param>
        /// <exception cref="ArgumentOutOfRangeException">The tray has no such place, or fewer places than are used.</exception>
        public (float X, float Y) TrayPlaceMiddle(int place, int used)
        {
            if (used < 1 || used > TrayPlaces || place < 0 || place >= used)
            {
                throw new ArgumentOutOfRangeException(nameof(place), place, $"{used} places used in a tray of {TrayPlaces} have no place {place}.");
            }

            (float placeWidth, float placeHeight) = TrayPlaceSize;
            int rowsUsed = (used + TrayColumns - 1) / TrayColumns;
            int row = place / TrayColumns;
            int inThisRow = Math.Min(TrayColumns, used - row * TrayColumns);
            float x = Well.MiddleX + (place % TrayColumns - (inThisRow - 1) / 2f) * placeWidth;
            float y = Well.MiddleY + ((rowsUsed - 1) / 2f - row) * placeHeight;
            return (x, y);
        }

        /// <summary>Upright, from the foot of the screen up to the rail: the button, the tray, the pips and the painting, in the middle.</summary>
        private static Parts Stacked(PageLayout page, float pictureAspect, float pipsHeight, float pipBarWidth, float buttonScale)
        {
            ScreenView view = page.View;
            var plaque = Box.Around(0f, page.Plaque.MiddleY, PlaqueWidth, page.Plaque.Height);
            float trayHeight = page.Between(106f, 130f);
            float trayWidth = Math.Min(WidestTray, view.Width - 2 * TraySideMargin);
            float hintRise = HintHeight / 2 - HintMiddleUnderTrayTop;

            // The gaps of the tight screen, from the foot of the screen up to the rail.
            float underButton = page.Between(8f, 14f) + ButtonLip * buttonScale;
            float overButton = page.Between(9f, 14f);
            float pipsToHint = page.Between(9f, 12f);
            float plateToPips = page.Between(6f, 9f);
            float wires = page.Between(LightOverFrame + 2f, LightOverFrame + 6f);
            float plateUnderFrame = HallLayout.PlateHeight - HallLayout.PlateOverFrame;

            float taken = underButton + ButtonHeight * buttonScale + overButton + trayHeight + hintRise + pipsToHint + pipsHeight
                + plateToPips + plateUnderFrame + 2 * HallLayout.FrameWidth + wires;
            float tallestPicture = Math.Max(1f, page.RailBottom - view.UsableBottom - taken);
            float widestPicture = Math.Min(WidestPicture, view.Width - 2 * (HallLayout.FrameWidth + WallBesideFrame));
            float pictureWidth = view.OnWholePixels(Math.Min(widestPicture, tallestPicture * pictureAspect));
            float pictureHeight = view.OnWholePixels(pictureWidth / pictureAspect);
            float slack = Math.Max(0f, tallestPicture - pictureHeight);

            float buttonBottom = view.UsableBottom + underButton + SlackUnderButton * slack;
            var button = new Box(-ButtonWidth * buttonScale / 2, buttonBottom, ButtonWidth * buttonScale, ButtonHeight * buttonScale);
            var tray = new Box(-trayWidth / 2, button.Top + overButton + SlackOverButton * slack, trayWidth, trayHeight);

            float pictureTop = view.OnWholePixels(page.RailBottom - wires - SlackToWires * slack - HallLayout.FrameWidth);
            var hanging = new FramePlace(new Box(view.OnWholePixels(-pictureWidth / 2), pictureTop - pictureHeight, pictureWidth, pictureHeight), page.RailBottom);
            var pips = new Box(-pipBarWidth / 2, hanging.PlateBottom - plateToPips - pipsHeight, pipBarWidth, pipsHeight);
            return new Parts(plaque, hanging, pips, hanging.PlateBottom, tray, button);
        }

        /// <summary>
        /// Sideways: the painting at the left, as high as the screen allows under the rail, its plate under it; at its
        /// right a column with the pips hung from the rail, the tray under them and the button at the foot, the column
        /// as high as the painting's room and centred in it. The two columns stand together in the screen's middle.
        /// </summary>
        private static Parts SideBySide(PageLayout page, float pictureAspect, float pipsHeight, float pipBarWidth, float buttonScale)
        {
            ScreenView view = page.View;
            float plateUnderFrame = HallLayout.PlateHeight - HallLayout.PlateOverFrame;
            float wires = page.Between(LightOverFrame + 2f, LightOverFrame + 6f);
            float roomTop = page.RailBottom;
            float roomBottom = view.UsableBottom + WallUnderThePlate;

            float usableHalf = Math.Min(-view.UsableLeft, view.UsableRight);
            float widestFrame = 2 * (usableHalf - SidewaysWallAtTheSides) - GapBetweenColumns - SidewaysColumnWidth;
            float tallestPicture = Math.Max(1f, roomTop - roomBottom - wires - 2 * HallLayout.FrameWidth - plateUnderFrame);
            float widestPicture = Math.Max(1f, widestFrame - 2 * HallLayout.FrameWidth);
            float pictureWidth = view.OnWholePixels(Math.Min(widestPicture, tallestPicture * pictureAspect));
            float pictureHeight = view.OnWholePixels(pictureWidth / pictureAspect);
            float slack = Math.Max(0f, tallestPicture - pictureHeight);

            float frameWidth = pictureWidth + 2 * HallLayout.FrameWidth;
            float pairWidth = frameWidth + GapBetweenColumns + SidewaysColumnWidth;
            float paintingMiddle = view.OnWholePixels(-pairWidth / 2 + frameWidth / 2);
            float columnMiddle = pairWidth / 2 - SidewaysColumnWidth / 2;

            float pictureTop = view.OnWholePixels(roomTop - wires - SlackToWires * slack - HallLayout.FrameWidth);
            var hanging = new FramePlace(new Box(view.OnWholePixels(paintingMiddle - pictureWidth / 2), pictureTop - pictureHeight, pictureWidth, pictureHeight), page.RailBottom);
            var plaque = Box.Around(paintingMiddle, page.Plaque.MiddleY, PlaqueWidth, page.Plaque.Height);

            // The column, from the rail down: the pips on their wires, the hint riding on the tray's top edge, the
            // tray, and the button with its lip; what the room has beyond the tallest tray is wall above and below.
            float hintRise = HintHeight / 2 - HintMiddleUnderTrayTop;
            float overButton = page.Between(14f, 18f);
            float buttonHeight = ButtonHeight * buttonScale + ButtonLip * buttonScale;
            float fixedParts = SidewaysPipWires + pipsHeight + GapPipsToTray + hintRise + overButton + buttonHeight;
            float trayHeight = Math.Min(TallestSidewaysTray, roomTop - roomBottom - fixedParts);
            float columnSlack = Math.Max(0f, roomTop - roomBottom - fixedParts - trayHeight);
            float pipsTop = roomTop - SidewaysPipWires - columnSlack / 2;
            var pips = new Box(columnMiddle - pipBarWidth / 2, pipsTop - pipsHeight, pipBarWidth, pipsHeight);
            var tray = new Box(columnMiddle - SidewaysColumnWidth / 2, pips.Bottom - GapPipsToTray - hintRise - trayHeight, SidewaysColumnWidth, trayHeight);
            var button = new Box(columnMiddle - ButtonWidth * buttonScale / 2, tray.Bottom - overButton - ButtonHeight * buttonScale, ButtonWidth * buttonScale, ButtonHeight * buttonScale);
            return new Parts(plaque, hanging, pips, page.RailBottom, tray, button);
        }

        /// <summary>Upright: one row of places, as many as fit the well's width once the pieces are as high as the well lets them be.</summary>
        private static (int Columns, int Rows, float PieceScale) OneRowIn(Box well, (float Width, float Height) cell)
        {
            float fitsTheWell = Math.Min(1f, PieceInTheWell * well.Height / (PieceOverItsCellUpright * cell.Height));
            float widthInTheTray = PieceOverItsCellAcross * cell.Width * fitsTheWell;
            int places = Math.Clamp((int)(PieceInItsPlace * well.Width / widthInTheTray), 1, MostTrayPlaces);
            float fitsItsPlace = PieceInItsPlace * (well.Width / places) / (PieceOverItsCellAcross * cell.Width);
            return (places, 1, Math.Min(fitsTheWell, fitsItsPlace));
        }

        /// <summary>Sideways: the grid of places that lays the pieces largest, every grid holding as many places as a tray shows at most.</summary>
        private static (int Columns, int Rows, float PieceScale) LargestGridIn(Box well, (float Width, float Height) cell)
        {
            (int Columns, int Rows, float PieceScale) best = (1, 1, 0f);
            foreach ((int columns, int rows) in SidewaysGrids)
            {
                float fitsAcross = PieceInItsPlace * (well.Width / columns) / (PieceOverItsCellAcross * cell.Width);
                float fitsUpright = PieceInTheWell * (well.Height / rows) / (PieceOverItsCellUpright * cell.Height);
                float scale = Math.Min(1f, Math.Min(fitsAcross, fitsUpright));
                if (scale > best.PieceScale)
                {
                    best = (columns, rows, scale);
                }
            }

            return best;
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

        /// <summary>What an arrangement places; the rest of the screen follows from these.</summary>
        private readonly struct Parts
        {
            public Parts(Box plaque, FramePlace hanging, Box pips, float pipsHangFrom, Box tray, Box button)
            {
                Plaque = plaque;
                Hanging = hanging;
                Pips = pips;
                PipsHangFrom = pipsHangFrom;
                Tray = tray;
                Button = button;
            }

            public Box Plaque { get; }

            public FramePlace Hanging { get; }

            public Box Pips { get; }

            public float PipsHangFrom { get; }

            public Box Tray { get; }

            public Box Button { get; }
        }
    }
}
