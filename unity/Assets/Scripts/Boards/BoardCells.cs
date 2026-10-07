using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Client.Boards
{
    /// <summary>The cells of a board that pieces can lie in.</summary>
    public static class BoardCells
    {
        /// <summary>Every playable cell, row by row from the top, each row from the left.</summary>
        public static IEnumerable<Position> Of(BoardShape shape)
        {
            for (int y = 0; y < shape.Height; y++)
            {
                for (int x = 0; x < shape.Width; x++)
                {
                    var cell = new Position(x, y);
                    if (shape.IsPlayable(cell))
                    {
                        yield return cell;
                    }
                }
            }
        }
    }
}
