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

        /// <summary>Paints a palette color that does not create a match at the cell, if one exists.</summary>
        public void Paint(int index, IReadOnlyList<GemColor> palette, Rng rng)
        {
            _allowed.Clear();
            foreach (GemColor candidate in palette)
            {
                _board.Set(index, Tile.Gem(candidate));
                if (!_matches.HasMatchAt(index))
                {
                    _allowed.Add(candidate);
                }
            }

            GemColor color = _allowed.Count > 0 ? _allowed[rng.NextInt(_allowed.Count)] : palette[rng.NextInt(palette.Count)];
            _board.Set(index, Tile.Gem(color));
        }
    }
}
