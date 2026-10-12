using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Rearranges free gems when no legal move is left: first by permuting their colors, then (if that keeps
    /// failing) by repainting them. Never costs the player a move.
    /// </summary>
    internal sealed class Shuffler
    {
        private readonly Board _board;
        private readonly MatchFinder _matches;
        private readonly MoveFinder _moves;
        private readonly GemPainter _painter;
        private readonly RuleSet _rules;
        private readonly Rng _rng;
        private readonly StatsCounter _stats;
        private readonly EventLog _events;
        private readonly List<int> _cells = new List<int>();
        private readonly List<Tile> _gems = new List<Tile>();

        public Shuffler(Board board, MatchFinder matches, MoveFinder moves, RuleSet rules, Rng rng, StatsCounter stats, EventLog events)
        {
            _board = board;
            _matches = matches;
            _moves = moves;
            _painter = new GemPainter(board, matches);
            _rules = rules;
            _rng = rng;
            _stats = stats;
            _events = events;
        }

        /// <returns>False when no arrangement with a legal move was found (the board is degenerate).</returns>
        public bool TryShuffle(IReadOnlyList<GemColor> palette)
        {
            CollectFreeGems();
            if (_cells.Count == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < _rules.ShuffleAttempts; attempt++)
            {
                Permute();
                if (IsPlayable())
                {
                    return Succeed();
                }
            }

            for (int attempt = 0; attempt < _rules.ShuffleAttempts; attempt++)
            {
                foreach (int cell in _cells)
                {
                    _painter.Paint(cell, palette, _rng);
                }

                if (IsPlayable())
                {
                    return Succeed();
                }
            }

            return false;
        }

        /// <summary>
        /// The shuffle for a game that was over and may go on (the extra moves): a board without a move is revived as
        /// <see cref="TryShuffle"/> revives it, and one that cannot be revived stays exactly as it was.
        /// </summary>
        /// <returns>False when nothing changed.</returns>
        public bool TryShuffleOrLeave(IReadOnlyList<GemColor> palette)
        {
            CollectFreeGems();
            Tile[] before = _gems.ToArray();
            if (TryShuffle(palette))
            {
                return true;
            }

            PutBack(before);
            return false;
        }

        /// <summary>
        /// The shuffle as a booster. The board has moves, so nothing forces a result and only a true rearrangement
        /// counts: the free gems trade places, none is repainted, and at least one cell holds another gem than
        /// before. A board that offers no such arrangement stays exactly as it was.
        /// </summary>
        /// <returns>False when nothing changed.</returns>
        public bool TryRearrangeOrLeave()
        {
            CollectFreeGems();
            Tile[] before = _gems.ToArray();
            for (int attempt = 0; attempt < _rules.ShuffleAttempts && before.Length > 1; attempt++)
            {
                Permute();
                if (IsPlayable() && IsAnotherArrangementThan(before))
                {
                    return Succeed();
                }
            }

            PutBack(before);
            return false;
        }

        /// <summary>Whether a collected cell holds another gem than it did in <paramref name="before"/>.</summary>
        private bool IsAnotherArrangementThan(Tile[] before)
        {
            for (int k = 0; k < before.Length; k++)
            {
                if (_gems[k] != before[k])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Writes the gems back to the collected cells as they lay in <paramref name="before"/>.</summary>
        private void PutBack(Tile[] before)
        {
            for (int k = 0; k < _cells.Count; k++)
            {
                _board.Set(_cells[k], before[k]);
            }
        }

        private void CollectFreeGems()
        {
            _cells.Clear();
            _gems.Clear();
            for (int i = 0; i < _board.Shape.CellCount; i++)
            {
                if (_board.Shape.IsPlayable(i) && _board.Get(i).IsGem && _board.Chain(i) == 0)
                {
                    _cells.Add(i);
                    _gems.Add(_board.Get(i));
                }
            }
        }

        /// <summary>Fisher-Yates over the collected gems, written back to the same cells.</summary>
        private void Permute()
        {
            for (int i = _gems.Count - 1; i > 0; i--)
            {
                int j = _rng.NextInt(i + 1);
                Tile swap = _gems[i];
                _gems[i] = _gems[j];
                _gems[j] = swap;
            }

            for (int k = 0; k < _cells.Count; k++)
            {
                _board.Set(_cells[k], _gems[k]);
            }
        }

        private bool IsPlayable() => !_matches.AnyMatch() && _moves.HasAny();

        private bool Succeed()
        {
            _stats.Shuffles++;
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.Shuffled());
            }

            return true;
        }
    }
}
