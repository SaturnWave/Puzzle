using System;

namespace Puzzle.Core.Engine
{
    public sealed class GameOptions
    {
        private readonly int? _moveLimitOverride;

        public static GameOptions Default { get; } = new GameOptions();

        public RuleSet Rules { get; init; } = RuleSet.Default;

        /// <summary>Record <see cref="Events.GameEvent"/>s for the view layer. Off for bot runs (allocation-free).</summary>
        public bool RecordEvents { get; init; }

        /// <summary>
        /// Replaces the level's move limit. The bot calibration plays with a large override to learn how many
        /// moves a level really needs.
        /// </summary>
        public int? MoveLimitOverride
        {
            get => _moveLimitOverride;
            init => _moveLimitOverride = value is null || value > 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MoveLimitOverride), value, "Move limit must be positive.");
        }
    }
}
