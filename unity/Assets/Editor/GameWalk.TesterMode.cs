#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the tester mode, once on each screen, on the game's own session and by touches alone, for a new
    /// player with one life left and too few coins for any set: the support card's version row tapped six times asks
    /// nothing, the seventh asks whether to turn the mode on; "Vazgeç" takes the question back; seven taps more and
    /// "Aç" turn it on, every card goes up and the hall's wallet row shows "∞"; the heart opens the lives card in the
    /// mode. The level is lost, its offer of extra moves (whose "Vazgeç" names no life) taken for coins the player
    /// does not have, and the coins and the life stay as they were: the lost card says no life goes and has no pill,
    /// and its "Tekrar dene" brings the level's card, not the lives card. Through that card the level is played again
    /// and left after a move: the leaving card says no life goes, and none does. In the shop the dearest set is bought
    /// and the coins stay. Last, the settings card shows the tester mode's row under the
    /// other switches, its switch turns the mode off, and the row shows the coins and the life again; opened anew, the
    /// card has no tester row (<c>walk-testermode-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        public static void RunTheTesterModeWalk(ScreenShape screen, Studio studio)
        {
            LifeRules lives = StoryStudio.Economy.Lives;
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            PlayerProgress oneLife = standing.WithWallet(standing.Wallet.WithLives(new LifeMeter(lives.Max - 1, StoryStudio.Clock.Now + NextLifeWithoutALife)));
            var settings = new SettingsInMemory(DeviceSettings.Default);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(oneLife), StoryStudio.AttemptsKeptInMemory(),
                settings, DeviceAbilities.OfThisBuild, CuePlayer.Silent, screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-testermode-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            reel.Shoot("hall");
            ThroughTheCard.Require(!MuseumOf(session).Wallet.IsUnlimited, "a first start's wallet row is in the tester mode");

            TurnTheTesterModeOn(settings, reel);
            Wallet onceOn = session.Progress.Wallet;
            LoseTheLevelAndKeepTheLife(onceOn, reel);
            BuyTheDearestSetAndKeepTheCoins(reel);
            TurnTheTesterModeOff(settings, reel);
            reel.WriteSheet();
            Debug.Log(
                $"Screen shots: walk {reel.Name}: the tester mode asked for after seven taps, turned on, a level lost and a set bought with {onceOn.Coins} coins "
                + $"and one life kept, turned off; the player now stands at \"{session.Progress}\".");
        }

        /// <summary>The support card's version row: six taps ask nothing, the seventh asks; "Vazgeç" changes nothing; asked again, "Aç" turns the mode on and takes every card up.</summary>
        private static void TurnTheTesterModeOn(SettingsInMemory settings, Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).SupportRowPixel, "the settings card's support row", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Support, "the support row did not bring the support card");
            reel.Shoot("support");

            TapTheVersion(reel, TesterMode.TapsToAsk - 1);
            ThroughTheCard.Require(!MuseumOf(session).AsksAboutTheTesterMode, $"{TesterMode.TapsToAsk - 1} taps on the version row asked about the tester mode");
            TapTheVersion(reel, 1);
            ThroughTheCard.Require(MuseumOf(session).AsksAboutTheTesterMode, $"{TesterMode.TapsToAsk} taps on the version row did not ask about the tester mode");
            reel.Shoot("question");

            Press(session, MuseumOf(session).LeaveTheTesterModeOffPixel, "the question's Vazgeç", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(
                !MuseumOf(session).AsksAboutTheTesterMode && MuseumOf(session).SupportDonePixel != null && !settings.Settings.TesterMode,
                "Vazgeç did not leave the support card as it came, or turned the tester mode on");

            TapTheVersion(reel, TesterMode.TapsToAsk);
            Press(session, MuseumOf(session).TurnOnTheTesterModePixel, "the question's Aç", reel, "turn-on-pressed");
            ThroughTheCard.Require(settings.Settings.TesterMode, $"Aç left the settings at {settings.Settings}");
            reel.Settle();
            MuseumStage museum = MuseumOf(session);
            ThroughTheCard.Require(!museum.ShowsTheSettings && museum.PlayButtonPixel != null, "after Aç a card still hangs over the hall, or the hall does not listen");
            ThroughTheCard.Require(museum.Wallet.IsUnlimited, $"after Aç the wallet row shows {museum.Wallet}, not the tester mode's");
            reel.Shoot("hall-unlimited");

            Press(session, museum.LivesPixel, "the heart's pill", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).ShowsTheLivesCard, "the heart's pill did not open the lives card");
            reel.Shoot("lives-card");
            Press(session, MuseumOf(session).LivesCardDonePixel, "the lives card's Tamam", reel, pressedShot: null);
            reel.Settle();
        }

        private static void TapTheVersion(Reel reel, int taps)
        {
            for (int tap = 0; tap < taps; tap++)
            {
                Press(reel.Session, MuseumOf(reel.Session).VersionRowPixel, "the support card's version row", reel, pressedShot: null);
                reel.Settle();
            }
        }

        /// <summary>
        /// Into the level and lost: the offer's "+5 hamle" is lit and taken for coins the player does not have, and the
        /// coins stay; run out again, the loss costs no life, its card says so, and "Tekrar dene" brings the level's
        /// card down; its "Oyna" opens the level again, and leaving it after a move costs no life either.
        /// </summary>
        private static void LoseTheLevelAndKeepTheLife(Wallet onceOn, Reel reel)
        {
            GameSession session = reel.Session;
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            FlowStory.PlayToTheEnding(session, FlowEnding.Lost, new RandomPolicy());
            reel.Settle();
            GameStage stage = session.Stage;
            if (stage.IsOfferingExtraMoves)
            {
                ThroughTheCard.Require(stage.ExtraMovesOnTheCard?.CoinsCoverIt == true, $"in the tester mode the offer says {stage.ExtraMovesOnTheCard}: its coins do not cover the moves");
                reel.Shoot("offer");
                Press(session, stage.TakeTheMovesForCoinsPixel, "the offer's +5 hamle", reel, pressedShot: null);
                reel.Settle();
                ThroughTheCard.Require(
                    stage.Game.Status == GameStatus.Playing && session.Progress.Wallet.Coins == onceOn.Coins,
                    $"after +5 hamle the game is {stage.Game.Status} and the coins {session.Progress.Wallet.Coins}, from {onceOn.Coins}");
                reel.Shoot("moves-taken");
                FlowStory.PlayTheGame(session, new RandomPolicy(), new Rng(BotSeed, BotStream));
                reel.Settle();
            }

            ThroughTheCard.Require(stage.Game.Status.IsLoss() && !stage.IsOfferingExtraMoves && stage.ResultButtonPixel != null, $"the level ended {stage.Game.Status}, not with the card of a loss");
            ThroughTheCard.Require(
                session.Progress.Wallet.Lives.Equals(onceOn.Lives) && session.Progress.Wallet.Coins == onceOn.Coins,
                $"a loss in the tester mode left the wallet at {session.Progress.Wallet}, from {onceOn}");
            reel.Shoot("card-lost");

            Press(session, stage.ResultButtonPixel, "the lost card's Tekrar dene", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the lost card's Tekrar dene opened no museum");
            reel.Settle();
            MuseumStage museum = MuseumOf(session);
            ThroughTheCard.Require(museum.ShowsTheStartCard && !museum.ShowsTheLivesCard, "after a loss in the tester mode Tekrar dene did not bring the level's card");
            reel.Shoot("hall-with-the-card");
            LeaveAfterAMoveAndKeepTheLife(onceOn, reel);
        }

        /// <summary>The level's card's "Oyna", one move, the pause disc: the leaving card says no life goes; its museum button leads to the hall with the wallet as it was.</summary>
        private static void LeaveAfterAMoveAndKeepTheLife(Wallet onceOn, Reel reel)
        {
            GameSession session = reel.Session;
            ThroughTheCard.PressPlay(session);
            reel.Settle();
            Game game = session.Stage.Game;
            Move move = Expert().ChooseMove(game, game.FindLegalMoves(), new Rng(BotSeed, BotStream));
            ThroughTheCard.Require(session.TryPlay(move) && game.Status == GameStatus.Playing, $"the move {move} was refused or decided the level");
            reel.Settle();
            Press(session, session.Stage.PauseButtonPixel, "the shelf's pause disc", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(
                session.Stage.IsAskingWhetherToLeave && session.Stage.CardTells is LevelResult.Leaving { HasPlayed: true },
                "the pause disc after a move did not bring the card that asks whether to leave");
            reel.Shoot("leave-card");

            Press(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button", reel, pressedShot: null);
            WaitFor(session, () => session.Museum != null, "the leaving card's museum button opened no museum");
            reel.Settle();
            ThroughTheCard.Require(
                session.Progress.Wallet.Lives.Equals(onceOn.Lives) && session.Progress.Wallet.Coins == onceOn.Coins,
                $"leaving after a move in the tester mode left the wallet at {session.Progress.Wallet}, from {onceOn}");
        }

        /// <summary>In the shop the dearest set's button is lit for coins that do not cover it; bought, the boosters arrive and the coins stay.</summary>
        private static void BuyTheDearestSetAndKeepTheCoins(Reel reel)
        {
            GameSession session = reel.Session;
            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            int dearest = StoryStudio.Economy.Shop.BoosterSets.Count - 1;
            BoosterSet set = StoryStudio.Economy.Shop.BoosterSets[dearest];
            Wallet before = session.Progress.Wallet;
            ThroughTheCard.Require(!before.Coins.Covers(set.Price), $"{before.Coins} coins cover the {set}: the walk needs a set they do not");
            ThroughTheCard.Require(MuseumOf(session).IsShopSetLit(dearest), $"in the tester mode the button of the {set} is dimmed");
            reel.Shoot("shop");

            Press(session, MuseumOf(session).ShopSetPixel(dearest), "the dearest set's button", reel, pressedShot: null);
            reel.Settle();
            Wallet after = session.Progress.Wallet;
            ThroughTheCard.Require(MuseumOf(session).NoticeShown == ShopWords.BoughtTitle, $"the set's button brought the card \"{MuseumOf(session).NoticeShown}\"");
            ThroughTheCard.Require(
                after.Coins == before.Coins && after.Shelf.Total == before.Shelf.Total + set.Boosters.Total,
                $"the {set} bought in the tester mode left {after}, from {before}");
            reel.Shoot("set-bought");
            Press(session, MuseumOf(session).NoticeDonePixel, "the card's Tamam", reel, pressedShot: null);
            reel.Settle();
        }

        /// <summary>The settings card shows the tester mode's row under the other switches; its switch turns the mode off at once, and the card opened anew has no such row.</summary>
        private static void TurnTheTesterModeOff(SettingsInMemory settings, Reel reel)
        {
            GameSession session = reel.Session;
            TurnThePage(reel, Destination.Home, picturesTheTurn: false);
            Press(session, MuseumOf(session).GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            IReadOnlyList<(DeviceSetting Setting, bool IsOn)> shown = MuseumOf(session).SwitchesShown;
            int honoured = DeviceAbilities.OfThisBuild.Settings.Count;
            ThroughTheCard.Require(
                shown.Count == honoured + 1 && shown[honoured] == (DeviceSetting.TesterMode, true),
                $"in the tester mode the settings card's switches show {string.Join(", ", shown)}");
            reel.Shoot("settings-tester-row");

            Press(session, MuseumOf(session).SwitchPixel(DeviceSetting.TesterMode), "the tester mode's switch", reel, pressedShot: null);
            ThroughTheCard.Require(!settings.Settings.TesterMode, $"the tester mode's switch left the settings at {settings.Settings}");
            reel.Settle();
            reel.Shoot("tester-row-off");
            Press(session, MuseumOf(session).CardClosePixel, "the settings card's round button", reel, pressedShot: null);
            reel.Settle();
            MuseumStage museum = MuseumOf(session);
            ThroughTheCard.Require(
                !museum.Wallet.IsUnlimited && museum.CoinsShown == session.Progress.Wallet.Coins,
                $"with the tester mode off the wallet row shows {museum.Wallet} and {museum.CoinsShown} coins, and the player has {session.Progress.Wallet.Coins}");
            reel.Shoot("hall-normal");

            Press(session, museum.GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(
                MuseumOf(session).SwitchesShown.Count == DeviceAbilities.OfThisBuild.Settings.Count,
                "the settings card opened after the tester mode was turned off still has its row");
            Press(session, MuseumOf(session).CardClosePixel, "the settings card's round button", reel, pressedShot: null);
            reel.Settle();
        }
    }
}
