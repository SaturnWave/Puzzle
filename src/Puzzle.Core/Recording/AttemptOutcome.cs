using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Core.Recording
{
    /// <summary>How far one goal of the level got by the end of an attempt.</summary>
    public sealed record GoalOutcome
    {
        /// <exception cref="ArgumentException"><paramref name="goal"/> is missing or <paramref name="done"/> is negative.</exception>
        public GoalOutcome(GoalSpec goal, int done)
        {
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            Done = done >= 0 ? done : throw new ArgumentOutOfRangeException(nameof(done), done, "Progress cannot be negative.");
        }

        public GoalSpec Goal { get; }

        /// <summary>Counted by the end of the attempt; may exceed the target.</summary>
        public int Done { get; }

        public bool IsComplete => Done >= Goal.Target;
    }

    /// <summary>What an attempt came to: its result, the moves it used, each goal's progress and what was bought or used.</summary>
    public sealed class AttemptOutcome
    {
        /// <param name="result">How the attempt ended.</param>
        /// <param name="movesUsed">Moves played, including any bought ones.</param>
        /// <param name="goals">Each goal of the level in level order, with its progress at the end.</param>
        /// <param name="extraMovesBought">Whether the "+5 moves" offer was taken.</param>
        /// <param name="boostersUsed">
        /// Names of the boosters used, in order. Names rather than a closed type: the engine has no boosters yet, and a
        /// record written by a newer game must stay readable by an older tool.
        /// </param>
        /// <param name="endedUtc">When the attempt ended; stored in UTC.</param>
        /// <exception cref="ArgumentException">A value is out of range or missing.</exception>
        public AttemptOutcome(
            AttemptResult result,
            int movesUsed,
            IReadOnlyList<GoalOutcome> goals,
            bool extraMovesBought,
            IReadOnlyList<string> boostersUsed,
            DateTimeOffset endedUtc)
        {
            Result = Enum.IsDefined(typeof(AttemptResult), result)
                ? result
                : throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown attempt result.");
            MovesUsed = movesUsed >= 0 ? movesUsed : throw new ArgumentOutOfRangeException(nameof(movesUsed), movesUsed, "Moves used cannot be negative.");
            Goals = Array.AsReadOnly(CheckedGoals(goals));
            ExtraMovesBought = extraMovesBought;
            BoostersUsed = Array.AsReadOnly(CheckedBoosters(boostersUsed));
            EndedUtc = endedUtc.ToUniversalTime();
        }

        public AttemptResult Result { get; }

        public int MovesUsed { get; }

        /// <summary>Each goal of the level in level order, with its progress at the end.</summary>
        public IReadOnlyList<GoalOutcome> Goals { get; }

        /// <summary>Whether the "+5 moves" offer was taken.</summary>
        public bool ExtraMovesBought { get; }

        /// <summary>Names of the boosters used, in order.</summary>
        public IReadOnlyList<string> BoostersUsed { get; }

        public DateTimeOffset EndedUtc { get; }

        /// <summary>
        /// The outcome of <paramref name="game"/> as it stands at <paramref name="endedUtc"/>: a game still being played
        /// is an abandoned attempt. The engine has neither boosters nor the "+5 moves" offer yet (docs/GAME_DESIGN.md §21,
        /// step 4), so none are recorded.
        /// </summary>
        public static AttemptOutcome Of(Game game, DateTimeOffset endedUtc)
        {
            var goals = new GoalOutcome[game.Goals.Count];
            for (int i = 0; i < goals.Length; i++)
            {
                GoalProgress progress = game.Goals[i];
                goals[i] = new GoalOutcome(progress.Goal, progress.Done);
            }

            return new AttemptOutcome(
                AttemptResults.AtEnd(game.Status),
                game.MovesUsed,
                goals,
                extraMovesBought: false,
                boostersUsed: Array.Empty<string>(),
                endedUtc);
        }

        private static GoalOutcome[] CheckedGoals(IReadOnlyList<GoalOutcome> goals)
        {
            if (goals is null)
            {
                throw new ArgumentNullException(nameof(goals));
            }

            if (goals.Count == 0 || goals.Count > LevelDefinition.MaxGoals || goals.Any(goal => goal is null))
            {
                throw new ArgumentException($"An attempt has 1..{LevelDefinition.MaxGoals} goals.", nameof(goals));
            }

            return goals.ToArray();
        }

        private static string[] CheckedBoosters(IReadOnlyList<string> boosters)
        {
            if (boosters is null)
            {
                throw new ArgumentNullException(nameof(boosters));
            }

            return boosters.All(name => !string.IsNullOrWhiteSpace(name))
                ? boosters.ToArray()
                : throw new ArgumentException("A booster name cannot be blank.", nameof(boosters));
        }
    }
}
