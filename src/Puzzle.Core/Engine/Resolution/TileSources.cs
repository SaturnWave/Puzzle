using System;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;

namespace Puzzle.Core.Engine.Resolution
{
    /// <summary>Where refills come from. Live games draw real tiles; bot lookahead draws tiles it cannot see.</summary>
    internal interface ITileSource
    {
        Tile Next(int column);
    }

    /// <summary>Items are indestructible, so the number on the board is simply spawned minus collected.</summary>
    internal sealed class ItemLedger
    {
        public int Spawned { get; set; }

        public int Collected { get; set; }

        public int OnBoard => Spawned - Collected;

        public ItemLedger Clone() => (ItemLedger)MemberwiseClone();
    }

    internal sealed class RandomTileSource : ITileSource
    {
        private readonly ColorPicker _colors;
        private readonly Rng _rng;
        private readonly ItemSpawnRules _items;
        private readonly ItemLedger _ledger;
        private int _spawnedItems;

        public RandomTileSource(ColorPicker colors, Rng rng, ItemSpawnRules items, ItemLedger ledger)
        {
            _colors = colors;
            _rng = rng;
            _items = items;
            _ledger = ledger;
        }

        public Tile Next(int column)
        {
            if (_spawnedItems < _items.Total && _ledger.OnBoard < _items.MaxOnBoard && _rng.RollPercent(_items.ChancePercent))
            {
                _spawnedItems++;
                _ledger.Spawned++;
                return Tile.Item;
            }

            return Tile.Gem(_colors.Next(_rng));
        }
    }

    internal sealed class HiddenTileSource : ITileSource
    {
        public static HiddenTileSource Instance { get; } = new HiddenTileSource();

        public Tile Next(int column) => Tile.Hidden;
    }

    /// <summary>
    /// Drought-dampened weighted color choice (the "dueness" model from the research): a color that just
    /// spawned loses weight, colors that did not spawn gain it. Integer fixed-point so every platform agrees.
    /// </summary>
    internal sealed class ColorPicker
    {
        private const long Scale = 1_000_000;

        private readonly int _count;
        private readonly long _smoothingPermille;
        private readonly long _baseShare;
        private readonly long[] _weights;

        public ColorPicker(int colorCount, int smoothingPermille)
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

        private ColorPicker(ColorPicker source)
        {
            _count = source._count;
            _smoothingPermille = source._smoothingPermille;
            _baseShare = source._baseShare;
            _weights = (long[])source._weights.Clone();
        }

        public GemColor Next(Rng rng)
        {
            int picked = PickWeighted(rng);
            if (_smoothingPermille > 0)
            {
                long drop = (Scale - _baseShare) * _smoothingPermille / 1000;
                long rise = _baseShare * _smoothingPermille / 1000;
                for (int c = 0; c < _count; c++)
                {
                    _weights[c] = c == picked ? Math.Max(0, _weights[c] - drop) : _weights[c] + rise;
                }
            }

            return (GemColor)picked;
        }

        public ColorPicker Clone() => new ColorPicker(this);

        private int PickWeighted(Rng rng)
        {
            long total = 0;
            foreach (long weight in _weights)
            {
                total += weight;
            }

            if (total <= 0)
            {
                return rng.NextInt(_count);
            }

            // The total stays near Scale (each drop balances the rises unless a weight clamps at zero),
            // so it is far inside the int range NextInt needs.
            long roll = rng.NextInt((int)Math.Min(total, int.MaxValue));
            for (int c = 0; c < _count; c++)
            {
                roll -= _weights[c];
                if (roll < 0)
                {
                    return c;
                }
            }

            return _count - 1;
        }
    }
}
