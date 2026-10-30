using System;

namespace Puzzle.Bot
{
    /// <summary>
    /// The knobs of <see cref="UstaPolicy"/>. Immutable: derive a variant with <c>with</c>, which validates every
    /// changed value just as an object initializer does.
    /// </summary>
    public sealed record UstaSettings
    {
        public const int MinSimulations = 2;
        public const int MaxSimulations = 4096;

        private readonly int _simulations = 24;
        private readonly int _rootWidth = 8;
        private readonly int _specialSlots = 2;
        private readonly double _followUpWeight = 1.0;
        private readonly double _followUpWinValue = MoveEvaluator.WinScore / 2;

        public static UstaSettings Default { get; } = new UstaSettings();

        /// <summary>
        /// Sampled futures per decision (<see cref="MinSimulations"/>..<see cref="MaxSimulations"/>): the budget, and
        /// the only setting the cost of a decision grows with. Small budgets also narrow the search to about a third
        /// as many candidate moves.
        /// </summary>
        public int Simulations
        {
            get => _simulations;
            init => _simulations = value >= MinSimulations && value <= MaxSimulations
                ? value
                : throw new ArgumentOutOfRangeException(nameof(Simulations), value, $"Simulations must be {MinSimulations}..{MaxSimulations}.");
        }

        /// <summary>The most root moves that are searched; the rest are judged by the one-ply prior alone. At least 1.</summary>
        public int RootWidth
        {
            get => _rootWidth;
            init => _rootWidth = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(RootWidth), value, "Root width must be at least 1.");
        }

        /// <summary>
        /// Candidate places kept for the best special-firing moves outside the prior's top when at least four moves
        /// are searched: the hidden-refill prior undervalues big clears. Zero or more.
        /// </summary>
        public int SpecialSlots
        {
            get => _specialSlots;
            init => _specialSlots = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(SpecialSlots), value, "Special slots must be zero or more.");
        }

        /// <summary>
        /// Weight of the best second-move score (0..1). Below 1 it prefers progress now to the same progress one move
        /// later. The default 1 counts both moves alike: on the 40-level sample (200 games per level, two independent
        /// seed sets) 0.9 needed about 0.15 more moves per game at the same cost and won no more often.
        /// </summary>
        public double FollowUpWeight
        {
            get => _followUpWeight;
            init => _followUpWeight = value >= 0 && value <= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(FollowUpWeight), value, "Follow-up weight must be 0..1.");
        }

        /// <summary>
        /// Value of a win found on the second move (0..<see cref="MoveEvaluator.WinScore"/>). Half the value of a win
        /// now keeps the scale linear in the moves still needed over the two-move horizon.
        /// </summary>
        public double FollowUpWinValue
        {
            get => _followUpWinValue;
            init => _followUpWinValue = value >= 0 && value <= MoveEvaluator.WinScore
                ? value
                : throw new ArgumentOutOfRangeException(nameof(FollowUpWinValue), value, $"Follow-up win value must be 0..{MoveEvaluator.WinScore}.");
        }
    }
}
