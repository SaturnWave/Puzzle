#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line, by the touches a player makes on the game's own session, the offer of five more
    /// moves to a game that ran out of them (UI stage 11). The offer comes when the moves run out with goals open, and
    /// until it is answered nothing ends the attempt: no life goes, no record is written, the board takes no touch and
    /// the way back does nothing. Paid with coins it costs exactly its price; paid with an ad it shows the card that
    /// stands in for one for its seconds and then counts one of the day's free ads; either way the same attempt goes on
    /// with five more moves on the plaque, and a second run-out is lost for good with no offer. A way to pay the
    /// wallet does not allow is dimmed and a press on it does nothing. Giving up costs a life, writes the record, and
    /// its card's "Tekrar dene" leads through the level's card, or to the lives card when no life is left. A board no
    /// shuffle brings back (made by hand) is refused the moves with nothing paid, and its card says why. A game won
    /// after the extra moves has its finale and the coins of its win. Every record written is played again on the
    /// engine (AttemptReplay) to the end seen.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.OfferCheck.Run
    /// </summary>
    public static class OfferCheck
    {
        /// <summary>A level the random bot loses by its moves, as the flow storyboards play it.</summary>
        private const int LostLevel = 40;

        private static readonly ScreenShape Screen = new ScreenShape(1080, 2340);

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var bench = new Bench();
                var log = new AttemptsInMemory(StoryStudio.Device);
                var passed = new List<string>
                {
                    TheOfferHoldsTheAttemptAndCoinsBuyFiveMoreOnce(bench, log),
                    AnAdIsShownForItsSecondsAndCountedForTheDay(bench, log),
                    AWayToPayTheWalletDoesNotAllowDoesNothingAndGivingUpCostsALife(bench, log),
                    GivingUpTheLastLifeLeadsToTheLivesCard(bench, log),
                    ABoardNoShuffleBringsBackIsRefusedAndNothingIsPaid(bench, log),
                    AWinAfterTheExtraMovesHasItsFinaleAndItsCoins(bench),
                };
                passed.Add(EveryRecordReplaysToTheEndSeen(bench, log));
                Debug.Log($"Offer check passed: {string.Join("; ", passed)}.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException || failure is IOException || failure is AttemptReplayException)
            {
                Debug.LogError($"Offer check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The coins path: the moves run out, the offer comes, and until it is answered the attempt holds; the green
        /// button pays the price, the plaque counts five more, the same attempt goes on, and its second run-out is
        /// lost for good at once, one life and one record.
        /// </summary>
        private static string TheOfferHoldsTheAttemptAndCoinsBuyFiveMoreOnce(Bench bench, AttemptsInMemory log)
        {
            Wallet rich = bench.Wallet(coins: 1250);
            using GameSession session = bench.SessionOutOfMoves(rich, log, out PlayerProgress atTheLosingMove);
            GameStage stage = session.Stage;
            int records = log.Records.Count;
            Require(atTheLosingMove.Wallet.Lives.IsFull, $"the move that ran out of moves cost a life before the offer was answered ({atTheLosingMove.Wallet.Lives})");
            Require(stage.IsOfferingExtraMoves && stage.CardTells is LevelResult.OutOfMoves, "no offer of extra moves hangs after the moves ran out");
            Require(stage.ExtraMovesOnTheCard is ExtraMovesTerms terms && terms.CoinsCoverIt && terms.FreeAdsLeftToday == 3, $"the offer shows the terms {stage.ExtraMovesOnTheCard}");

            NothingEndsTheAttemptWhileTheOfferHangs(session, log, atTheLosingMove);

            AtTheOffer.Take(session, ExtraMovesPayment.ByCoins);
            Game game = stage.Game;
            Require(game.Status == GameStatus.Playing && game.MovesLeft == ExtraMovesOffer.Moves, $"after the coins were paid the game is {game.Status} with {game.MovesLeft} moves left");
            Require(stage.Plaques.MovesLeft == ExtraMovesOffer.Moves && !stage.Plaques.DifferencesFrom(HudFigures.Of(game)).Any(), $"the plaque shows {stage.Plaques.MovesLeft} moves left, not the five more");
            Require(session.Progress.Wallet.Coins == rich.Coins.Less(StoryStudio.Economy.ExtraMoves.Price), $"the extra moves left {session.Progress.Wallet.Coins} of {rich.Coins} coins; their price is {StoryStudio.Economy.ExtraMoves.Price}");
            Require(session.Progress.Wallet.Ads.Equals(atTheLosingMove.Wallet.Ads), "the extra moves paid with coins counted an ad");
            Require(session.Progress.Wallet.Lives.IsFull && log.Records.Count == records, "the extra moves taken cost a life or wrote a record");
            Require(session.Progress.AttemptsOnLevel == atTheLosingMove.AttemptsOnLevel, "the extra moves counted another attempt");
            Require(!stage.IsOfferingExtraMoves && stage.CardTells == null, "a card still hangs after the extra moves were taken");

            GameStatus ending = AtTheOffer.PlayOn(session, new RandomPolicy(), new Rng(20261015, 7));
            StoryStudio.AdvanceUntilSettled(session);
            Require(ending.IsLoss(), $"the random bot won level {LostLevel} with the five more moves; the check needs it to run out again");
            Require(!stage.IsOfferingExtraMoves && stage.CardTells is LevelResult.Lost, "the moves ran out a second time and the card is not the loss for good");
            Require(log.Records.Count == records + 1 && log.Records.Last().Outcome.ExtraMovesBought, "the attempt that ran out a second time wrote no record, or one that does not say the moves were taken");
            Require(session.Progress.Wallet.Lives.LivesUnder(StoryStudio.Economy.Lives) == StoryStudio.Economy.Lives.Max - 1, $"the loss for good after the extra moves left lives \"{session.Progress.Wallet.Lives}\"");
            return $"coins: offer held, {StoryStudio.Economy.ExtraMoves.Price} coins paid, five more moves played, the second run-out lost for good ({ending})";
        }

        /// <summary>While the offer hangs: a swipe on the board, the way back and the pause disc do nothing, and nothing is kept.</summary>
        private static void NothingEndsTheAttemptWhileTheOfferHangs(GameSession session, AttemptsInMemory log, PlayerProgress atTheLosingMove)
        {
            GameStage stage = session.Stage;
            Game game = stage.Game;
            int movesUsed = game.MovesUsed;
            int records = log.Records.Count;
            Move? trade = game.FindLegalMoves().FirstOrDefault(move => move.Kind == MoveKind.Swap);
            if (trade is Move swap)
            {
                session.PointerDown(stage.PixelOf(swap.From));
                session.PointerMoved(stage.PixelOf(swap.To));
                session.PointerUp();
            }

            session.GoBack();
            Require(stage.PauseButtonPixel == null, "the pause disc listens under the offer");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.IsOfferingExtraMoves && game.MovesUsed == movesUsed && game.Status == GameStatus.LostOutOfMoves, "a swipe or the way back under the offer moved a piece or took the offer away");
            Require(session.Progress.Equals(atTheLosingMove) && log.Records.Count == records, "a swipe or the way back under the offer changed the progress or wrote a record");
        }

        /// <summary>
        /// The ad path: a player with no coin for the price presses the blue button; the engine gives the moves, the
        /// card that stands in for an ad comes down, and only when its seconds are over is one of the day's ads counted.
        /// </summary>
        private static string AnAdIsShownForItsSecondsAndCountedForTheDay(Bench bench, AttemptsInMemory log)
        {
            using GameSession session = bench.SessionOutOfMoves(bench.Wallet(coins: 0), log, out PlayerProgress atTheLosingMove);
            GameStage stage = session.Stage;
            int records = log.Records.Count;
            Require(stage.ExtraMovesOnTheCard is ExtraMovesTerms terms && !terms.CoinsCoverIt && terms.AnAdPaysToday, $"the offer to a player without coins shows the terms {stage.ExtraMovesOnTheCard}");

            ThroughTheCard.Press(session, stage.TakeTheMovesForCoinsPixel, "the offer's dimmed green button");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.IsOfferingExtraMoves && stage.Game.Status == GameStatus.LostOutOfMoves && session.Progress.Equals(atTheLosingMove), "a press on the dimmed green button did something");

            ThroughTheCard.Press(session, stage.TakeTheMovesForAnAdPixel, "the offer's Reklam izle");
            ThroughTheCard.WaitFor(session, () => stage.AdProgress > 0f, "the card that stands in for an ad did not come down and begin");
            Require(stage.IsShowingTheAd && session.Progress.Wallet.Ads.Equals(atTheLosingMove.Wallet.Ads), "the ad was counted before it was over");
            session.GoBack();
            float shown = StoryStudio.AdvanceUntilSettled(session);
            Require(shown >= SimulatedAd.Seconds - 0.5f, $"the ad was over {StoryStudio.Seconds(shown)} s after it began; it stands in for one for {SimulatedAd.Seconds} s");
            Require(!stage.IsShowingTheAd && !stage.IsOfferingExtraMoves, "the ad's card, or the offer's, still hangs");

            AdsWatched today = session.Progress.Wallet.Ads.On(DayStamp.Of(StoryStudio.Clock.Now));
            Require(today.ForExtraMoves == 1 && today.ForCoins == 0, $"after the ad the day counts {today}");
            Require(session.Progress.Wallet.Coins.IsNone && session.Progress.Wallet.Lives.IsFull && log.Records.Count == records, "the ad cost coins or a life, or wrote a record");
            Require(stage.Game.Status == GameStatus.Playing && stage.Game.MovesLeft == ExtraMovesOffer.Moves && stage.Plaques.MovesLeft == ExtraMovesOffer.Moves, "after the ad the game does not go on with five more moves");
            Require(session.TryPlay(stage.Game.FindLegalMoves()[0]), "the board refused a move after the ad");
            StoryStudio.AdvanceUntilSettled(session);
            return $"ad: shown for {StoryStudio.Seconds(shown)} s, then one of the day's {StoryStudio.Economy.ExtraMoves.FreeAdsPerDay} counted, the game went on";
        }

        /// <summary>
        /// No coin for the price and the day's ads used up: both buttons are dimmed and pressed in vain; "Vazgeç"
        /// loses the game for good, which costs a life and writes the record, and "Tekrar dene" brings the level's card.
        /// </summary>
        private static string AWayToPayTheWalletDoesNotAllowDoesNothingAndGivingUpCostsALife(Bench bench, AttemptsInMemory log)
        {
            Wallet spent = bench.Wallet(coins: 899).WithAds(new AdsWatched(DayStamp.Of(StoryStudio.Clock.Now), forExtraMoves: 3, forCoins: 0));
            using GameSession session = bench.SessionOutOfMoves(spent, log, out PlayerProgress atTheLosingMove);
            GameStage stage = session.Stage;
            int records = log.Records.Count;
            Require(stage.ExtraMovesOnTheCard is ExtraMovesTerms terms && !terms.CoinsCoverIt && !terms.AnAdPaysToday, $"the offer shows the terms {stage.ExtraMovesOnTheCard}");

            ThroughTheCard.Press(session, stage.TakeTheMovesForCoinsPixel, "the offer's dimmed green button");
            ThroughTheCard.Press(session, stage.TakeTheMovesForAnAdPixel, "the offer's dimmed blue button");
            StoryStudio.AdvanceUntilSettled(session);
            Require(stage.IsOfferingExtraMoves && !stage.IsShowingTheAd && session.Progress.Equals(atTheLosingMove) && log.Records.Count == records, "a press on a dimmed button of the offer did something");

            AtTheOffer.GiveUp(session);
            Require(stage.CardTells is LevelResult.Lost { Reason: LossReason.GaveUp }, $"after Vazgeç the card tells {stage.CardTells}");
            Require(session.Progress.Wallet.Lives.LivesUnder(StoryStudio.Economy.Lives) == StoryStudio.Economy.Lives.Max - 1, $"giving up left lives \"{session.Progress.Wallet.Lives}\"");
            Require(session.Progress.Wallet.Coins == spent.Coins && session.Progress.Wallet.Ads.Equals(spent.Ads), "giving up spent coins or an ad");
            Require(log.Records.Count == records + 1 && log.Records.Last().Outcome.Result == AttemptResult.OutOfMoves && !log.Records.Last().Outcome.ExtraMovesBought, "giving up wrote no record, or not one of a loss without the extra moves");

            ThroughTheCard.Press(session, stage.ResultButtonPixel, "the lost card's Tekrar dene");
            ThroughTheCard.WaitFor(session, () => session.Museum != null, "the lost card's Tekrar dene opened no museum");
            StoryStudio.AdvanceUntilSettled(session);
            Require(ThroughTheCard.MuseumOf(session).ShowsTheStartCard, "after giving up Tekrar dene did not bring the level's card down");
            return "no way to pay: both buttons dimmed and pressed in vain, Vazgeç cost a life and wrote the record, Tekrar dene brought the level's card";
        }

        /// <summary>Giving up with the last life: "Tekrar dene" brings the lives card, and no level's card.</summary>
        private static string GivingUpTheLastLifeLeadsToTheLivesCard(Bench bench, AttemptsInMemory log)
        {
            LifeRules lives = StoryStudio.Economy.Lives;
            Wallet lastLife = bench.Wallet(coins: 1250).WithLives(new LifeMeter(lives.Max - 1, StoryStudio.Clock.Now + lives.PerLife));
            using GameSession session = bench.SessionOutOfMoves(lastLife, log, out _);
            AtTheOffer.GiveUp(session);
            ThroughTheCard.Press(session, session.Stage.ResultButtonPixel, "the lost card's Tekrar dene");
            ThroughTheCard.WaitFor(session, () => session.Museum != null, "the lost card's Tekrar dene opened no museum");
            StoryStudio.AdvanceUntilSettled(session);
            Require(ThroughTheCard.MuseumOf(session).ShowsTheLivesCard && !ThroughTheCard.MuseumOf(session).ShowsTheStartCard, "giving up the last life did not lead to the lives card");
            return "the last life given up: the lives card came";
        }

        /// <summary>
        /// A board made by hand, boxes all around one rocket and one move: once the rocket has fired nothing can move
        /// and no shuffle brings a move back. The offer comes; its green button asks the engine, which refuses: nothing
        /// is paid, the card of the loss for good says why, a life goes and the record ends with the refusal.
        /// </summary>
        private static string ABoardNoShuffleBringsBackIsRefusedAndNothingIsPaid(Bench bench, AttemptsInMemory log)
        {
            LevelDefinition dead = HandMadeBoards.NoShuffleBringsBack();
            Wallet rich = bench.Wallet(coins: 1250);
            using GameSession session = bench.SessionOn(new[] { dead }, rich, log);
            int records = log.Records.Count;
            Require(session.TryPlay(Move.Tap(HandMadeBoards.TheRocket)), "the rocket of the board made by hand was not fired");
            Require(session.Stage.Game.Status == GameStatus.LostOutOfMoves && session.Progress.Wallet.Lives.IsFull, "the board made by hand did not run out of moves, or it cost a life at once");
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Stage.IsOfferingExtraMoves, "the board made by hand was offered no extra moves");

            AtTheOffer.Take(session, ExtraMovesPayment.ByCoins);
            GameStage stage = session.Stage;
            Require(stage.CardTells is LevelResult.Lost { Reason: LossReason.ExtraMovesRefused } refused && ResultWords.Title(refused) == "Oynanacak hamle kalmadı!", $"after the refused moves the card tells {stage.CardTells}");
            Require(session.Progress.Wallet.Coins == rich.Coins && session.Progress.Wallet.Ads.Equals(rich.Ads), "the refused extra moves were paid for");
            Require(session.Progress.Wallet.Lives.LivesUnder(StoryStudio.Economy.Lives) == StoryStudio.Economy.Lives.Max - 1, "the loss after the refused moves cost no life");
            Require(log.Records.Count == records + 1 && log.Records.Last().Actions.Last() is AttemptAction.ExtraMovesRefused, "the refused moves wrote no record, or one that does not end with the refusal");
            return "dead board: the engine refused, nothing paid, the card says Oynanacak hamle kalmadı!, a life and a record";
        }

        /// <summary>
        /// A game three moves short of its win (a rehearsed game of the level, started with fewer moves): the offer
        /// is taken with the coins on the canvas' wallet, the rest of the win is played, and the finale comes with the
        /// coins a win with the moves left over pays on its card.
        /// </summary>
        private static string AWinAfterTheExtraMovesHasItsFinaleAndItsCoins(Bench bench)
        {
            const int ShortBy = 3;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RehearsedGame rehearsed = RehearsedGame.Of(bench.Level(LostLevel), BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy expert) ? expert : throw new InvalidOperationException("The expert bot is missing."));
            int moves = rehearsed.MovesToWin - ShortBy;
            using EffectArt effectArt = EffectArt.Paint();
            Game game = rehearsed.BeforeItsDecidingMoveWith(moves);
            using GameStage stage = FinaleStory.OpenAtRest(game, bench.Art, effectArt, bench.Paintings, Screen, AttemptBrief.FirstTry(StoryStudio.Economy.Win));
            Require(stage.TryPlay(rehearsed.DecidingMoveWith(moves)) && game.Status == GameStatus.LostOutOfMoves, $"the rehearsed game with {moves} moves is {game.Status} after its last move");
            StageSteps.Settle(stage);
            ThroughTheStage.Press(stage, stage.TakeTheMovesForCoinsPixel, "the offer's green button");
            StageSteps.Settle(stage);
            Require(game.Status == GameStatus.Playing && game.MovesLeft == ExtraMovesOffer.Moves, "the canvas' wallet did not buy the extra moves");

            for (int move = moves + 1; game.Status == GameStatus.Playing; move++)
            {
                Require(stage.TryPlay(rehearsed.DecidingMoveWith(move)), $"the rehearsed move {move} was refused after the extra moves");
                if (game.Status == GameStatus.Playing)
                {
                    StageSteps.Settle(stage);
                }
            }

            int movesLeft = game.MovesLeft;
            Require(game.Status == GameStatus.Won && movesLeft == ExtraMovesOffer.Moves - ShortBy, $"after the extra moves the rehearsed game is {game.Status} with {movesLeft} moves left");
            StageSteps.AdvanceToTheFinale(stage, () => stage.Advance(StoryStudio.FrameSeconds), stage.LastTurn?.Seconds ?? 0f);
            Require(stage.IsShowingTheFinale, "a game won after the extra moves had no finale");
            StageSteps.Settle(stage);
            long paid = StoryStudio.Economy.Win.Of(movesLeft).Amount;
            Require(stage.CoinsOnTheCard == paid && stage.LastTurnFaults.Count == 0, $"the card of the win after the extra moves shows {stage.CoinsOnTheCard} coins, not {paid}, or the stage was at fault");
            return $"a win after the extra moves: finale, {paid} coins for {movesLeft} moves left";
        }

        /// <summary>Every record the check's sessions wrote is played again on the engine to the end that was seen.</summary>
        private static string EveryRecordReplaysToTheEndSeen(Bench bench, AttemptsInMemory log)
        {
            foreach (AttemptRecord record in log.Records)
            {
                LevelDefinition level = bench.Level(record.Start.Level.Value);
                Game replayed = AttemptReplay.Of(record, level);
                AttemptOutcome again = AttemptOutcome.Of(replayed, record.Outcome.EndedUtc);
                Require(again.Result == record.Outcome.Result, $"the record of level {record.Start.Level} replays to {again.Result}, and it was {record.Outcome.Result}");
                Require(again.ExtraMovesBought == record.Outcome.ExtraMovesBought && again.MovesUsed == record.Outcome.MovesUsed, $"the record of level {record.Start.Level} replays to {again.MovesUsed} moves and the extra moves {(again.ExtraMovesBought ? "taken" : "not taken")}");
                Require(again.Goals.Select(goal => goal.Done).SequenceEqual(record.Outcome.Goals.Select(goal => goal.Done)), $"the record of level {record.Start.Level} replays to other goals than it ended with");
            }

            return $"{log.Records.Count} records replayed to the end seen";
        }

        /// <exception cref="InvalidOperationException">What the game relies on did not hold; the message says what happened instead.</exception>
        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(char.ToUpperInvariant(otherwise[0]) + otherwise.Substring(1) + ".");
            }
        }

        /// <summary>What the check plays on: the packed levels, art and museum.</summary>
        private sealed class Bench
        {
            /// <summary>A bot does not always run out of moves; after this many attempts it is the wrong bot for the level.</summary>
            private const int MostAttempts = 8;

            private readonly IReadOnlyList<LevelDefinition> _levels = LevelPack.LoadPacked();

            /// <exception cref="InvalidOperationException">The art catalogue is missing.</exception>
            public Bench()
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                Art = art != null ? art : throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
            }

            public ArtCatalogue Art { get; }

            public PaintingGallery Paintings { get; } = PaintingGallery.OfThePackedMuseum();

            /// <summary>A level of the pack, or the board made by hand that no shuffle brings back.</summary>
            public LevelDefinition Level(int number) => number == HandMadeBoards.NoShuffleBringsBackNumber
                ? HandMadeBoards.NoShuffleBringsBack()
                : LevelPack.Numbered(_levels, number);

            /// <summary>A new player's wallet with so many coins and every life.</summary>
            public Wallet Wallet(long coins) => StoryStudio.Economy.StartingWallet.WithCoins(new Coins(coins));

            /// <summary>
            /// The game's own session at the board of the lost level, played by the random bot attempt after attempt
            /// until its moves run out; left with the offer at rest. What the progress was at the move that ran out of
            /// moves, before anything of it was shown, comes back too.
            /// </summary>
            /// <exception cref="InvalidOperationException">The bot never ran out of moves.</exception>
            public GameSession SessionOutOfMoves(Wallet wallet, IAttemptLog log, out PlayerProgress atTheLosingMove)
            {
                var rng = new Rng(20261015, 3);
                for (int attempt = 0; attempt < MostAttempts; attempt++)
                {
                    GameSession session = Session(_levels, StandingOn(_levels, LostLevel, wallet, attempt), log);
                    GameStatus ending = AtTheOffer.PlayOn(session, new RandomPolicy(), rng);
                    if (ending == GameStatus.LostOutOfMoves)
                    {
                        atTheLosingMove = session.Progress;
                        StoryStudio.AdvanceUntilSettled(session);
                        return session;
                    }

                    session.Dispose();
                }

                throw new InvalidOperationException($"The random bot did not run out of moves on level {LostLevel} in {MostAttempts} attempts.");
            }

            /// <summary>The game's own session at the board of the first of some levels, for a player standing on it with a wallet; at rest.</summary>
            public GameSession SessionOn(IReadOnlyList<LevelDefinition> levels, Wallet wallet, IAttemptLog log) =>
                Session(levels, StandingOn(levels, levels[0].Id.Value, wallet, attemptsOnLevel: 0), log);

            private GameSession Session(IReadOnlyList<LevelDefinition> levels, PlayerProgress standing, IAttemptLog log)
            {
                var session = new GameSession(levels, Art, Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), log, Screen, SessionOpening.AtTheBoard);
                StoryStudio.AdvanceUntilSettled(session);
                return session;
            }

            private static PlayerProgress StandingOn(IReadOnlyList<LevelDefinition> levels, int levelNumber, Wallet wallet, int attemptsOnLevel)
            {
                PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(levels, levelNumber), levels);
                return new PlayerProgress(standing.LevelsWon, attemptsOnLevel, standing.Museum.Placed, wallet);
            }
        }
    }

    /// <summary>The steps of the offer of extra moves, as a player's touches make them, for the Editor's checks and walks.</summary>
    internal static class AtTheOffer
    {
        /// <summary>
        /// Plays the open game with a bot until it is decided or its moves run out, every turn but the last to its
        /// rest; the session's clock is left at the last move, before anything of it is shown.
        /// </summary>
        /// <returns>How the game ended: won, out of moves (with the offer still to come where it is made), or with no move possible.</returns>
        public static GameStatus PlayOn(GameSession session, IBotPolicy player, Rng rng)
        {
            FlowStory.PlayTheGame(session, player, rng);
            return session.Stage.Game.Status;
        }

        /// <summary>"Vazgeç" on the offer at rest: the offer goes up and the card of the loss for good comes down; the session is left with it at rest.</summary>
        /// <exception cref="InvalidOperationException">No offer hangs at rest, or no card of a loss came in its place.</exception>
        public static void GiveUp(GameSession session)
        {
            StoryStudio.AdvanceUntilSettled(session);
            ThroughTheCard.Press(session, session.Stage.GiveUpPixel, "the offer's Vazgeç");
            StoryStudio.AdvanceUntilSettled(session);
            ThroughTheCard.Require(session.Stage.CardTells is LevelResult.Lost && session.Stage.ResultButtonPixel != null, "Vazgeç did not bring the card of the loss for good down in the offer's place");
        }

        /// <summary>
        /// One of the offer's buttons that pay: the session is left at rest, with the game going on (paid by an ad,
        /// once the ad is over) or, where the engine refused the moves, with the card of the loss for good.
        /// </summary>
        /// <exception cref="InvalidOperationException">No offer hangs at rest.</exception>
        public static void Take(GameSession session, ExtraMovesPayment paidBy)
        {
            StoryStudio.AdvanceUntilSettled(session);
            Vector2? button = paidBy == ExtraMovesPayment.ByCoins ? session.Stage.TakeTheMovesForCoinsPixel : session.Stage.TakeTheMovesForAnAdPixel;
            ThroughTheCard.Press(session, button, paidBy == ExtraMovesPayment.ByCoins ? "the offer's +5 hamle" : "the offer's Reklam izle");
            StoryStudio.AdvanceUntilSettled(session);
        }
    }

    /// <summary>Boards made by hand for the Editor's checks, which no level of the pack has.</summary>
    internal static class HandMadeBoards
    {
        /// <summary>A number no level of the pack has: the number of the board that no shuffle brings back.</summary>
        public const int NoShuffleBringsBackNumber = 9001;

        /// <summary>The rocket in the middle of that board.</summary>
        public static Position TheRocket { get; } = new Position(1, 1);

        /// <summary>
        /// Boxes all around one rocket, and one move: once the rocket has fired there is nothing to move and no gem a
        /// shuffle could lay into a move, so the extra moves are refused.
        /// </summary>
        public static LevelDefinition NoShuffleBringsBack()
        {
            LevelLayout layout = LevelLayout.Parse(new LayoutLayers { Cells = new[] { "###", "###", "###" }, Tiles = new[] { "333", "3H3", "333" } });
            return new LevelDefinition(
                new LevelId(NoShuffleBringsBackNumber), DifficultyTier.Normal, layout, new GoalSpec[] { new GoalSpec.BreakBoxes(8) }, moveLimit: 1,
                new SpawnRules { Palette = GemColors.First(5), Items = ItemSpawnRules.None });
        }
    }

    /// <summary>A finger's press on a stage that no session holds, as the stage studies make it.</summary>
    internal static class ThroughTheStage
    {
        /// <exception cref="InvalidOperationException">The stage shows no such control to press.</exception>
        public static void Press(GameStage stage, Vector2? pixel, string what)
        {
            stage.PointerDown(pixel ?? throw new InvalidOperationException($"The stage does not show {what}, which was to be pressed."));
            stage.PointerUp();
        }
    }
}
