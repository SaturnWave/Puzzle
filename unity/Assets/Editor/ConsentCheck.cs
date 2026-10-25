#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Canvases;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Client.Statistics;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks the play statistics' consent on the game's own session, by touches alone, with a recording in the place
    /// of Aptabase's package: before the question is answered and after "İzin verme" (the way back) nothing is handed
    /// over and the sending is off, though a level is played to its end and the shop is opened; after "İzin ver" the
    /// game's start goes first, then the shop's opening, a purchase by its product and a level's start and end, each
    /// with no id; a later run that was allowed asks nothing and notes its start; the settings card's switch turned off
    /// stops everything at once, and the privacy row's line follows it. It also checks the statistics' settings file:
    /// one only, in Assets/Resources, with no key or a key of the European region, and with crash reporting off; and that
    /// the privacy card's pages turn to their end and back.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.ConsentCheck.Run
    /// The command ends with exit code 1 when a check failed; the log's last line of the check says what came of it.
    /// </summary>
    public static class ConsentCheck
    {
        /// <summary>A level from the twentieth on: the shop offers the starter pack there, and the tutorial has ended.</summary>
        private const int LevelToStandOn = 25;

        private const ulong BotSeed = 20261024;
        private const ulong BotStream = 5;

        private static readonly ScreenShape Phone = new ScreenShape(1080, 2424, 0, 0);

        private static readonly HashSet<string> TheNotesThereAre = new HashSet<string>
        {
            "app_start", "level_start", "level_win", "level_lose", "painting_complete", "room_complete", "shop_open", "purchase_complete",
        };

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath)
                    ?? throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                PaintingGallery paintings = PaintingGallery.OfThePackedMuseum();
                var findings = new List<string> { CheckTheSettingsFile() };
                findings.Add(CheckThatNothingGoesBeforeAYesNorAfterANo(art, levels, paintings));
                findings.Add(CheckThatAYesLetsTheNotesOut(art, levels, paintings));
                findings.Add(CheckThatTheSwitchStopsThem(art, levels, paintings));
                Debug.Log($"Consent check: passed. {string.Join(" ", findings)}");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is LevelFormatException || failure is ArgumentException)
            {
                Debug.LogError($"Consent check: failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>One statistics settings file, the one the package reads at the start, with no key or a European one and its crash reporting off.</summary>
        private static string CheckTheSettingsFile()
        {
            string[] files = AssetDatabase.FindAssets("t:Settings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<AptabaseSDK.Settings>(path) != null)
                .ToArray();
            const string TheOne = "Assets/Resources/" + AptabaseNotes.SettingsResource + ".asset";
            Require(files.SequenceEqual(new[] { TheOne }), $"the statistics' settings files are [{string.Join(", ", files)}], not {TheOne} alone (the package's importer writes a second one with its placeholder key where none is found)");
            AptabaseSDK.Settings settings = AssetDatabase.LoadAssetAtPath<AptabaseSDK.Settings>(TheOne);
            Require(!settings.EnableCrashReporting, "the statistics' crash reporting is on: it would send error reports before the player's yes");
            StatisticsKey key = StatisticsKey.Read(settings.AppKey);
            Require(!(key is StatisticsKey.Refused), $"the statistics' settings file holds {key}");
            return $"The settings file is {TheOne} with {key}, crash reporting off.";
        }

        /// <summary>
        /// A first start: the question hangs over the hall and nothing is handed over; the way back answers it with no;
        /// a level played to its end and the shop opened hand over nothing either.
        /// </summary>
        private static string CheckThatNothingGoesBeforeAYesNorAfterANo(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings)
        {
            var notes = new RecordedNotes();
            var settings = new SettingsInMemory(DeviceSettings.Default);
            using GameSession session = SessionOf(art, levels, paintings, settings, notes);
            MuseumStage museum = MuseumOf(session);
            Require(museum.ShowsTheStatisticsQuestion && museum.AllowTheStatisticsPixel != null, "a first start did not bring the statistics question down over the hall");
            Require(notes.Said.SequenceEqual(new[] { "off" }), $"before the question was answered the sink heard [{notes}], not only \"off\"");

            session.GoBack();
            StoryStudio.AdvanceUntilSettled(session);
            Require(!MuseumOf(session).ShowsTheStatisticsQuestion, "the way back did not take the statistics question up");
            Require(settings.Settings.Statistics == StatisticsChoice.Declined, $"the way back on the question kept {settings.Settings.Statistics}, not a no");

            Press(session, MuseumOf(session).PixelOf(Destination.Shop), "the bar's place for the shop");
            Press(session, MuseumOf(session).PixelOf(Destination.Home), "the bar's place for the hall");
            GameStatus ending = PlayALevelToItsEnd(session);
            Require(notes.Said.SequenceEqual(new[] { "off" }), $"after a no, the shop and a level {ending} the sink heard [{notes}], not only \"off\"");
            return $"Before an answer and after the way back's no: [{notes}], with the shop opened and a level {ending}.";
        }

        /// <summary>
        /// A first start answered with "İzin ver": the start, the shop's opening, a pack bought and a level's start and
        /// end go out in that order, with their numbers or their product and no id.
        /// </summary>
        private static string CheckThatAYesLetsTheNotesOut(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings)
        {
            var notes = new RecordedNotes();
            var settings = new SettingsInMemory(DeviceSettings.Default);
            using GameSession session = SessionOf(art, levels, paintings, settings, notes);
            Press(session, MuseumOf(session).AllowTheStatisticsPixel, "the statistics question's İzin ver");
            Require(!MuseumOf(session).ShowsTheStatisticsQuestion && settings.Settings.Statistics == StatisticsChoice.Allowed, "İzin ver did not take the question up and keep a yes");
            Require(notes.Said.SequenceEqual(new[] { "off", "on", "app_start" }), $"after İzin ver the sink heard [{notes}], not off, on and the start");

            Press(session, MuseumOf(session).PixelOf(Destination.Shop), "the bar's place for the shop");
            StoreProduct pack = StoryStudio.Economy.Shop.Store.CoinPacks[1];
            Press(session, MuseumOf(session).ShopPackPixel(1), $"the button of {pack.Name}");
            Press(session, MuseumOf(session).NoticeBuyPixel, "the card's Satın al");
            Press(session, MuseumOf(session).NoticeDonePixel, "the card's Tamam");
            Press(session, MuseumOf(session).PixelOf(Destination.Home), "the bar's place for the hall");
            GameStatus ending = PlayALevelToItsEnd(session);

            string end = ending == GameStatus.Won ? "level_win" : "level_lose";
            Require(
                notes.Said.SequenceEqual(new[] { "off", "on", "app_start", "shop_open", "purchase_complete", "level_start", end }),
                $"after a yes, the shop, a pack and a level {ending} the sink heard [{notes}]");
            PlayNote.PurchaseCompleted bought = notes.Notes.OfType<PlayNote.PurchaseCompleted>().Single();
            Require(bought.Product == pack.IdSuffix, $"the purchase was noted as {bought.Product}, not by its product {pack.IdSuffix}");
            PlayNote.LevelEnded ended = notes.Notes.OfType<PlayNote.LevelEnded>().Single();
            Require(ended.Level == LevelToStandOn && ended.MovesLeft == session.Stage.Game.MovesLeft, $"the level's end was noted as {ended}, not level {LevelToStandOn} with the moves its board left");
            RequireNoId(notes, session);
            return $"After İzin ver: [{notes}].";
        }

        /// <summary>
        /// A later run of a player who said yes: no question, the start noted at once; the settings card's switch turned
        /// off stops the notes before the next one, the privacy row's line says so, and the privacy card's pages turn.
        /// </summary>
        private static string CheckThatTheSwitchStopsThem(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings)
        {
            var notes = new RecordedNotes();
            var settings = new SettingsInMemory(DeviceSettings.Default.With(DeviceSetting.Statistics, isOn: true));
            using GameSession session = SessionOf(art, levels, paintings, settings, notes);
            Require(!MuseumOf(session).ShowsTheStatisticsQuestion, "a run after a yes asked the statistics question again");
            Require(notes.Said.SequenceEqual(new[] { "on", "app_start" }), $"a run after a yes opened with [{notes}], not on and the start");

            Press(session, MuseumOf(session).GearPixel, "the wallet row's gear");
            Require(MuseumOf(session).SwitchesShown.Contains((DeviceSetting.Statistics, true)), $"the settings card shows [{string.Join(", ", MuseumOf(session).SwitchesShown)}], not the statistics on");
            Require(MuseumOf(session).PrivacyLineShown == SettingsWords.PrivacyLine(settings.Settings), "the privacy row's line does not say that statistics leave the phone too");
            string pages = TurnThePrivacyPages(session);

            Press(session, MuseumOf(session).SwitchPixel(DeviceSetting.Statistics), "the statistics' switch");
            Require(settings.Settings.Statistics == StatisticsChoice.Declined, $"the switch turned off kept {settings.Settings.Statistics}");
            Require(MuseumOf(session).PrivacyLineShown == SettingsWords.PrivacyLine(settings.Settings), "the privacy row's line did not turn back to the purchases alone");
            Press(session, MuseumOf(session).CardClosePixel, "the settings card's round button");
            Press(session, MuseumOf(session).PixelOf(Destination.Shop), "the bar's place for the shop");
            Press(session, MuseumOf(session).PixelOf(Destination.Home), "the bar's place for the hall");
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            Require(notes.Said.SequenceEqual(new[] { "on", "app_start", "off" }), $"after the switch was turned off the sink heard [{notes}]");
            return $"After a yes in an earlier run, the switch turned off: [{notes}]; {pages}.";
        }

        /// <summary>The privacy card from the settings card: every page by its arrow to the next, then back to the first, then the card closed.</summary>
        private static string TurnThePrivacyPages(GameSession session)
        {
            Press(session, MuseumOf(session).PrivacyRowPixel, "the settings card's privacy row");
            (int Page, int Pages) shown = MuseumOf(session).PrivacyPageShown ?? throw new InvalidOperationException("the privacy row did not bring the privacy card");
            int pages = shown.Pages;
            for (int page = 2; page <= pages; page++)
            {
                Press(session, MuseumOf(session).PrivacyNextPagePixel, $"the privacy card's arrow to page {page}");
                Require(MuseumOf(session).PrivacyPageShown == (page, pages), $"the arrow showed {MuseumOf(session).PrivacyPageShown}, not page {page} of {pages}");
            }

            Require(MuseumOf(session).PrivacyNextPagePixel == null, "the privacy card's last page offers a next one");
            for (int page = pages - 1; page >= 1; page--)
            {
                Press(session, MuseumOf(session).PrivacyPreviousPagePixel, $"the privacy card's arrow back to page {page}");
            }

            Require(MuseumOf(session).PrivacyPageShown == (1, pages) && MuseumOf(session).PrivacyPreviousPagePixel == null, "the privacy card's arrows did not lead back to its first page");
            Press(session, MuseumOf(session).CardClosePixel, "the privacy card's round button");
            return $"the privacy card's {pages} pages turn to the end and back";
        }

        /// <summary>From the hall, through the level's card, the level played to its end by the strong bot; an offer of more moves is turned down.</summary>
        private static GameStatus PlayALevelToItsEnd(GameSession session)
        {
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            IBotPolicy expert = BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot) ? bot : throw new InvalidOperationException("The bots' list has no expert.");
            FlowStory.PlayTheGame(session, expert, new Rng(BotSeed, BotStream));
            StoryStudio.AdvanceUntilSettled(session);
            if (session.Stage.IsOfferingExtraMoves)
            {
                AtTheOffer.GiveUp(session);
            }

            return session.Stage.Game.Status;
        }

        /// <summary>No note carries the phone's id, as it is or as the cards show it.</summary>
        private static void RequireNoId(RecordedNotes notes, GameSession session)
        {
            string[] ids = { StoryStudio.Device.Value, SettingsWords.PlayerId(StoryStudio.Device) };
            foreach (PlayNote note in notes.Notes)
            {
                Require(TheNotesThereAre.Contains(note.Name), $"the note {note.Name} is none of the notes the privacy card names");
                Require(
                    note.Properties.Values.All(value => !(value is string text) || ids.All(id => !text.Contains(id))),
                    $"the note {note} carries the phone's id");
            }

            Require(session.Progress.Purchases.Transactions.All(transaction => notes.Notes.All(note => !note.ToString().Contains(transaction))), "a note carries a transaction's id");
        }

        /// <summary>The game's own session in the hall of a player on level 25, with the tools' scripted store and a recording in the place of the statistics' package.</summary>
        private static GameSession SessionOf(ArtCatalogue art, IReadOnlyList<LevelDefinition> levels, PaintingGallery paintings, ISettingsStore settings, RecordedNotes notes)
        {
            // A scene of its own that is never saved: nothing of an earlier session is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EconomyRules rules = StoryStudio.Economy;
            var session = new GameSession(
                levels, art, paintings, rules, StoryStudio.Clock, new ProgressInMemory(StoryStudio.StandingOn(levels[LevelToStandOn - 1], levels)),
                StoryStudio.AttemptsKeptInMemory(), settings, DeviceAbilities.OfThisBuild, CuePlayer.Silent,
                ScriptedStore.AtUsPrices(rules.Shop.Store, Application.identifier, offersRestore: false), PlayStatistics.Through(notes), Phone, SessionOpening.InTheMuseum);
            StoryStudio.AdvanceUntilSettled(session);
            return session;
        }

        private static MuseumStage MuseumOf(GameSession session) =>
            session.Museum ?? throw new InvalidOperationException("The session is not in the museum, where the check expected it.");

        private static void Press(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            session.PointerUp();
            StoryStudio.AdvanceUntilSettled(session);
        }

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }

        /// <summary>What the gate handed over and how it turned the sending, in order: "on", "off", or a note's name.</summary>
        private sealed class RecordedNotes : IStatisticsSink
        {
            public List<string> Said { get; } = new List<string>();

            public List<PlayNote> Notes { get; } = new List<PlayNote>();

            public void Send(PlayNote note)
            {
                Notes.Add(note);
                Said.Add(note.Name);
            }

            public void Turn(bool isOn) => Said.Add(isOn ? "on" : "off");

            public override string ToString() => string.Join(", ", Said);
        }
    }
}
