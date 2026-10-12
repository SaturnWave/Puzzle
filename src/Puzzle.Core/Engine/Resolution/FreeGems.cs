using System.Collections.Generic;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// The gems a gift may land on: plain gems that no chain holds. A start booster's specials and the finale's
    /// rockets replace such gems, each one a different gem.
    /// </summary>
    internal sealed class FreeGems
    {
        private readonly Board _board;
        private readonly List<int> _cells = new List<int>();

        public FreeGems(Board board)
        {
            _board = board;
        }

        /// <summary>How many gems are left to take since the last <see cref="Collect"/>.</summary>
        public int Count => _cells.Count;

        /// <summary>Looks at the board as it is now; gems taken before may be taken again.</summary>
        public void Collect()
        {
            _cells.Clear();
            BoardShape shape = _board.Shape;
            for (int i = 0; i < shape.CellCount; i++)
            {
                if (shape.IsPlayable(i) && _board.Get(i).IsGem && _board.Chain(i) == 0)
                {
                    _cells.Add(i);
                }
            }
        }

        /// <summary>The cell of a gem chosen at random among those not taken yet; requires <see cref="Count"/> above 0.</summary>
        public int Take(Rng rng)
        {
            int pick = rng.NextInt(_cells.Count);
            int cell = _cells[pick];
            _cells[pick] = _cells[_cells.Count - 1];
            _cells.RemoveAt(_cells.Count - 1);
            return cell;
        }
    }
}
