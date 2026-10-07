using Puzzle.Core.Model;

namespace Puzzle.Client.Layout
{
    /// <summary>Where the wall's two lamps hang beside a board.</summary>
    public enum SconcePlace
    {
        /// <summary>The wall above the board is too short for a lamp: none is shown.</summary>
        None,

        /// <summary>On the wall above the frame, near the board's top corners.</summary>
        AboveTheFrame,

        /// <summary>In the board's two top corners, where the board's own shape leaves the wall bare.</summary>
        InTheCutCorners,

        /// <summary>On the wall at the board's two sides, where a wide screen leaves it bare.</summary>
        BesideTheFrame,
    }

    /// <summary>
    /// The rule that hangs the lamps. Above the board it is the web test page's: in the board's top corners when
    /// both are cut away wide enough, else above the frame; either way only when enough bare wall lies above the
    /// board's cells. A screen wider than a phone has no wall above its board, since the board takes all the height,
    /// but bare wall at both sides of it: there the lamps hang beside the frame. This file has no Unity dependency,
    /// so the repository's tests compile it too.
    /// </summary>
    public static class Sconces
    {
        /// <summary>A lamp is 30 points wide: a corner must be cut away this many cells wide to hold one.</summary>
        public const int CutCornerCells = 2;

        /// <summary>
        /// The bare wall a lamp needs above the cells, in points. A lamp above the frame reaches 80 points above the
        /// cells; the rest of its room keeps its bowl clear of the shadow under the plaques.
        /// </summary>
        public const float RoomAboveTheFrame = 86f;
        public const float RoomInTheCutCorners = 62f;

        /// <summary>The bare wall a lamp needs beside the frame: its 30 points, and 9 of wall at each side of it.</summary>
        public const float RoomBesideTheFrame = 48f;

        /// <summary>The lamp's foot: this far above the cells' top edge over the frame, or below it in a cut corner.</summary>
        public const float FootAboveTheCells = ScreenLayout.FrameReach + 10f;
        public const float FootBelowTheCellsTop = 13f;

        /// <summary>The lamp's middle is this far in from the side of the board's cells.</summary>
        public const float MiddleInFromTheSide = 23f;

        /// <summary>
        /// Beside the frame a lamp's foot is this far below the cells' top edge, in parts of the board's height:
        /// its bowl then stands about a third of the way down the board, where a lamp beside a picture hangs.
        /// </summary>
        public const float FootDownTheBoard = 0.42f;

        /// <param name="wallAboveBoard">The bare wall between the plaques and the top of the board's cells, in points.</param>
        /// <param name="wallBesideBoard">The bare wall between a side of the screen and the board's frame, in points.</param>
        public static SconcePlace PlaceBeside(BoardShape board, float wallAboveBoard, float wallBesideBoard)
        {
            bool hasCutCorners = HasBothTopCornersCut(board);
            if (hasCutCorners && wallAboveBoard >= RoomInTheCutCorners)
            {
                return SconcePlace.InTheCutCorners;
            }

            if (!hasCutCorners && wallAboveBoard >= RoomAboveTheFrame)
            {
                return SconcePlace.AboveTheFrame;
            }

            return wallBesideBoard >= RoomBesideTheFrame ? SconcePlace.BesideTheFrame : SconcePlace.None;
        }

        private static bool HasBothTopCornersCut(BoardShape board)
        {
            for (int step = 0; step < CutCornerCells; step++)
            {
                bool leftIsCell = board.IsPlayable(new Position(step, 0));
                bool rightIsCell = board.IsPlayable(new Position(board.Width - 1 - step, 0));
                if (leftIsCell || rightIsCell)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
