using System;
using System.Collections.Generic;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>
    /// Fires queued special effects in FIFO order. One effect hits each cell at most once; a special it hits
    /// is queued behind it, which produces chain reactions.
    /// </summary>
    internal sealed class EffectResolver
    {
        private readonly Board _board;
        private readonly BoardShape _shape;
        private readonly GoalTracker _goals;
        private readonly StatsCounter _stats;
        private readonly EventLog _events;
        private readonly DamageResolver _damage;
        private readonly Queue<Effect> _pending;
        private readonly RuleSet _rules;
        private readonly Rng _rng;
        private readonly int[] _hitStamp;
        private readonly List<int> _ties = new List<int>();
        private readonly int[] _colorCounts = new int[GemColors.Count];
        private int _stamp;

        public EffectResolver(
            Board board,
            GoalTracker goals,
            StatsCounter stats,
            EventLog events,
            DamageResolver damage,
            Queue<Effect> pending,
            RuleSet rules,
            Rng rng)
        {
            _board = board;
            _shape = board.Shape;
            _goals = goals;
            _stats = stats;
            _events = events;
            _damage = damage;
            _pending = pending;
            _rules = rules;
            _rng = rng;
            _hitStamp = new int[_shape.CellCount];
        }

        public void ProcessPending()
        {
            while (_pending.Count > 0)
            {
                Apply(_pending.Dequeue());
            }
        }

        private void Apply(Effect effect)
        {
            if (effect.RequiredAtOrigin is TileKind required)
            {
                if (_board.Get(effect.Origin).Kind != required)
                {
                    return; // another effect already fired this special
                }

                _board.Set(effect.Origin, Tile.Empty);
                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.TileDestroyed(_shape.PositionOf(effect.Origin), Tile.Special(required)));
                }
            }

            _stats.SpecialsActivated++;
            _stamp++;
            if (_events.IsEnabled)
            {
                _events.Add(new GameEvent.EffectTriggered(_shape.PositionOf(effect.Origin), effect.Kind, effect.Radius));
            }

            int x = _shape.XOf(effect.Origin);
            int y = _shape.YOf(effect.Origin);
            switch (effect.Kind)
            {
                case EffectKind.ClearRow:
                    HitRow(y);
                    break;
                case EffectKind.ClearColumn:
                    HitColumn(x);
                    break;
                case EffectKind.Explode:
                    HitArea(x, y, effect.Radius);
                    break;
                case EffectKind.Cross:
                    for (int offset = -effect.Radius; offset <= effect.Radius; offset++)
                    {
                        HitRow(y + offset);
                        HitColumn(x + offset);
                    }

                    break;
                case EffectKind.Seeker:
                    ApplySeeker(effect);
                    break;
                case EffectKind.ClearColor:
                    ApplyClearColor(effect.Color);
                    break;
                case EffectKind.ConvertColor:
                    ApplyConversion(effect.Payload ?? throw new InvalidOperationException("A conversion needs a payload."));
                    break;
                case EffectKind.ClearBoard:
                    for (int i = 0; i < _shape.CellCount; i++)
                    {
                        HitOnce(i);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unhandled effect {effect.Kind}.");
            }
        }

        private void HitOnce(int index)
        {
            if (index < 0 || !_shape.IsPlayable(index) || _hitStamp[index] == _stamp)
            {
                return;
            }

            _hitStamp[index] = _stamp;
            _damage.Hit(index);
        }

        private void HitRow(int y)
        {
            if (y < 0 || y >= _shape.Height)
            {
                return;
            }

            for (int x = 0; x < _shape.Width; x++)
            {
                HitOnce(_shape.IndexOf(x, y));
            }
        }

        private void HitColumn(int x)
        {
            if (x < 0 || x >= _shape.Width)
            {
                return;
            }

            for (int y = 0; y < _shape.Height; y++)
            {
                HitOnce(_shape.IndexOf(x, y));
            }
        }

        private void HitArea(int centerX, int centerY, int radius)
        {
            for (int y = Math.Max(0, centerY - radius); y <= Math.Min(_shape.Height - 1, centerY + radius); y++)
            {
                for (int x = Math.Max(0, centerX - radius); x <= Math.Min(_shape.Width - 1, centerX + radius); x++)
                {
                    HitOnce(_shape.IndexOf(x, y));
                }
            }
        }

        /// <summary>Hits the plus around the origin, then flies to <see cref="Effect.Count"/> targets.</summary>
        private void ApplySeeker(Effect effect)
        {
            int origin = effect.Origin;
            HitOnce(origin);
            HitOnce(_shape.Left(origin));
            HitOnce(_shape.Right(origin));
            HitOnce(_shape.Up(origin));
            HitOnce(_shape.Down(origin));

            for (int flight = 0; flight < effect.Count; flight++)
            {
                int target = PickSeekerTarget();
                if (target < 0)
                {
                    return;
                }

                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.SeekerLaunched(_shape.PositionOf(origin), _shape.PositionOf(target)));
                }

                if (effect.Payload is TileKind payload)
                {
                    _hitStamp[target] = _stamp; // later flights pick other targets
                    _pending.Enqueue(Effect.ForSpecial(payload, target, _rules));
                }
                else
                {
                    HitOnce(target);
                }
            }
        }

        /// <summary>The not-yet-hit cell that helps the level's goals most; ties are broken randomly.</summary>
        private int PickSeekerTarget()
        {
            int best = int.MinValue;
            _ties.Clear();
            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (!_shape.IsPlayable(i) || _hitStamp[i] == _stamp)
                {
                    continue;
                }

                int score = SeekerScore(i);
                if (score > best)
                {
                    best = score;
                    _ties.Clear();
                }

                if (score == best)
                {
                    _ties.Add(i);
                }
            }

            return _ties.Count == 0 ? -1 : _ties[_rng.NextInt(_ties.Count)];
        }

        private int SeekerScore(int index)
        {
            Tile tile = _board.Get(index);
            int score = 0;
            if (_board.Chain(index) > 0)
            {
                score += _goals.WantsChains ? 60 : 2;
            }

            if (tile.Kind == TileKind.Box)
            {
                score += _goals.WantsBoxes ? 70 + (tile.Hp == 1 ? 5 : 0) : 3;
            }
            else if (_board.Jelly(index) > 0)
            {
                score += _goals.WantsJelly ? 50 : 1;
            }

            if (tile.IsGem)
            {
                score += _goals.WantsColor(tile.Color) ? 30 : 1;
            }
            else if (tile.IsSpecial)
            {
                score += 4;
            }
            else if (tile.Kind == TileKind.Item)
            {
                score -= 10; // items cannot be destroyed
            }

            int above = _shape.FallSource(index);
            if (_goals.WantsItems && above >= 0 && _board.Get(above).Kind == TileKind.Item)
            {
                score += 25; // clearing below an item lets it drop
            }

            return score;
        }

        private void ApplyClearColor(GemColor? requested)
        {
            // MostCommonColor only runs (and only draws randomness) when no color was requested.
            if ((requested ?? MostCommonColor()) is not GemColor color)
            {
                return;
            }

            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (_board.Get(i).IsGemOf(color))
                {
                    HitOnce(i);
                }
            }
        }

        /// <summary>Turns every free gem of the most common color into the payload special and queues them all.</summary>
        private void ApplyConversion(TileKind payload)
        {
            if (MostCommonColor() is not GemColor color)
            {
                return;
            }

            for (int i = 0; i < _shape.CellCount; i++)
            {
                if (!_board.Get(i).IsGemOf(color) || _board.Chain(i) > 0)
                {
                    continue;
                }

                TileKind kind = payload.IsRocket()
                    ? (_rng.NextInt(2) == 0 ? TileKind.RocketH : TileKind.RocketV)
                    : payload;
                _board.Set(i, Tile.Special(kind));
                if (_events.IsEnabled)
                {
                    _events.Add(new GameEvent.SpecialCreated(_shape.PositionOf(i), kind));
                }

                _pending.Enqueue(Effect.ForSpecial(kind, i, _rules).RequiringTileAtOrigin(kind));
            }
        }

        private GemColor? MostCommonColor()
        {
            Array.Clear(_colorCounts, 0, _colorCounts.Length);
            for (int i = 0; i < _shape.CellCount; i++)
            {
                Tile tile = _board.Get(i);
                if (tile.IsGem)
                {
                    _colorCounts[(int)tile.Color]++;
                }
            }

            int best = 0;
            _ties.Clear();
            for (int c = 0; c < _colorCounts.Length; c++)
            {
                if (_colorCounts[c] > best)
                {
                    best = _colorCounts[c];
                    _ties.Clear();
                }

                if (_colorCounts[c] == best && best > 0)
                {
                    _ties.Add(c);
                }
            }

            return _ties.Count == 0 ? (GemColor?)null : (GemColor)_ties[_rng.NextInt(_ties.Count)];
        }
    }
}
