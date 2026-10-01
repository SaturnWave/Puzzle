using System.Collections.Generic;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Colors cells one by one so that no cell completes a match with gems already on the board.
    /// Shared by the starting-board fill and the shuffle fallback.
    /// </summary>
    internal sealed class GemPainter
    {
        private readonly Board _board;
        private readonly MatchFinder _matches;
        private readonly List<GemColor> _allowed = new List<GemColor>(GemColors.Count);

        public GemPainter(Board board, MatchFinder matches)
        {
            _board = board;
            _matches = matches;
        }

        /// <summary>Paints a color that does not create a match at the cell, if one exists among the first <paramref name="colors"/>.</summary>
        public void Paint(int index, int colors, Rng rng)
        {
            _allowed.Clear();
            for (int c = 0; c < colors; c++)
            {
                _board.Set(index, Tile.Gem((GemColor)c));
                if (!_matches.HasMatchAt(index))
                {
                    _allowed.Add((GemColor)c);
                }
            }

            GemColor color = _allowed.Count > 0 ? _allowed[rng.NextInt(_allowed.Count)] : (GemColor)rng.NextInt(colors);
            _board.Set(index, Tile.Gem(color));
        }
    }
}
