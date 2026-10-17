#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Puzzle.Client.Gallery;
using Puzzle.Client.Screens;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Meta;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// What the stage is told about the attempt it shows, beyond the game itself: the things only the player's
    /// progress knows, the economy's rule for what a win pays, which the plaques and the result panel say, and the
    /// build's for how an ad is shown, which the offer of extra moves plays.
    /// </summary>
    public sealed class AttemptBrief
    {
        /// <param name="attemptNumber">Which try at the level this is, counted from 1.</param>
        /// <param name="winPays">What a win pays, as the attempt's ledger pays it (<see cref="AttemptLedger"/>): the card shows those coins.</param>
        /// <param name="reward">The jigsaw pieces winning this attempt gives; nothing when it gives none.</param>
        /// <param name="startBoosters">The start boosters the player took into the attempt; nothing when they took none.</param>
        /// <param name="adsShownAs">How the build shows the ad that pays for extra moves (the settings' build.ads); a test build knows the simulated one alone.</param>
        /// <exception cref="ArgumentOutOfRangeException">The attempt number is less than 1.</exception>
        public AttemptBrief(
            int attemptNumber, bool isOnLastLevel, WinReward winPays, PieceReward? reward = null, StartChoice? startBoosters = null, AdSource adsShownAs = AdSource.Simulated)
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
            AdsShownAs = adsShownAs;
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

        /// <summary>How the ad that pays for the extra moves is shown: the economy's rule, which refuses any way a build does not have.</summary>
        public AdSource AdsShownAs { get; }

        /// <summary>A first try at a level that has a level after it and gives no jigsaw piece: what a picture of a single level shows.</summary>
        public static AttemptBrief FirstTry(WinReward winPays) => new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, winPays);
    }

    /// <summary>
    /// What the player chose on a card in front of the board: where they go on to from a decided game's card, or, on a
    /// card that does not end the attempt (the question whether to leave, the offer of extra moves), what becomes of
    /// the game.
    /// </summary>
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

        /// <summary>The green button of the card of a game out of moves: five more moves, paid with coins.</summary>
        TakeTheMovesForCoins,

        /// <summary>Its blue button: five more moves, paid with one of the day's free ads.</summary>
        TakeTheMovesForAnAd,

        /// <summary>Its "Vazgeç": the game is lost for good, which costs a life.</summary>
        GiveUp,
    }

    /// <summary>How a lost game was lost for good.</summary>
    public enum LossReason
    {
        /// <summary>The moves ran out before the goals were met, after the extra moves were taken once.</summary>
        OutOfMoves,

        /// <summary>No move was possible even after the board was shuffled; a validated level never gets here.</summary>
        NoMovePossible,

        /// <summary>The moves ran out and the player turned the offer of extra moves down.</summary>
        GaveUp,

        /// <summary>The moves ran out and the extra moves were asked for, but the board has no move a shuffle could bring back: the engine gave none, and nothing was paid.</summary>
        ExtraMovesRefused,
    }

    /// <summary>
    /// What the card that hangs in front of a board tells. A closed set (private constructor): a decided attempt is
    /// either <see cref="Won"/> or <see cref="Lost"/>; a game whose moves ran out for the first time is offered five
    /// more (<see cref="OutOfMoves"/>) before it is lost; and an attempt that is still being played has a card only
    /// while its player is asked whether they mean to leave it (<see cref="Leaving"/>). This file has no Unity
    /// dependency, so the repository's tests compile it too.
    /// </summary>
    public abstract class LevelResult
    {
        /// <summary>
        /// A game out of moves was close to its win when at least this share of its goals was done, in percent: the
        /// near-miss line the level reports use, so the card never tells a player far from the goals that it was close.
        /// </summary>
        public const int CloseFromGoalsDonePercent = 80;

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
        /// <exception cref="InvalidOperationException">The card asks whether to leave, or offers extra moves: the game is not decided, and the card leads nowhere in the museum.</exception>
        public MuseumDoor GoingOnLeadsTo() => this switch
        {
            Won { IsLastLevel: true } => MuseumDoor.HomeWithTheStartCard,
            Won _ => MuseumDoor.Home,
            Lost _ => MuseumDoor.HomeWithTheStartCard,
            Leaving _ => throw new InvalidOperationException("The card that asks whether to leave keeps the player in the game; it leads nowhere in the museum."),
            OutOfMoves _ => throw new InvalidOperationException("The card that offers extra moves decides the game first; it leads nowhere in the museum."),
            _ => throw new InvalidOperationException($"A result of the kind {GetType().Name} leads nowhere."),
        };

        /// <summary>
        /// The result of a game that is over for good, by the way the engine ended it. A game whose moves ran out is
        /// lost for good only once the extra moves were taken, or turned down (<see cref="LostForGood"/>); while they
        /// are offered its card is the offer's (<see cref="OfferTo"/>).
        /// </summary>
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

        /// <summary>The result of a lost game that is lost for good for a reason the engine's status does not tell: the offer of extra moves turned down, or refused by the engine.</summary>
        /// <exception cref="InvalidOperationException">The game is not lost.</exception>
        public static LevelResult LostForGood(Game game, AttemptBrief brief, LossReason reason) => game.Status.IsLoss()
            ? new Lost(game.Level.Id.Value, reason, brief.AttemptNumber, OpenGoalsOf(game))
            : throw new InvalidOperationException($"Level {game.Level.Id.Value} is {game.Status}; only a lost game is lost for good.");

        /// <summary>What a player is shown whose moves ran out for the first time: what is left of the goals, and the offer of five more moves.</summary>
        /// <exception cref="InvalidOperationException">The offer is not made to the game (<see cref="OutOfMovesOffer.IsMadeIn"/>).</exception>
        public static OutOfMoves OfferTo(Game game, AttemptBrief brief) => OutOfMovesOffer.IsMadeIn(game)
            ? new OutOfMoves(game.Level.Id.Value, brief.AttemptNumber, OpenGoalsOf(game), isClose: game.Goals.OverallProgress * 100.0 >= CloseFromGoalsDonePercent)
            : throw new InvalidOperationException($"Level {game.Level.Id.Value} is {game.Status}{(game.ExtraMovesTaken ? " and took its extra moves" : string.Empty)}; it is offered no extra moves.");

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

        /// <summary>
        /// The moves ran out with goals still open, for the first time in the attempt: the game is not lost yet. Its
        /// card offers five more moves for coins or for one of the day's free ads, and giving up, which loses it for good.
        /// </summary>
        public sealed class OutOfMoves : LevelResult
        {
            public OutOfMoves(int levelNumber, int attemptNumber, IReadOnlyList<GoalFigure> openGoals, bool isClose)
                : base(levelNumber)
            {
                AttemptNumber = attemptNumber;
                OpenGoals = openGoals;
                IsClose = isClose;
            }

            /// <summary>Which try at the level this is, counted from 1.</summary>
            public int AttemptNumber { get; }

            /// <summary>What is left to do: the goals not reached yet, each with what is left of it.</summary>
            public IReadOnlyList<GoalFigure> OpenGoals { get; }

            /// <summary>True when the game had done at least <see cref="CloseFromGoalsDonePercent"/> percent of its goals: the card says it was close.</summary>
            public bool IsClose { get; }
        }

        /// <summary>The game ended with goals still open, and nothing brings it back.</summary>
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
    /// page's (result-panel.js) and the design canvas' (WinTR, LoseTR).
    /// </summary>
    public static class ResultWords
    {
        /// <summary>The offer's card: "Vazgeç", underlined, beside what giving up costs (LoseTR).</summary>
        public const string GiveUp = "Vazgeç";

        /// <summary>What the offer's blue button says while the day has a free ad left for the extra moves, and once it has none (the button is dimmed then).</summary>
        public const string WatchAnAd = "Reklam izle";
        public const string NoAdLeftToday = "Bugünlük bitti";

        /// <summary>On the pill of a card whose heart says what a life is: one life fewer.</summary>
        public const string OneLifeFewer = "–1";

        /// <summary>Where a card would say a life goes, in the tester mode, which takes none (<see cref="WalletHandling.Unlimited"/>).</summary>
        public const string NoLifeInTheTesterMode = "Test modu · can gitmez";

        /// <summary>The nameplate: what happened.</summary>
        public static string Title(LevelResult result) => result switch
        {
            LevelResult.Won _ => "Bölüm tamamlandı!",
            LevelResult.OutOfMoves _ => OutOfMovesTitle,
            LevelResult.Lost { Reason: LossReason.NoMovePossible } => NoMoveLeftTitle,
            LevelResult.Lost { Reason: LossReason.ExtraMovesRefused } => NoMoveLeftTitle,
            LevelResult.Lost _ => OutOfMovesTitle,
            LevelResult.Leaving _ => "Bölümden çık?",
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The line under the alcove: what a won game gave, or, where it gave nothing and for a lost game, the level
        /// with the moves it took or the attempt it was. On the offer of extra moves, over the goals that are left:
        /// that the game was close where it was (<see cref="LevelResult.OutOfMoves.IsClose"/>), and a plain line where it was not.
        /// </summary>
        public static string Headline(LevelResult result) => result switch
        {
            LevelResult.Won { Reward: PieceReward reward } => MuseumWords.Earned(reward),
            LevelResult.Won won => LevelAndMoves(won),
            LevelResult.OutOfMoves { IsClose: true } => "Az kaldı!",
            LevelResult.OutOfMoves _ => "Kalan hedefler",
            LevelResult.Lost lost => LevelAndAttempt(lost.LevelNumber, lost.AttemptNumber),
            LevelResult.Leaving leaving => LevelAndAttempt(leaving.LevelNumber, leaving.AttemptNumber),
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The smaller line under the headline of a card of what is left: what the alcove above it shows, or, for a
        /// game whose extra moves the engine refused, why it is lost and that nothing was paid for them.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The game is won: its card says what the moves left over became beside its coins (<see cref="MovesLeftOver"/>);
        /// or it is offered extra moves: its goals are under the headline, and what giving up costs is beside <see cref="GiveUp"/>.
        /// </exception>
        public static string Footnote(LevelResult result) => result switch
        {
            LevelResult.Won _ => throw new InvalidOperationException("A won level's card has no footnote: beside its coins it says what the moves left over became."),
            LevelResult.OutOfMoves _ => throw new InvalidOperationException("The offer's card has no footnote: its goals stand under the headline, and what giving up costs beside \"Vazgeç\"."),
            LevelResult.Lost { Reason: LossReason.ExtraMovesRefused } => "+5 hamle verilemedi, bir şey harcanmadı",
            LevelResult.Lost _ => WhatIsLeftOfTheGoals,
            LevelResult.Leaving _ => WhatIsLeftOfTheGoals,
            _ => throw Unknown(result),
        };

        /// <summary>
        /// Beside the heart of a card: what the attempt costs in lives (<see cref="AttemptLedger"/>). A loss for good
        /// took one; leaving after a move takes one and leaving before any takes none; giving up on the offer of extra
        /// moves takes one.
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is won: a win costs no life, and its card says nothing of lives.</exception>
        public static string Lives(LevelResult result) => result switch
        {
            LevelResult.Won _ => throw new InvalidOperationException("A won level's card says nothing of lives: a win costs none."),
            LevelResult.OutOfMoves _ => "1 can gider",
            LevelResult.Lost _ => "1 can gitti",
            LevelResult.Leaving { HasPlayed: true } => "Çıkarsan 1 can gider",
            LevelResult.Leaving _ => "Henüz hamle yapmadın, can gitmez",
            _ => throw Unknown(result),
        };

        /// <summary>True when the card's heart stands on a pill that says one life fewer: the attempt costs, or cost, a life.</summary>
        public static bool CostsALife(LevelResult result) => !(result is LevelResult.Won || result is LevelResult.Leaving { HasPlayed: false });

        /// <summary>
        /// Beside the heart of a card, for a wallet held as the client holds it: as <see cref="Lives(LevelResult)"/>
        /// says, but in the tester mode, where the attempt would have cost a life and costs none, that it costs none.
        /// </summary>
        /// <exception cref="InvalidOperationException">The game is won: its card says nothing of lives.</exception>
        public static string Lives(LevelResult result, WalletHandling handling) =>
            CostsALife(result) && !CostsALife(result, handling) ? NoLifeInTheTesterMode : Lives(result);

        /// <summary>True when the card's heart stands on a pill that says one life fewer, for a wallet held as the client holds it: never in the tester mode.</summary>
        public static bool CostsALife(LevelResult result, WalletHandling handling) => handling switch
        {
            WalletHandling.Normal => CostsALife(result),
            WalletHandling.Unlimited => false,
            _ => throw new ArgumentOutOfRangeException(nameof(handling), handling, "A way of holding the wallet the card does not know."),
        };

        /// <summary>What giving up the offer of extra moves costs, beside the heart after "Vazgeç": a life; nothing in the tester mode, where "Vazgeç" stands alone.</summary>
        public static string? GiveUpCosts(LevelResult.OutOfMoves offer, WalletHandling handling) => CostsALife(offer, handling) ? Lives(offer) : null;

        /// <summary>The offer's green button: the moves it gives while the coins cover their price; that they do not, where they do not (the button is dimmed then).</summary>
        public static string TakeTheMovesForCoins(ExtraMovesTerms terms) => terms.CoinsCoverIt ? $"+{ExtraMovesOffer.Moves} hamle" : LevelStartWords.CoinsShort;

        /// <summary>The offer's blue button: an ad pays for the moves while the day has one left for them.</summary>
        public static string TakeTheMovesForAnAd(ExtraMovesTerms terms) => terms.AnAdPaysToday ? WatchAnAd : NoAdLeftToday;

        /// <summary>The price of the extra moves in the pill on the green button, as the wallet writes an amount.</summary>
        public static string PriceOfTheMoves(ExtraMovesTerms terms) => WalletWords.Amount(terms.Price);

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
        /// <exception cref="InvalidOperationException">The card offers extra moves: its green button pays for them (<see cref="TakeTheMovesForCoins"/>).</exception>
        public static string GoOn(LevelResult result) => result switch
        {
            LevelResult.Leaving _ => "Oyna",
            LevelResult.Won { IsLastLevel: true } => "Tekrar oyna",
            LevelResult.Won _ => "Devam",
            LevelResult.Lost _ => "Tekrar dene",
            LevelResult.OutOfMoves _ => throw new InvalidOperationException("The offer's green button pays for the extra moves; it does not go on."),
            _ => throw Unknown(result),
        };

        /// <summary>
        /// The green button's label where the button is the narrow one, beside the blue button that leads to the
        /// jigsaw: a label of two words stands on two lines there, as the blue button's own does, so its lettering
        /// stays as large as its neighbour's instead of being squeezed onto one line.
        /// </summary>
        public static string GoOnOnANarrowButton(LevelResult result) => GoOn(result).Replace(' ', '\n');

        private const string OutOfMovesTitle = "Hamle bitti!";
        private const string NoMoveLeftTitle = "Oynanacak hamle kalmadı!";
        private const string WhatIsLeftOfTheGoals = "Hedefe bu kadar kaldı";

        private static string LevelAndMoves(LevelResult.Won won) => $"Bölüm {Number(won.LevelNumber)} · {Number(won.MovesUsed)} hamlede";

        private static string LevelAndAttempt(int levelNumber, int attemptNumber) => $"Bölüm {Number(levelNumber)} · {Number(attemptNumber)}. deneme";

        private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

        private static InvalidOperationException Unknown(LevelResult result) =>
            new InvalidOperationException($"A result of the kind {result.GetType().Name} has no words.");
    }
}
