using System;
using System.Collections.Generic;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>Everything a refill decision may look at: the cell being filled and the board around it.</summary>
    internal readonly struct SpawnContext
    {
        public SpawnContext(Board board, MatchFinder matches, GoalTracker goals, ItemLedger items, int target)
        {
            Board = board;
            Matches = matches;
            Goals = goals;
            Items = items;
            Target = target;
        }

        public Board Board { get; }

        public MatchFinder Matches { get; }

        public GoalTracker Goals { get; }

        public ItemLedger Items { get; }

        /// <summary>The empty cell the new tile lands in.</summary>
        public int Target { get; }
    }

    /// <summary>Where refills come from. Live games use the spawn director; bot lookahead draws tiles it cannot see.</summary>
    internal interface ITileSource
    {
        Tile Next(in SpawnContext context);

        /// <summary>An independent copy that continues with the same random sequence and internal state.</summary>
        ITileSource Clone();
    }

    /// <summary>Items are indestructible, so the number on the board is simply spawned minus collected.</summary>
    internal sealed class ItemLedger
    {
        public int Spawned { get; set; }

        public int Collected { get; set; }

        public int OnBoard => Spawned - Collected;

        public ItemLedger Clone() => (ItemLedger)MemberwiseClone();
    }

    internal sealed class HiddenTileSource : ITileSource
    {
        public static HiddenTileSource Instance { get; } = new HiddenTileSource();

        public Tile Next(in SpawnContext context) => Tile.Hidden;

        public ITileSource Clone() => this;
    }

    /// <summary>
    /// The spawn director: every refill is decided by several algorithms working together. The color balance
    /// (drought dampening) proposes weights, each steering strategy rescales them, then one weighted draw picks
    /// the color. Items are rolled first, within the level's item limits.
    /// </summary>
    internal sealed class SpawnDirector : ITileSource
    {
        private readonly IReadOnlyList<GemColor> _palette;
        private readonly ColorBalance _balance;
        private readonly IReadOnlyList<ISpawnStrategy> _strategies;
        private readonly ItemSpawnRules _items;
        private readonly Rng _rng;
        private readonly long[] _weights;
        private int _spawnedItems;

        private SpawnDirector(
            IReadOnlyList<GemColor> palette,
            ColorBalance balance,
            IReadOnlyList<ISpawnStrategy> strategies,
            ItemSpawnRules items,
            Rng rng,
            int spawnedItems)
        {
            _palette = palette;
            _balance = balance;
            _strategies = strategies;
            _items = items;
            _rng = rng;
            _weights = new long[palette.Count];
            _spawnedItems = spawnedItems;
        }

        /// <param name="assistPercent">
        /// Dynamic difficulty assist, 0..100: adds free matches and favors goal colors on top of the level's own
        /// (designer-set) biases.
        /// </param>
        public static SpawnDirector Create(SpawnRules rules, int assistPercent, Rng rng)
        {
            int cascadeBias = Math.Max(-100, Math.Min(200, rules.CascadeBiasPercent + assistPercent));
            int goalBias = Math.Max(-90, Math.Min(200, rules.GoalBiasPercent + assistPercent / 2));
            var strategies = new List<ISpawnStrategy>();
            if (goalBias != 0)
            {
                strategies.Add(new GoalBiasStrategy(goalBias));
            }

            if (cascadeBias != 0)
            {
                strategies.Add(new CascadeBiasStrategy(cascadeBias));
            }

            return new SpawnDirector(rules.Palette, new ColorBalance(rules.Colors, rules.SmoothingPermille), strategies, rules.Items, rng, 0);
        }

        public Tile Next(in SpawnContext context)
        {
            if (_spawnedItems < _items.Total && context.Items.OnBoard < _items.MaxOnBoard && _rng.RollPercent(_items.ChancePercent))
            {
                _spawnedItems++;
                context.Items.Spawned++;
                return Tile.Item;
            }

            _balance.CopyWeightsTo(_weights);
            foreach (ISpawnStrategy strategy in _strategies)
            {
                strategy.Adjust(context, _palette, _weights);
            }

            if (Total(_weights) <= 0)
            {
                _balance.CopyWeightsTo(_weights); // every option was vetoed: fall back to plain balance
            }

            int picked = PickWeighted(_weights);
            _balance.Record(picked);
            return Tile.Gem(_palette[picked]);
        }

        public ITileSource Clone() =>
            new SpawnDirector(_palette, _balance.Clone(), _strategies, _items, _rng.Clone(), _spawnedItems);

        private static long Total(long[] weights)
        {
            long total = 0;
            foreach (long weight in weights)
            {
                total += weight;
            }

            return total;
        }

        private int PickWeighted(long[] weights)
        {
            long total = Total(weights);
            if (total <= 0)
            {
                return _rng.NextInt(weights.Length);
            }

            // Balance weights total about one million and strategies scale them by at most 3x per strategy,
            // so the sum stays far inside the int range NextInt needs.
            long roll = _rng.NextInt((int)Math.Min(total, int.MaxValue));
            for (int c = 0; c < weights.Length; c++)
            {
                roll -= weights[c];
                if (roll < 0)
                {
                    return c;
                }
            }

            return weights.Length - 1;
        }
    }

    /// <summary>One steering algorithm of the spawn director: rescales the color weights of a single refill.</summary>
    internal interface ISpawnStrategy
    {
        /// <param name="weights">One weight per palette entry, adjusted in place.</param>
        void Adjust(in SpawnContext context, IReadOnlyList<GemColor> palette, long[] weights);
    }

    /// <summary>Makes the colors that open "collect" goals still need more (or less) likely.</summary>
    internal sealed class GoalBiasStrategy : ISpawnStrategy
    {
        private readonly int _percent;

        public GoalBiasStrategy(int percent)
        {
            _percent = percent;
        }

        public void Adjust(in SpawnContext context, IReadOnlyList<GemColor> palette, long[] weights)
        {
            for (int c = 0; c < palette.Count; c++)
            {
                if (context.Goals.WantsColor(palette[c]))
                {
                    weights[c] = weights[c] * (100 + _percent) / 100;
                }
            }
        }
    }

    /// <summary>
    /// Makes refills that would match right where they land more (or less) likely — the dial between a board
    /// that "plays itself" and one where every match has to be earned.
    /// </summary>
    internal sealed class CascadeBiasStrategy : ISpawnStrategy
    {
        private readonly int _percent;

        public CascadeBiasStrategy(int percent)
        {
            _percent = percent;
        }

        public void Adjust(in SpawnContext context, IReadOnlyList<GemColor> palette, long[] weights)
        {
            Board board = context.Board;
            Tile original = board.Get(context.Target);
            for (int c = 0; c < palette.Count; c++)
            {
                board.Set(context.Target, Tile.Gem(palette[c]));
                if (context.Matches.HasMatchAt(context.Target))
                {
                    weights[c] = weights[c] * (100 + _percent) / 100;
                }
            }

            board.Set(context.Target, original);
        }
    }

    /// <summary>
    /// Drought-dampened color weights (the "dueness" model from the research): a color that just spawned loses
    /// weight, colors that did not spawn gain it. Integer fixed-point so every platform agrees.
    /// </summary>
    internal sealed class ColorBalance
    {
        private const long Scale = 1_000_000;

        private readonly int _count;
        private readonly long _smoothingPermille;
        private readonly long _baseShare;
        private readonly long[] _weights;

        public ColorBalance(int colorCount, int smoothingPermille)
        {
            if (colorCount < 1 || colorCount > GemColors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(colorCount), colorCount, null);
            }

            _count = colorCount;
            _smoothingPermille = smoothingPermille;
            _baseShare = Scale / colorCount;
            _weights = new long[colorCount];
            for (int c = 0; c < colorCount; c++)
            {
                _weights[c] = _baseShare;
            }
        }

        private ColorBalance(ColorBalance source)
        {
            _count = source._count;
            _smoothingPermille = source._smoothingPermille;
            _baseShare = source._baseShare;
            _weights = (long[])source._weights.Clone();
        }

        public void CopyWeightsTo(long[] target) => Array.Copy(_weights, target, _count);

        public void Record(int picked)
        {
            if (_smoothingPermille == 0)
            {
                return;
            }

            long drop = (Scale - _baseShare) * _smoothingPermille / 1000;
            long rise = _baseShare * _smoothingPermille / 1000;
            for (int c = 0; c < _count; c++)
            {
                _weights[c] = c == picked ? Math.Max(0, _weights[c] - drop) : _weights[c] + rise;
            }
        }

        public ColorBalance Clone() => new ColorBalance(this);
    }
}
