using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;

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
                _board.Set(from, Tile.Empty);
                _board.Set(to, Tile.Empty);
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
                _board.Set(specialAt, Tile.Empty);
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
            Tile special = _board.Get(at);
            _board.Set(at, Tile.Empty);
            _pending.Enqueue(Effect.ForSpecial(special.Kind, at, _rules));
            RunRounds(PlayerMatch.None);
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
