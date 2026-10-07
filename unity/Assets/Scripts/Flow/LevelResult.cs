#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Gallery;
using Puzzle.Core.Engine;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the stage is told about the attempt it shows, beyond the game itself: the things only the player's
    /// progress knows, and the result panel says.
    /// </summary>
    public readonly struct AttemptBrief
    {
        /// <param name="attemptNumber">Which try at the level this is, counted from 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">The attempt number is less than 1.</exception>
        public AttemptBrief(int attemptNumber, bool isOnLastLevel)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "A player's attempts are counted from 1.");
            }

            AttemptNumber = attemptNumber;
            IsOnLastLevel = isOnLastLevel;
        }

        /// <summary>A first try at a level that has a level after it: what a picture of a single level shows.</summary>
        public static AttemptBrief FirstTry => new AttemptBrief(attemptNumber: 1, isOnLastLevel: false);

        public int AttemptNumber { get; }

        /// <summary>True when no level follows this one: winning it leads nowhere new.</summary>
        public bool IsOnLastLevel { get; }
    }

    /// <summary>How a lost game was lost.</summary>
    public enum LossReason
    {
        /// <summary>The moves ran out before the goals were met.</summary>
        OutOfMoves,

        /// <summary>No move was possible even after the board was shuffled; a validated level never gets here.</summary>
        NoMovePossible,
    }

    /// <summary>
    /// How a decided attempt came out, as the result panel tells it. A closed set (private constructor): a level is
    /// either <see cref="Won"/> or <see cref="Lost"/>. This file has no Unity dependency, so the repository's tests
    /// compile it too.
    /// </summary>
    public abstract class LevelResult
    {
        private LevelResult(int levelNumber)
        {
            LevelNumber = levelNumber;
        }

        /// <summary>The number the player knows the level by.</summary>
        public int LevelNumber { get; }

        /// <summary>The result of a game that is over.</summary>
        /// <exception cref="InvalidOperationException">The game is still being played.</exception>
        public static LevelResult Of(Game game, AttemptBrief brief)
        {
            int level = game.Level.Id.Value;
            return game.Status switch
            {
                GameStatus.Won => new Won(level, game.MovesUsed, game.MovesLeft, brief.IsOnLastLevel),
                GameStatus.LostOutOfMoves => new Lost(level, LossReason.OutOfMoves, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.LostNoMovesPossible => new Lost(level, LossReason.NoMovePossible, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.Playing => throw new InvalidOperationException($"Level {level} is still being played; it has no result yet."),
                _ => throw new InvalidOperationException($"A game that ended as {game.Status} is not known to the result panel."),
            };
        }

        /// <summary>The goals a lost game did not reach, each with what was left of it, in the order of the goals plaque.</summary>
        private static List<GoalFigure> OpenGoalsOf(Game game)
        {
            var open = new List<GoalFigure>();
            foreach (GoalFigure goal in HudFigures.Of(game).Goals)
            {
                if (goal.Remaining > 0)
                {
                    open.Add(goal);
                }
            }

            return open;
        }

        /// <summary>The level's goals were met.</summary>
        public sealed class Won : LevelResult
        {
            public Won(int levelNumber, int movesUsed, int movesLeft, bool isLastLevel)
                : base(levelNumber)
            {
                MovesUsed = movesUsed;
                MovesLeft = movesLeft;
                IsLastLevel = isLastLevel;
            }

            public int MovesUsed { get; }

            /// <summary>The moves the player did not need.</summary>
            public int MovesLeft { get; }

            /// <summary>True when no level follows: the button then offers the same level again.</summary>
            public bool IsLastLevel { get; }
        }

        /// <summary>The game ended with goals still open.</summary>
        public sealed class Lost : LevelResult
        {
            public Lost(int levelNumber, LossReason reason, int attemptNumber, IReadOnlyList<GoalFigure> openGoals)
                : base(levelNumber)
            {
                Reason = reason;
                AttemptNumber = attemptNumber;
                OpenGoals = openGoals;
            }

            public LossReason Reason { get; }

            /// <summary>Which try at the level this was, counted from 1.</summary>
            public int AttemptNumber { get; }

            /// <summary>What was missing: the goals that were not reached, each with what was left of it.</summary>
            public IReadOnlyList<GoalFigure> OpenGoals { get; }
        }
    }

    /// <summary>
    /// What the result panel says, in the player's language. The titles and the button's labels are the web test
    /// page's (result-panel.js), which are the design's.
    /// </summary>
    public static class ResultWords
    {
        /// <summary>The nameplate: what happened.</summary>
        public static string Title(LevelResult result) => result switch
        {
            LevelResult.Won _ => "Bölüm tamamlandı!",
            LevelResult.Lost { Reason: LossReason.OutOfMoves } => "Hamle bitti!",
            LevelResult.Lost { Reason: LossReason.NoMovePossible } => "Oynanacak hamle kalmadı!",
            _ => throw Unknown(result),
        };

        /// <summary>The line under the alcove: the level, and the moves a won game took or the attempt a lost one was.</summary>
        public static string Headline(LevelResult result) => result switch
        {
            LevelResult.Won won => $"Bölüm {Number(won.LevelNumber)} · {Number(won.MovesUsed)} hamlede",
            LevelResult.Lost lost => $"Bölüm {Number(lost.LevelNumber)} · {Number(lost.AttemptNumber)}. deneme",
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The smaller line under the headline: the moves a won game left over, or, for a lost one, what the alcove
        /// above it shows.
        /// </summary>
        public static string Footnote(LevelResult result) => result switch
        {
            LevelResult.Won { MovesLeft: 0 } => "Son hamlede tamamlandı",
            LevelResult.Won won => $"{Number(won.MovesLeft)} hamle arttı",
            LevelResult.Lost _ => "Hedefe bu kadar kaldı",
            _ => throw Unknown(result),
        };

        /// <summary>The button: where it leads.</summary>
        public static string GoOn(LevelResult result) => result switch
        {
            LevelResult.Won { IsLastLevel: true } => "Tekrar oyna",
            LevelResult.Won _ => "Sonraki bölüm",
            LevelResult.Lost _ => "Tekrar dene",
            _ => throw Unknown(result),
        };

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

        private static InvalidOperationException Unknown(LevelResult result) =>
            new InvalidOperationException($"A result of the kind {result.GetType().Name} has no words.");
    }
}
