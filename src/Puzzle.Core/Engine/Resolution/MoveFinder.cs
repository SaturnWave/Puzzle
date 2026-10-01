using System.Collections.Generic;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Enumerates legal moves: a swap is legal when either tile is a special or the swap creates a match;
    /// a tap is legal on any free special when the rules allow tap activation.
    /// </summary>
    internal sealed class MoveFinder
    {
        private readonly Board _board;
        private readonly BoardShape _shape;
        private readonly MatchFinder _matches;
        private readonly RuleSet _rules;

        public MoveFinder(Board board, MatchFinder matches, RuleSet rules)
        {
            _board = board;
            _shape = board.Shape;
            _matches = matches;
            _rules = rules;
        }

        public void Collect(List<Move> output) => Scan(output, int.MaxValue);

        public bool HasAny() => Scan(null, 1) > 0;

        /// <summary>Counts legal moves, stopping early at <paramref name="stopAt"/>.</summary>
        public int Count(int stopAt) => Scan(null, stopAt);

        public bool IsLegalSwap(int a, int b)
        {
            if (!_board.CanSwap(a) || !_board.CanSwap(b))
            {
                return false;
            }

            if (_board.Get(a).IsSpecial || _board.Get(b).IsSpecial)
            {
                return true;
            }

            _board.Swap(a, b);
            bool createsMatch = _matches.HasMatchAt(a) || _matches.HasMatchAt(b);
            _board.Swap(a, b);
            return createsMatch;
        }

        public bool IsLegalTap(int index) => _rules.AllowTapActivation && _board.Get(index).IsSpecial && _board.Chain(index) == 0;

        private int Scan(List<Move>? output, int stopAt)
        {
            int found = 0;
            for (int i = 0; i < _shape.CellCount && found < stopAt; i++)
            {
                if (!_shape.IsPlayable(i))
                {
                    continue;
                }

                if (IsLegalTap(i))
                {
                    output?.Add(Move.Tap(_shape.PositionOf(i)));
                    found++;
                }

                found += TryAddSwap(output, i, _shape.Right(i));
                found += TryAddSwap(output, i, _shape.Down(i));
            }

            return found;
        }

        private int TryAddSwap(List<Move>? output, int from, int to)
        {
            if (to < 0 || !IsLegalSwap(from, to))
            {
                return 0;
            }

            output?.Add(Move.Swap(_shape.PositionOf(from), _shape.PositionOf(to)));
            return 1;
        }
    }
}
