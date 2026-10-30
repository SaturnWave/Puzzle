using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Resolves one legal move to a stable board: fire queued effects, settle gravity, clear matches,
    /// repeat until nothing matches. See docs/GAME_DESIGN.md §5.
    /// </summary>
    internal sealed class TurnResolver
    {
        private readonly Board _board;
        private readonly BoardShape _shape;
        private readonly RuleSet _rules;
        private readonly Queue<Effect> _pending;
        private readonly DamageResolver _damage;
        private readonly EffectResolver _effects;
        private readonly MatchFinder _matches;
        private readonly Gravity _gravity;
        private readonly StatsCounter _stats;
        private readonly EventLog _events;
        private readonly int[] _sideHitStamp;
        private int _sideStamp;

        public TurnResolver(
            Board board,
            RuleSet rules,
            Queue<Effect> pending,
            DamageResolver damage,
            EffectResolver effects,
            MatchFinder matches,
            Gravity gravity,
            StatsCounter stats,
            EventLog events)
        {
            _board = board;
            _shape = board.Shape;
            _rules = rules;
            _pending = pending;
            _damage = damage;
            _effects = effects;
            _matches = matches;
            _gravity = gravity;
            _stats = stats;
            _events = events;
            _sideHitStamp = new int[_shape.CellCount];
        }

        public void ResolveSwap(int from, int to)
        {
            Tile moved = _board.Get(from);
            Tile target = _board.Get(to);
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.Swapped(_shape.PositionOf(from), _shape.PositionOf(to)));
            }

            if (moved.IsSpecial && target.IsSpecial)
            {
                Consume(from);
                Consume(to);
                _pending.Enqueue(Effect.ForCombo(moved.Kind, target.Kind, to, _rules));
                RunRounds(PlayerMatch.None);
                return;
            }

            _board.Swap(from, to);
            if (moved.IsSpecial || target.IsSpecial)
            {
                // After the swap the special sits on the other cell and fires there.
                int specialAt = moved.IsSpecial ? to : from;
                Tile special = moved.IsSpecial ? moved : target;
                Tile partner = moved.IsSpecial ? target : moved;
                Consume(specialAt);
                _pending.Enqueue(special.Kind == TileKind.ColorBomb && partner.IsGem
                    ? Effect.ClearColorOf(partner.Color, specialAt)
                    : Effect.ForSpecial(special.Kind, specialAt, _rules));
                RunRounds(PlayerMatch.None);
                return;
            }

            RunRounds(new PlayerMatch(from, to));
        }

        public void ResolveTap(int at)
        {
            Tile special = Consume(at);
            _pending.Enqueue(Effect.ForSpecial(special.Kind, at, _rules));
            RunRounds(PlayerMatch.None);
        }

        /// <summary>A booster's strike: every cell is hit once, then the board settles as it does after a move.</summary>
        public void ResolveStrike(IReadOnlyList<int> cells)
        {
            foreach (int cell in cells)
            {
                _damage.Hit(cell);
            }

            RunRounds(PlayerMatch.None);
        }

        /// <summary>
        /// The celebration after a win: free gems turn into rockets and fire, <paramref name="rockets"/> of them in
        /// all. A board with fewer free gems than rockets left is served in waves, each on the board the wave before
        /// it left behind.
        /// </summary>
        /// <returns>The rockets fired; fewer than asked only when the board ran out of free gems altogether.</returns>
        public int ResolveFinale(int rockets, FreeGems gems, Rng rng)
        {
            int fired = 0;
            while (fired < rockets)
            {
                gems.Collect();
                if (gems.Count == 0)
                {
                    break;
                }

                for (int wave = Math.Min(rockets - fired, gems.Count); wave > 0; wave--)
                {
                    PlaceArmed(gems.Take(rng), rng.NextInt(2) == 0 ? TileKind.RocketH : TileKind.RocketV);
                    fired++;
                }

                RunRounds(PlayerMatch.None);
            }

            return fired;
        }

        /// <summary>
        /// Puts a special on the board and queues it to fire where it stands. Another effect may reach it first and
        /// fire it; the queued entry then finds the tile gone and does nothing.
        /// </summary>
        private void PlaceArmed(int cell, TileKind special)
        {
            _board.Set(cell, Tile.Special(special));
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.SpecialCreated(_shape.PositionOf(cell), special));
            }

            _pending.Enqueue(Effect.ForSpecial(special, cell, _rules).RequiringTileAtOrigin(special));
        }

        /// <summary>Takes a special off the board as it fires (reported as destroyed so the view removes it).</summary>
        private Tile Consume(int index)
        {
            Tile special = _board.Get(index);
            _board.Set(index, Tile.Empty);
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.TileDestroyed(_shape.PositionOf(index), special));
            }

            return special;
        }

        private void RunRounds(PlayerMatch playerMatch)
        {
            _effects.ProcessPending();
            for (int round = 0; ; round++)
            {
                if (round > _rules.MaxResolutionSteps)
                {
                    throw new InvalidOperationException($"Move resolution did not settle within {_rules.MaxResolutionSteps} rounds.");
                }

                _gravity.Settle();
                bool isPlayerRound = round == 0 && playerMatch.Exists;
                IReadOnlyList<MatchGroup> groups = isPlayerRound
                    ? _matches.FindGroups(playerMatch.CellA, playerMatch.CellB)
                    : _matches.FindGroups(-1, -1);
                if (groups.Count == 0)
                {
                    return;
                }

                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.RoundStarted(round));
                }

                if (!isPlayerRound)
                {
                    _stats.Cascades++;
                }

                ClearGroups(groups);
                _effects.ProcessPending();
            }
        }

        private void ClearGroups(IReadOnlyList<MatchGroup> groups)
        {
            foreach (MatchGroup group in groups)
            {
                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.MatchCleared(group.Color, ToPositions(group.Cells)));
                }

                foreach (int cell in group.Cells)
                {
                    _damage.Hit(cell);
                }
            }

            // A box next to several matched cells still takes only one side hit per round.
            _sideStamp++;
            foreach (MatchGroup group in groups)
            {
                foreach (int cell in group.Cells)
                {
                    SideHit(_shape.Left(cell));
                    SideHit(_shape.Right(cell));
                    SideHit(_shape.Up(cell));
                    SideHit(_shape.Down(cell));
                }
            }

            foreach (MatchGroup group in groups)
            {
                if (group.Special is SpecialSpawn spawn && _board.Get(spawn.Anchor).IsEmpty && _board.Chain(spawn.Anchor) == 0)
                {
                    _board.Set(spawn.Anchor, Tile.Special(spawn.Kind));
                    _stats.RecordSpecialCreated(spawn.Kind);
                    if (_events.IsEnabled)
                    {
                        _events.Add(new GameEvent.SpecialCreated(_shape.PositionOf(spawn.Anchor), spawn.Kind));
                    }
                }
            }
        }

        private void SideHit(int index)
        {
            if (index < 0 || _sideHitStamp[index] == _sideStamp)
            {
                return;
            }

            _sideHitStamp[index] = _sideStamp;
            _damage.SideHit(index);
        }

        private Position[] ToPositions(List<int> cells)
        {
            var positions = new Position[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                positions[i] = _shape.PositionOf(cells[i]);
            }

            return positions;
        }

        /// <summary>The two cells of a plain swap; specials spawn there when they belong to the match.</summary>
        private readonly struct PlayerMatch
        {
            public PlayerMatch(int cellA, int cellB)
            {
                CellA = cellA;
                CellB = cellB;
                Exists = true;
            }

            public static PlayerMatch None => default;

            public int CellA { get; }

            public int CellB { get; }

            public bool Exists { get; }
        }
    }
}
