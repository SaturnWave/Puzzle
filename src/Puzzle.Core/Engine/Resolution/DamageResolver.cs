using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Applies one "hit" to a cell — the single damage rule shared by matches and special effects.
    /// </summary>
    /// <remarks>
    /// A chain absorbs the hit and protects the tile and the floor. Otherwise a gem or hidden refill is
    /// destroyed, a special is destroyed and queued to fire, a box loses one hit point, an item is unaffected.
    /// The jelly floor loses a layer unless a chain or a box covered the cell.
    /// </remarks>
    internal sealed class DamageResolver
    {
        private readonly Board _board;
        private readonly GoalTracker _goals;
        private readonly StatsCounter _stats;
        private readonly EventLog _events;
        private readonly Queue<Effect> _pending;
        private readonly RuleSet _rules;

        public DamageResolver(Board board, GoalTracker goals, StatsCounter stats, EventLog events, Queue<Effect> pending, RuleSet rules)
        {
            _board = board;
            _goals = goals;
            _stats = stats;
            _events = events;
            _pending = pending;
            _rules = rules;
        }

        public void Hit(int index)
        {
            if (!_board.Shape.IsPlayable(index))
            {
                return;
            }

            if (_board.Chain(index) > 0)
            {
                DamageChain(index);
                return;
            }

            Tile tile = _board.Get(index);
            switch (tile.Kind)
            {
                case TileKind.Gem:
                    Destroy(index, tile);
                    _goals.RecordGemCleared(tile.Color);
                    _stats.GemsCleared++;
                    DamageJelly(index);
                    break;
                case TileKind.RocketH:
                case TileKind.RocketV:
                case TileKind.Bomb:
                case TileKind.BigBomb:
                case TileKind.Seeker:
                case TileKind.ColorBomb:
                    Destroy(index, tile);
                    _pending.Enqueue(Effect.ForSpecial(tile.Kind, index, _rules));
                    DamageJelly(index);
                    break;
                case TileKind.Box:
                    DamageBox(index, tile);
                    break;
                case TileKind.Hidden:
                    Destroy(index, tile);
                    DamageJelly(index);
                    break;
                case TileKind.Item:
                case TileKind.Empty:
                    DamageJelly(index);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled tile kind {tile.Kind}.");
            }
        }

        /// <summary>A match next to the cell: only blockers that react to adjacent matches take damage.</summary>
        public void SideHit(int index)
        {
            Tile tile = _board.Get(index);
            if (tile.Kind == TileKind.Box)
            {
                DamageBox(index, tile);
            }
        }

        private void Destroy(int index, Tile tile)
        {
            _board.Set(index, Tile.Empty);
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.TileDestroyed(_board.Shape.PositionOf(index), tile));
            }
        }

        private void DamageBox(int index, Tile box)
        {
            int hpLeft = box.Hp - 1;
            _board.Set(index, hpLeft > 0 ? Tile.Box(hpLeft) : Tile.Empty);
            _stats.BoxHpRemoved++;
            if (hpLeft == 0)
            {
                _stats.BoxesDestroyed++;
                _goals.RecordBoxDestroyed();
            }

            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.BoxDamaged(_board.Shape.PositionOf(index), hpLeft));
            }
        }

        private void DamageChain(int index)
        {
            int hpLeft = _board.Chain(index) - 1;
            _board.SetChain(index, hpLeft);
            _stats.ChainHpRemoved++;
            if (hpLeft == 0)
            {
                _stats.ChainsBroken++;
                _goals.RecordChainBroken();
            }

            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.ChainDamaged(_board.Shape.PositionOf(index), hpLeft));
            }
        }

        private void DamageJelly(int index)
        {
            int layers = _board.Jelly(index);
            if (layers == 0)
            {
                return;
            }

            _board.SetJelly(index, layers - 1);
            _stats.JellyLayersRemoved++;
            if (layers == 1)
            {
                _stats.JellyCleared++;
                _goals.RecordJellyCleared();
            }

            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.JellyDamaged(_board.Shape.PositionOf(index), layers - 1));
            }
        }
    }
}
