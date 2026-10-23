#nullable enable
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using Puzzle.Client.Words;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the way back from the hall and of drags over what can be pressed (the phone test of 2026-10-23),
    /// once on each screen, in the language the tools speak, on the game's own session and by touches alone, for a
    /// player who stands on a level. In the bare hall the device's way back brings down the card that asks whether to
    /// close the game, its question in the language spoken (pictured); a touch beside it and a drag across its "Çık"
    /// press nothing; the way back again takes it up; its "Vazgeç" takes it up; its "Çık" asks the game to close
    /// (which the walk reads, as the running game would) and leaves the card hanging, as the Editor stays open. From
    /// the shop, the map and the collection the way back still leads to the hall, with no card. Then drags that leave
    /// the slop of a tap press nothing they began and ended on: across the settings card's language row and each of its
    /// switches (pictured), the level card's "Oyna" and a start booster's disc, and up and back down over a set's button
    /// in the shop; a finger that wavers within the slop still presses the language row (<c>walk-wayback-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>A drag over a control goes this share of the screen's width at a stretch (or twice it), each stretch in this long: far beyond the slop of a tap, and not off a card's row.</summary>
        private const float DragShare = 0.08f;
        private const float DragStretch = 0.15f;

        /// <summary>A finger that wavers this many points while it presses still taps.</summary>
        private const float Waver = 0.4f * TapSlop.Points;

        public static int RunTheWayBackWalk(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            var settings = new SettingsInMemory(DeviceSettings.Default);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(),
                settings, DeviceAbilities.OfThisBuild, CuePlayer.Silent, screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-wayback-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            var finger = new SwipingFinger(reel, ScreenView.Of(screen));
            reel.Settle();
            reel.Shoot("hall");

            AnswerTheQuestionOfTheWayBack(finger);
            GoBackToTheHallFromEveryPage(reel);
            DragOverTheSettingsCard(finger, settings);
            DragOverTheLevelCardAndTheShop(finger);
            reel.WriteSheet();
            Debug.Log(
                $"Screen shots: walk {reel.Name}: in {Languages.CodeOf(Speech.Language)} the way back in the hall asked \"{MuseumWords.QuitQuestion}\" "
                + $"(\"{MuseumWords.StayInTheGame}\" / \"{MuseumWords.QuitTheGame}\"); beside the card and a drag over its answer pressed nothing; the way back, "
                + "its first answer and its second (the wish to close read) did what they should; the shop, the map and the collection led back to the hall; "
                + $"drags over the settings card's rows, the level card and the shop pressed nothing; {reel.Taken} pictures.");
            return reel.Taken;
        }

        /// <summary>The hall's question of the way back: what it asks, what presses nothing, and each of its three ways out.</summary>
        private static void AnswerTheQuestionOfTheWayBack(SwipingFinger finger)
        {
            Reel reel = finger.Reel;
            GameSession session = reel.Session;
            AskWhetherToQuit(reel);
            reel.Shoot("quit-card");

            Press(session, MuseumOf(session).PixelOf(Destination.Map), "the bar's place for the map beside the card", reel, pressedShot: null);
            reel.Settle();
            RequireTheQuestion(session, "a touch beside the card");

            Vector2 quit = PixelOrFail(MuseumOf(session).QuitTheGamePixel, "the card's Çık");
            finger.SwipeAlong(quit, heldShot: "quit-dragged-held", (-DragShare, 0f, DragStretch), (2f * DragShare, 0f, DragStretch));
            RequireTheQuestion(session, "a drag across its Çık");
            ThroughTheCard.Require(!session.TakeTheWishToQuit(), "a drag across the card's Çık asked the game to close");

            session.GoBack();
            reel.Settle();
            RequireNoQuestion(session, "the way back pressed again");

            AskWhetherToQuit(reel);
            Press(session, MuseumOf(session).StayInTheGamePixel, "the card's Vazgeç", reel, pressedShot: null);
            reel.Settle();
            RequireNoQuestion(session, "its Vazgeç");
            ThroughTheCard.Require(!session.TakeTheWishToQuit(), "the card's Vazgeç asked the game to close");

            AskWhetherToQuit(reel);
            Press(session, MuseumOf(session).QuitTheGamePixel, "the card's Çık", reel, "quit-pressed");
            reel.Settle();
            ThroughTheCard.Require(session.TakeTheWishToQuit(), "the card's Çık did not ask the game to close");
            ThroughTheCard.Require(!session.TakeTheWishToQuit(), "the wish to close was there to take a second time");
            RequireTheQuestion(session, "its Çık, which leaves the closing to the running game");
            session.GoBack();
            reel.Settle();
            RequireNoQuestion(session, "the way back after Çık");
        }

        /// <summary>The way back from the shop, the map and the collection leads to the hall as before, and asks nothing there.</summary>
        private static void GoBackToTheHallFromEveryPage(Reel reel)
        {
            GameSession session = reel.Session;
            foreach (Destination page in new[] { Destination.Shop, Destination.Map, Destination.Collection })
            {
                TurnThePage(reel, page, picturesTheTurn: false);
                session.GoBack();
                reel.Settle();
                ThroughTheCard.Require(
                    MuseumOf(session).Showing == Destination.Home && MuseumOf(session).QuitQuestionShown == null,
                    $"the way back from the {page} left the museum on {MuseumOf(session).Showing}, or asked whether to close the game");
            }
        }

        /// <summary>
        /// Drags across the settings card's language row and each of its switches press nothing: no language card, every
        /// setting as it was. A press on the language row that wavers within the slop still opens the language card.
        /// </summary>
        private static void DragOverTheSettingsCard(SwipingFinger finger, SettingsInMemory settings)
        {
            Reel reel = finger.Reel;
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings, "the gear did not bring the settings card");

            Vector2 languageRow = PixelOrFail(MuseumOf(session).LanguageRowPixel, "the settings card's language row");
            finger.SwipeAlong(languageRow, heldShot: "language-row-dragged-held", (-DragShare, 0f, DragStretch), (2f * DragShare, 0f, DragStretch));
            ThroughTheCard.Require(
                MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings,
                $"a drag across the language row left {MuseumOf(session).SettingsCardInFront?.ToString() ?? "no card"} in front");

            IReadOnlyList<DeviceSetting> switches = MuseumOf(session).SwitchesShown.Select(shown => shown.Setting).ToArray();
            foreach (DeviceSetting setting in switches)
            {
                // From the switch at the row's right end towards the row's middle, and back.
                Vector2 onTheRow = PixelOrFail(MuseumOf(session).SwitchPixel(setting), $"the switch of {setting}");
                finger.SwipeAlong(onTheRow, heldShot: null, (-2f * DragShare, 0f, DragStretch), (2f * DragShare, 0f, DragStretch));
                ThroughTheCard.Require(
                    settings.Settings.Equals(DeviceSettings.Default) && MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings,
                    $"a drag across the row of {setting} turned the settings to {settings.Settings}, or took the card away");
            }

            reel.Shoot("settings-after-the-drags");
            PressWavering(finger, MuseumOf(session).LanguageRowPixel, "the settings card's language row");
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Language, "a press on the language row that wavered within the slop did not open the language card");
            session.GoBack();
            reel.Settle();
            session.GoBack();
            reel.Settle();
            ThroughTheCard.Require(!MuseumOf(session).ShowsTheSettings, "the way back left a settings card hanging");
        }

        /// <summary>
        /// Drags across the level card's "Oyna" and a start booster's disc lead into no level and choose nothing; one up
        /// and back down over a set's button in the shop buys nothing and brings no card.
        /// </summary>
        private static void DragOverTheLevelCardAndTheShop(SwipingFinger finger)
        {
            Reel reel = finger.Reel;
            GameSession session = reel.Session;
            PlayerProgress before = session.Progress;
            Press(session, MuseumOf(session).PlayButtonPixel, "the hall's play button", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).ShowsTheStartCard, "the play button did not bring the level's card");

            Vector2 play = PixelOrFail(MuseumOf(session).StartCardPlayPixel, "the level card's Oyna");
            finger.SwipeAlong(play, heldShot: null, (-DragShare, 0f, DragStretch), (2f * DragShare, 0f, DragStretch));
            BoosterKind booster = LevelBrief.StartBoosters[0];
            Vector2 disc = PixelOrFail(MuseumOf(session).StartCardDiscPixel(booster), $"the disc of {booster.Name()}");
            finger.SwipeAlong(disc, heldShot: null, (0f, DragShare, DragStretch), (0f, -DragShare, DragStretch));
            ThroughTheCard.Require(
                session.Museum != null && MuseumOf(session).ShowsTheStartCard && MuseumOf(session).PlayerLeftFor == null
                && MuseumOf(session).StartChoice.Equals(StartChoice.None) && MuseumOf(session).OffersABooster == null,
                "a drag across the level card's Oyna or a start booster's disc led into the level, chose a booster or offered one");
            session.GoBack();
            reel.Settle();

            TurnThePage(reel, Destination.Shop, picturesTheTurn: false);
            Vector2 set = PixelOrFail(MuseumOf(session).ShopSetPixel(0), "the shop's first set");
            finger.SwipeAlong(set, heldShot: null, (0f, DragShare, DragStretch), (0f, -DragShare, DragStretch));
            ThroughTheCard.Require(
                MuseumOf(session).Showing == Destination.Shop && MuseumOf(session).NoticeShown == null && session.Progress.Equals(before),
                $"a drag up and back over the shop's set turned to {MuseumOf(session).Showing}, brought a card or changed the progress to \"{session.Progress}\"");
            reel.Shoot("shop-after-the-drag");
            TurnThePage(reel, Destination.Home, picturesTheTurn: false);
        }

        /// <summary>The way back in the bare hall brings down the card that asks whether to close the game, in the language spoken.</summary>
        private static void AskWhetherToQuit(Reel reel)
        {
            reel.Session.GoBack();
            reel.Settle();
            RequireTheQuestion(reel.Session, "the way back in the hall");
        }

        private static void RequireTheQuestion(GameSession session, string after) =>
            ThroughTheCard.Require(
                MuseumOf(session).Showing == Destination.Home && MuseumOf(session).QuitQuestionShown == MuseumWords.QuitQuestion,
                $"after {after} the hall shows {MuseumOf(session).Showing} and asks \"{MuseumOf(session).QuitQuestionShown}\", not \"{MuseumWords.QuitQuestion}\"");

        private static void RequireNoQuestion(GameSession session, string after) =>
            ThroughTheCard.Require(
                MuseumOf(session).Showing == Destination.Home && MuseumOf(session).QuitQuestionShown == null,
                $"after {after} the museum shows {MuseumOf(session).Showing}, or still asks whether to close the game");

        /// <summary>A finger comes down on a control, wavers a little within the slop of a tap, and lifts where it came down.</summary>
        private static void PressWavering(SwipingFinger finger, Vector2? pixel, string what)
        {
            GameSession session = finger.Reel.Session;
            Vector2 at = PixelOrFail(pixel, what);
            float pixelsPerPoint = finger.View.PixelsPerPoint;
            session.PointerDown(at);
            session.PointerMoved(at + new Vector2(Waver, -Waver) * pixelsPerPoint);
            session.PointerMoved(at + new Vector2(-Waver, 0f) * pixelsPerPoint);
            session.PointerMoved(at);
            session.PointerUp();
        }
    }
}
