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
    }

    /// <summary>
    /// The rule that hangs the lamps, as the web test page has it: in the board's top corners when both are cut away
    /// wide enough, else above the frame; either way only when enough bare wall lies above the board's cells. This
    /// file has no Unity dependency, so the repository's tests compile it too.
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

        /// <summary>The lamp's foot: this far above the cells' top edge over the frame, or below it in a cut corner.</summary>
        public const float FootAboveTheCells = ScreenLayout.FrameReach + 10f;
        public const float FootBelowTheCellsTop = 13f;

        /// <summary>The lamp's middle is this far in from the side of the board's cells.</summary>
        public const float MiddleInFromTheSide = 23f;

        /// <param name="wallAboveBoard">The bare wall between the plaques and the top of the board's cells, in points.</param>
        public static SconcePlace PlaceBeside(BoardShape board, float wallAboveBoard)
        {
            if (HasBothTopCornersCut(board))
            {
                return wallAboveBoard >= RoomInTheCutCorners ? SconcePlace.InTheCutCorners : SconcePlace.None;
            }

            return wallAboveBoard >= RoomAboveTheFrame ? SconcePlace.AboveTheFrame : SconcePlace.None;
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
