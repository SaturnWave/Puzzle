#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Layout
{
    /// <summary>Where one painting of the collection hangs: its picture in a narrow frame, and the brass plate under it.</summary>
    public readonly struct ExhibitPlace
    {
        public ExhibitPlace(Box picture, float frameWidth, float plateMiddle, float widestPlate, bool hangsFromTheRail, float badgeMiddleX, float badgeMiddleY)
        {
            Picture = picture;
            FrameWidth = frameWidth;
            PlateMiddle = plateMiddle;
            WidestPlate = widestPlate;
            HangsFromTheRail = hangsFromTheRail;
            BadgeMiddleX = badgeMiddleX;
            BadgeMiddleY = badgeMiddleY;
        }

        public Box Picture { get; }

        /// <summary>How wide the frame is on every side of the picture.</summary>
        public float FrameWidth { get; }

        /// <summary>The frame's outer edge.</summary>
        public Box Frame => new Box(Picture.Left - FrameWidth, Picture.Bottom - FrameWidth, Picture.Width + 2 * FrameWidth, Picture.Height + 2 * FrameWidth);

        /// <summary>The height of the middle of the plate; it is centred under the frame.</summary>
        public float PlateMiddle { get; }

        /// <summary>The plate is no wider than this: it keeps clear of the painting beside it.</summary>
        public float WidestPlate { get; }

        /// <summary>True for the paintings of the top row: their wires are seen coming down from the picture rail.</summary>
        public bool HangsFromTheRail { get; }

        /// <summary>
        /// The middle of the red badge that counts the pieces waiting on the painting: on the frame's upper right
        /// corner, and never so far out that the badge (<see cref="CollectionLayout.BadgeReach"/> to each side of
        /// its middle) leaves the painting's own share of the wall, which is what keeps it on the screen and off
        /// the painting beside it.
        /// </summary>
        public float BadgeMiddleX { get; }

        public float BadgeMiddleY { get; }

        /// <summary>Everything of the painting a finger may touch: its frame and its plate.</summary>
        public Box Touch => new Box(Frame.Left, PlateMiddle - CollectionLayout.PlateHeight / 2, Frame.Width, Frame.Top - (PlateMiddle - CollectionLayout.PlateHeight / 2));
    }

    /// <summary>
    /// Where everything of the collection's page lies, as the design canvas hangs a room's paintings (CollectionTR):
    /// under the page's plaque and rail the room's five paintings in rows, each in a narrow gilt frame with its
    /// brass plate, over the navigation bar. A phone shows them two to a row, an iPad and the open foldable three; a
    /// row that is not full is centred. Each painting is as large as its proportions and its share of the wall allow.
    /// Lengths are in points. No Unity dependency.
    /// </summary>
    /// <remarks>
    /// What the page's top takes (the cornice, the wallet row, the plaque) comes out of the paintings alone: the
    /// wall between the rail, the rows and the bar keeps its measures, and every row of paintings is drawn as much
    /// lower as the room is short.
    /// </remarks>
    public sealed class CollectionLayout
    {
        /// <summary>The frames of the collection are the hall's frame at this share of its size.</summary>
        public const float FrameScale = 0.5f;

        /// <summary>A painting's plate: its height, and how far it rides over the frame's lower bar.</summary>
        public const float PlateHeight = 28f;
        public const float PlateOverFrame = 6f;

        /// <summary>
        /// From this width on, a screen shows three paintings in a row. A screen as high as the design's phone and
        /// this wide hangs a room's five paintings larger in all in two rows of three than in three rows of two, or
        /// about as large where they are all wide landscapes; the wider, the more the two rows win. Every iPad
        /// upright is wider (the mini is 420 points, the 12.9" Pro 480), and so is the open foldable; every phone
        /// (360) and a 10:16 tablet (400) are narrower and keep two to a row.
        /// </summary>
        private const float WidthForThreeInARow = 410f;

        /// <summary>A painting's share of the wall is at most this wide, and keeps this much wall around what hangs in it.</summary>
        private const float WidestShare = 206f;
        private const float WallBeside = 7f;
        private const float WallOverAndUnder = 7f;

        /// <summary>The wall between the rail and the top row, and between the bottom row and the bar.</summary>
        private const float UnderTheRail = 8f;
        private const float OverTheBar = 6f;

        /// <summary>
        /// The badge of the waiting pieces: its middle lies this far inside the frame's upper right corner, the
        /// widest badge (two figures, with its ring and shadow) reaches this far from its middle, and it keeps this
        /// far inside the painting's share of the wall.
        /// </summary>
        public const float BadgeReach = 15f;
        private const float BadgeInFromCorner = 3f;
        private const float BadgeInsideTheShare = 3f;

        /// <summary>
        /// On the plaque, in the plaque's own design points from its middle (<c>PagePlaque</c>): the middles of the
        /// two round buttons that turn to the room before and the room after, and their size.
        /// </summary>
        public const float PreviousRoomAt = -22f;
        public const float NextRoomAt = 154f;
        public const float RoomButtonSize = 30f;

        /// <summary>The design's plaque is this high; what stands on a plaque is drawn at the share of it the page's plaque has.</summary>
        private const float PlaqueDesignHeight = 58f;

        private CollectionLayout(PageLayout page, IReadOnlyList<float> pictureAspects)
        {
            Page = page;
            ScreenView view = page.View;
            int perRow = view.Width >= WidthForThreeInARow ? 3 : 2;
            int rows = (pictureAspects.Count + perRow - 1) / perRow;
            float top = page.RailBottom - UnderTheRail;
            float foot = page.BarReach + OverTheBar;
            float shareWidth = Math.Min(WidestShare, view.Width / perRow);
            float shareHeight = (top - foot) / rows;

            var exhibits = new ExhibitPlace[pictureAspects.Count];
            for (int painting = 0; painting < exhibits.Length; painting++)
            {
                int row = painting / perRow;
                int inThisRow = Math.Min(perRow, pictureAspects.Count - row * perRow);
                float middleX = (painting % perRow - (inThisRow - 1) / 2f) * shareWidth;
                var share = new Box(middleX - shareWidth / 2, top - (row + 1) * shareHeight, shareWidth, shareHeight);
                exhibits[painting] = Hung(share, pictureAspects[painting], view, hangsFromTheRail: row == 0);
            }

            Exhibits = exhibits;
            float onThePlaque = page.Plaque.Height / PlaqueDesignHeight;
            float buttonSize = RoomButtonSize * onThePlaque;
            PreviousRoom = Box.Around(page.Plaque.MiddleX + PreviousRoomAt * onThePlaque, page.Plaque.MiddleY, buttonSize, buttonSize);
            NextRoom = Box.Around(page.Plaque.MiddleX + NextRoomAt * onThePlaque, page.Plaque.MiddleY, buttonSize, buttonSize);
        }

        public PageLayout Page { get; }

        /// <summary>The room's paintings, in the order they are restored.</summary>
        public IReadOnlyList<ExhibitPlace> Exhibits { get; }

        /// <summary>The round buttons on the plaque that turn to the room before and to the room after.</summary>
        public Box PreviousRoom { get; }

        public Box NextRoom { get; }

        /// <param name="pictureAspects">Each painting's width over its height, in the order the paintings hang.</param>
        /// <exception cref="ArgumentException">No painting, or proportions that are not those of a picture.</exception>
        public static CollectionLayout Of(ScreenShape screen, IReadOnlyList<float> pictureAspects)
        {
            if (pictureAspects.Count == 0)
            {
                throw new ArgumentException("A wall of the collection has at least one painting.", nameof(pictureAspects));
            }

            foreach (float aspect in pictureAspects)
            {
                if (!(aspect > 0f) || float.IsInfinity(aspect))
                {
                    throw new ArgumentException($"A picture has a width and a height; {aspect} is not their proportion.", nameof(pictureAspects));
                }
            }

            return new CollectionLayout(PageLayout.Of(screen), pictureAspects);
        }

        /// <summary>A painting in its share of the wall: as large as fits, its frame and plate together in the share's middle.</summary>
        private static ExhibitPlace Hung(Box share, float aspect, ScreenView view, bool hangsFromTheRail)
        {
            float frame = HallLayout.FrameWidth * FrameScale;
            float plateUnderFrame = PlateHeight - PlateOverFrame;
            float widest = share.Width - 2 * (frame + WallBeside);
            float tallest = share.Height - 2 * (frame + WallOverAndUnder) - plateUnderFrame;
            float width = view.OnWholePixels(Math.Min(widest, tallest * aspect));
            float height = view.OnWholePixels(width / aspect);
            float hangingHeight = height + 2 * frame + plateUnderFrame;
            float pictureTop = view.OnWholePixels(share.MiddleY + hangingHeight / 2 - frame);
            var picture = new Box(view.OnWholePixels(share.MiddleX - width / 2), pictureTop - height, width, height);
            float plateMiddle = picture.Bottom - frame + PlateOverFrame - PlateHeight / 2;
            float badgeX = Math.Min(picture.Right + frame - BadgeInFromCorner, share.Right - BadgeInsideTheShare - BadgeReach);
            float badgeY = Math.Min(picture.Top + frame - BadgeInFromCorner, share.Top - BadgeInsideTheShare - BadgeReach);
            return new ExhibitPlace(picture, frame, plateMiddle, share.Width - 2 * WallBeside, hangsFromTheRail, badgeX, badgeY);
        }
    }

    /// <summary>
    /// Where a painting of the collection hangs when it is looked at by itself, in front of the darkened room: as
    /// large as the screen allows, in the hall's own frame, with its brass plate and a line under it that says how
    /// to go back. Lengths are in points. No Unity dependency.
    /// </summary>
    public sealed class ViewerLayout
    {
        private const float WidestPicture = 470f;
        private const float WallBesideFrame = 10f;

        /// <summary>Clear of the cornice at the top; at the foot, room for the line under the plate.</summary>
        private const float OverTheFrame = 34f;
        private const float GapPlateToLine = 16f;
        private const float LineHeight = 14f;
        private const float UnderTheLine = 26f;

        private ViewerLayout(ScreenView view, float pictureAspect)
        {
            View = view;
            float plateUnderFrame = HallLayout.PlateHeight - HallLayout.PlateOverFrame;
            float usable = view.UsableTop - view.UsableBottom;
            float tallest = usable - OverTheFrame - 2 * HallLayout.FrameWidth - plateUnderFrame - GapPlateToLine - LineHeight - UnderTheLine;
            float widest = Math.Min(WidestPicture, view.Width - 2 * (HallLayout.FrameWidth + WallBesideFrame));
            float width = view.OnWholePixels(Math.Min(widest, Math.Max(1f, tallest) * pictureAspect));
            float height = view.OnWholePixels(width / pictureAspect);

            // The painting with its plate and the line under it hang together in the middle of the room they have.
            float group = height + 2 * HallLayout.FrameWidth + plateUnderFrame + GapPlateToLine + LineHeight;
            float roomMiddle = (view.UsableTop - OverTheFrame + view.UsableBottom + UnderTheLine) / 2;
            float groupTop = roomMiddle + group / 2;
            float pictureTop = view.OnWholePixels(groupTop - HallLayout.FrameWidth);
            Hanging = new FramePlace(new Box(view.OnWholePixels(-width / 2), pictureTop - height, width, height), view.Top);
            LineMiddle = Hanging.PlateBottom - GapPlateToLine - LineHeight / 2;
        }

        public ScreenView View { get; }

        /// <summary>Where the painting hangs; its wires come down from the top of the screen.</summary>
        public FramePlace Hanging { get; }

        /// <summary>The height of the middle of the line under the plate.</summary>
        public float LineMiddle { get; }

        /// <param name="pictureAspect">The painting's width over its height.</param>
        /// <exception cref="ArgumentOutOfRangeException">The proportions are not those of a picture.</exception>
        public static ViewerLayout Of(ScreenShape screen, float pictureAspect)
        {
            if (!(pictureAspect > 0f) || float.IsInfinity(pictureAspect))
            {
                throw new ArgumentOutOfRangeException(nameof(pictureAspect), pictureAspect, "A picture has a width and a height.");
            }

            return new ViewerLayout(ScreenView.Of(screen), pictureAspect);
        }
    }
}
