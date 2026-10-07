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
        /// <param name="reward">The jigsaw pieces winning this attempt gives; nothing when it gives none.</param>
        /// <exception cref="ArgumentOutOfRangeException">The attempt number is less than 1.</exception>
        public AttemptBrief(int attemptNumber, bool isOnLastLevel, PieceReward? reward = null)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "A player's attempts are counted from 1.");
            }

            AttemptNumber = attemptNumber;
            IsOnLastLevel = isOnLastLevel;
            Reward = reward;
        }

        /// <summary>A first try at a level that has a level after it and gives no jigsaw piece: what a picture of a single level shows.</summary>
        public static AttemptBrief FirstTry => new AttemptBrief(attemptNumber: 1, isOnLastLevel: false);

        public int AttemptNumber { get; }

        /// <summary>True when no level follows this one: winning it leads nowhere new.</summary>
        public bool IsOnLastLevel { get; }

        /// <summary>The jigsaw pieces of a painting that winning the attempt gives the player; nothing when it gives none.</summary>
        public PieceReward? Reward { get; }
    }

    /// <summary>Where the player goes on to from a result's card.</summary>
    public enum ResultChoice
    {
        /// <summary>On with the levels: the next one after a win, the same one's next board after a loss.</summary>
        GoOn,

        /// <summary>Back to the museum's hall.</summary>
        ToTheMuseum,

        /// <summary>The player wants to put the pieces they won in their place: the painting's jigsaw opens, and its way back leads to the card again.</summary>
        PlaceThePieces,

        /// <summary>The player asked to leave a level and thought better of it: the card goes up and the game goes on.</summary>
        Stay,
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
    /// What the card that hangs in front of a board tells. A closed set (private constructor): a decided attempt is
    /// either <see cref="Won"/> or <see cref="Lost"/>, and an attempt that is still being played has a card only while
    /// its player is asked whether they mean to leave it (<see cref="Leaving"/>). This file has no Unity dependency,
    /// so the repository's tests compile it too.
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
                GameStatus.Won => new Won(level, game.MovesUsed, game.MovesLeft, brief.IsOnLastLevel, brief.Reward),
                GameStatus.LostOutOfMoves => new Lost(level, LossReason.OutOfMoves, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.LostNoMovesPossible => new Lost(level, LossReason.NoMovePossible, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.Playing => throw new InvalidOperationException($"Level {level} is still being played; it has no result yet."),
                _ => throw new InvalidOperationException($"A game that ended as {game.Status} is not known to the result panel."),
            };
        }

        /// <summary>What a player is shown who asks to leave the game they are playing.</summary>
        /// <exception cref="InvalidOperationException">The game is decided: it has a result, and nothing is left to leave.</exception>
        public static LevelResult ForLeaving(Game game, AttemptBrief brief) => game.Status == GameStatus.Playing
            ? new Leaving(game.Level.Id.Value, brief.AttemptNumber, hasPlayed: game.MovesUsed > 0, OpenGoalsOf(game))
            : throw new InvalidOperationException($"Level {game.Level.Id.Value} is decided ({game.Status}); it is not left, it has a result.");

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
            public Won(int levelNumber, int movesUsed, int movesLeft, bool isLastLevel, PieceReward? reward)
                : base(levelNumber)
            {
                MovesUsed = movesUsed;
                MovesLeft = movesLeft;
                IsLastLevel = isLastLevel;
                Reward = reward;
            }

            public int MovesUsed { get; }

            /// <summary>The moves the player did not need.</summary>
            public int MovesLeft { get; }

            /// <summary>True when no level follows: the button then offers the same level again.</summary>
            public bool IsLastLevel { get; }

            /// <summary>The jigsaw pieces the win gave; nothing for a level that was won before.</summary>
            public PieceReward? Reward { get; }
        }

        /// <summary>The player asked to leave a game that is still being played.</summary>
        public sealed class Leaving : LevelResult
        {
            public Leaving(int levelNumber, int attemptNumber, bool hasPlayed, IReadOnlyList<GoalFigure> openGoals)
                : base(levelNumber)
            {
                AttemptNumber = attemptNumber;
                HasPlayed = hasPlayed;
                OpenGoals = openGoals;
            }

            /// <summary>Which try at the level this is, counted from 1.</summary>
            public int AttemptNumber { get; }

            /// <summary>True once a move was played: the attempt counts, so the level's next board is another one.</summary>
            public bool HasPlayed { get; }

            /// <summary>What is still to do: the goals not reached yet, each with what is left of it.</summary>
            public IReadOnlyList<GoalFigure> OpenGoals { get; }
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
            LevelResult.Leaving _ => "Bölümden çık?",
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The line under the alcove: what a won game gave, or, where it gave nothing and for a lost game, the level
        /// with the moves it took or the attempt it was.
        /// </summary>
        public static string Headline(LevelResult result) => result switch
        {
            LevelResult.Won { Reward: PieceReward reward } => MuseumWords.Earned(reward),
            LevelResult.Won won => LevelAndMoves(won),
            LevelResult.Lost lost => $"Bölüm {Number(lost.LevelNumber)} · {Number(lost.AttemptNumber)}. deneme",
            LevelResult.Leaving leaving => $"Bölüm {Number(leaving.LevelNumber)} · {Number(leaving.AttemptNumber)}. deneme",
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The smaller line under the headline: the level and its moves where the headline says what was won, else
        /// the moves a won game left over, or, for a lost one, what the alcove above it shows; a player who asks
        /// to leave is told what leaving costs (the attempt counts once a move was played: the level's next board
        /// is then another one).
        /// </summary>
        public static string Footnote(LevelResult result) => result switch
        {
            LevelResult.Won { Reward: PieceReward _ } won => LevelAndMoves(won),
            LevelResult.Won { MovesLeft: 0 } => "Son hamlede tamamlandı",
            LevelResult.Won won => $"{Number(won.MovesLeft)} hamle arttı",
            LevelResult.Lost _ => "Hedefe bu kadar kaldı",
            LevelResult.Leaving { HasPlayed: true } => "Çıkarsan bu deneme sayılır",
            LevelResult.Leaving _ => "Henüz hamle yapmadın",
            _ => throw Unknown(result),
        };

        /// <summary>The green button: on with the levels, or, for a player who asked to leave, on with the game.</summary>
        public static string GoOn(LevelResult result) => result switch
        {
            LevelResult.Leaving _ => "Oyna",
            LevelResult.Won { IsLastLevel: true } => "Tekrar oyna",
            LevelResult.Won _ => "Devam",
            LevelResult.Lost _ => "Tekrar dene",
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The green button's label where the button is the narrow one, beside the blue button that leads to the
        /// jigsaw: a label of two words stands on two lines there, as the blue button's own does, so its lettering
        /// stays as large as its neighbour's instead of being squeezed onto one line.
        /// </summary>
        public static string GoOnOnANarrowButton(LevelResult result) => GoOn(result).Replace(' ', '\n');

        private static string LevelAndMoves(LevelResult.Won won) => $"Bölüm {Number(won.LevelNumber)} · {Number(won.MovesUsed)} hamlede";

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

        private static InvalidOperationException Unknown(LevelResult result) =>
            new InvalidOperationException($"A result of the kind {result.GetType().Name} has no words.");
    }
}
