#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Stage;
using Puzzle.Client.Words;
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

        /// <summary>Where the tools keep the records of their stand-in phone (<see cref="RecordsOnAPhone"/>), and the real lines they are made of.</summary>
        private const string PhoneFolder = "../out/unity/phone/Android/data/com.saturnwave.puzzle/files";
        private const string RecordsFixture = "../tests/fixtures/attempt-records.first-shuffle-rule.jsonl";

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

        /// <summary>The board's shelf of a first try at the game's level, by the economy the tools play by.</summary>
        public static ShelfBrief ShelfOf(Puzzle.Core.Engine.Game game) => ShelfBrief.Of(game.Level.Id.Value, attemptsBefore: 0, Economy);

        /// <summary>
        /// What the design canvas' player holds on the board's shelf (BoardTR): three spatulas, two of each brush, no
        /// shuffle, the three start boosters the welcome gift gives, 1.250 coins and every life.
        /// </summary>
        public static WalletView CanvasWallet => WalletView.Of(CanvasPlayersWallet, Economy.Lives, Clock.Now);

        /// <summary>What the extra moves cost the design canvas' player at the tools' instant: 1.250 coins cover them, and no ad was watched today.</summary>
        public static ExtraMovesTerms CanvasExtraMoves => ExtraMovesFor(CanvasPlayersWallet);

        /// <summary>What the extra moves cost a wallet at the tools' instant, by the economy the tools play by.</summary>
        public static ExtraMovesTerms ExtraMovesFor(Wallet wallet) => Economy.ExtraMoves.TermsFor(wallet, DayStamp.Of(Clock.Now));

        /// <summary>The design canvas' player's wallet itself (BoardTR): what <see cref="CanvasWallet"/> shows.</summary>
        public static Wallet CanvasPlayersWallet => Economy.StartingWallet
            .WithCoins(new Coins(1250))
            .WithShelf(Economy.StartingWallet.Shelf.With(BoosterKind.HitCell, 3).With(BoosterKind.HitRow, 2).With(BoosterKind.HitColumn, 2));

        /// <summary>An attempt log that forgets, for a tool's session: the tools must not write a player's log.</summary>
        public static IAttemptLog AttemptsKeptInMemory() => new AttemptsInMemory(Device);

        /// <summary>
        /// The tools' stand-in for a phone's records: an attempt log in a folder of the tools laid out as a phone's
        /// data folder is, so a picture shows a path as long as a phone's, begun anew with so many lines of real
        /// records (the game's lines of tests/fixtures/attempt-records.first-shuffle-rule.jsonl, over and over).
        /// </summary>
        /// <exception cref="IOException">The folder could not be made anew, or the records could not be written.</exception>
        public static AttemptLogFile RecordsOnAPhone(int records)
        {
            if (Directory.Exists(PhoneFolder))
            {
                Directory.Delete(PhoneFolder, recursive: true);
            }

            Directory.CreateDirectory(PhoneFolder);
            string[] game = File.ReadAllLines(RecordsFixture);
            var lines = new string[records];
            for (int line = 0; line < records; line++)
            {
                lines[line] = game[line % game.Length];
            }

            var log = new AttemptLogFile(PhoneFolder, Device);
            File.WriteAllLines(log.Path, lines);
            return log;
        }

        /// <summary>
        /// The phone as a tool's pictures show it without a session: a first start's settings, a row for each setting
        /// the abilities name, the tools' id, this build's version and its newest levels, the records of a log, and the
        /// language the tools speak among those the game offers.
        /// </summary>
        public static DeviceView PhoneWith(IAttemptLog log, IReadOnlyList<LevelDefinition> levels, DeviceAbilities abilities) =>
            new DeviceView(
                DeviceSettings.Default, abilities.Settings, log.Device, Application.version, levels.Select(level => level.ContentVersion).Max(),
                log.CountRecords(), log.FilePath, Economy.Ads, Economy.Shop.RealMoney, Speech.Language, WordsPack.Offered);

        /// <summary>
        /// The progress of a new player who stands on a level, never having tried it: the welcome gift and the gift of
        /// every booster of play that opened on the way there, as a player who came so far was given them, so a session
        /// that opens on it has nothing more to give.
        /// </summary>
        /// <exception cref="ArgumentException">The pack has no such level.</exception>
        public static PlayerProgress StandingOn(LevelDefinition level, IReadOnlyList<LevelDefinition> levels)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index].Id.Value == level.Id.Value)
                {
                    return new PlayerProgress(levelsWon: index, attemptsOnLevel: 0).WithWallet(Economy.Unlocks.GivenTo(Economy.StartingWallet, index + 1));
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
