#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Gallery;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the stage is told about the attempt it shows, beyond the game itself: the things only the player's
    /// progress knows, and the economy's rule for what a win pays, which the plaques and the result panel say.
    /// </summary>
    public sealed class AttemptBrief
    {
        /// <param name="attemptNumber">Which try at the level this is, counted from 1.</param>
        /// <param name="winPays">What a win pays, as the attempt's ledger pays it (<see cref="AttemptLedger"/>): the card shows those coins.</param>
        /// <param name="reward">The jigsaw pieces winning this attempt gives; nothing when it gives none.</param>
        /// <param name="startBoosters">The start boosters the player took into the attempt; nothing when they took none.</param>
        /// <exception cref="ArgumentOutOfRangeException">The attempt number is less than 1.</exception>
        public AttemptBrief(int attemptNumber, bool isOnLastLevel, WinReward winPays, PieceReward? reward = null, StartChoice? startBoosters = null)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber), attemptNumber, "A player's attempts are counted from 1.");
            }

            AttemptNumber = attemptNumber;
            IsOnLastLevel = isOnLastLevel;
            WinPays = winPays ?? throw new ArgumentNullException(nameof(winPays));
            Reward = reward;
            StartBoosters = startBoosters ?? StartChoice.None;
        }

        public int AttemptNumber { get; }

        /// <summary>True when no level follows this one: winning it leads nowhere new.</summary>
        public bool IsOnLastLevel { get; }

        /// <summary>What a win of the attempt pays: the economy's rule, which the attempt's ledger pays by.</summary>
        public WinReward WinPays { get; }

        /// <summary>The jigsaw pieces of a painting that winning the attempt gives the player; nothing when it gives none.</summary>
        public PieceReward? Reward { get; }

        /// <summary>The start boosters the player took into the attempt: they leave the shelf with its first move.</summary>
        public StartChoice StartBoosters { get; }

        /// <summary>A first try at a level that has a level after it and gives no jigsaw piece: what a picture of a single level shows.</summary>
        public static AttemptBrief FirstTry(WinReward winPays) => new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, winPays);
    }

    /// <summary>Where the player goes on to from a result's card.</summary>
    public enum ResultChoice
    {
        /// <summary>On with the levels, through the hall: the next one after a win, the same one again after a loss (<see cref="LevelResult.GoingOnLeadsTo"/>).</summary>
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

        /// <summary>
        /// Where the green button of a decided game's card leads, the card before a level being the one door into
        /// it: after a win to the hall, where the coins arrive and the next level waits behind the play button;
        /// after a loss, and after the last level won once more, to the hall with the card of the same level coming
        /// down, so it is tried again with its start boosters chosen anew.
        /// </summary>
        /// <exception cref="InvalidOperationException">The card asks whether to leave: its green button keeps the player in the game.</exception>
        public MuseumDoor GoingOnLeadsTo() => this switch
        {
            Won { IsLastLevel: true } => MuseumDoor.HomeWithTheStartCard,
            Won _ => MuseumDoor.Home,
            Lost _ => MuseumDoor.HomeWithTheStartCard,
            Leaving _ => throw new InvalidOperationException("The card that asks whether to leave keeps the player in the game; it leads nowhere in the museum."),
            _ => throw new InvalidOperationException($"A result of the kind {GetType().Name} leads nowhere."),
        };

        /// <summary>The result of a game that is over.</summary>
        /// <exception cref="InvalidOperationException">The game is still being played.</exception>
        public static LevelResult Of(Game game, AttemptBrief brief)
        {
            int level = game.Level.Id.Value;
            return game.Status switch
            {
                GameStatus.Won => new Won(level, game.MovesUsed, game.MovesLeft, brief.IsOnLastLevel, brief.Reward, brief.WinPays.Of(game.MovesLeft)),
                GameStatus.LostOutOfMoves => new Lost(level, LossReason.OutOfMoves, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.LostNoMovesPossible => new Lost(level, LossReason.NoMovePossible, brief.AttemptNumber, OpenGoalsOf(game)),
                GameStatus.Playing => throw new InvalidOperationException($"Level {level} is still being played; it has no result yet."),
                _ => throw new InvalidOperationException($"A game that ended as {game.Status} is not known to the result panel."),
            };
        }

        /// <summary>What a player is shown who asks to leave the game they are playing.</summary>
        /// <exception cref="InvalidOperationException">The game is decided: it has a result, and nothing is left to leave.</exception>
        public static LevelResult ForLeaving(Game game, AttemptBrief brief) => game.Status == GameStatus.Playing
            ? new Leaving(game.Level.Id.Value, brief.AttemptNumber, hasPlayed: AttemptBegun.In(game), OpenGoalsOf(game))
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
            public Won(int levelNumber, int movesUsed, int movesLeft, bool isLastLevel, PieceReward? reward, Coins coins)
                : base(levelNumber)
            {
                MovesUsed = movesUsed;
                MovesLeft = movesLeft;
                IsLastLevel = isLastLevel;
                Reward = reward;
                Coins = coins;
            }

            /// <summary>
            /// The coins the win paid: what the economy's rule pays for it with the moves the game says were left,
            /// which the attempt's ledger paid at the winning move. The finale's rockets do not change it.
            /// </summary>
            public Coins Coins { get; }

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

            /// <summary>True once a move was played, or a booster used during play, which counts as one: the attempt counts, so the level's next board is another one.</summary>
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
        /// The smaller line under the headline of a card of what is left: for a lost game, what the alcove above it
        /// shows; a player who asks to leave is told what leaving costs, the life that leaving after a move takes
        /// (<see cref="AttemptLedger"/>) or that nothing is lost before one.
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is won: its card says what the moves left over became beside its coins (<see cref="MovesLeftOver"/>).</exception>
        public static string Footnote(LevelResult result) => result switch
        {
            LevelResult.Won _ => throw new InvalidOperationException("A won level's card has no footnote: beside its coins it says what the moves left over became."),
            LevelResult.Lost _ => "Hedefe bu kadar kaldı",
            LevelResult.Leaving { HasPlayed: true } => "Çıkarsan 1 can gider",
            LevelResult.Leaving _ => "Henüz hamle yapmadın, can gitmez",
            _ => throw Unknown(result),
        };

        /// <summary>The coins a win paid, on their pill (WinTR, "+45").</summary>
        public static string CoinsWon(Coins coins) => $"+{NumberWords.Amount(coins.Amount)}";

        /// <summary>
        /// Beside the coins of a won card: what became of the moves left over, which the finale turned into rockets,
        /// the canvas' brushes (WinTR), on two lines beside the brush; with no move left over, one line and no brush.
        /// </summary>
        public static string MovesLeftOver(LevelResult.Won won) => won.MovesLeft == 0
            ? "Son hamlede tamamlandı"
            : $"Kalan {Number(won.MovesLeft)} hamle\nfırçaya dönüştü";

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
