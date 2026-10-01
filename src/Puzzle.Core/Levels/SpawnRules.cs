using System;
using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>What falls from the spawners of a level.</summary>
    public sealed class SpawnRules
    {
        private readonly int _colors = 5;
        private readonly int _smoothingPermille = 60;
        private readonly ItemSpawnRules _items = ItemSpawnRules.None;

        public static SpawnRules Default => new SpawnRules();

        /// <summary>Number of gem colors in play; the first N <see cref="GemColor"/> values are used.</summary>
        public int Colors
        {
            get => _colors;
            init => _colors = value >= GemColors.MinPerLevel && value <= GemColors.Count
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Colors), value, $"Colors must be {GemColors.MinPerLevel}..{GemColors.Count}.");
        }

        /// <summary>
        /// Drought dampening strength in permille: 0 is plain uniform randomness, higher values push colors
        /// toward a regular rotation (fewer droughts, but also fewer natural clusters).
        /// </summary>
        public int SmoothingPermille
        {
            get => _smoothingPermille;
            init => _smoothingPermille = value >= 0 && value <= 1000
                ? value
                : throw new ArgumentOutOfRangeException(nameof(SmoothingPermille), value, "Smoothing must be 0..1000 permille.");
        }

        public ItemSpawnRules Items
        {
            get => _items;
            init => _items = value ?? throw new ArgumentNullException(nameof(Items));
        }
    }

    /// <summary>How many droppable items spawn during a level, on top of those preset in the layout.</summary>
    public sealed class ItemSpawnRules
    {
        private readonly int _total;
        private readonly int _maxOnBoard;
        private readonly int _chancePercent;

        public static ItemSpawnRules None => new ItemSpawnRules();

        public int Total
        {
            get => _total;
            init => _total = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Total), value, "Item total cannot be negative.");
        }

        public int MaxOnBoard
        {
            get => _maxOnBoard;
            init => _maxOnBoard = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxOnBoard), value, "Max items on board cannot be negative.");
        }

        /// <summary>Chance that a refill becomes an item while the limits allow it.</summary>
        public int ChancePercent
        {
            get => _chancePercent;
            init => _chancePercent = value >= 0 && value <= 100
                ? value
                : throw new ArgumentOutOfRangeException(nameof(ChancePercent), value, "Item chance must be 0..100.");
        }
    }
}
