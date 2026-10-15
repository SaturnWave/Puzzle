#nullable enable
using System.IO;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the settings, once on each screen, on the game's own session and by touches alone, over the tools'
    /// stand-in phone with its records (<see cref="StoryStudio.RecordsOnAPhone"/>): on every page that shows the
    /// wallet row (the hall, the map, the collection, the shop) the gear brings the settings card down; while it
    /// hangs, a press on the bar behind it does nothing; the way back or its round button takes it up, and the page
    /// is the player's again. Then in the hall the privacy card counts the records; "Kayıtları sil" asks first and
    /// "Vazgeç" changes nothing; asked again, "Sil" empties the file and the count reads 0, with the progress, the
    /// wallet and the settings as they were; the way back closes the privacy card to the settings card; the support
    /// card comes and its "Tamam" takes it up; the way back closes the settings (<c>walk-settings-...</c>). The
    /// build honours no setting yet, so the card has no switch to flip: that step comes with the sounds.
    /// </summary>
    internal static partial class GameWalk
    {
        private static readonly Destination[] PagesWithTheWalletRow = { Destination.Home, Destination.Map, Destination.Collection, Destination.Shop };

        public static void RunTheSettingsWalk(ScreenShape screen, Studio studio)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            AttemptLogFile records = StoryStudio.RecordsOnAPhone(SettingsShots.RecordsOnThePhone);
            var settings = new SettingsInMemory(DeviceSettings.Default);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), records, settings, DeviceAbilities.OfThisBuild,
                screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-settings-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            PlayerProgress before = session.Progress;

            foreach (Destination page in PagesWithTheWalletRow)
            {
                OpenAndCloseTheSettingsOn(page, reel);
            }

            TurnThePage(reel, Destination.Home, picturesTheTurn: false);
            DeleteTheRecordsAfterTheQuestion(records, reel);
            ThroughTheCard.Require(
                session.Progress.Equals(before) && settings.Settings.Equals(DeviceSettings.Default),
                $"the settings walk changed the progress ({session.Progress}, was {before}) or the settings ({settings.Settings})");

            LookAtTheSupportCard(reel);
            session.GoBack();
            reel.Settle();
            ThroughTheCard.Require(!MuseumOf(session).ShowsTheSettings, "the way back left the settings card hanging");
            TurnThePage(reel, Destination.Map, picturesTheTurn: false);
            reel.WriteSheet();
            Debug.Log($"Screen shots: walk {reel.Name}: the gear on {PagesWithTheWalletRow.Length} pages, {SettingsShots.RecordsOnThePhone} records deleted after a question, the support card.");
        }

        /// <summary>
        /// On a page, the gear brings the settings card down; a press on the bar behind it leads nowhere; the way back
        /// (on the hall and the collection) or its round button (on the map and the shop) takes it up.
        /// </summary>
        private static void OpenAndCloseTheSettingsOn(Destination page, Reel reel)
        {
            GameSession session = reel.Session;
            if (MuseumOf(session).Showing != page)
            {
                TurnThePage(reel, page, picturesTheTurn: false);
            }

            reel.Shoot($"{page.ToString().ToLowerInvariant()}");
            Press(session, MuseumOf(session).GearPixel, $"the gear on the {page}", reel, page == Destination.Home ? "gear-pressed" : null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings, $"the gear on the {page} did not bring the settings card");
            reel.Shoot($"{page.ToString().ToLowerInvariant()}-settings");

            Destination behind = page == Destination.Map ? Destination.Shop : Destination.Map;
            Press(session, MuseumOf(session).PixelOf(behind), $"the bar's place for {behind} behind the card", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(
                MuseumOf(session).Showing == page && MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings,
                $"a press on the bar behind the settings card turned to {MuseumOf(session).Showing} or took the card away");

            if (page == Destination.Home || page == Destination.Collection)
            {
                session.GoBack();
            }
            else
            {
                Press(session, MuseumOf(session).CardClosePixel, "the settings card's round button", reel, pressedShot: null);
            }

            reel.Settle();
            ThroughTheCard.Require(!MuseumOf(session).ShowsTheSettings && MuseumOf(session).Showing == page, $"the settings card on the {page} did not go up, or the page changed");
        }

        /// <summary>The privacy card counts the records; the question's "Vazgeç" keeps them; "Sil" deletes them; the way back leads to the settings card.</summary>
        private static void DeleteTheRecordsAfterTheQuestion(AttemptLogFile records, Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).GearPixel, "the hall's gear", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).PrivacyRowPixel, "the settings card's privacy row", reel, "privacy-row-pressed");
            reel.Settle();
            string kept = PrivacyWords.RecordsKept(SettingsShots.RecordsOnThePhone);
            ThroughTheCard.Require(MuseumOf(session).PrivacyShown == (kept, false), $"the privacy card shows {MuseumOf(session).PrivacyShown}, not \"{kept}\"");
            reel.Shoot("privacy");

            Press(session, MuseumOf(session).DeleteRecordsPixel, "the privacy card's Kayıtları sil", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).PrivacyShown is (_, true), "Kayıtları sil did not ask first");
            reel.Shoot("privacy-question");
            Press(session, MuseumOf(session).KeepRecordsPixel, "the question's Vazgeç", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(
                MuseumOf(session).PrivacyShown == (kept, false) && File.ReadAllLines(records.Path).Length == SettingsShots.RecordsOnThePhone,
                "Vazgeç did not leave the records as they were");

            Press(session, MuseumOf(session).DeleteRecordsPixel, "the privacy card's Kayıtları sil", reel, pressedShot: null);
            reel.Settle();
            Press(session, MuseumOf(session).ConfirmDeletePixel, "the question's Sil", reel, "privacy-sil-pressed");
            reel.Settle();
            ThroughTheCard.Require(
                MuseumOf(session).PrivacyShown == (PrivacyWords.RecordsKept(0), false) && !File.Exists(records.Path) && records.CountRecords() == 0,
                $"Sil left the privacy card at {MuseumOf(session).PrivacyShown}, or records on the stand-in phone");
            ThroughTheCard.Require(MuseumOf(session).DeleteRecordsPixel == null, "with no record left, Kayıtları sil can still be pressed");
            reel.Shoot("privacy-deleted");

            session.GoBack();
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings, "the way back from the privacy card did not lead to the settings card");
        }

        /// <summary>The support card comes in front of the settings card, and its "Tamam" leads back to it.</summary>
        private static void LookAtTheSupportCard(Reel reel)
        {
            GameSession session = reel.Session;
            Press(session, MuseumOf(session).SupportRowPixel, "the settings card's support row", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Support, "the support row did not bring the support card");
            reel.Shoot("support");
            Press(session, MuseumOf(session).SupportDonePixel, "the support card's Tamam", reel, pressedShot: null);
            reel.Settle();
            ThroughTheCard.Require(MuseumOf(session).SettingsCardInFront == SettingsCardKind.Settings, "the support card's Tamam did not lead back to the settings card");
        }
    }
}
