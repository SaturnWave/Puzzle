#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Stage;
using Puzzle.Core.Levels;
using Puzzle.Core.Recording;
using Puzzle.Meta;
using Puzzle.Meta.Settings;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// What the storyboard commands share: moving a session's clock the way a phone moves it, and writing what its
    /// camera sees. The clock is moved in the steps of a 60 Hz screen, so a picture shows what a frame of the game
    /// shows, and the same session and the same moments always give the same pictures.
    /// </summary>
    internal static class StoryStudio
    {
        public const string ShotsFolder = "../out/unity/shots";

        /// <summary>The clock is moved in the steps a phone would move it.</summary>
        public const float FrameSeconds = 1f / 60f;

        /// <summary>A stage that still moves after this long never comes to rest: a mistake, not a long turn.</summary>
        private const float LongestSettleSeconds = 30f;

        /// <summary>A sheet shows its pictures at a third of their size.</summary>
        public const int SheetShrinksBy = 3;

        private static readonly Lazy<EconomyRules> PackedEconomy = new Lazy<EconomyRules>(EconomyPack.LoadPacked);

        /// <summary>The economy the game is packed with: the tools' sessions keep their attempts by the game's own rules.</summary>
        /// <exception cref="EconomyFormatException">The project carries no economy pack the game can play by.</exception>
        public static EconomyRules Economy => PackedEconomy.Value;

        /// <summary>
        /// The tools' wall clock: always a Wednesday noon in Istanbul, so what a picture or a check shows of lives
        /// and days does not depend on the hour it was made at.
        /// </summary>
        public static IWallClock Clock { get; } = new FixedWallClock(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3)));

        /// <summary>The tools' phone: an id of its own, which no phone's random id is likely to be.</summary>
        public static DeviceId Device { get; } = new DeviceId("00000000000000000000000000000000");

        /// <summary>An attempt log that forgets, for a tool's session: the tools must not write a player's log.</summary>
        public static IAttemptLog AttemptsKeptInMemory() => new AttemptsInMemory(Device);

        /// <summary>The progress of a new player who stands on a level, never having tried it.</summary>
        /// <exception cref="ArgumentException">The pack has no such level.</exception>
        public static PlayerProgress StandingOn(LevelDefinition level, IReadOnlyList<LevelDefinition> levels)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index].Id.Value == level.Id.Value)
                {
                    return new PlayerProgress(levelsWon: index, attemptsOnLevel: 0).WithWallet(Economy.StartingWallet);
                }
            }

            throw new ArgumentException($"The pack has no level {level.Id.Value}.", nameof(level));
        }

        /// <summary>
        /// The game's own session at the board of the level its progress stands on, as the board studies need it:
        /// the museum the game opens in is not their subject.
        /// </summary>
        public static GameSession AtTheBoard(IReadOnlyList<LevelDefinition> levels, ArtCatalogue art, IProgressStore store, ScreenShape screen) =>
            new GameSession(levels, art, PaintingGallery.OfThePackedMuseum(), Economy, Clock, store, AttemptsKeptInMemory(), screen, SessionOpening.AtTheBoard);

        /// <summary>Moves the session's clock frame by frame for a while.</summary>
        public static void AdvanceBy(float seconds, GameSession session) => AdvanceTo(seconds, session, clock: 0f);

        /// <summary>Moves the session's clock frame by frame up to a moment; returns where the clock then stands.</summary>
        /// <param name="clock">Where the clock stands now, counted from the same beginning as the moment.</param>
        public static float AdvanceTo(float seconds, GameSession session, float clock)
        {
            while (clock + FrameSeconds / 2f < seconds)
            {
                session.Advance(FrameSeconds);
                clock += FrameSeconds;
            }

            return clock;
        }

        /// <summary>Moves the session's clock frame by frame until nothing on the stage it shows moves; returns how long that took.</summary>
        /// <exception cref="InvalidOperationException">The stage does not come to rest.</exception>
        public static float AdvanceUntilSettled(GameSession session)
        {
            float waited = 0f;
            while (!session.Front.IsSettled)
            {
                if (waited > LongestSettleSeconds)
                {
                    throw new InvalidOperationException($"The stage is still moving after {LongestSettleSeconds} s.");
                }

                session.Advance(FrameSeconds);
                waited += FrameSeconds;
            }

            return waited;
        }

        /// <summary>Writes what the game's camera sees now as a file in the shots folder, and adds it to a sheet.</summary>
        public static void Shoot(GameSession session, ScreenShape screen, ContactSheet sheet, string file) => Shoot(session.Front, screen, sheet, file);

        /// <summary>Writes what a stage's camera sees now as a file in the shots folder (or in a folder under it, named with the file), and adds it to a sheet.</summary>
        public static void Shoot(IStage stage, ScreenShape screen, ContactSheet sheet, string file)
        {
            Texture2D picture = Take(stage, screen);
            try
            {
                Keep(picture, sheet, file);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(picture);
            }
        }

        /// <summary>What a stage's camera sees now, as a picture that is not written anywhere yet. The caller owns it and destroys it.</summary>
        public static Texture2D Take(IStage stage, ScreenShape screen) => CameraPicture.Take(stage.Camera, screen.PixelWidth, screen.PixelHeight);

        /// <summary>Writes a picture that was taken earlier as a file in the shots folder, and adds it to a sheet. The picture stays the caller's.</summary>
        public static void Keep(Texture2D picture, ContactSheet sheet, string file)
        {
            CameraPicture.WritePng(picture, Path.Combine(ShotsFolder, file));
            sheet.Add(picture);
            Debug.Log($"Storyboard: {file}");
        }

        /// <summary>Writes a sheet's pictures side by side as one file in the shots folder; a sheet without pictures writes none.</summary>
        public static void WriteSheet(ContactSheet sheet, string file)
        {
            if (sheet.IsEmpty)
            {
                return;
            }

            Texture2D picture = sheet.ToPicture();
            try
            {
                CameraPicture.WritePng(picture, Path.Combine(ShotsFolder, file));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(picture);
            }
        }

        /// <summary>Seconds as a file name and the log write them: 0.25.</summary>
        public static string Seconds(float seconds) => seconds.ToString("F2", CultureInfo.InvariantCulture);
    }
}
