using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Settles the board after clears: straight falls (through gaps) with spawner refills, diagonal slides
    /// into cells sealed off by boxes or chains, and item collection at column exits.
    /// </summary>
    internal sealed class Gravity
    {
        private readonly Board _board;
        private readonly BoardShape _shape;
        private readonly ITileSource _source;
        private readonly GoalTracker _goals;
        private readonly StatsCounter _stats;
        private readonly EventLog _events;
        private readonly ItemLedger _items;
        private int _slideParity;

        public Gravity(Board board, ITileSource source, GoalTracker goals, StatsCounter stats, EventLog events, ItemLedger items)
        {
            _board = board;
            _shape = board.Shape;
            _source = source;
            _goals = goals;
            _stats = stats;
            _events = events;
            _items = items;
        }

        /// <summary>Runs until no tile can move and no item sits on an exit.</summary>
        public void Settle()
        {
            do
            {
                Fill();
            }
            while (CollectItems());
        }

        private void Fill()
        {
            do
            {
                FallStraight();
            }
            while (SlideOneDiagonally());
        }

        /// <summary>
        /// One bottom-up pass per column. Every empty cell pulls the nearest tile above it, or a fresh spawn when
        /// the column is open to its spawner. A box or chained tile blocks the pull.
        /// </summary>
        private void FallStraight()
        {
            for (int x = 0; x < _shape.Width; x++)
            {
                IReadOnlyList<int> column = _shape.ColumnBottomUp(x);
                for (int k = 0; k < column.Count; k++)
                {
                    int target = column[k];
                    if (!_board.Get(target).IsEmpty)
                    {
                        continue;
                    }

                    int source = NearestTileAbove(column, k);
                    if (source >= 0)
                    {
                        if (_board.CanFall(source))
                        {
                            MoveTile(source, target);
                        }
                    }
                    else if (_shape.HasSpawner(x))
                    {
                        Spawn(target, x);
                    }
                }
            }
        }

        private int NearestTileAbove(IReadOnlyList<int> column, int k)
        {
            for (int m = k + 1; m < column.Count; m++)
            {
                if (!_board.Get(column[m]).IsEmpty)
                {
                    return column[m];
                }
            }

            return -1;
        }

        /// <summary>
        /// After straight falls, any remaining empty cell is sealed from above. Fill the deepest one from an
        /// upper diagonal neighbor that is resting (would not fall straight anyway). Alternates the preferred
        /// side each call so neither side is favored.
        /// </summary>
        private bool SlideOneDiagonally()
        {
            bool leftFirst = _slideParity == 0;
            _slideParity ^= 1;
            for (int y = _shape.Height - 1; y >= 0; y--)
            {
                for (int k = 0; k < _shape.Width; k++)
                {
                    int x = leftFirst ? k : _shape.Width - 1 - k;
                    int target = _shape.IndexOf(x, y);
                    if (!_shape.IsPlayable(target) || !_board.Get(target).IsEmpty)
                    {
                        continue;
                    }

                    int first = leftFirst ? _shape.UpLeft(target) : _shape.UpRight(target);
                    int second = leftFirst ? _shape.UpRight(target) : _shape.UpLeft(target);
                    if (TrySlide(first, target) || TrySlide(second, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TrySlide(int source, int target)
        {
            if (source < 0 || !_board.CanFall(source))
            {
                return false;
            }

            int below = _shape.FallTarget(source);
            if (below >= 0 && _board.Get(below).IsEmpty)
            {
                return false;
            }

            MoveTile(source, target);
            return true;
        }

        private bool CollectItems()
        {
            bool collected = false;
            for (int x = 0; x < _shape.Width; x++)
            {
                IReadOnlyList<int> column = _shape.ColumnBottomUp(x);
                if (column.Count == 0)
                {
                    continue;
                }

                int exit = column[0];
                if (_board.Get(exit).Kind != TileKind.Item || _board.Chain(exit) > 0)
                {
                    continue;
                }

                _board.Set(exit, Tile.Empty);
                _items.Collected++;
                _stats.ItemsCollected++;
                _goals.RecordItemCollected();
                collected = true;
                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.ItemCollected(_shape.PositionOf(exit)));
                }
            }

            return collected;
        }

        private void MoveTile(int source, int target)
        {
            _board.Set(target, _board.Get(source));
            _board.Set(source, Tile.Empty);
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.TileMoved(_shape.PositionOf(source), _shape.PositionOf(target)));
            }
        }

        private void Spawn(int target, int column)
        {
            Tile tile = _source.Next(column);
            _board.Set(target, tile);
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.TileSpawned(_shape.PositionOf(target), tile));
            }
        }
    }
}
