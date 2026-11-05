using System.Collections.Generic;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Builds the starting board: layout presets as given, random cells colored without any ready-made match,
    /// re-rolled until the board offers <see cref="RuleSet.MinInitialMoves"/> legal moves.
    /// </summary>
    internal sealed class BoardInitializer
    {
        private readonly Board _board;
        private readonly MatchFinder _matches;
        private readonly MoveFinder _moves;
        private readonly GemPainter _painter;
        private readonly RuleSet _rules;

        public BoardInitializer(Board board, MatchFinder matches, MoveFinder moves, RuleSet rules)
        {
            _board = board;
            _matches = matches;
            _moves = moves;
            _painter = new GemPainter(board, matches);
            _rules = rules;
        }

        /// <exception cref="LevelInitializationException">No valid starting board exists for this layout.</exception>
        public void Fill(LevelLayout layout, IReadOnlyList<GemColor> palette, Rng rng)
        {
            BoardShape shape = _board.Shape;
            if (!layout.HasRandomCells)
            {
                PlacePresets(layout);
                RequirePresetBoardPlayable();
                return;
            }

            for (int attempt = 0; attempt < _rules.InitialFillAttempts; attempt++)
            {
                PlacePresets(layout);
                for (int i = 0; i < shape.CellCount; i++)
                {
                    if (shape.IsPlayable(i) && layout.Preset(i) is null)
                    {
                        _painter.Paint(i, palette, rng);
                    }
                }

                if (!_matches.AnyMatch() && _moves.Count(_rules.MinInitialMoves) >= _rules.MinInitialMoves)
                {
                    return;
                }
            }

            throw new LevelInitializationException(
                $"No starting board without matches and with {_rules.MinInitialMoves}+ legal moves after {_rules.InitialFillAttempts} attempts.");
        }

        private void PlacePresets(LevelLayout layout)
        {
            BoardShape shape = _board.Shape;
            for (int i = 0; i < shape.CellCount; i++)
            {
                if (!shape.IsPlayable(i))
                {
                    continue;
                }

                _board.Set(i, layout.Preset(i) ?? Tile.Empty);
                _board.SetChain(i, layout.Chain(i));
                _board.SetJelly(i, layout.Jelly(i));
            }
        }

        private void RequirePresetBoardPlayable()
        {
            if (_matches.AnyMatch())
            {
                throw new LevelInitializationException("The preset starting board already contains a match.");
            }

            if (!_moves.HasAny())
            {
                throw new LevelInitializationException("The preset starting board has no legal move.");
            }
        }
    }
}
