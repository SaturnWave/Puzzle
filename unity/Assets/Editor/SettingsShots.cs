#nullable enable
using System;
using System.IO;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The pictures of the settings card and the two cards it opens, on the game's own session and by touches alone:
    /// the hall with the settings card come down from the gear; the privacy card in front of it with the tools' stand-in
    /// records, its question, and the card once "Sil" has deleted them; the support card. The records lie in a folder
    /// of the tools laid out as a phone's (<see cref="StoryStudio.RecordsOnAPhone"/>), so the support card shows a
    /// path as long as a phone's. And a look ahead: the settings card with the switches of sound and vibration, which
    /// this build does not honour yet, drawn on the museum's stage alone.
    /// </summary>
    internal static class SettingsShots
    {
        /// <summary>How many records the tools' stand-in phone keeps.</summary>
        public const int RecordsOnThePhone = 37;

        /// <returns>How many pictures were taken.</returns>
        public static int ShootTheSettings(PlayerState state, ScreenShape screen, Studio studio)
        {
            using GameSession session = SessionInTheHall(state, screen, studio, StoryStudio.AttemptsKeptInMemory());
            MuseumStage museum = OpenTheSettings(session);
            ScreenShots.WritePicture(museum, screen, studio.FileName("settings", state, screen));
            return 1;
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

            Press(session, museum.DeleteRecordsPixel, "the privacy card's Kayıtları sil");
            Require(museum.PrivacyShown is (_, true), "Kayıtları sil did not ask first");
            ScreenShots.WritePicture(museum, screen, studio.FileName("privacy-question", state, screen));

            Press(session, museum.ConfirmDeletePixel, "the question's Sil");
            Require(museum.PrivacyShown == (PrivacyWords.RecordsKept(0), false), $"after Sil the privacy card shows {museum.PrivacyShown}");
            Require(!File.Exists(records.Path) && records.CountRecords() == 0, "Sil left records on the stand-in phone");
            ScreenShots.WritePicture(museum, screen, studio.FileName("privacy-deleted", state, screen));
            return 3;
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

        /// <summary>The settings card with two switches, the second turned off, on the museum's stage alone: how it will look once the build honours them.</summary>
        /// <returns>How many pictures were taken.</returns>
        public static int ShootThePreview(PlayerState state, ScreenShape screen, Studio studio)
        {
            var twoSwitches = new DeviceAbilities(DeviceSetting.Sound, DeviceSetting.Vibration);
            using MuseumStage museum = ScreenShots.OpenTheMuseum(
                MuseumDoor.Home, state, screen, studio, StoryStudio.PhoneWith(StoryStudio.AttemptsKeptInMemory(), studio.Levels, twoSwitches));
            StageSteps.Settle(museum);
            PressOnTheStage(museum, museum.GearPixel, "the wallet row's gear");
            PressOnTheStage(museum, museum.SwitchPixel(DeviceSetting.Vibration), "the switch of vibration");
            Require(museum.SwitchesShown.Count == 2 && museum.SwitchesShown[1] == (DeviceSetting.Vibration, false), "the vibration's switch did not turn off");
            ScreenShots.WritePicture(museum, screen, studio.FileName("settings-preview-two-switches", state, screen));
            return 1;
        }

        private static GameSession SessionInTheHall(PlayerState state, ScreenShape screen, Studio studio, IAttemptLog records)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), records,
                new SettingsInMemory(DeviceSettings.Default), DeviceAbilities.OfThisBuild, screen, SessionOpening.InTheMuseum);
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
