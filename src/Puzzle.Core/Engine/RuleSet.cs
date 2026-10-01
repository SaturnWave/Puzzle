using System;
using Puzzle.Core.Model;

namespace Puzzle.Core.Engine
{
    /// <summary>
    /// Game-wide tuning constants. Changing any of these changes every level's difficulty, so levels must be
    /// re-calibrated by the bot afterwards.
    /// </summary>
    public sealed class RuleSet
    {
        private readonly int _bombRadius = 1;
        private readonly int _bigBombRadius = 2;
        private readonly int _seekerComboTargets = 3;
        private readonly int _minInitialMoves = 3;

        public static RuleSet Default { get; } = new RuleSet();

        /// <summary>Chebyshev radius of the small bomb: 1 means a 3x3 area.</summary>
        public int BombRadius
        {
            get => _bombRadius;
            init => _bombRadius = RequireRange(value, 1, 5, nameof(BombRadius));
        }

        /// <summary>Radius of the big bomb: 2 means a 5x5 area. Two bombs swapped together blast with the sum of their radii.</summary>
        public int BigBombRadius
        {
            get => _bigBombRadius;
            init => _bigBombRadius = RequireRange(value, 1, 6, nameof(BigBombRadius));
        }

        /// <summary>Blast radius of a bomb kind.</summary>
        public int RadiusOf(TileKind bomb) => bomb switch
        {
            TileKind.Bomb => BombRadius,
            TileKind.BigBomb => BigBombRadius,
            _ => throw new ArgumentOutOfRangeException(nameof(bomb), bomb, "Not a bomb."),
        };

        /// <summary>How many targets the seeker + seeker combo flies to.</summary>
        public int SeekerComboTargets
        {
            get => _seekerComboTargets;
            init => _seekerComboTargets = RequireRange(value, 1, 10, nameof(SeekerComboTargets));
        }

        /// <summary>Whether tapping a special activates it (costs a move), Royal Match style.</summary>
        public bool AllowTapActivation { get; init; } = true;

        /// <summary>A freshly generated starting board must offer at least this many legal moves.</summary>
        public int MinInitialMoves
        {
            get => _minInitialMoves;
            init => _minInitialMoves = RequireRange(value, 1, 50, nameof(MinInitialMoves));
        }

        public int InitialFillAttempts { get; init; } = 60;

        public int ShuffleAttempts { get; init; } = 60;

        /// <summary>Safety valve against a resolution loop that never settles (would indicate an engine bug).</summary>
        public int MaxResolutionSteps { get; init; } = 500;

        private static int RequireRange(int value, int min, int max, string name) =>
            value >= min && value <= max
                ? value
                : throw new ArgumentOutOfRangeException(name, value, $"{name} must be {min}..{max}.");
    }
}
