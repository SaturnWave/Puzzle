using Puzzle.Client.Boards;
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>
    /// How a board comes on: its pieces pop up in a wave that runs from the top left corner to the bottom right one,
    /// each with the tape that lies over it. The floor and the slabs are there from the start, so the board's shape
    /// is seen before its pieces.
    /// </summary>
    public static class BoardEntrance
    {
        /// <summary>Each diagonal of the board starts this long after the one before it.</summary>
        private const float SecondsPerDiagonal = 0.014f;

        private const float PopSeconds = 0.3f;

        public static void Play(BoardView board, TweenSet tweens)
        {
            foreach (PieceSprite piece in board.Pieces)
            {
                PieceSprite popping = piece;
                tweens.Start(new Tween(DelayOf(piece.Cell.X + piece.Cell.Y), PopSeconds, Easing.Overshoot, grown => popping.Size(grown, grown)));
            }

            foreach (CellSprite tape in board.Tapes)
            {
                Transform popping = tape.Transform;
                Vector3 atRest = popping.localScale;
                tweens.Start(new Tween(DelayOf(tape.Cell.X + tape.Cell.Y), PopSeconds, Easing.Overshoot, grown => popping.localScale = atRest * grown));
            }
        }

        private static float DelayOf(int diagonal) => diagonal * SecondsPerDiagonal;
    }
}
