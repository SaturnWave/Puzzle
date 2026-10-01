using System;

namespace Puzzle.Core.Engine
{
    public sealed class GameOptions
    {
        private readonly int? _moveLimitOverride;
        private readonly int _assistPercent;

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

        /// <summary>
        /// Dynamic difficulty assist for this attempt, 0..100, applied by the spawn director on top of the level's
        /// own biases. 0 (the default) plays the level exactly as calibrated. Runtime assist only ever helps:
        /// making a level harder is a design-time decision (see docs/research/spawn-control-and-solvability.md).
        /// </summary>
        public int AssistPercent
        {
            get => _assistPercent;
            init => _assistPercent = value >= 0 && value <= 100
                ? value
                : throw new ArgumentOutOfRangeException(nameof(AssistPercent), value, "Assist must be 0..100.");
        }
    }
}
