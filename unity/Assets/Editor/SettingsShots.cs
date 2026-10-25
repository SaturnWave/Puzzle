#nullable enable
using System;
using System.IO;
using System.Linq;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Purchases;
using Puzzle.Client.Screens;
using Puzzle.Client.Statistics;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The pictures of the settings card and the three cards it opens, on the game's own session and by touches alone:
    /// the hall with the settings card come down from the gear, as the build has it and as a device with nothing to buzz
    /// has it (an iPad: no switch of vibration); the privacy card in front of it with the tools' stand-in
    /// records, its question, and the card once "Sil" has deleted them; the support card; the language card. The records lie in a folder
    /// of the tools laid out as a phone's (<see cref="StoryStudio.RecordsOnAPhone"/>), so the support card shows a
    /// path as long as a phone's. And the settings card with its switch of vibration turned off, drawn on the museum's
    /// stage alone (the page "settingspreview", named when the build did not honour the switches yet).
    /// </summary>
    internal static class SettingsShots
    {
        /// <summary>How many records the tools' stand-in phone keeps.</summary>
        public const int RecordsOnThePhone = 37;

        /// <summary>
        /// The settings card with every switch the build honours, and again on a device with nothing to buzz (an iPad,
        /// a tablet without a vibration motor), where the game leaves the switch of vibration out.
        /// </summary>
        /// <returns>How many pictures were taken.</returns>
        /// <exception cref="InvalidOperationException">The gear did not bring the card, or the card shows other switches than the device's.</exception>
        public static int ShootTheSettings(PlayerState state, ScreenShape screen, Studio studio)
        {
            ShootTheSettingsOf(DeviceAbilities.OfThisBuild, "settings", state, screen, studio);
            ShootTheSettingsOf(DeviceAbilities.OfThisBuildWithoutVibrator, "settings-without-vibrator", state, screen, studio);
            return 2;
        }

        /// <returns>How many pictures were taken.</returns>
        /// <exception cref="InvalidOperationException">A press did not lead where it should, or the records were not deleted.</exception>
        public static int ShootThePrivacy(PlayerState state, ScreenShape screen, Studio studio)
        {
            AttemptLogFile records = StoryStudio.RecordsOnAPhone(RecordsOnThePhone);
            using GameSession session = SessionInTheHall(state, screen, studio, records);
            MuseumStage museum = OpenTheSettings(session);
            Press(session, museum.PrivacyRowPixel, "the settings card's privacy row");
            Require(museum.PrivacyShown?.Count == PrivacyWords.RecordsKept(RecordsOnThePhone), $"the privacy card counts \"{museum.PrivacyShown?.Count}\", not {RecordsOnThePhone} records");
            ScreenShots.WritePicture(museum, screen, studio.FileName("privacy", state, screen));
            int pages = museum.PrivacyPageShown?.Pages ?? 1;
            for (int page = 2; page <= pages; page++)
            {
                Press(session, museum.PrivacyNextPagePixel, $"the privacy card's arrow to page {page}");
                Require(museum.PrivacyPageShown == (page, pages), $"the privacy card's arrow showed {museum.PrivacyPageShown}, not page {page} of {pages}");
                ScreenShots.WritePicture(museum, screen, studio.FileName($"privacy-page-{page}", state, screen));
            }

            Press(session, museum.DeleteRecordsPixel, "the privacy card's Kayıtları sil");
            Require(museum.PrivacyShown is (_, true), "Kayıtları sil did not ask first");
            ScreenShots.WritePicture(museum, screen, studio.FileName("privacy-question", state, screen));

            Press(session, museum.ConfirmDeletePixel, "the question's Sil");
            Require(museum.PrivacyShown == (PrivacyWords.RecordsKept(0), false), $"after Sil the privacy card shows {museum.PrivacyShown}");
            Require(!File.Exists(records.Path) && records.CountRecords() == 0, "Sil left records on the stand-in phone");
            ScreenShots.WritePicture(museum, screen, studio.FileName("privacy-deleted", state, screen));
            return 2 + pages;
        }

        /// <summary>
        /// The question about the play statistics as a first start brings it down over the hall, and the settings card
        /// after its "İzin ver", the statistics' switch on and the privacy row saying that statistics leave too.
        /// </summary>
        /// <returns>How many pictures were taken.</returns>
        /// <exception cref="InvalidOperationException">The question did not come, or its answer did not reach the settings card.</exception>
        public static int ShootTheConsent(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = new SettingsInMemory(DeviceSettings.Default);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), StoryStudio.AttemptsKeptInMemory(),
                settings, DeviceAbilities.OfThisBuild, CuePlayer.Silent, ScriptedStore.AtUsPrices(StoryStudio.Economy.Shop.Store, Application.identifier, offersRestore: false),
                PlayStatistics.Through(new NotesToTheLog(Debug.Log, "the tools send nothing")), screen, SessionOpening.InTheMuseum);
            StoryStudio.AdvanceUntilSettled(session);
            MuseumStage museum = session.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
            Require(museum.ShowsTheStatisticsQuestion, "a first start did not bring the statistics question down");
            ScreenShots.WritePicture(museum, screen, studio.FileName("consent", state, screen));

            Press(session, museum.AllowTheStatisticsPixel, "the statistics question's İzin ver");
            Require(settings.Settings.Statistics == StatisticsChoice.Allowed && !museum.ShowsTheStatisticsQuestion, "İzin ver did not keep a yes and take the question up");
            Press(session, museum.GearPixel, "the wallet row's gear");
            Require(museum.SwitchesShown.Contains((DeviceSetting.Statistics, true)), "the settings card does not show the statistics' switch on after İzin ver");
            ScreenShots.WritePicture(museum, screen, studio.FileName("settings-statistics-on", state, screen));
            return 2;
        }

        /// <returns>How many pictures were taken.</returns>
        public static int ShootTheSupport(PlayerState state, ScreenShape screen, Studio studio)
        {
            using GameSession session = SessionInTheHall(state, screen, studio, StoryStudio.RecordsOnAPhone(RecordsOnThePhone));
            MuseumStage museum = OpenTheSettings(session);
            Press(session, museum.SupportRowPixel, "the settings card's support row");
            Require(museum.SettingsCardInFront == SettingsCardKind.Support, "the support row did not bring the support card");
            ScreenShots.WritePicture(museum, screen, studio.FileName("support", state, screen));
            return 1;
        }

        /// <returns>How many pictures were taken.</returns>
        public static int ShootTheLanguages(PlayerState state, ScreenShape screen, Studio studio)
        {
            using GameSession session = SessionInTheHall(state, screen, studio, StoryStudio.AttemptsKeptInMemory());
            MuseumStage museum = OpenTheSettings(session);
            Press(session, museum.LanguageRowPixel, "the settings card's language row");
            Require(museum.SettingsCardInFront == SettingsCardKind.Language, "the language row did not bring the language card");
            ScreenShots.WritePicture(museum, screen, studio.FileName("languages", state, screen));
            return 1;
        }

        /// <summary>The settings card with the build's switches, the vibration's turned off, on the museum's stage alone.</summary>
        /// <returns>How many pictures were taken.</returns>
        public static int ShootThePreview(PlayerState state, ScreenShape screen, Studio studio)
        {
            using MuseumStage museum = ScreenShots.OpenTheMuseum(
                MuseumDoor.Home, state, screen, studio, StoryStudio.PhoneWith(StoryStudio.AttemptsKeptInMemory(), studio.Levels, DeviceAbilities.OfThisBuild));
            StageSteps.Settle(museum);
            PressOnTheStage(museum, museum.GearPixel, "the wallet row's gear");
            PressOnTheStage(museum, museum.SwitchPixel(DeviceSetting.Vibration), "the switch of vibration");
            Require(
                museum.SwitchesShown.Count == DeviceAbilities.OfThisBuild.Settings.Count && museum.SwitchesShown.Contains((DeviceSetting.Vibration, false)),
                "the vibration's switch did not turn off");
            ScreenShots.WritePicture(museum, screen, studio.FileName("settings-preview-switches", state, screen));
            return 1;
        }

        private static void ShootTheSettingsOf(DeviceAbilities abilities, string name, PlayerState state, ScreenShape screen, Studio studio)
        {
            using GameSession session = SessionInTheHall(state, screen, studio, StoryStudio.AttemptsKeptInMemory(), abilities);
            MuseumStage museum = OpenTheSettings(session);
            Require(
                museum.SwitchesShown.Count == abilities.Settings.Count,
                $"the settings card shows {museum.SwitchesShown.Count} switches, not the device's {abilities.Settings.Count}");
            ScreenShots.WritePicture(museum, screen, studio.FileName(name, state, screen));
        }

        private static GameSession SessionInTheHall(PlayerState state, ScreenShape screen, Studio studio, IAttemptLog records) =>
            SessionInTheHall(state, screen, studio, records, DeviceAbilities.OfThisBuild);

        private static GameSession SessionInTheHall(PlayerState state, ScreenShape screen, Studio studio, IAttemptLog records, DeviceAbilities abilities)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), records,
                new SettingsInMemory(DeviceSettings.Default), abilities, CuePlayer.Silent, screen, SessionOpening.InTheMuseum);
            StoryStudio.AdvanceUntilSettled(session);
            return session;
        }

        private static MuseumStage OpenTheSettings(GameSession session)
        {
            MuseumStage museum = session.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
            Press(session, museum.GearPixel, "the wallet row's gear");
            Require(museum.SettingsCardInFront == SettingsCardKind.Settings, "the gear did not bring the settings card");
            return museum;
        }

        private static void Press(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            session.PointerUp();
            StoryStudio.AdvanceUntilSettled(session);
        }

        private static void PressOnTheStage(MuseumStage museum, Vector2? pixel, string what)
        {
            museum.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            museum.PointerUp();
            StageSteps.Settle(museum);
        }

        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(otherwise);
            }
        }
    }
}
