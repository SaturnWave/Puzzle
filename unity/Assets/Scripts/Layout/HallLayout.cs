#nullable enable
using System;

namespace Puzzle.Client.Layout
{
    /// <summary>
    /// Where everything of the museum's home lies, the hall with the painting that is being restored: under the
    /// page's plaque and rail hangs the painting in its frame, with its picture light above and its brass plate
    /// below; under it stands the workbench on the floor, and in front of the floor the play button, over the
    /// navigation bar. The painting comes first: it is as large as its proportions and the wall allow; on a short
    /// screen the workbench is drawn smaller, and smaller still where a cutout or a system bar takes more of the
    /// height, before the painting gives up what it needs to be seen. No Unity dependency.
    /// </summary>
    public sealed class HallLayout
    {
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
        /// picture keeps while the bench can still shrink for it.
        /// </summary>
        private const float TightBench = 0.7f;
        private const float SmallestBench = 0.45f;
        private const float PictureHeightTheBenchGivesWayTo = 136f;

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
            float missing = PictureHeightTheBenchGivesWayTo - TallestPictureOver(usualBench);
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
        /// and the button has room for it, smaller where it has not.
        /// </summary>
        public float BannerScale
        {
            get
            {
                float room = Hanging.PlateBottom - Button.Top - 2 * BannerClearance;
                float tallest = room / (BannerHeight + BannerNameplateRise + BannerPinsRise);
                float widest = (Page.View.Width - 2 * BannerSideMargin) / BannerWidth;
                return Math.Max(0.1f, Math.Min(1f, Math.Min(tallest, widest)));
            }
        }

        /// <summary>The body of the banner of a restored room, in the middle of the wall between the brass plate and the button; its nameplate rises over its top edge.</summary>
        public Box Banner
        {
            get
            {
                float scale = BannerScale;
                float roomMiddle = (Hanging.PlateBottom + Button.Top) / 2;
                return Box.Around(0f, roomMiddle - (BannerNameplateRise + BannerPinsRise) * scale / 2, BannerWidth * scale, BannerHeight * scale);
            }
        }

        /// <summary>Where the painting hangs: its picture, and the rail its wires come down from.</summary>
        public FramePlace Hanging => new FramePlace(Picture, Page.RailBottom);

        /// <summary>The bare wall between a side of the screen and the frame.</summary>
        public float WallBesideTheFrame => Frame.Left - Page.View.Left;

        /// <summary>True when a wall lamp has room at each side of the painting.</summary>
        public bool HasLamps => WallBesideTheFrame >= WallForALamp;

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
