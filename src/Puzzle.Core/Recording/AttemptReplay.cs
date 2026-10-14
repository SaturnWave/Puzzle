using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;

namespace Puzzle.Core.Recording
{
    /// <summary>
    /// An attempt played again from what its record keeps: the level, the seeds, the start boosters and the actions in
    /// their order give the same game, because the engine draws every random number from the seeds
    /// (docs/GAME_DESIGN.md §18). The replay is checked as it goes: every action has to do in the replay what it did in
    /// the attempt, so a record that is not of this level's game is told as such, never played on into another game.
    /// </summary>
    public static class AttemptReplay
    {
        /// <summary>
        /// The game of the attempt <paramref name="record"/> keeps, played again on <paramref name="level"/> up to where
        /// the attempt ended, with the same result, moves used, goals, boosters used and extra moves. An attempt left
        /// before it was decided is replayed to the game as it was being played.
        /// </summary>
        /// <param name="level">The level the attempt was played on: its number, content version and move limit are the record's.</param>
        /// <exception cref="ArgumentException">
        /// The record keeps no actions, is of another level, content version or move limit, or names a start booster twice.
        /// </exception>
        /// <exception cref="AttemptReplayException">The game did not do one of the actions as the record says, or ended otherwise than it.</exception>
        public static Game Of(AttemptRecord record, LevelDefinition level)
        {
            IReadOnlyList<AttemptAction> actions = record.Actions
                ?? throw new ArgumentException("The record keeps no actions, so it cannot be replayed.", nameof(record));
            AttemptStart start = record.Start;
            if (start.Level != level.Id || start.ContentVersion != level.ContentVersion || start.MoveLimit != level.MoveLimit)
            {
                throw new ArgumentException(
                    $"The record is of level {start.Level} (content version {start.ContentVersion}, {start.MoveLimit} moves), "
                    + $"not of level {level.Id} (content version {level.ContentVersion}, {level.MoveLimit} moves).",
                    nameof(level));
            }

            Game game = Play(level, start.Seeds, StartBoostersOf(record.Outcome), record.Lab?.AssistPercent ?? 0, actions);
            if (DifferenceBetween(AttemptOutcome.Of(game, record.Outcome.EndedUtc), record.Outcome) is string difference)
            {
                throw new AttemptReplayException(actions.Count, $"The replay of all {actions.Count} actions ends otherwise than the attempt: {difference}.");
            }

            return game;
        }

        /// <summary>
        /// The game <paramref name="actions"/> play on <paramref name="level"/> from the start that the seeds and the start
        /// boosters give, as the released game plays (no assist). The game keeps its own actions, equal to these.
        /// </summary>
        /// <exception cref="AttemptReplayException">The game did not do one of the actions as the list says.</exception>
        public static Game Play(LevelDefinition level, AttemptSeeds seeds, IReadOnlyList<StartBooster> startBoosters, IReadOnlyList<AttemptAction> actions) =>
            Play(level, seeds, startBoosters, assistPercent: 0, actions);

        private static Game Play(LevelDefinition level, AttemptSeeds seeds, IReadOnlyList<StartBooster> startBoosters, int assistPercent, IReadOnlyList<AttemptAction> actions)
        {
            var options = new GameOptions { StartBoosters = startBoosters, AssistPercent = assistPercent, RecordActions = true };
            Game game = Game.Start(level, seeds, options);
            for (int index = 0; index < actions.Count; index++)
            {
                AttemptAction action = actions[index];
                bool asksAPlayedGame = !(action is AttemptAction.ExtraMovesTaken || action is AttemptAction.ExtraMovesRefused);
                if (asksAPlayedGame && game.Status.IsOver())
                {
                    throw new AttemptReplayException(index, $"Action {index + 1} ({action}) comes after the game was over ({game.Status}).");
                }

                Do(action, game);

                // The game keeps what it did: one more action, and the very one recorded, or the replay has gone elsewhere.
                bool didAsRecorded = game.Actions.Count == index + 1 && game.Actions[index].Equals(action);
                if (!didAsRecorded)
                {
                    string done = game.Actions.Count == index ? "nothing" : game.Actions[index].ToString();
                    throw new AttemptReplayException(index, $"Action {index + 1} ({action}) did {done} in the replay.");
                }
            }

            return game;
        }

        /// <summary>Asks the game for what the action is; whether it did that is told by the actions the game keeps.</summary>
        private static void Do(AttemptAction action, Game game)
        {
            switch (action)
            {
                case AttemptAction.MovePlayed played:
                    game.TryPlay(played.Move);
                    break;
                case AttemptAction.BoosterUsed used:
                    game.TryUseBooster(used.Booster);
                    break;
                case AttemptAction.ShuffleRefused _:
                    game.TryUseBooster(new BoosterUse.Shuffle());
                    break;
                case AttemptAction.ExtraMovesTaken _:
                case AttemptAction.ExtraMovesRefused _:
                    game.TryTakeExtraMoves();
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled action {action.GetType().Name}.");
            }
        }

        /// <summary>The start boosters the attempt took, by the names its outcome lists them under; the other names are boosters of play.</summary>
        private static StartBooster[] StartBoostersOf(AttemptOutcome outcome) =>
            outcome.BoostersUsed.Select(BoosterNames.StartBoosterNamed).OfType<StartBooster>().ToArray();

        /// <summary>In words, where a replay's outcome differs from the attempt's, times aside; nothing when they agree.</summary>
        private static string? DifferenceBetween(AttemptOutcome replayed, AttemptOutcome recorded)
        {
            if (replayed.Result != recorded.Result || replayed.MovesUsed != recorded.MovesUsed)
            {
                return $"{replayed.Result} after {replayed.MovesUsed} moves, not {recorded.Result} after {recorded.MovesUsed}";
            }

            if (!replayed.Goals.SequenceEqual(recorded.Goals))
            {
                return $"goals done {Done(replayed)}, not {Done(recorded)}";
            }

            if (replayed.ExtraMovesBought != recorded.ExtraMovesBought || !replayed.BoostersUsed.SequenceEqual(recorded.BoostersUsed))
            {
                return $"boosters [{string.Join(", ", replayed.BoostersUsed)}] and extra moves {replayed.ExtraMovesBought}, "
                    + $"not [{string.Join(", ", recorded.BoostersUsed)}] and {recorded.ExtraMovesBought}";
            }

            return null;
        }

        private static string Done(AttemptOutcome outcome) => string.Join("/", outcome.Goals.Select(goal => goal.Done));
    }

    /// <summary>A record does not replay: the game did not do one of its actions as the record says, or ended otherwise than the attempt.</summary>
    public sealed class AttemptReplayException : Exception
    {
        public AttemptReplayException(int actionIndex, string message)
            : base(message)
        {
            ActionIndex = actionIndex;
        }

        /// <summary>The 0-based index of the action the replay went wrong at; the count of actions when only the ending differs.</summary>
        public int ActionIndex { get; }
    }
}
