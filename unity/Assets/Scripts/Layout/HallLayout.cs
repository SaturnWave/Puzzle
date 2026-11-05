#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Avatar;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where everything of the museum's home lies, the hall with the painting that is being restored: under the
    /// page's plaque and rail hangs the painting in its frame, with its picture light above and its brass plate
    /// below; under it stands the workbench on the floor, the painter at its right (<see cref="PainterBesideTheBench"/>)
    /// and the gift frame on the wall at its left, and in front of the floor the play button, over the
    /// navigation bar. The painting comes first: it is as large as its proportions and the wall allow; on a short
    /// screen the workbench is drawn smaller, and smaller still where a cutout or a system bar takes more of the
    /// height, before the painting gives up what it needs to be seen; the painter is drawn with the bench. On a
    /// screen held sideways the hall keeps this upright column in its middle, the painter in it, and the wide wall
    /// at its sides is the gallery's: a lamp beside the frame, and on a tablet the room's restored paintings, two
    /// above each other at each side (<see cref="PaintingsBeside"/>).
    /// No Unity dependency.
    /// </summary>
    /// <remarks>
    /// What the page's top takes (the cornice, the wallet row, the plaque) comes out of the wall in this order: the
    /// painting first gives up what it has over the height the workbench gives way to (<see cref="PictureHeightTheBenchGivesWayTo"/>
    /// on a tight screen, <see cref="RoomyPictureHeightTheBenchGivesWayTo"/> on a roomy one); then the workbench is
    /// drawn smaller, down to its smallest; only then does the painting go under that height, and on no screen the
    /// game supports under <see cref="SmallestPicture"/>.
    /// </remarks>
    public sealed class HallLayout
    {
        /// <summary>The least height the painting has on any screen the game supports: the tests hold every layout to it.</summary>
        public const float SmallestPicture = 100f;

        /// <summary>The frame is this wide on every side of the picture (the frame's own drawing).</summary>
        public const float FrameWidth = 23f;

        /// <summary>The largest picture: the design's, 302 points wide on its 390-point canvas, on a 360-point screen.</summary>
        public const float WidestPicture = 290f;
        private const float WallBesideFrame = 10f;

        /// <summary>The brass plate: it hangs on the frame's lower bar, so far over it, and is so high.</summary>
        public const float PlateHeight = 35f;
        public const float PlateOverFrame = 13f;

        /// <summary>The picture light rises this far over the frame's top edge; the wires from the rail are at least a little longer.</summary>
        private const float LightOverFrame = 20f;

        /// <summary>The workbench's drawing: how far it stands above the line where the wall meets the floor, and how far its feet stand below it.</summary>
        public const float BenchAboveFloorLine = 163f;
        public const float BenchFeetBelowFloorLine = 21f;
        private const float BenchHeight = BenchAboveFloorLine + BenchFeetBelowFloorLine;

        /// <summary>
        /// How large the workbench is drawn on the tight screen; the smallest it is ever drawn; and the height the
        /// picture keeps while the bench can still shrink for it, on a tight screen and on a roomy one. The design
        /// draws the painting 165 of the game's points high on its own phone (HomeTR: 302 of 390 points wide,
        /// Wheatstacks); the roomy height comes as near that as the wallet row leaves room for with a bench of
        /// three quarters.
        /// </summary>
        private const float TightBench = 0.7f;
        public const float SmallestBench = 0.45f;
        public const float PictureHeightTheBenchGivesWayTo = 136f;
        public const float RoomyPictureHeightTheBenchGivesWayTo = 156f;

        /// <summary>The skirting board along the foot of the wall.</summary>
        public const float SkirtingHeight = 14f;

        /// <summary>The play button's pill at full size, and the lip under it.</summary>
        public const float ButtonHeight = 72f;
        public const float ButtonWidth = 236f;
        public const float ButtonLip = 7f;

        /// <summary>
        /// While jigsaw pieces wait to be placed, the button that leads to the painting's jigsaw stands beside the
        /// play button, as on the design canvas: the play button is then this wide, the jigsaw's this wide, with
        /// this gap between them, all at full size.
        /// </summary>
        public const float PlayWidthBesideJigsaw = 198f;
        public const float JigsawButtonWidth = 150f;
        public const float GapBetweenButtons = 9f;

        /// <summary>A wall lamp hangs beside the painting where the wall there is at least this wide: on an open foldable, not on a phone.</summary>
        public const float WallForALamp = 70f;

        /// <summary>
        /// On a screen held sideways, from this much bare wall beside the frame on the room's restored paintings hang
        /// on it: the 12.9" iPad has 259 to 363 points there, as its painting is wide or narrow, a 16:10 tablet 344 to
        /// 448. Elsewhere the lamp alone hangs there.
        /// </summary>
        public const float WallForPaintings = 190f;

        /// <summary>At most this many of the room's restored paintings hang beside the frame: two above each other at each side.</summary>
        public const int MostPaintingsBeside = 4;

        /// <summary>
        /// With paintings beside it, the lamp hangs in this much wall next to the frame, and the paintings hang beyond
        /// it, each in a share of the wall at most this wide, clear of the screen's side by this much, between the
        /// picture rail and the skirting board with this much wall under the one and over the other.
        /// </summary>
        private const float LampRoomBesideTheFrame = 80f;
        private const float WidestShareBeside = 150f;
        private const float WallAtTheSide = 12f;
        private const float PaintingsUnderTheRail = 10f;
        private const float PaintingsOverTheSkirting = 8f;

        /// <summary>
        /// The note the hall has for a player with nothing to restore just now: a pill of this height that stands
        /// this far over the play button's top edge, in front of the floor.
        /// </summary>
        public const float NoteHeight = 24f;
        private const float NoteOverButton = 5f;

        /// <summary>
        /// The banner of a room that has just been restored stands where the workbench stands otherwise: a plaque
        /// of this size at full size, with a nameplate that rides on its top edge and rises this far over it (the
        /// pins of the nameplate's wires a little further). It keeps this far from the brass plate above it and from
        /// the rim of the button under it, and so far from the screen's sides.
        /// </summary>
        public const float BannerWidth = 300f;
        public const float BannerHeight = 76f;
        public const float BannerNameplateRise = 20f;
        public const float BannerPinsRise = 6f;
        private const float BannerClearance = 10f;
        private const float BannerSideMargin = 14f;

        /// <summary>
        /// The gift frame on the wall beside the workbench, in the canvas' points around the frame's middle: a gilt
        /// frame of 60 by 60, its picture 50 by 50 in it; the pin it hangs from 39.5 over its middle, the wire running
        /// from the pin to 18 at each side 27 over the middle, behind the frame; the pill of 21 with its middle 34.5
        /// under the frame's; the red dot (13 across) at its upper right. From the pin's top to the pill's foot it is
        /// 87 high, the frame's middle 42 under the top. The canvas hangs it at the bench's right (HomeTR); since the
        /// painter stands there (§22, 2026-10-24) it hangs at the bench's left, as high: its middle 149 to the left of
        /// the screen's middle, and never nearer the screen's side than 46.
        /// </summary>
        public const float GiftFrameSize = 60f;
        public const float GiftPictureSize = 50f;
        public const float GiftPinOverMiddle = 39.5f;
        public const float GiftWireEndsAcross = 18f;
        public const float GiftWireEndsOverMiddle = 27f;
        public const float GiftPillUnderMiddle = 34.5f;
        public const float GiftPillHeight = 21f;
        public const float GiftDotSize = 13f;
        public static readonly (float Across, float Up) GiftDotFromMiddle = (28.5f, 30.5f);
        private const float GiftHeight = 87f;
        private const float GiftMiddleUnderTop = 42f;
        private const float GiftAcross = 149f;
        private const float GiftFromTheSide = 46f;

        /// <summary>The gift's foot stands this far over the skirting board, on a tight screen and a roomy one; its pin keeps this far under the brass plate.</summary>
        private const float TightGiftOverTheSkirting = 6f;
        private const float RoomyGiftOverTheSkirting = 26f;
        private const float GiftUnderThePlate = 4f;

        /// <summary>
        /// The painter stands on the floor at the workbench's right (§22), his feet on the line of the bench's feet:
        /// this share of the bench's drawing high (brushes and all) with the palette in his hand, which is 150 points,
        /// 450 pixels, on the 1080x2424 phone; and never so high that his raised arms, when a room is restored, reach
        /// the painting's brass plate. He keeps this far right of the bench's table, which is 200 of the bench's
        /// points wide, and this far from the screen's side; where both cannot be, he stands a little in front of the
        /// table's end.
        /// </summary>
        public const float PainterAgainstTheBench = 0.92f;
        private const float TableHalfWidth = 100f;
        private const float PainterBesideTheTable = 4f;
        private const float PainterFromTheSide = 4f;
        private const float PainterUnderThePlate = 4f;

        /// <summary>In the hall of a room just restored, the banner keeps this far from the painter who cheers beside it.</summary>
        private const float PainterBesideTheBanner = 6f;

        private HallLayout(PageLayout page, float pictureAspect, bool hasJigsawButton)
        {
            Page = page;
            ScreenView view = page.View;

            ButtonScale = page.Between(0.8f, 0.92f);
            float buttonBottom = page.BarReach + page.Between(9f, 18f) + ButtonLip * ButtonScale;
            if (hasJigsawButton)
            {
                float rowWidth = (PlayWidthBesideJigsaw + GapBetweenButtons + JigsawButtonWidth) * ButtonScale;
                Button = new Box(-rowWidth / 2, buttonBottom, PlayWidthBesideJigsaw * ButtonScale, ButtonHeight * ButtonScale);
                JigsawButton = new Box(rowWidth / 2 - JigsawButtonWidth * ButtonScale, buttonBottom, JigsawButtonWidth * ButtonScale, ButtonHeight * ButtonScale);
            }
            else
            {
                Button = new Box(-ButtonWidth * ButtonScale / 2, buttonBottom, ButtonWidth * ButtonScale, ButtonHeight * ButtonScale);
            }

            // The painting's room on the wall: from under the picture light down to the plate's foot, clear of the
            // brushes on the bench. Every point of the bench's height is taken from it, so where the picture would
            // be lower than it should, the bench is drawn as much smaller as gives the picture that height back.
            float benchFeet = Button.Top + page.Between(12f, 16f);
            float wires = page.Between(LightOverFrame + 3f, LightOverFrame + 8f);
            float roomTop = page.RailBottom - wires;
            float roomOverTheBench = page.Between(6f, 26f) + (PlateHeight - PlateOverFrame) + 2 * FrameWidth;
            float TallestPictureOver(float benchScale) => roomTop - benchFeet - BenchHeight * benchScale - roomOverTheBench;

            float usualBench = page.Between(TightBench, 1f);
            float missing = page.Between(PictureHeightTheBenchGivesWayTo, RoomyPictureHeightTheBenchGivesWayTo) - TallestPictureOver(usualBench);
            BenchScale = Math.Clamp(usualBench - Math.Max(0f, missing) / BenchHeight, SmallestBench, 1f);
            FloorLine = benchFeet + BenchFeetBelowFloorLine * BenchScale;
            float tallestPicture = Math.Max(1f, TallestPictureOver(BenchScale));
            float widestPicture = Math.Min(WidestPicture, view.Width - 2 * (FrameWidth + WallBesideFrame));
            float pictureWidth = view.OnWholePixels(Math.Min(widestPicture, tallestPicture * pictureAspect));
            float pictureHeight = view.OnWholePixels(pictureWidth / pictureAspect);

            // What the painting leaves of its room is wall: half of it lengthens the wires, the rest shows over the bench.
            float slack = tallestPicture - pictureHeight;
            float pictureTop = view.OnWholePixels(roomTop - FrameWidth - slack / 2);
            Picture = new Box(view.OnWholePixels(-pictureWidth / 2), pictureTop - pictureHeight, pictureWidth, pictureHeight);

            // The gift frame is drawn as large as the wallet row, and smaller where the wall between the skirting and the plate is shorter.
            float giftFoot = FloorLine + SkirtingHeight + page.Between(TightGiftOverTheSkirting, RoomyGiftOverTheSkirting);
            GiftScale = Math.Min(page.WalletRowScale, (Hanging.PlateBottom - GiftUnderThePlate - giftFoot) / GiftHeight);
            float giftMiddleX = -Math.Min(GiftAcross * GiftScale, -view.UsableLeft - GiftFromTheSide * GiftScale);
            GiftMiddle = (giftMiddleX, giftFoot + (GiftHeight - GiftMiddleUnderTop) * GiftScale);

            // One scale for the painter in every pose: as the bench is drawn, but low enough for his raised arms.
            float byTheBench = PainterAgainstTheBench * BenchHeight * BenchScale / PainterPictures.MeasureOf(PainterPicture.StandingWithPalette).Top;
            float underThePlate = (Hanging.PlateBottom - PainterUnderThePlate - BenchFeet) / PainterPictures.MeasureOf(PainterPicture.StandingCheering).Top;
            PainterPointsPerMetre = Math.Max(1f, Math.Min(byTheBench, underThePlate));
        }

        public PageLayout Page { get; }

        /// <summary>The picture itself, inside its frame.</summary>
        public Box Picture { get; }

        /// <summary>The frame's outer edge.</summary>
        public Box Frame => new Box(Picture.Left - FrameWidth, Picture.Bottom - FrameWidth, Picture.Width + 2 * FrameWidth, Picture.Height + 2 * FrameWidth);

        /// <summary>The height of the middle of the brass plate under the painting; it is centred under the frame.</summary>
        public float PlateMiddle => Frame.Bottom + PlateOverFrame - PlateHeight / 2;

        /// <summary>The plate is no wider than the frame it hangs on, less its corners.</summary>
        public float WidestPlate => Frame.Width - 2 * FrameWidth;

        /// <summary>Where the wall meets the floor: the foot of the skirting board.</summary>
        public float FloorLine { get; }

        /// <summary>How large the workbench is drawn; its feet stand on the middle of the floor's width.</summary>
        public float BenchScale { get; }

        /// <summary>The height the workbench's feet stand at.</summary>
        public float BenchFeet => FloorLine - BenchFeetBelowFloorLine * BenchScale;

        /// <summary>How large the play button is drawn.</summary>
        public float ButtonScale { get; }

        /// <summary>The play button's pill; its lip shows under it.</summary>
        public Box Button { get; }

        /// <summary>The pill of the button that leads to the painting's jigsaw, beside the play button; nothing while no piece waits.</summary>
        public Box? JigsawButton { get; }

        /// <summary>The height of the middle of the hall's note, which is centred on the screen's width.</summary>
        public float NoteMiddle => Button.Top + NoteOverButton + NoteHeight / 2;

        /// <summary>
        /// How large the banner of a restored room is drawn: its full size where the wall between the brass plate
        /// and the button has room for it, smaller where it has not, and smaller again where it would reach the
        /// painter who cheers beside it.
        /// </summary>
        public float BannerScale => RestoredRoom().BannerScale;

        /// <summary>
        /// The body of the banner of a restored room, in the middle of the wall between the brass plate and the button
        /// (its nameplate rises over its top edge): in the middle of the screen where the painter beside it leaves it
        /// room, else in the middle of the wall at his left.
        /// </summary>
        public Box Banner => RestoredRoom().Banner;

        /// <summary>
        /// How many points a metre of the painter is drawn in this hall, in every pose: <see cref="PainterAgainstTheBench"/>
        /// of the bench's drawing with the palette in his hand, or less where his raised arms would reach the brass plate.
        /// </summary>
        public float PainterPointsPerMetre { get; }

        /// <summary>
        /// Where the painter stands in the hall as the player's progress has it, in the pose he is shown in: at the
        /// workbench's right, his feet on the line of the bench's feet, clear of the screen's side.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A bust, which does not stand in the hall.</exception>
        public FigurePlace PainterBesideTheBench(PainterPicture pose)
        {
            StandingMeasure figure = PainterPictures.MeasureOf(pose);
            float besideTheTable = TableHalfWidth * BenchScale + PainterBesideTheTable + figure.Left * PainterPointsPerMetre;
            float clearOfTheSide = Page.View.UsableRight - PainterFromTheSide - figure.Right * PainterPointsPerMetre;
            return new FigurePlace(Math.Min(besideTheTable, clearOfTheSide), BenchFeet, PainterPointsPerMetre);
        }

        /// <summary>
        /// Where the painter cheers in the hall of a room just restored, where the workbench stands aside: at the
        /// banner's right, as near it as his lowered body allows, clear of the screen's side.
        /// </summary>
        public FigurePlace CheeringPainter => RestoredRoom().Painter;

        /// <summary>Where the painting hangs: its picture, and the rail its wires come down from.</summary>
        public FramePlace Hanging => new FramePlace(Picture, Page.RailBottom);

        /// <summary>How large the gift frame and all of it is drawn: a share of the canvas' points.</summary>
        public float GiftScale { get; }

        /// <summary>The middle of the gift frame.</summary>
        public (float X, float Y) GiftMiddle { get; }

        /// <summary>What a finger presses for the gift: from the pin's top to the pill's foot, as wide as the frame.</summary>
        public Box GiftButton => new Box(
            GiftMiddle.X - GiftFrameSize / 2f * GiftScale,
            GiftMiddle.Y - (GiftHeight - GiftMiddleUnderTop) * GiftScale,
            GiftFrameSize * GiftScale,
            GiftHeight * GiftScale);

        /// <summary>The bare wall between the frame and the nearer side of the usable screen, the same at both sides of the frame.</summary>
        public float WallBesideTheFrame => Math.Min(Frame.Left - Page.View.UsableLeft, Page.View.UsableRight - Frame.Right);

        /// <summary>True when a wall lamp has room at each side of the painting.</summary>
        public bool HasLamps => WallBesideTheFrame >= WallForALamp;

        /// <summary>
        /// True on a screen held sideways (<see cref="PageLayout.IsSideways"/>) whose wall beside the frame is wide
        /// enough for the room's restored paintings: every tablet held sideways. An upright screen keeps its hall as it
        /// is, and so does the open foldable either way, whose wall is wide for a narrow painting but whose screen is
        /// nearly square.
        /// </summary>
        public bool HasPaintingsBeside => Page.IsSideways && WallBesideTheFrame >= WallForPaintings;

        /// <summary>
        /// How far right of the screen's middle the right lamp hangs, the left one as far left: midway in the wall
        /// beside the frame, or nearer the frame where paintings hang beyond the lamp.
        /// </summary>
        public float LampMiddleX => Frame.Right + (HasPaintingsBeside ? LampRoomBesideTheFrame : WallBesideTheFrame) / 2;

        /// <summary>
        /// Where the room's restored paintings hang beside the frame, in the order they are given: the left upper
        /// place, the right upper one, then the left and the right lower ones, each in the collection's narrow frame
        /// with its plate, the upper two from the picture rail. Nothing where the wall is too narrow for them
        /// (<see cref="HasPaintingsBeside"/>), and no more than <see cref="MostPaintingsBeside"/>.
        /// </summary>
        /// <param name="pictureAspects">Each restored painting's width over its height, in the order they are to hang.</param>
        /// <exception cref="ArgumentException">Proportions that are not those of a picture.</exception>
        public IReadOnlyList<ExhibitPlace> PaintingsBeside(IReadOnlyList<float> pictureAspects)
        {
            int count = HasPaintingsBeside ? Math.Min(pictureAspects.Count, MostPaintingsBeside) : 0;
            ScreenView view = Page.View;
            float wingInside = Frame.Right + LampRoomBesideTheFrame;
            float wingOutside = Math.Min(-view.UsableLeft, view.UsableRight) - WallAtTheSide;
            float shareWidth = Math.Min(WidestShareBeside, wingOutside - wingInside);
            float shareMiddle = (wingInside + wingOutside) / 2;
            float top = Page.RailBottom - PaintingsUnderTheRail;
            float foot = FloorLine + SkirtingHeight + PaintingsOverTheSkirting;
            float shareHeight = (top - foot) / 2;

            var places = new ExhibitPlace[count];
            for (int painting = 0; painting < count; painting++)
            {
                float aspect = pictureAspects[painting];
                if (!(aspect > 0f) || float.IsInfinity(aspect))
                {
                    throw new ArgumentException($"A picture has a width and a height; {aspect} is not their proportion.", nameof(pictureAspects));
                }

                int side = painting % 2 == 0 ? -1 : 1;
                int row = painting / 2;
                var share = new Box(side * shareMiddle - shareWidth / 2, top - (row + 1) * shareHeight, shareWidth, shareHeight);
                places[painting] = CollectionLayout.HungIn(share, aspect, view, hangsFromTheRail: row == 0);
            }

            return places;
        }

        /// <summary>
        /// The hall of a room just restored: the painter cheering at the right, and the banner as large as the wall
        /// between the plate and the button allows, in the middle of the screen where it stays clear of him below his
        /// raised arms, else only as far left as he needs and, where the wall at his left is narrower than the banner,
        /// drawn smaller to fit it.
        /// </summary>
        private (Box Banner, float BannerScale, FigurePlace Painter) RestoredRoom()
        {
            ScreenView view = Page.View;
            float room = Hanging.PlateBottom - Button.Top - 2 * BannerClearance;
            float tallest = room / (BannerHeight + BannerNameplateRise + BannerPinsRise);
            float widest = (view.Width - 2 * BannerSideMargin) / BannerWidth;
            float fullScale = Math.Max(0.1f, Math.Min(1f, Math.Min(tallest, widest)));

            StandingMeasure cheering = PainterPictures.MeasureOf(PainterPicture.StandingCheering);
            float besideTheBanner = BannerWidth * fullScale / 2 + PainterBesideTheBanner + cheering.LowLeft * PainterPointsPerMetre;
            float clearOfTheSide = view.UsableRight - PainterFromTheSide - cheering.Right * PainterPointsPerMetre;
            var painter = new FigurePlace(Math.Min(besideTheBanner, clearOfTheSide), BenchFeet, PainterPointsPerMetre);

            // As large as the wall left of him allows, and in the middle of the screen, or only as far left of it as he needs.
            float roomRight = painter.LowBounds(cheering).Left - PainterBesideTheBanner;
            float roomLeft = view.UsableLeft + BannerSideMargin;
            float scale = Math.Max(0.1f, Math.Min(fullScale, (roomRight - roomLeft) / BannerWidth));
            float middleX = Math.Min(0f, roomRight - BannerWidth * scale / 2);
            float roomMiddle = (Hanging.PlateBottom + Button.Top) / 2;
            Box banner = Box.Around(middleX, roomMiddle - (BannerNameplateRise + BannerPinsRise) * scale / 2, BannerWidth * scale, BannerHeight * scale);
            return (banner, scale, painter);
        }

        /// <param name="pictureAspect">The painting's width over its height.</param>
        /// <param name="hasJigsawButton">True while pieces wait: the button that leads to the jigsaw stands beside the play button.</param>
        /// <exception cref="ArgumentOutOfRangeException">The proportions are not those of a picture.</exception>
        public static HallLayout Of(ScreenShape screen, float pictureAspect, bool hasJigsawButton = false)
        {
            if (!(pictureAspect > 0f) || float.IsInfinity(pictureAspect))
            {
                throw new ArgumentOutOfRangeException(nameof(pictureAspect), pictureAspect, "A picture has a width and a height.");
            }

            return new HallLayout(PageLayout.Of(screen), pictureAspect, hasJigsawButton);
        }
    }
}
