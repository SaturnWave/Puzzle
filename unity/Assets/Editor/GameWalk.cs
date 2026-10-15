#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// A walk through the game for <see cref="ScreenShots"/>, on the game's own session and by touches alone, with
    /// a picture at every stop: the hall, where the day's gift is taken if it waits, the shop, the collection and the
    /// map (a page turning is pictured too), the play
    /// button and the level's card coming down, a start booster chosen on it and the card closed (which spends
    /// nothing), the card again, the booster chosen and "Oyna", the board with the booster's special, the round
    /// button that asks whether to leave (answered first with "no" by the device's way back, then with "yes", which
    /// leads to the hall and spends nothing), through the card into the board again, a whole level played by the
    /// expert bot, after whose first move the booster has left the shelf, its card; from a won level's card the blue
    /// button to the painting's jigsaw, where the piece just won is carried to its place by a finger and what else
    /// waits is placed by the blue button, then back to the card and by its "Devam" to the hall (through the
    /// restored room's celebration when the jigsaw completed a room), to the collection once more, and from there
    /// to the hall by the device's way back. The pictures are named <c>walk-&lt;state&gt;-&lt;size&gt;-NN-&lt;what&gt;.png</c>,
    /// with a sheet of all of them. A second, short walk follows the same player without a life: the play button
    /// opens the lives card and no board, its "Tamam" closes it, the heart's pill opens it again and the device's
    /// way back closes it (<c>walk-nolife-...</c>). Once on each screen, a level is lost and tried again: its card's
    /// "Tekrar dene" leads to the hall with the level's card coming down, and through it to the same level's next
    /// board; with the last life lost instead, the lives card comes down and no board opens
    /// (<c>walk-retry-...</c>, <c>walk-lastlife-...</c>); and a start booster held none of is bought on the card
    /// that offers it, chosen and taken into the level, while with too few coins nothing is bought
    /// (<c>walk-buy-...</c>); and the walks of the shop, the gift and a painting's reward (GameWalk.Shop.cs), and of
    /// the settings, the privacy card's deleted records and the support card (GameWalk.Settings.cs). The walk
    /// fails when a touch does not lead where it should, or when what was placed, spent, bought or given is not in the
    /// player's progress.
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>The bot's choices are random where several moves are equally good; the walk always makes the same ones.</summary>
        private const ulong BotSeed = 20261007;
        private const ulong BotStream = 13;

        /// <summary>The moments of a page turn that are pictured, in seconds from the press.</summary>
        private static readonly float[] TurnMoments = { 0.1f, 0.2f };

        /// <summary>A held button is pictured this long after the finger came down; a room that goes dark or light, this long into it.</summary>
        private const float PressedMoment = 2f * StoryStudio.FrameSeconds;
        private const float DarkMoment = 0.1f;

        /// <summary>A piece that dropped in is pictured this long after; flying pieces this long after the button; a celebration this far into it.</summary>
        private const float DroppedMoment = 0.05f;
        private const float FlyingMoment = 0.32f;
        private const float CelebrationMoment = 0.28f;

        /// <summary>A restored room is pictured this long after the hall opened on it, while its light runs, and this much later, at rest.</summary>
        private const float RestoredRoomMoment = 0.85f;
        private const float RestoredRoomRests = 1.6f;

        /// <summary>No longer than this passes between a press that leaves a stage and the next stage.</summary>
        private const float LongestLeave = 2f;

        /// <summary>The walk of a player without a life has the next one back this long after the tools' instant.</summary>
        private static readonly TimeSpan NextLifeWithoutALife = new TimeSpan(0, 12, 34);

        /// <summary>The level the retries are walked on: one the random bot loses (the attempt log check's).</summary>
        private const int RetriedLevel = 40;

        public static void Run(PlayerState state, ScreenShape screen, Studio studio)
        {
            if (!state.Wallet.HasALife)
            {
                // A player without a life has no level to walk through: only the walk that shows them the lives card.
                WalkWithoutALife(state, screen, studio);
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-{state.Label}-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            int levelToPlay = studio.Levels[state.Progress.LevelIndexIn(studio.Levels.Count)].Id.Value;

            reel.Settle();
            reel.Shoot("home");
            TakeTheGift(reel);
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            reel.Shoot("shop");
            TurnThePage(reel, Destination.Collection, picturesTheTurn: false);
            reel.Shoot("collection");
            TurnThePage(reel, Destination.Map, picturesTheTurn: true);
            reel.Shoot("map");
            TurnThePage(reel, Destination.Home, picturesTheTurn: false);

            BoosterKind[] taken = TheFirstStartBoosterHeld(state.Progress.Wallet.Shelf);
            CloseTheCardWithABoosterChosen(reel, taken);
            OpenTheLevel(reel, levelToPlay, taken, pictures: true);
            LeaveTheLevelAndComeBack(reel, levelToPlay, taken);
            BoosterShelf beforeTheLevel = session.Progress.Wallet.Shelf;
            Coins coinsBeforeTheLevel = session.Progress.Wallet.Coins;
            BoosterKind[] usedOnTheShelf = UseTheSpatulaWhereItIsHeld(reel);
            GameStatus ending = PlayTheLevel(reel);
            RequireSpent(session, beforeTheLevel, taken.Concat(usedOnTheShelf).ToArray(), "the level's first move and the shelf's spatula");
            RequireTheCoinsOnTheCardPaid(session, coinsBeforeTheLevel, ending);
            bool hadPiecesToPlace = session.Stage.PlaceButtonPixel != null;
            string atTheJigsaw = hadPiecesToPlace ? PlaceWhatWasWon(reel) : "no piece to place";
            if (session.Museum == null)
            {
                GoOnFromTheCard(reel, ending);
            }

            reel.Settle();
            reel.Shoot("home-again");
            if (MuseumOf(session).Showing != Destination.Home || MuseumOf(session).ShowsTheStartCard)
            {
                throw new InvalidOperationException($"From a result's card the museum opened on {MuseumOf(session).Showing}, or with a card hanging, not on its bare hall.");
            }

            // The row counts on its own clock, which no settling of the hall waits for: it has counted once its time is up.
            reel.Advance(Tempo.CoinsCount);
            ThroughTheCard.Require(
                MuseumOf(session).CoinsShown == session.Progress.Wallet.Coins,
                $"back in the hall the wallet row shows {MuseumOf(session).CoinsShown} coins once it has counted, and the player has {session.Progress.Wallet.Coins}");

            TurnThePage(reel, Destination.Collection, picturesTheTurn: false);
            reel.Shoot("collection-again");
            session.GoBack();
            reel.Settle();
            if (MuseumOf(session).Showing != Destination.Home)
            {
                throw new InvalidOperationException($"The device's way back left the museum on {MuseumOf(session).Showing}, not on its home.");
            }

            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: hall (the day's gift taken where it waited), shop, collection, map, the level's card closed, the card, level {levelToPlay} with [{string.Join(", ", taken.Select(BoosterKinds.Name))}] ({ending}), {atTheJigsaw}, hall, collection; the player now stands at \"{session.Progress}\".");
            WalkWithoutALife(state, screen, studio);
        }

        /// <summary>
        /// The walks of the level's card, by a new player who stands on a level: once with lives left, a lost card's
        /// "Tekrar dene" brings the hall with the level's card down, and through it the level's next board; once with
        /// the last life lost, the lives card comes down instead and no board opens; and a start booster held none
        /// of is bought on the card that offers it, or, with too few coins, is not.
        /// </summary>
        public static void RunTheCardWalks(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            WalkARetry(standing, screen, studio);
            WalkTheLastLifeLost(standing, screen, studio);
            WalkABuy(standing, screen, studio);
            RunTheShopWalks(screen, studio);
            RunTheSettingsWalk(screen, studio);
        }

        /// <summary>
        /// A player with none of the extra moves and coins enough for one: the plus on its disc brings the card that
        /// offers it down in front of the level's card; its round button closes it, and nothing is bought; opened
        /// again, its "Satın al" buys one for its price, the row counts the coins down and the disc counts the
        /// booster, which is chosen and taken into the level. With one coin too few the button is dimmed and a
        /// press on it buys nothing.
        /// </summary>
        private static void WalkABuy(PlayerProgress standing, ScreenShape screen, Studio studio)
        {
            const BoosterKind offered = BoosterKind.StartExtraMoves;
            Coins price = StoryStudio.Economy.Shop.PriceOf(offered);
            BoosterShelf noneOffered = Enumerable.Range(0, standing.Wallet.Shelf.Count(offered)).Aggregate(standing.Wallet.Shelf, (shelf, _) => shelf.WithOneFewer(offered));
            PlayerProgress before = standing.WithWallet(standing.Wallet.WithShelf(noneOffered).WithCoins(price + new Coins(1050)));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using (var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(before), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum))
            {
                var reel = new Reel(session, screen, $"walk-buy-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                ThroughTheCard.BringDownTheCard(session);
                OpenTheOffer(reel, offered);
                reel.Shoot("offer");
                Press(session, MuseumOf(session).GetMoreClosePixel, "the offer's round button", reel, pressedShot: null);
                reel.Settle();
                ThroughTheCard.Require(
                    MuseumOf(session).OffersABooster == null && MuseumOf(session).StartCardPlayPixel != null && session.Progress.Equals(before),
                    "the offer's round button did not give the level's card back, or closing it bought something");

                OpenTheOffer(reel, offered);
                Press(session, MuseumOf(session).GetMoreBuyPixel, "the offer's Satın al", reel, "buy-pressed");
                reel.Advance(Tempo.CoinsCount / 3f);
                reel.Shoot("coins-count-down");
                reel.Settle();
                reel.Shoot("bought");
                ThroughTheCard.Require(
                    session.Progress.Wallet.Coins == before.Wallet.Coins.Less(price) && session.Progress.Wallet.Shelf.Equals(noneOffered.WithOneMore(offered)),
                    $"Satın al left the wallet at {session.Progress.Wallet.Coins} coins and {session.Progress.Wallet.Shelf}, not one {offered.Name()} for {price}");
                ThroughTheCard.Require(MuseumOf(session).OffersABooster == null && MuseumOf(session).StartCardPlayPixel != null, "after Satın al the level's card is not the card in front");

                ThroughTheCard.Choose(session, offered);
                reel.Shoot("bought-and-chosen");
                ThroughTheCard.PressPlay(session);
                reel.Settle();
                reel.Shoot("board");
                LevelDefinition level = session.Stage.Game.Level;
                ThroughTheCard.Require(
                    session.Stage.Game.MovesLeft == level.MoveLimit + LevelStart.Rules.StartBoosterExtraMoves,
                    $"the board taken into with the extra moves just bought has {session.Stage.Game.MovesLeft} moves, not {level.MoveLimit} and the booster's");
                reel.WriteSheet();
            }

            PlayerProgress oneCoinShort = before.WithWallet(before.Wallet.WithCoins(price.Less(new Coins(1))));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using (var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(oneCoinShort), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum))
            {
                var reel = new Reel(session, screen, $"walk-buy-short-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
                reel.Settle();
                ThroughTheCard.BringDownTheCard(session);
                OpenTheOffer(reel, offered);
                Press(session, MuseumOf(session).GetMoreBuyPixel, "the offer's dimmed button", reel, pressedShot: null);
                reel.Settle();
                reel.Shoot("too-few-coins");
                ThroughTheCard.Require(
                    MuseumOf(session).OffersABooster == offered && session.Progress.Equals(oneCoinShort),
                    "a press on the dimmed button of an offer the coins do not cover bought something, or closed the offer");
                reel.WriteSheet();
            }

            Debug.Log($"Screen shots: walk walk-buy-{screen}: the offer of {offered.Name()} closed, bought for {price} and taken into the level; with one coin too few nothing was bought.");
        }

        /// <summary>The plus on a start booster's disc on the level's card: the card that offers the booster comes down in front.</summary>
        private static void OpenTheOffer(Reel reel, BoosterKind offered)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).StartCardDiscPixel(offered), $"the plus of {offered.Name()}", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).OffersABooster == offered && MuseumOf(session).GetMoreBuyPixel != null, $"the plus of {offered.Name()} brought no card that offers it");
        }

        /// <summary>
        /// Through the card with the colour bomb chosen, a loss (where the moves ran out, the offer's ad buys five more,
        /// which run out too), "Tekrar dene", the hall with the same level's card down (the colour bomb one fewer),
        /// "Oyna" with nothing chosen, the level's second board; played to its end, the pause disc under its card
        /// listens to nothing, the device's way back under an offer of extra moves does nothing and the offer is
        /// given up, and the way back under the card of the loss leads to the bare hall.
        /// </summary>
        private static void WalkARetry(PlayerProgress standing, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-retry-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            BoosterShelf held = standing.Wallet.Shelf;

            reel.Settle();
            ThroughTheCard.BringDownTheCard(session);
            ThroughTheCard.Choose(session, BoosterKind.StartColorBomb);
            reel.Shoot("card-colour-bomb");
            ThroughTheCard.PressPlay(session);
            reel.Settle();
            reel.Shoot("board-colour-bomb");

            FlowStory.PlayToTheEnding(session, FlowEnding.Lost, new RandomPolicy());
            reel.Settle();
            if (session.Stage.IsOfferingExtraMoves)
            {
                reel.Shoot("offer");
                TakeTheMovesForAnAdAndRunOutAgain(reel);
            }

            reel.Shoot("card-lost");
            int lostLevel = session.Stage.Game.Level.Id.Value;
            Press(session, session.Stage.ResultButtonPixel, "the lost card's Tekrar dene", reel, "retry-pressed");
            WaitFor(session, () => session.Museum != null, "the lost card's Tekrar dene opened no museum");
            reel.Advance(DarkMoment);
            reel.Shoot("hall-with-the-card-comes");
            reel.Settle();
            reel.Shoot("hall-with-the-card");
            MuseumStage museum = MuseumOf(session);
            ThroughTheCard.Require(museum.ShowsTheStartCard && museum.StartCardPlayPixel != null, "the lost card's Tekrar dene did not bring the level's card down over the hall");
            ThroughTheCard.Require(museum.StartChoice.Count == 0, $"the level's card came down with {museum.StartChoice} chosen; nothing is chosen for the player");
            ThroughTheCard.Require(
                session.Progress.Wallet.Shelf.Count(BoosterKind.StartColorBomb) <= held.Count(BoosterKind.StartColorBomb) - 1,
                "the colour bomb taken into the lost attempt was not spent");

            ThroughTheCard.PressPlay(session);
            reel.Settle();
            reel.Shoot("board-again");
            ThroughTheCard.Require(session.Stage.Game.Level.Id.Value == lostLevel, $"the level's card led to level {session.Stage.Game.Level.Id.Value}, not to level {lostLevel}, which was lost");
            ThroughTheCard.Require(session.Stage.WordsOnTheShelf != AttemptWords.HowToPlay, "the board of a retry says how to play, as a first try does");

            FlowStory.PlayTheGame(session, new RandomPolicy(), new Rng(BotSeed, BotStream));
            reel.Settle();
            ThroughTheCard.Require(session.Stage.PauseButtonPixel == null, "the pause disc listens under a result's card; the card has its own ways out");
            if (session.Stage.IsOfferingExtraMoves)
            {
                session.GoBack();
                reel.Settle();
                ThroughTheCard.Require(session.Board != null && session.Stage.IsOfferingExtraMoves, "the device's way back under the offer of extra moves took the offer away");
                AtTheOffer.GiveUp(session);
            }

            session.GoBack();
            WaitFor(session, () => session.Museum != null, "the device's way back under the card opened no museum");
            reel.Settle();
            reel.Shoot("hall");
            ThroughTheCard.Require(!MuseumOf(session).ShowsTheStartCard && MuseumOf(session).PlayButtonPixel != null, "the device's way back under a card did not lead to the bare hall");
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: level {lostLevel} lost with the colour bomb, Tekrar dene, the hall with its card, its second board, the hall; the player now stands at \"{session.Progress}\".");
        }

        /// <summary>The same level with one life: lost (where the moves ran out, the offer is given up, which takes the life), its card's "Tekrar dene" brings the lives card down, and no level's card and no board.</summary>
        private static void WalkTheLastLifeLost(PlayerProgress standing, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LifeRules lives = StoryStudio.Economy.Lives;
            PlayerProgress oneLife = standing.WithWallet(standing.Wallet.WithLives(new LifeMeter(lives.Max - 1, StoryStudio.Clock.Now + NextLifeWithoutALife)));
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(oneLife), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-lastlife-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);

            reel.Settle();
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            FlowStory.PlayToTheEnding(session, FlowEnding.Lost, new RandomPolicy());
            reel.Settle();
            if (session.Stage.IsOfferingExtraMoves)
            {
                reel.Shoot("offer");
                ThroughTheCard.Require(session.Progress.Wallet.Lives.SettledAt(StoryStudio.Clock.Now, lives).LivesUnder(lives) == 1, "the last life went before the offer of extra moves was answered");
                AtTheOffer.GiveUp(session);
            }

            reel.Shoot("card-lost");
            Press(session, session.Stage.ResultButtonPixel, "the lost card's Tekrar dene", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the lost card's Tekrar dene opened no museum");
            reel.Settle();
            reel.Shoot("lives-card");
            MuseumStage museum = MuseumOf(session);
            ThroughTheCard.Require(
                museum.ShowsTheLivesCard && !museum.ShowsTheStartCard && museum.LivesCardDonePixel != null && !museum.Wallet.HasALife,
                "with the last life lost, the lost card's Tekrar dene did not bring the lives card down instead of the level's card");

            Press(session, museum.LivesCardDonePixel, "the lives card's Tamam", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).PlayButtonPixel, "the hall's play button", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).ShowsTheLivesCard && !MuseumOf(session).ShowsTheStartCard && session.Board == null, "without a life the play button brought the level's card or a board");
            reel.Shoot("lives-card-again");
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: the last life lost, Tekrar dene and the play button brought the lives card and no board.");
        }

        /// <summary>The first start booster the player holds, to be taken into the walk's level; none for a player who holds none.</summary>
        private static BoosterKind[] TheFirstStartBoosterHeld(BoosterShelf shelf) => LevelBrief.StartBoosters.Where(shelf.Has).Take(1).ToArray();

        /// <summary>A won level's card, at rest, shows the coins the attempt's ledger paid for the win: what the player has more than before the level.</summary>
        /// <exception cref="InvalidOperationException">The card shows other coins, or a lost level's card shows any.</exception>
        private static void RequireTheCoinsOnTheCardPaid(GameSession session, Coins before, GameStatus ending)
        {
            long paid = session.Progress.Wallet.Coins.Amount - before.Amount;
            long? shown = session.Stage.CoinsOnTheCard;
            bool isRight = ending == GameStatus.Won ? shown == paid : shown == null && paid == 0;
            ThroughTheCard.Require(isRight, $"the card of a level {ending} shows {(shown is long coins ? coins.ToString(CultureInfo.InvariantCulture) : "no")} coins; the level paid {paid}");
        }

        /// <exception cref="InvalidOperationException">The boosters taken into the level did not leave the shelf, or more did.</exception>
        private static void RequireSpent(GameSession session, BoosterShelf before, BoosterKind[] taken, string when)
        {
            BoosterShelf expected = before;
            foreach (BoosterKind booster in taken)
            {
                expected = expected.WithOneFewer(booster);
            }

            ThroughTheCard.Require(session.Progress.Wallet.Shelf.Equals(expected), $"after {when} the shelf holds {session.Progress.Wallet.Shelf}, not {expected}");
        }

        /// <summary>
        /// The card's green button: a won level's "Devam" leads to the bare hall, a lost one's "Tekrar dene" to the
        /// hall with the level's card coming down, which is closed again for the rest of the walk.
        /// </summary>
        private static void GoOnFromTheCard(Reel reel, GameStatus ending)
        {
            GameSession session = reel.Session;
            bool isWon = ending == GameStatus.Won;
            Press(session, session.Stage.ResultButtonPixel, isWon ? "the won card's Devam" : "the lost card's Tekrar dene", reel, "go-on-pressed");
            WaitFor(session, () => session.Museum != null, "the card's green button opened no museum");
            reel.Advance(DarkMoment);
            reel.Shoot("hall-comes");
            if (isWon && MuseumOf(session).CoinsShown >= session.Progress.Wallet.Coins)
            {
                throw new InvalidOperationException($"The hall opened after a win with its wallet row at {MuseumOf(session).CoinsShown} coins of {session.Progress.Wallet.Coins}: the coins the win paid do not count up.");
            }
            reel.Settle();
            if (MuseumOf(session).ShowsTheStartCard == isWon)
            {
                throw new InvalidOperationException(isWon
                    ? "A won card's Devam brought the level's card down; it leads to the bare hall."
                    : "A lost card's Tekrar dene did not bring the level's card down over the hall.");
            }

            if (!isWon)
            {
                reel.Shoot("start-card-again");
                Press(session, MuseumOf(session).StartCardClosePixel, "the level's card's round button", reel, pressedShot: null);
                reel.Settle();
            }
        }

        /// <summary>
        /// The play button brings the level's card down; a start booster chosen on it is put back by a second press
        /// and chosen again, and the card's round button closes it: the player's progress is as it was, nothing spent.
        /// </summary>
        private static void CloseTheCardWithABoosterChosen(Reel reel, BoosterKind[] taken)
        {
            GameSession session = reel.Session;
            PlayerProgress before = session.Progress;
            Press(session, MuseumOf(session).PlayButtonPixel, "the play button", reel, "play-pressed");
            reel.Advance(Tempo.CardAfterDark + Tempo.CardComesDown / 2f);
            reel.Shoot("start-card-comes");
            ThroughTheCard.BringDownTheCard(session);
            reel.Shoot("start-card");
            foreach (BoosterKind booster in taken)
            {
                ThroughTheCard.Choose(session, booster);
                reel.Shoot("start-card-chosen");
                Press(session, MuseumOf(session).StartCardDiscPixel(booster), $"the disc of {booster.Name()} once more", reel, pressedShot: null);
                reel.Settle();
                ThroughTheCard.Require(!MuseumOf(session).StartChoice.Has(booster), $"a second press on the disc of {booster.Name()} did not put it back");
                ThroughTheCard.Choose(session, booster);
            }

            Press(session, MuseumOf(session).StartCardClosePixel, "the level's card's round button", reel, pressedShot: null);
            reel.Settle();
            reel.Shoot("start-card-closed");
            if (MuseumOf(session).ShowsTheStartCard || MuseumOf(session).PlayButtonPixel == null || !session.Progress.Equals(before))
            {
                throw new InvalidOperationException($"The level's card's round button did not give the hall back, or closing it changed the progress to \"{session.Progress}\".");
            }
        }

        /// <summary>
        /// The same player with every life lost, the next back in 12:34: the play button opens the lives card and no
        /// board; its "Tamam" closes it; the heart's pill opens it again, and the device's way back closes it.
        /// </summary>
        private static void WalkWithoutALife(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LifeRules lives = StoryStudio.Economy.Lives;
            var none = new LifeMeter(lives.Max, StoryStudio.Clock.Now + NextLifeWithoutALife);
            PlayerProgress progress = state.Progress.WithWallet(state.Progress.Wallet.WithLives(none));
            using var session = new GameSession(studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-nolife-{state.Label}-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);

            reel.Settle();
            reel.Shoot("home");
            Press(session, MuseumOf(session).PlayButtonPixel, "the play button", reel, pressedShot: null);
            reel.Settle();
            if (session.Board != null || !MuseumOf(session).ShowsTheLivesCard)
            {
                throw new InvalidOperationException("Without a life the play button did not open the lives card, or opened a board.");
            }

            reel.Shoot("lives-card");
            Press(session, MuseumOf(session).LivesCardDonePixel, "the lives card's Tamam", reel, pressedShot: null);
            reel.Settle();
            if (MuseumOf(session).ShowsTheLivesCard || MuseumOf(session).PlayButtonPixel == null)
            {
                throw new InvalidOperationException("The lives card's Tamam did not close it and give the hall back.");
            }

            Press(session, MuseumOf(session).LivesPixel, "the heart's pill", reel, "heart-pressed");
            reel.Settle();
            if (!MuseumOf(session).ShowsTheLivesCard)
            {
                throw new InvalidOperationException("The heart's pill did not open the lives card.");
            }

            session.GoBack();
            reel.Settle();
            if (MuseumOf(session).ShowsTheLivesCard || session.Board != null || !session.Progress.Equals(progress))
            {
                throw new InvalidOperationException($"The device's way back did not close the lives card, or the walk changed the progress to \"{session.Progress}\".");
            }

            reel.Shoot("home-again");
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: without a life the play button opened the lives card and no board; Tamam, the heart's pill and the way back led where they should.");
        }

        /// <summary>From the hall into the level the player stands on: the play button, the level's card, the boosters taken, its "Oyna".</summary>
        private static void OpenTheLevel(Reel reel, int levelToPlay, BoosterKind[] taken, bool pictures)
        {
            GameSession session = reel.Session;
            ThroughTheCard.BringDownTheCard(session);
            foreach (BoosterKind booster in taken)
            {
                ThroughTheCard.Choose(session, booster);
            }

            if (pictures)
            {
                reel.Shoot("start-card-to-play");
            }

            Press(session, MuseumOf(session).StartCardPlayPixel, "the card's Oyna", reel, pictures ? "oyna-pressed" : null);
            WaitFor(session, () => session.Board != null, "the card's Oyna opened no board");
            if (session.Stage.Game.Level.Id.Value != levelToPlay)
            {
                throw new InvalidOperationException($"The card's Oyna opened level {session.Stage.Game.Level.Id.Value}, not level {levelToPlay}, where the player stands.");
            }

            string[] took = session.Stage.Game.BoostersUsed.ToArray();
            if (!took.SequenceEqual(taken.Select(BoosterKinds.Name)))
            {
                throw new InvalidOperationException($"The board took [{string.Join(", ", took)}], not the boosters chosen on its card, [{string.Join(", ", taken.Select(BoosterKinds.Name))}].");
            }

            if (pictures)
            {
                reel.Advance(DarkMoment);
                reel.Shoot("board-comes");
            }

            reel.Settle();
        }

        /// <summary>
        /// The shelf's pause disc asks whether to leave; the device's way back says no and the game goes on; asked
        /// again, the card's teal button leads to the hall, with the player's progress as it was (no move was
        /// played, so the attempt does not count and the boosters taken into it are not spent); the play button and
        /// the level's card then open the same level again.
        /// </summary>
        private static void LeaveTheLevelAndComeBack(Reel reel, int levelToPlay, BoosterKind[] taken)
        {
            GameSession session = reel.Session;
            PlayerProgress before = session.Progress;
            reel.Shoot("board");
            Press(session, session.Stage.PauseButtonPixel, "the shelf's pause disc", reel, "pause-pressed");
            reel.Settle();
            reel.Shoot("leave-asked");
            if (!session.Stage.IsAskingWhetherToLeave)
            {
                throw new InvalidOperationException("The shelf's pause disc did not ask whether to leave the level.");
            }

            session.GoBack();
            reel.Settle();
            if (session.Board == null || session.Stage.IsAskingWhetherToLeave || session.Stage.PauseButtonPixel == null)
            {
                throw new InvalidOperationException("The device's way back did not take the leaving card away and give the board back.");
            }

            session.GoBack();
            reel.Settle();
            Press(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the leaving card's museum button opened no museum");
            reel.Settle();
            reel.Shoot("left-for-the-hall");
            if (!session.Progress.Equals(before))
            {
                throw new InvalidOperationException($"Leaving a level in which no move was played changed the progress from \"{before}\" to \"{session.Progress}\".");
            }

            OpenTheLevel(reel, levelToPlay, taken, pictures: false);
        }

        /// <summary>Through the open level to its card.</summary>
        /// <summary>
        /// Where the spatula is open and held, it is used by touches before any move: its disc, then a gem in the
        /// board's lower half. It is the attempt's first move then, and is spent with the start boosters taken.
        /// </summary>
        /// <returns>The spatula when it was used; none where the level has it locked or the player holds none.</returns>
        private static BoosterKind[] UseTheSpatulaWhereItIsHeld(Reel reel)
        {
            GameSession session = reel.Session;
            reel.Settle();
            if (!(session.Stage.BoosterDiscPixel(BoosterKind.HitCell) is Vector2 disc) || !session.Progress.Wallet.Shelf.Has(BoosterKind.HitCell))
            {
                return Array.Empty<BoosterKind>();
            }

            Press(session, disc, "the spatula's disc", reel, pressedShot: null);
            reel.Shoot("spatula-armed");
            Game game = session.Stage.Game;
            Position target = Enumerable.Range(0, game.Board.Shape.Height).Reverse().SelectMany(y => Enumerable.Range(0, game.Board.Shape.Width).Select(x => new Position(x, y)))
                .First(cell => game.Board.Shape.IsPlayable(cell) && game.Board.TileAt(cell).IsGem);
            Press(session, session.Stage.PixelOf(target), "a gem for the spatula", reel, pressedShot: null);
            reel.Advance(Tempo.SpatulaStrikes);
            reel.Shoot("spatula-strikes");
            reel.Settle();
            ThroughTheCard.Require(game.BoostersUsed.Contains(BoosterNames.Of(new BoosterUse.HitCell(target))) && game.MovesUsed == 0, "the spatula used on the shelf did not strike before any move");
            return new[] { BoosterKind.HitCell };
        }

        /// <summary>The level played to its end by the strong bot; where its moves run out, the offer of five more is turned down.</summary>
        private static GameStatus PlayTheLevel(Reel reel)
        {
            GameSession session = reel.Session;
            FlowStory.PlayTheGame(session, Expert(), new Rng(BotSeed, BotStream));
            GameStatus ending = session.Stage.Game.Status;
            reel.Settle();
            if (session.Stage.IsOfferingExtraMoves)
            {
                reel.Shoot("offer");
                AtTheOffer.GiveUp(session);
            }

            reel.Shoot($"card-{ending.ToString().ToLowerInvariant()}");
            return ending;
        }

        /// <summary>
        /// The offer's blue button: the card that stands in for an ad comes down, its bar fills, and when it is over
        /// the game goes on with five more moves; the random bot plays them until the moves run out once more, and the
        /// card of the loss for good comes, with no offer.
        /// </summary>
        /// <exception cref="InvalidOperationException">The ad did not come, run or end as it should, or the game was won after all.</exception>
        private static void TakeTheMovesForAnAdAndRunOutAgain(Reel reel)
        {
            GameSession session = reel.Session;
            GameStage stage = session.Stage;
            Press(session, stage.TakeTheMovesForAnAdPixel, "the offer's Reklam izle", reel, "ad-pressed");
            WaitFor(session, () => stage.AdProgress > 0f, "the card that stands in for an ad did not come down and begin");
            reel.Shoot("ad-begins");
            for (float waited = 0f; stage.AdProgress < 1f; waited += StoryStudio.FrameSeconds)
            {
                ThroughTheCard.Require(waited <= 2f * SimulatedAd.Seconds, $"the ad is not over {2f * SimulatedAd.Seconds} s after it began");
                session.Advance(StoryStudio.FrameSeconds);
            }

            reel.Shoot("ad-ends");
            reel.Settle();
            reel.Shoot("moves-taken");
            ThroughTheCard.Require(stage.Game.Status == GameStatus.Playing && stage.Plaques.MovesLeft == ExtraMovesOffer.Moves, "after the ad the game does not go on with five more moves on the plaque");

            FlowStory.PlayTheGame(session, new RandomPolicy(), new Rng(BotSeed, BotStream));
            reel.Settle();
            ThroughTheCard.Require(stage.Game.Status.IsLoss() && !stage.IsOfferingExtraMoves && stage.ResultButtonPixel != null, $"after the extra moves the game is {stage.Game.Status}, or the offer came a second time");
        }

        /// <summary>
        /// From a won level's card to the jigsaw and back: the first piece of the tray is carried to its place by a
        /// finger, the blue button places what else waits, and the round button leads back to the card, which then
        /// has its button to the museum.
        /// </summary>
        /// <returns>What happened at the jigsaw, for the walk's line in the log.</returns>
        private static string PlaceWhatWasWon(Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, session.Stage.PlaceButtonPixel, "the card's jigsaw button", reel, "place-pressed");
            WaitFor(session, () => session.Jigsaw != null, "the card's jigsaw button opened no jigsaw");
            reel.Advance(DarkMoment);
            reel.Shoot("jigsaw-comes");
            reel.Settle();
            reel.Shoot("jigsaw");

            PaintingNumber painting = JigsawOf(session).Table.Painting;
            int waited = JigsawOf(session).Table.Waiting.Count;
            int paintingRewardsBefore = session.Progress.Wallet.Rewards.ForPaintings;
            CarryThePieceHome(reel);
            if (JigsawOf(session).Table.Waiting.Count > 0)
            {
                reel.Settle();
                reel.Shoot("piece-placed");
                Press(session, JigsawOf(session).TrayButtonPixel, "the button that places every piece", reel, pressedShot: null);
                reel.Advance(FlyingMoment);
                reel.Shoot("pieces-fly");
            }

            PictureTheEndOfThePlacing(reel);
            if (session.Progress.Museum.WaitingIn(painting).Count != 0 || session.HasUnsavedProgress)
            {
                throw new InvalidOperationException($"After the jigsaw of painting {painting} the progress is \"{session.Progress}\": pieces still wait, or what was placed is not saved.");
            }

            int rewardsDue = session.Progress.Museum.IsComplete(painting) ? 1 : 0;
            ThroughTheCard.Require(
                session.Progress.Wallet.Rewards.ForPaintings == paintingRewardsBefore + rewardsDue,
                $"the jigsaw of painting {painting} gave {session.Progress.Wallet.Rewards.ForPaintings - paintingRewardsBefore} painting rewards, not {rewardsDue}");

            bool restoredTheRoom = RoomWall.Of(painting.Room, session.Progress.Museum, reel.Catalogue).IsRestored;
            Press(session, JigsawOf(session).BackButtonPixel, "the jigsaw's back button", reel, "back-pressed");
            string restored = session.Progress.Museum.IsComplete(painting) ? ", which restored it" : string.Empty;
            if (restoredTheRoom)
            {
                WalkThroughTheRestoredRoom(reel);
                return $"{waited} piece(s) of painting {painting} placed{restored} and room {painting.Room} with it";
            }

            WaitFor(session, () => session.Board != null, "the jigsaw's back button did not lead back to the card");
            reel.Advance(Tempo.CardAfterDark + Tempo.CardComesDown / 2f);
            reel.Shoot("card-comes-back");
            reel.Settle();
            reel.Shoot("card-again");
            return $"{waited} piece(s) of painting {painting} placed{restored}";
        }

        /// <summary>
        /// The jigsaw that completed a room leads to the hall, which shows the room restored: pictured in its
        /// celebration and at rest; its green button then leads on to the hall as the player's progress has it.
        /// </summary>
        private static void WalkThroughTheRestoredRoom(Reel reel)
        {
            GameSession session = reel.Session;
            WaitFor(session, () => session.Museum != null, "the jigsaw that restored a room did not lead to the hall");
            reel.Advance(RestoredRoomMoment);
            reel.Shoot("room-restored");
            reel.Advance(RestoredRoomRests);
            reel.Shoot("room-restored-rest");
            Press(session, MuseumOf(session).GoOnButtonPixel, "the restored room's button that leads on", reel, pressedShot: null);
            reel.Settle();
            if (MuseumOf(session).GoOnButtonPixel != null || MuseumOf(session).PlayButtonPixel == null)
            {
                throw new InvalidOperationException("The restored room's button did not lead on to the hall with its play button.");
            }
        }

        /// <summary>A finger takes the tray's first piece, carries it over the painting and lets it go on its place.</summary>
        private static void CarryThePieceHome(Reel reel)
        {
            GameSession session = reel.Session;
            JigsawStage jigsaw = JigsawOf(session);
            int piece = jigsaw.PiecesInTheTray.Count > 0
                ? jigsaw.PiecesInTheTray[0]
                : throw new InvalidOperationException("A card led to a jigsaw whose tray shows no piece.");
            Vector2 inTheTray = PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray");
            Vector2 onItsPlace = PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place");

            session.PointerDown(inTheTray);
            reel.Settle();
            session.PointerMoved(Vector2.Lerp(inTheTray, onItsPlace, 0.55f));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Shoot("piece-carried");
            session.PointerMoved(onItsPlace);
            session.PointerUp();
            int? next = jigsaw.PiecesInTheTray.Where(shown => shown != piece).Select(shown => (int?)shown).FirstOrDefault();
            if (next is int taken)
            {
                // The finger does not wait for the piece it let go: it comes down on the next one in the same moment.
                session.PointerDown(PixelOrFail(jigsaw.PixelOfTheTrayPiece(taken), "the next piece in the tray"));
            }

            reel.Advance(Tempo.PieceDropsIn + DroppedMoment);
            reel.Shoot("piece-dropped");
            if (!jigsaw.Table.InPlace.Contains(piece))
            {
                throw new InvalidOperationException($"Piece {piece} was let go on its place and did not drop in.");
            }

            if (next is int carried)
            {
                CarryHomeThePieceTakenAtOnce(reel, jigsaw, carried);
            }
        }

        /// <summary>The piece a finger took while the one before it was still dropping in must be in the hand; it is carried to its place like the first.</summary>
        private static void CarryHomeThePieceTakenAtOnce(Reel reel, JigsawStage jigsaw, int piece)
        {
            GameSession session = reel.Session;
            if (jigsaw.PieceInTheHand != piece)
            {
                throw new InvalidOperationException($"A finger that came down on piece {piece} while the piece before it dropped in did not pick it up.");
            }

            session.PointerMoved(PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the next piece's place"));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Shoot("next-piece-carried-at-once");
            session.PointerUp();
            reel.Advance(Tempo.PieceDropsIn + DroppedMoment);
            if (!jigsaw.Table.InPlace.Contains(piece))
            {
                throw new InvalidOperationException($"Piece {piece}, taken while the piece before it dropped in, was let go on its place and did not drop in.");
            }
        }

        /// <summary>Every piece is in place: a painting that is whole now is pictured in its celebration, and then, like any other, at rest.</summary>
        private static void PictureTheEndOfThePlacing(Reel reel)
        {
            JigsawStage jigsaw = JigsawOf(reel.Session);
            WaitFor(reel.Session, () => jigsaw.Table.Waiting.Count == 0, "the jigsaw's pieces were not all placed");
            if (jigsaw.Table.IsWhole && !jigsaw.IsSettled)
            {
                reel.Advance(Tempo.PauseBeforeRestored + CelebrationMoment);
                reel.Shoot("painting-restored");
            }

            reel.Settle();
            reel.Shoot("jigsaw-done");
        }

        /// <summary>A finger presses a place on the bar and lifts; the page turns, with pictures of the turn if asked.</summary>
        private static void TurnThePage(Reel reel, Destination to, bool picturesTheTurn)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).PixelOf(to), $"the bar's place for {to}", reel, pressedShot: null);
            if (picturesTheTurn)
            {
                float clock = 0f;
                foreach (float moment in TurnMoments)
                {
                    clock = reel.AdvanceTo(moment, clock);
                    reel.Shoot($"turn-t{StoryStudio.Seconds(moment)}");
                }
            }

            reel.Settle();
            if (MuseumOf(session).Showing != to)
            {
                throw new InvalidOperationException($"A press on the bar's place for {to} left the museum on {MuseumOf(session).Showing}.");
            }
        }

        /// <summary>A finger comes down on a control and lifts on it; the held control is pictured if a name for that picture is given.</summary>
        /// <exception cref="InvalidOperationException">The screen shows no such control to press.</exception>
        private static void Press(GameSession session, Vector2? pixel, string what, Reel reel, string? pressedShot)
        {
            session.PointerDown(PixelOrFail(pixel, what));
            if (pressedShot != null)
            {
                reel.Advance(PressedMoment);
                reel.Shoot(pressedShot);
            }

            session.PointerUp();
        }

        /// <summary>Moves the clock until something has happened that a press leads to.</summary>
        /// <exception cref="InvalidOperationException">It did not happen in time: the press led nowhere.</exception>
        private static void WaitFor(GameSession session, Func<bool> hasHappened, string otherwise)
        {
            for (float waited = 0f; !hasHappened(); waited += StoryStudio.FrameSeconds)
            {
                if (waited > LongestLeave)
                {
                    throw new InvalidOperationException($"After {LongestLeave} s {otherwise}.");
                }

                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        private static Vector2 PixelOrFail(Vector2? pixel, string what) =>
            pixel ?? throw new InvalidOperationException($"The screen does not show {what}, which the walk was to touch.");

        private static MuseumStage MuseumOf(GameSession session) =>
            session.Museum ?? throw new InvalidOperationException("The session is not in the museum, where the walk expected it.");

        private static JigsawStage JigsawOf(GameSession session) =>
            session.Jigsaw ?? throw new InvalidOperationException("The session is not at a jigsaw, where the walk expected it.");

        private static IBotPolicy Expert() => BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot)
            ? bot
            : throw new InvalidOperationException("The bots' list has no expert.");

        /// <summary>The pictures of one walk: numbered in the order they are taken, and gathered on one sheet.</summary>
        private sealed class Reel
        {
            private readonly ScreenShape _screen;
            private readonly ContactSheet _sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
            private int _shots;

            public Reel(GameSession session, ScreenShape screen, string name, MuseumCatalogue catalogue)
            {
                Session = session;
                _screen = screen;
                Name = name;
                Catalogue = catalogue;
                TakeAwayTheReelBefore(name);
            }

            public GameSession Session { get; }

            /// <summary>What hangs where in the museum the walk goes through.</summary>
            public MuseumCatalogue Catalogue { get; }

            public string Name { get; }

            /// <summary>
            /// The pictures of an earlier walk of the same name are this walk's to replace: a walk that has other
            /// stops numbers its pictures differently, and the old ones would be left standing among the new.
            /// </summary>
            private static void TakeAwayTheReelBefore(string name)
            {
                if (!Directory.Exists(StoryStudio.ShotsFolder))
                {
                    return;
                }

                foreach (string earlier in Directory.GetFiles(StoryStudio.ShotsFolder, $"{name}-??-*.png"))
                {
                    File.Delete(earlier);
                }
            }

            public void Advance(float seconds) => StoryStudio.AdvanceBy(seconds, Session);

            public float AdvanceTo(float seconds, float clock) => StoryStudio.AdvanceTo(seconds, Session, clock);

            public void Settle() => StoryStudio.AdvanceUntilSettled(Session);

            public void Shoot(string what)
            {
                _shots++;
                StoryStudio.Shoot(Session, _screen, _sheet, $"{Name}-{_shots:D2}-{what}.png");
            }

            public void WriteSheet() => StoryStudio.WriteSheet(_sheet, $"{Name}-sheet.png");
        }
    }
}
