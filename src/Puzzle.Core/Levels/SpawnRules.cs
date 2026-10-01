using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Model;

namespace Puzzle.Core.Levels
{
    /// <summary>
    /// What falls from the spawners of a level, and how the spawn director steers it
    /// (see docs/GAME_DESIGN.md §6).
    /// </summary>
    public sealed class SpawnRules
    {
        private readonly IReadOnlyList<GemColor> _palette = GemColors.First(5);
        private readonly int _smoothingPermille = 60;
        private readonly int _goalBiasPercent;
        private readonly int _cascadeBiasPercent;
        private readonly ItemSpawnRules _items = ItemSpawnRules.None;

        public static SpawnRules Default => new SpawnRules();

        /// <summary>The distinct gem colors in play, 3..8 of them.</summary>
        public IReadOnlyList<GemColor> Palette
        {
            get => _palette;
            init
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(Palette));
                }

                if (value.Count < GemColors.MinPerLevel || value.Count > GemColors.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(Palette), value.Count, $"A palette has {GemColors.MinPerLevel}..{GemColors.Count} colors.");
                }

                if (value.Any(color => !Enum.IsDefined(typeof(GemColor), color)) || value.Distinct().Count() != value.Count)
                {
                    throw new ArgumentException("A palette must list distinct, known colors.", nameof(Palette));
                }

                _palette = value.ToArray();
            }
        }

        public int Colors => _palette.Count;

        /// <summary>
        /// Drought dampening strength in permille: 0 is plain weighted randomness, higher values push colors
        /// toward a regular rotation (fewer droughts, but also fewer natural clusters).
        /// </summary>
        public int SmoothingPermille
        {
            get => _smoothingPermille;
            init => _smoothingPermille = RequireRange(value, 0, 1000, nameof(SmoothingPermille));
        }

        /// <summary>
        /// Weight change for colors an open "collect color" goal needs: +50 makes them 1.5x as likely,
        /// -50 half as likely.
        /// </summary>
        public int GoalBiasPercent
        {
            get => _goalBiasPercent;
            init => _goalBiasPercent = RequireRange(value, -90, 200, nameof(GoalBiasPercent));
        }

        /// <summary>
        /// Weight change for a refill color that would match immediately where it lands. -100 never hands out
        /// a free match (unless every color would match), +100 doubles the chance. The strongest lever on how
        /// "explosive" a big board feels.
        /// </summary>
        public int CascadeBiasPercent
        {
            get => _cascadeBiasPercent;
            init => _cascadeBiasPercent = RequireRange(value, -100, 200, nameof(CascadeBiasPercent));
        }

        public ItemSpawnRules Items
        {
            get => _items;
            init => _items = value ?? throw new ArgumentNullException(nameof(Items));
        }

        public bool Contains(GemColor color) => _palette.Contains(color);


        private static int RequireRange(int value, int min, int max, string name) =>
            value >= min && value <= max
                ? value
                : throw new ArgumentOutOfRangeException(name, value, $"{name} must be {min}..{max}.");
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
