#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Screens;
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
    /// Checks from the command line that the phone's attempt records do what the game relies on, with Unity's own file
    /// calls and JSON reader. The device file makes a random id on the first start, in the layout the tests pin, with a
    /// first start's settings, and a restart reads the same id; a setting kept is written at once and survives a
    /// restart, also a switch flipped by touches on the settings card of the game's own session (given the two
    /// switches the sounds will bring), which a session opened anew shows as it was left; a file of the first layout
    /// keeps its id and reads with a first start's settings; a damaged device file
    /// is moved aside for a new id and costs neither the progress nor the wallet beside it, and a later game's file is
    /// left as it is, a setting changed on it included. Then a won, a lost and a left attempt are played by bot
    /// through the game's own session, on a log of its
    /// own: each is a line in the log the moment it ends for good (the won one at its deciding move, before any of it is
    /// shown; the lost one when its offer of extra moves is given up, never while it hangs; the left one at the press of
    /// the leaving card's museum button), and a level left before its first move leaves no line. The offer is taken by
    /// touches too, paid with coins and with an ad: no line while it hangs or when the moves are given, and one that
    /// says they were taken when the attempt ends; and a board made by hand that no shuffle brings back is refused
    /// them, its line ending with the refusal. An attempt gone into by touches through the level's card, with two start boosters chosen
    /// there, is played by bot too: closing the card and leaving the board before a move spend nothing and write
    /// nothing, and the attempt played to its end spends the two at its first move and is a line that names them. An
    /// attempt in which every booster of play is used by touches on the shelf, the first of them before any move, is
    /// played to its end by bot: the booster counts as the first move, every one is spent, and its line names the
    /// shuffle rule of today. An attempt with all three start boosters, every booster of play and the extra moves,
    /// is played on the engine alone and logged the same way. An attempt played in the tester mode, left after a move,
    /// takes no life and no coin and its line alone is marked ("testerMode":true). A new player on the spatula's level
    /// is given its three, once, in the save file. Last, the file is read back with Unity's reader, and every record is played again on
    /// the engine (Puzzle.Core's AttemptReplay) to the end that was seen: the same board, status, moves used and goals;
    /// and the records written before the shuffle rule changed (tests/fixtures/attempt-records.first-shuffle-rule.jsonl),
    /// which name no rule, are read the same way and replay by the first rule to their end. It works in a folder of its
    /// own (out/unity/attempt-log). The damaged and the later device file write three warnings to the log on purpose.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.AttemptLogCheck.Run
    /// </summary>
    public static class AttemptLogCheck
    {
        private const string Folder = "../out/unity/attempt-log";

        /// <summary>A level the strong bot wins, one the random bot loses, one to leave, as the flow storyboards play them.</summary>
        private const int WonLevel = 1;
        private const int LostLevel = 40;
        private const int LeftLevel = 47;

        /// <summary>A bot does not always end a level the way the check needs; after this many attempts it is the wrong bot for the level.</summary>
        private const int MostAttempts = 8;

        /// <summary>Fifty refused shuffles in a row would be a board no shuffle rearranges.</summary>
        private const int MostShuffleRequests = 50;

        /// <summary>A press that leads nowhere within this long led nowhere.</summary>
        private const float LongestWait = 3f;

        private const ulong BotSeed = 20261014;
        private const ulong BotStream = 3;

        /// <summary>A later game's device file, with an id and settings this game reads and something it does not know.</summary>
        private const string LaterDeviceDocument = "{\"version\":5,\"id\":\"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a\",\"settings\":{\"sound\":false,\"vibration\":true,\"testerMode\":false,\"language\":\"en\",\"music\":false}}";

        /// <summary>Records the game wrote before the shuffle booster's rule changed, which name no rule.</summary>
        private const string FirstRuleRecords = "../tests/fixtures/attempt-records.first-shuffle-rule.jsonl";

        /// <summary>The level every booster of play is open on, and the spatula's own.</summary>
        private const int OpenLevel = 40;
        private const int SpatulasLevel = 7;

        private static readonly ScreenShape Screen = new ScreenShape(1080, 2340);

        private static string LogPath => Path.Combine(Folder, AttemptLogFile.FileName);

        public static void Run()
        {
            try
            {
                // A scene of its own that is never saved: nothing of an earlier run is left in this one.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                StartWithAnEmptyFolder();
                ADamagedDeviceFileGivesANewIdAndALaterOneIsLeftAsItIs();
                ASettingKeptSurvivesARestartAndAnOlderFileReadsWithTheSettingsOfAFirstStart();
                DeviceId device = TheFirstStartMakesTheIdAndARestartReadsIt();

                var bench = new Bench(device);
                ASwitchFlippedOnTheCardIsKeptAtOnceAndARestartShowsIt(bench);
                var ended = new List<Ending>();
                PlayUntil(bench, WonLevel, status => status == GameStatus.Won, Bot(SkillProfile.Expert.Name), ended);
                PlayUntil(bench, LostLevel, status => status.IsLoss(), new RandomPolicy(), ended);
                LeaveBeforeAndAfterAMove(bench, ended);
                PlayWithStartBoostersChosenOnTheCard(bench, ended);
                PlayWithEveryBoosterOnTheShelf(bench, ended);
                PlayThroughTheOffer(bench, ended);
                PlayWithEveryBoosterOnTheEngine(bench, ended);
                AnAttemptInTheTesterModeIsMarkedAndCostsNothing(bench, ended);
                ANewPlayerOnTheSpatulasLevelIsGivenItsThreeOnce(bench);

                int replayed = EveryLineReadsBackAndReplaysToTheEndSeen(bench, ended);
                int beforeTheRule = RecordsBeforeTheShuffleRuleReplayByTheFirstRule(bench);
                Debug.Log($"Attempt log check passed: device {device}; {replayed} records in {LogPath} ({string.Join("; ", ended.Select(end => end.What))}), each read back with Unity's reader and replayed on the engine to the end seen; {beforeTheRule} records written before the shuffle rule changed replayed by the first rule.");
            }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException || failure is InvalidOperationException
                || failure is ArgumentException || failure is AttemptReplayException || failure is LevelFormatException)
            {
                Debug.LogError($"Attempt log check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static void StartWithAnEmptyFolder()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }

            Directory.CreateDirectory(Folder);
        }

        /// <exception cref="InvalidOperationException">The device file was not written as the tests pin it, or a restart read another id or other settings.</exception>
        private static DeviceId TheFirstStartMakesTheIdAndARestartReadsIt()
        {
            DeviceId made = DeviceFile.LoadOrMake(Folder).Device;
            string written = File.ReadAllText(Path.Combine(Folder, DeviceFile.FileName));
            Require(written == "{\"version\":4,\"id\":\"" + made.Value + "\",\"settings\":{\"sound\":true,\"vibration\":true,\"testerMode\":false,\"language\":\"\"}}", $"the first start wrote the device file as {written}");
            DeviceFile afterARestart = DeviceFile.LoadOrMake(Folder);
            Require(afterARestart.Device.Equals(made), $"a restart read the id {afterARestart.Device}, not {made}");
            Require(afterARestart.Settings.Equals(DeviceSettings.Default), $"a restart read the settings {afterARestart.Settings}, not a first start's");
            return made;
        }

        /// <summary>
        /// A setting kept is in the file at once and a restart reads it with the same id; a file of the first layout
        /// keeps its id and reads with a first start's settings until one is changed.
        /// </summary>
        /// <exception cref="InvalidOperationException">A setting was not written at once, did not survive a restart, or the first layout lost its id.</exception>
        private static void ASettingKeptSurvivesARestartAndAnOlderFileReadsWithTheSettingsOfAFirstStart()
        {
            string folder = Path.Combine(Folder, "device-settings");
            DeviceFile phone = DeviceFile.LoadOrMake(folder);
            DeviceSettings quiet = DeviceSettings.Default.With(DeviceSetting.Sound, isOn: false);
            phone.Keep(quiet);
            string written = File.ReadAllText(Path.Combine(folder, DeviceFile.FileName));
            Require(written.Contains("\"sound\":false"), $"the setting kept was not written at once: {written}");
            DeviceFile afterARestart = DeviceFile.LoadOrMake(folder);
            Require(afterARestart.Settings.Equals(quiet) && afterARestart.Device.Equals(phone.Device), $"a restart read {afterARestart.Settings} of {afterARestart.Device}, not {quiet} of {phone.Device}");

            const string firstLayout = "{\"version\":1,\"id\":\"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a\"}";
            string older = Path.Combine(Folder, "device-first-layout");
            Directory.CreateDirectory(older);
            string olderFile = Path.Combine(older, DeviceFile.FileName);
            File.WriteAllText(olderFile, firstLayout);
            DeviceFile read = DeviceFile.LoadOrMake(older);
            Require(read.Device.Value == "6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a" && read.Settings.Equals(DeviceSettings.Default), $"a file of the first layout read as {read.Settings} of {read.Device}");
            Require(File.ReadAllText(olderFile) == firstLayout, "a file of the first layout was written before anything was changed");
            read.Keep(quiet);
            Require(File.ReadAllText(olderFile).StartsWith("{\"version\":4,\"id\":\"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a\"", StringComparison.Ordinal), "a setting changed on a file of the first layout did not write this layout with its id");
        }

        /// <summary>
        /// A switch flipped on the settings card, through the game's own session and by touches, is in the device file
        /// at once, and a session opened anew on the same file shows it as it was left. The build honours no setting
        /// yet, so the session is given the two switches the sounds will bring; the game's card has none until then.
        /// </summary>
        /// <exception cref="InvalidOperationException">A press led nowhere, the setting was not written at once, or a restart showed it otherwise.</exception>
        private static void ASwitchFlippedOnTheCardIsKeptAtOnceAndARestartShowsIt(Bench bench)
        {
            string folder = Path.Combine(Folder, "device-switch");
            var twoSwitches = new DeviceAbilities(DeviceSetting.Sound, DeviceSetting.Vibration);
            using (GameSession session = bench.SessionOnThePhone(LeftLevel, DeviceFile.LoadOrMake(folder), twoSwitches))
            {
                StoryStudio.AdvanceUntilSettled(session);
                MuseumStage museum = session.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
                Press(session, museum.GearPixel, "the wallet row's gear");
                StoryStudio.AdvanceUntilSettled(session);
                Press(session, museum.SwitchPixel(DeviceSetting.Vibration), "the settings card's switch of vibration");

                // At once: before the knob has slid across, the file holds it.
                string written = File.ReadAllText(Path.Combine(folder, DeviceFile.FileName));
                Require(written.Contains("\"sound\":true,\"vibration\":false"), $"the switch flipped on the card was not written at once: {written}");
            }

            using (GameSession again = bench.SessionOnThePhone(LeftLevel, DeviceFile.LoadOrMake(folder), twoSwitches))
            {
                StoryStudio.AdvanceUntilSettled(again);
                MuseumStage museum = again.Museum ?? throw new InvalidOperationException("The session did not open in the museum.");
                Press(again, museum.GearPixel, "the wallet row's gear");
                StoryStudio.AdvanceUntilSettled(again);
                IReadOnlyList<(DeviceSetting Setting, bool IsOn)> shown = museum.SwitchesShown;
                Require(
                    shown.Count == 2 && shown[0] == (DeviceSetting.Sound, true) && shown[1] == (DeviceSetting.Vibration, false),
                    $"after a restart the card's switches show {string.Join(", ", shown)}");
            }
        }

        /// <exception cref="InvalidOperationException">A damaged file was written over or kept its id, the progress beside it changed, or a later game's file was changed.</exception>
        private static void ADamagedDeviceFileGivesANewIdAndALaterOneIsLeftAsItIs()
        {
            const string damagedDocument = "{\"version\":1,\"id\":\"ada@example.com\"}";
            string damaged = Path.Combine(Folder, "device-damaged");
            Directory.CreateDirectory(damaged);
            File.WriteAllText(Path.Combine(damaged, DeviceFile.FileName), damagedDocument);
            var progressFile = new ProgressFile(damaged, StoryStudio.Economy.StartingWallet);
            PlayerProgress saved = new PlayerProgress(levelsWon: 12, attemptsOnLevel: 1).WithWallet(StoryStudio.Economy.StartingWallet.WithCoins(new Coins(1250)));
            progressFile.Save(saved);
            string progressBefore = File.ReadAllText(Path.Combine(damaged, ProgressFile.FileName));

            DeviceFile made = DeviceFile.LoadOrMake(damaged);
            Require(File.ReadAllText(Path.Combine(damaged, DeviceFile.FileName + ".damaged")) == damagedDocument, "the damaged device file was not kept aside as it was");
            Require(made.Settings.Equals(DeviceSettings.Default), $"the settings made in place of a damaged file are {made.Settings}");
            Require(DeviceFile.LoadOrMake(damaged).Device.Equals(made.Device), "the id made in place of a damaged file did not read back");
            Require(File.ReadAllText(Path.Combine(damaged, ProgressFile.FileName)) == progressBefore && progressFile.Load().Equals(saved), "a damaged device file cost the progress or the wallet beside it");

            string later = Path.Combine(Folder, "device-later");
            Directory.CreateDirectory(later);
            string laterFile = Path.Combine(later, DeviceFile.FileName);
            File.WriteAllText(laterFile, LaterDeviceDocument);
            DeviceFile kept = DeviceFile.LoadOrMake(later);
            Require(kept.Device.Value == "6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a", $"a later game's id read as {kept.Device}");
            Require(!kept.Settings.Sound, $"a later game's settings read as {kept.Settings}");
            kept.Keep(DeviceSettings.Default);
            Require(File.ReadAllText(laterFile) == LaterDeviceDocument, "a setting changed on a later game's device file was written over it");
            File.WriteAllText(laterFile, "{\"version\":5,\"id\":\"a later spelling\"}");
            DeviceFile.LoadOrMake(later);
            Require(File.ReadAllText(laterFile) == "{\"version\":5,\"id\":\"a later spelling\"}", "a later game's device file was written over");
        }

        /// <summary>
        /// Plays attempts of a level by bot until one ends as asked, each on a session of its own that stands where a
        /// player who had tried the level so often would stand; every attempt that ends is a line in the log at the
        /// move that ends it, before any of it is shown.
        /// </summary>
        /// <exception cref="InvalidOperationException">No line was written at the deciding move, or the bot never ended the level as asked.</exception>
        private static void PlayUntil(Bench bench, int levelNumber, Func<GameStatus, bool> isTheEnding, IBotPolicy bot, List<Ending> ended)
        {
            var rng = new Rng(BotSeed, BotStream);
            for (int attempt = 0; attempt < MostAttempts; attempt++)
            {
                using GameSession session = bench.SessionAt(levelNumber, attempt);
                StoryStudio.AdvanceUntilSettled(session); // the board takes no move while it comes in
                int linesBefore = LinesInTheLog();
                FlowStory.PlayTheGame(session, bot, rng);
                Game game = session.Stage.Game;
                bool isOffered = OutOfMovesOffer.IsMadeIn(game);
                if (isOffered)
                {
                    // The moves ran out: nothing ends the attempt until the offer of five more is answered, here by giving up.
                    Require(LinesInTheLog() == linesBefore, $"a line was written while attempt {attempt} of level {levelNumber} was offered extra moves");
                    AtTheOffer.GiveUp(session);
                }

                ended.Add(Ending.Of(game, $"level {levelNumber} {game.Status} after {game.MovesUsed} moves{(isOffered ? ", the extra moves given up" : string.Empty)}"));
                Require(LinesInTheLog() == linesBefore + 1, $"no line was written when attempt {attempt} of level {levelNumber} was decided for good");
                if (isTheEnding(game.Status))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"{MostAttempts} attempts of level {levelNumber} did not end as the check needs; pick another bot or level.");
        }

        /// <summary>A level left before its first move is no attempt and has no line; left after one, it is abandoned, and has its line.</summary>
        /// <exception cref="InvalidOperationException">A line too many or too few, or the way out did not lead to the museum.</exception>
        private static void LeaveBeforeAndAfterAMove(Bench bench, List<Ending> ended)
        {
            using (GameSession session = bench.SessionAt(LeftLevel, attempt: 0))
            {
                int linesBefore = LinesInTheLog();
                Leave(session);
                Require(LinesInTheLog() == linesBefore, "a level left before its first move was written as an attempt");
            }

            using (GameSession session = bench.SessionAt(LeftLevel, attempt: 0))
            {
                StoryStudio.AdvanceUntilSettled(session);
                Game game = session.Stage.Game;
                Move move = Bot(SkillProfile.Expert.Name).ChooseMove(game, game.FindLegalMoves(), new Rng(BotSeed, BotStream));
                Require(session.TryPlay(move) && game.Status == GameStatus.Playing, $"the move {move} of level {LeftLevel} was refused or decided the level");
                StoryStudio.AdvanceUntilSettled(session);
                ended.Add(Ending.Of(game, $"level {LeftLevel} left after {game.MovesUsed} move"));
                int linesBefore = LinesInTheLog();
                Leave(session);
                Require(LinesInTheLog() == linesBefore + 1, "no line was written when a level was left after a move");
            }
        }

        /// <summary>
        /// The game's own session at a board with the tester mode on in its settings: a move played and the level left
        /// after it take neither a life nor a coin, and the attempt's line is marked; no line before it is.
        /// </summary>
        /// <exception cref="InvalidOperationException">A life or a coin went, the line is not marked, or a line before it is.</exception>
        private static void AnAttemptInTheTesterModeIsMarkedAndCostsNothing(Bench bench, List<Ending> ended)
        {
            string[] before = File.Exists(LogPath) ? File.ReadAllLines(LogPath) : Array.Empty<string>();
            Require(before.All(line => !line.Contains("\"testerMode\"")), "a line played outside the tester mode is marked as played in it");

            var testerMode = new SettingsInMemory(DeviceSettings.Default.With(DeviceSetting.TesterMode, isOn: true));
            using GameSession session = bench.SessionAt(LeftLevel, attempt: 0, testerMode);
            StoryStudio.AdvanceUntilSettled(session);
            Wallet wallet = session.Progress.Wallet;
            Game game = session.Stage.Game;
            Move move = Bot(SkillProfile.Expert.Name).ChooseMove(game, game.FindLegalMoves(), new Rng(BotSeed, BotStream));
            Require(session.TryPlay(move) && game.Status == GameStatus.Playing, $"the move {move} of level {LeftLevel} was refused or decided the level");
            StoryStudio.AdvanceUntilSettled(session);
            ended.Add(Ending.Of(game, $"level {LeftLevel} left after {game.MovesUsed} move in the tester mode").InTheTesterMode());
            Leave(session);

            string[] after = File.ReadAllLines(LogPath);
            Require(after.Length == before.Length + 1, "no line was written when a level was left after a move in the tester mode");
            Require(after[after.Length - 1].Contains("\"testerMode\":true"), $"the line of an attempt in the tester mode is not marked: {after[after.Length - 1]}");
            Require(
                session.Progress.Wallet.Lives.Equals(wallet.Lives) && session.Progress.Wallet.Coins == wallet.Coins,
                $"leaving after a move in the tester mode left the wallet at {session.Progress.Wallet}, from {wallet}");
        }

        /// <summary>
        /// A level gone into as a player goes, with the brush and splat and the rainbow palette chosen on its card.
        /// The card closed with them chosen, and the board left before a move, spend nothing and write no line; the
        /// attempt played to its end by bot takes their specials, spends both at its first move, and is one line.
        /// </summary>
        /// <exception cref="InvalidOperationException">Something was spent or written that should not have been, or the other way round.</exception>
        private static void PlayWithStartBoostersChosenOnTheCard(Bench bench, List<Ending> ended)
        {
            BoosterKind[] chosen = { BoosterKind.StartRocketAndBomb, BoosterKind.StartColorBomb };
            using GameSession session = bench.SessionInTheHall(LeftLevel);
            PlayerProgress before = session.Progress;
            int linesBefore = LinesInTheLog();

            ThroughTheCard.BringDownTheCard(session);
            foreach (BoosterKind booster in chosen)
            {
                ThroughTheCard.Choose(session, booster);
            }

            Press(session, ThroughTheCard.MuseumOf(session).StartCardClosePixel, "the level's card's round button");
            StoryStudio.AdvanceUntilSettled(session);
            Require(!ThroughTheCard.MuseumOf(session).ShowsTheStartCard, "the level's card's round button did not close it");
            Require(session.Progress.Equals(before), $"closing the level's card with boosters chosen changed the progress to \"{session.Progress}\"");

            ThroughTheCard.IntoTheLevel(session, chosen);
            Leave(session);
            Require(session.Progress.Equals(before) && LinesInTheLog() == linesBefore, $"leaving a board taken into with boosters before a move changed the progress to \"{session.Progress}\" or wrote a line");

            ThroughTheCard.IntoTheLevel(session, chosen);
            Game game = session.Stage.Game;
            string[] names = chosen.Select(BoosterKinds.Name).ToArray();
            Require(game.BoostersUsed.SequenceEqual(names), $"the board took [{string.Join(", ", game.BoostersUsed)}], not [{string.Join(", ", names)}]");
            FlowStory.PlayTheGame(session, Bot(SkillProfile.Expert.Name), new Rng(BotSeed, BotStream));
            ended.Add(Ending.Of(game, $"level {LeftLevel} with [{string.Join(", ", names)}] chosen on its card, {game.Status} after {game.MovesUsed} moves"));
            Require(LinesInTheLog() == linesBefore + 1, "the attempt taken into with boosters chosen on the card wrote no line, or more than one");

            BoosterShelf spent = chosen.Aggregate(before.Wallet.Shelf, (shelf, booster) => shelf.WithOneFewer(booster));
            Require(session.Progress.Wallet.Shelf.Equals(spent), $"after the attempt the shelf holds {session.Progress.Wallet.Shelf}, not {spent}");
        }

        /// <summary>
        /// Every booster of play used by touches on the shelf of level 40, the spatula before any move; then the bot
        /// plays the attempt to its end. The spatula counts the attempt as a first move does, each booster is spent,
        /// and the attempt is one line that names the shuffle rule of today.
        /// </summary>
        /// <exception cref="InvalidOperationException">A booster was not used or not spent, or the line is not as the game writes it.</exception>
        private static void PlayWithEveryBoosterOnTheShelf(Bench bench, List<Ending> ended)
        {
            using GameSession session = bench.SessionAt(OpenLevel, attempt: 0);
            StoryStudio.AdvanceUntilSettled(session);
            PlayerProgress before = session.Progress;
            int linesBefore = LinesInTheLog();
            foreach (BoosterKind booster in BoosterKinds.InLevel)
            {
                Press(session, session.Stage.BoosterDiscPixel(booster), $"the disc of {booster.Name()}");
                if (booster != BoosterKind.Shuffle)
                {
                    Press(session, session.Stage.PixelOf(AGemLowOnTheBoard(session.Stage.Game)), $"a cell for {booster.Name()}");
                }

                StoryStudio.AdvanceUntilSettled(session);
                Require(session.Progress.Wallet.Shelf.Count(booster) == before.Wallet.Shelf.Count(booster) - 1, $"the {booster.Name()} used on the shelf was not spent");
            }

            Require(session.Progress.AttemptsOnLevel == before.AttemptsOnLevel + 1 && session.Stage.Game.MovesUsed == 0, "the spatula used before any move did not count the attempt");
            Game game = session.Stage.Game;
            FlowStory.PlayTheGame(session, new RandomPolicy(), new Rng(BotSeed, BotStream));
            ended.Add(Ending.Of(game, $"level {OpenLevel} with every booster of play used on the shelf, {game.Status} after {game.MovesUsed} moves"));
            Require(LinesInTheLog() == linesBefore + 1, "the attempt with the boosters of the shelf wrote no line, or more than one");
            Require(File.ReadAllLines(LogPath).Last().Contains("\"shuffleRule\":2,\"actions\":["), "the line of an attempt played today does not name the shuffle rule it was played under");
        }

        /// <summary>A gem in the board's lower half, where a strike always finds something to hit.</summary>
        private static Position AGemLowOnTheBoard(Game game)
        {
            BoardShape shape = game.Board.Shape;
            for (int y = shape.Height - 2; y > 0; y--)
            {
                for (int x = 1; x < shape.Width - 1; x++)
                {
                    var cell = new Position(x, y);
                    if (shape.IsPlayable(cell) && game.Board.TileAt(cell).IsGem)
                    {
                        return cell;
                    }
                }
            }

            throw new InvalidOperationException($"Level {game.Level.Id} has no gem to aim at.");
        }

        /// <summary>
        /// A new player who stands on the spatula's level is given its three as the session opens, kept as given in the
        /// save file; a second session over that file gives none more.
        /// </summary>
        /// <exception cref="InvalidOperationException">The gift was not given, not saved, or given twice.</exception>
        private static void ANewPlayerOnTheSpatulasLevelIsGivenItsThreeOnce(Bench bench)
        {
            string folder = Path.Combine(Folder, "spatula-gift");
            Directory.CreateDirectory(folder);
            var store = new ProgressFile(folder, StoryStudio.Economy.StartingWallet);
            store.Save(bench.StandingOn(SpatulasLevel).WithWallet(StoryStudio.Economy.StartingWallet)); // a save from before the gifts
            using (GameSession first = bench.SessionOver(store))
            {
                Require(first.Progress.Wallet.Shelf.Count(BoosterKind.HitCell) == 3, $"a new player on level {SpatulasLevel} holds {first.Progress.Wallet.Shelf.Count(BoosterKind.HitCell)} spatulas, not the gift of 3");
            }

            PlayerProgress saved = new ProgressFile(folder, StoryStudio.Economy.StartingWallet).Load();
            Require(saved.Wallet.Shelf.Count(BoosterKind.HitCell) == 3 && saved.Wallet.Rewards.UnlockGifts.Contains(BoosterKind.HitCell), "the spatula's gift is not in the save file");
            Require(File.ReadAllText(Path.Combine(folder, ProgressFile.FileName)).Contains("\"unlockGifts\":[\"hit-cell\"]"), "the save file does not name the spatula's gift as given");
            using GameSession second = bench.SessionOver(new ProgressFile(folder, StoryStudio.Economy.StartingWallet));
            Require(second.Progress.Wallet.Shelf.Count(BoosterKind.HitCell) == 3, "a second session gave the spatula's gift again");
        }

        /// <summary>
        /// The records the game wrote before the shuffle rule changed are read with Unity's reader: they name no rule,
        /// read as played under the first one, and replay by it to their end, refused shuffles included.
        /// </summary>
        /// <returns>How many were replayed.</returns>
        /// <exception cref="InvalidOperationException">A record reads as another rule, or names no refused shuffle where the file has one.</exception>
        private static int RecordsBeforeTheShuffleRuleReplayByTheFirstRule(Bench bench)
        {
            string[] lines = File.ReadAllLines(FirstRuleRecords);
            Require(lines.Length > 0 && lines.Any(line => line.Contains("shuffle-refused")), $"{FirstRuleRecords} holds no record with a refused shuffle");
            foreach (string line in lines)
            {
                AttemptRecord record = JsonUtility.FromJson<RecordLineDocument>(line).ToRecord();
                Require(record.Start.ShuffleRule == ShuffleBoosterRule.RandomOrders, $"a record that names no rule reads as {record.Start.ShuffleRule}");
                AttemptReplay.Of(record, bench.Level(record.Start.Level.Value));
            }

            return lines.Length;
        }

        /// <summary>The shelf's pause disc asks whether to leave; the card's museum button leaves for the hall.</summary>
        private static void Leave(GameSession session)
        {
            StoryStudio.AdvanceUntilSettled(session);
            Press(session, session.Stage.PauseButtonPixel, "the shelf's pause disc");
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Stage.IsAskingWhetherToLeave, "the shelf's pause disc did not ask whether to leave the level");
            Press(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button");
            for (float waited = 0f; session.Museum == null; waited += StoryStudio.FrameSeconds)
            {
                Require(waited <= LongestWait, "the leaving card's museum button opened no museum");
                session.Advance(StoryStudio.FrameSeconds);
            }
        }

        /// <exception cref="InvalidOperationException">The button is not on the screen to be pressed.</exception>
        private static void Press(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(pixel ?? throw new InvalidOperationException($"There is no {what} to press."));
            session.PointerUp();
        }

        /// <summary>
        /// The offer of extra moves by touches on the game's own session: on level 40, played by the random bot until
        /// its moves run out, the five more paid with coins and, on another attempt, with an ad; no line is written
        /// while the offer hangs or when the moves are given, and the attempt played on to its end is one line that
        /// says the moves were taken. Then a board made by hand that no shuffle brings back is refused the moves: its
        /// line ends with the refusal, which drew from the shuffle stream.
        /// </summary>
        /// <exception cref="InvalidOperationException">A line was written too soon or not at all, or the moves were not given or refused as the board says.</exception>
        private static void PlayThroughTheOffer(Bench bench, List<Ending> ended)
        {
            Wallet rich = StoryStudio.Economy.StartingWallet.WithCoins(new Coins(1250));
            foreach (ExtraMovesPayment paidBy in new[] { ExtraMovesPayment.ByCoins, ExtraMovesPayment.ByAd })
            {
                var rng = new Rng(BotSeed, BotStream);
                for (int attempt = 0; ; attempt++)
                {
                    Require(attempt < MostAttempts, $"{MostAttempts} attempts of level {LostLevel} did not run out of moves; pick another bot or level");
                    using GameSession session = bench.SessionAt(LostLevel, attempt, rich);
                    StoryStudio.AdvanceUntilSettled(session);
                    int linesBefore = LinesInTheLog();
                    FlowStory.PlayTheGame(session, new RandomPolicy(), rng);
                    Game game = session.Stage.Game;
                    if (!OutOfMovesOffer.IsMadeIn(game))
                    {
                        ended.Add(Ending.Of(game, $"level {LostLevel} {game.Status} after {game.MovesUsed} moves"));
                        Require(LinesInTheLog() == linesBefore + 1, $"no line was written when attempt {attempt} of level {LostLevel} was decided");
                        continue;
                    }

                    Require(LinesInTheLog() == linesBefore, "a line was written while the offer of extra moves hung");
                    AtTheOffer.Take(session, paidBy);
                    Require(game.ExtraMovesTaken && game.Status == GameStatus.Playing && LinesInTheLog() == linesBefore, $"the extra moves paid {paidBy} were not given, or a line was written when they were");
                    FlowStory.PlayTheGame(session, new RandomPolicy(), rng);
                    ended.Add(Ending.Of(game, $"level {LostLevel} with the extra moves paid {paidBy}, {game.Status} after {game.MovesUsed} moves"));
                    Require(LinesInTheLog() == linesBefore + 1 && File.ReadAllLines(LogPath).Last().Contains("\"extraMovesBought\":true"), $"the attempt with the extra moves paid {paidBy} is not one line that says they were taken");
                    break;
                }
            }

            using GameSession onTheDeadBoard = bench.SessionOn(HandMadeBoards.NoShuffleBringsBack(), rich);
            StoryStudio.AdvanceUntilSettled(onTheDeadBoard);
            int linesBeforeTheRefusal = LinesInTheLog();
            Require(onTheDeadBoard.TryPlay(Move.Tap(HandMadeBoards.TheRocket)), "the rocket of the board made by hand was not fired");
            AtTheOffer.Take(onTheDeadBoard, ExtraMovesPayment.ByCoins);
            Game refused = onTheDeadBoard.Stage.Game;
            Require(!refused.ExtraMovesTaken && refused.Actions.Last() is AttemptAction.ExtraMovesRefused, "the board made by hand was given the extra moves, or the refusal was not kept");
            ended.Add(Ending.Of(refused, $"the board made by hand {refused.Status} with the extra moves refused"));
            Require(LinesInTheLog() == linesBeforeTheRefusal + 1, "the attempt whose extra moves were refused is not one line");
        }

        /// <summary>
        /// An attempt as no screen lets a player play it: all three start boosters, every booster of play, the
        /// shuffle asked for until it is accepted, and the extra moves if the moves run out. It is played on the engine
        /// as the game starts it, and logged as the game logs an attempt.
        /// </summary>
        /// <exception cref="InvalidOperationException">A booster found nothing to do anywhere on the board.</exception>
        private static void PlayWithEveryBoosterOnTheEngine(Bench bench, List<Ending> ended)
        {
            LevelDefinition level = bench.Level(LostLevel);
            AttemptSeeds seeds = LevelStart.SeedsOf(level, attempt: 0);
            var startBoosters = new[] { StartBooster.RocketAndBomb, StartBooster.ColorBomb, StartBooster.ExtraMoves };
            Game game = Game.Start(level, seeds, new GameOptions { StartBoosters = startBoosters, RecordActions = true });
            var rng = new Rng(BotSeed, BotStream);
            var bot = new RandomPolicy();

            UseAimedAnywhere(game, at => new BoosterUse.HitCell(at));
            PlayOn(game, bot, rng, moves: 2);
            UseAimedAnywhere(game, at => new BoosterUse.HitRow(at));
            UseAimedAnywhere(game, at => new BoosterUse.HitColumn(at));
            AskForAShuffleUntilOneIsAccepted(game);
            PlayOn(game, bot, rng, moves: int.MaxValue);
            if (game.Status == GameStatus.LostOutOfMoves && game.TryTakeExtraMoves())
            {
                PlayOn(game, bot, rng, moves: int.MaxValue);
            }

            DateTimeOffset now = StoryStudio.Clock.Now;
            var start = new AttemptStart(level.Id, level.ContentVersion, level.MoveLimit, seeds, attempt: 0, bench.Log.Device, "unity", now, game.ShuffleBoosterRule);
            bench.Log.Append(new AttemptRecord(start, AttemptOutcome.Of(game, now), lab: null, game.Actions));
            string actions = string.Join(" ", game.Actions.Where(action => !(action is AttemptAction.MovePlayed)).Select(AttemptActionNames.Of));
            ended.Add(Ending.Of(game, $"level {LostLevel} on the engine with {actions}, {game.Status} after {game.MovesUsed} moves"));
        }

        private static void UseAimedAnywhere(Game game, Func<Position, BoosterUse> aimedAt)
        {
            BoardShape shape = game.Board.Shape;
            for (int y = 0; y < shape.Height; y++)
            {
                for (int x = 0; x < shape.Width; x++)
                {
                    var at = new Position(x, y);
                    if (game.Status == GameStatus.Playing && shape.IsPlayable(at) && game.TryUseBooster(aimedAt(at)))
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException($"No cell of level {game.Level.Id} took the booster {aimedAt(new Position(0, 0))}.");
        }

        private static void AskForAShuffleUntilOneIsAccepted(Game game)
        {
            for (int asked = 1; game.Status == GameStatus.Playing && !game.TryUseBooster(new BoosterUse.Shuffle()); asked++)
            {
                Require(asked < MostShuffleRequests, $"{MostShuffleRequests} shuffles of level {game.Level.Id} in a row were refused");
            }
        }

        private static void PlayOn(Game game, IBotPolicy bot, Rng rng, int moves)
        {
            for (int played = 0; played < moves && game.Status == GameStatus.Playing; played++)
            {
                Move move = bot.ChooseMove(game, game.FindLegalMoves(), rng);
                Require(game.TryPlay(move), $"the engine refused the bot's move {move}");
            }
        }

        /// <summary>
        /// Reads the log back with Unity's reader and plays every record again on the engine: each must come to the
        /// end that was seen when its attempt ended, and be the game's own record of this phone.
        /// </summary>
        /// <returns>How many records were replayed.</returns>
        /// <exception cref="InvalidOperationException">The log holds another count of records, or a record that is not the game's own or ends elsewhere.</exception>
        private static int EveryLineReadsBackAndReplaysToTheEndSeen(Bench bench, IReadOnlyList<Ending> ended)
        {
            string[] lines = File.ReadAllLines(LogPath);
            Require(lines.Length == ended.Count, $"the log holds {lines.Length} lines for {ended.Count} attempts that ended");
            for (int index = 0; index < lines.Length; index++)
            {
                AttemptRecord record = JsonUtility.FromJson<RecordLineDocument>(lines[index]).ToRecord();
                LevelDefinition level = bench.Level(record.Start.Level.Value);
                Require(
                    record.Start.Device.Equals(bench.Log.Device) && record.Start.Client == "unity" && record.Start.MoveLimit == level.MoveLimit && lines[index].Contains("\"attempt\":"),
                    $"line {index + 1} is not the game's own record of this phone: {lines[index]}");

                Ending replayed = Ending.Of(AttemptReplay.Of(record, level), ended[index].What);
                Require(replayed.Equals(ended[index]), $"line {index + 1} replays to {replayed}, and the attempt ended at {ended[index]}");
                Require(record.TesterMode == ended[index].IsInTheTesterMode, $"line {index + 1} reads back {(record.TesterMode ? "in" : "out of")} the tester mode: {lines[index]}");
            }

            return lines.Length;
        }

        private static int LinesInTheLog() => File.Exists(LogPath) ? File.ReadAllLines(LogPath).Length : 0;

        private static IBotPolicy Bot(string name) => BotPolicies.TryCreate(name, out IBotPolicy bot)
            ? bot
            : throw new InvalidOperationException($"The bots' list has no {name}.");

        /// <exception cref="InvalidOperationException">What the game relies on did not hold; the message says what happened instead.</exception>
        private static void Require(bool holds, string otherwise)
        {
            if (!holds)
            {
                throw new InvalidOperationException(char.ToUpperInvariant(otherwise[0]) + otherwise.Substring(1) + ".");
            }
        }

        /// <summary>What the check plays on: the packed levels, art and museum, and the log in the check's folder under the phone's id.</summary>
        private sealed class Bench
        {
            private readonly IReadOnlyList<LevelDefinition> _levels = LevelPack.LoadPacked();
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings = PaintingGallery.OfThePackedMuseum();

            /// <exception cref="InvalidOperationException">The art catalogue is missing.</exception>
            public Bench(DeviceId device)
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                _art = art != null ? art : throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                Log = new AttemptLogFile(Folder, device);
            }

            public AttemptLogFile Log { get; }

            /// <summary>A level of the pack, or the board made by hand that no shuffle brings back.</summary>
            public LevelDefinition Level(int number) => number == HandMadeBoards.NoShuffleBringsBackNumber
                ? HandMadeBoards.NoShuffleBringsBack()
                : LevelPack.Numbered(_levels, number);

            /// <summary>The game's own session at the board of a level, for a player with a wallet who has tried it so many times before, on the check's log.</summary>
            public GameSession SessionAt(int levelNumber, int attempt, Wallet wallet)
            {
                PlayerProgress standing = StoryStudio.StandingOn(Level(levelNumber), _levels);
                var progress = new PlayerProgress(standing.LevelsWon, attempt).WithWallet(wallet);
                return new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), Log, Screen, SessionOpening.AtTheBoard);
            }

            /// <summary>The game's own session at the board of a level that is the whole pack, for a new player with a wallet, on the check's log.</summary>
            public GameSession SessionOn(LevelDefinition level, Wallet wallet) =>
                new GameSession(new[] { level }, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(new PlayerProgress(levelsWon: 0, attemptsOnLevel: 0).WithWallet(wallet)), Log, Screen, SessionOpening.AtTheBoard);

            /// <summary>The game's own session in the hall of a new player who stands on a level, on the check's log, with the settings of a device file and so many switches.</summary>
            public GameSession SessionOnThePhone(int levelNumber, DeviceFile phone, DeviceAbilities abilities) =>
                new GameSession(
                    _levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(StoryStudio.StandingOn(Level(levelNumber), _levels)), Log,
                    phone, abilities, CuePlayer.Silent, Screen, SessionOpening.InTheMuseum);

            /// <summary>The game's own session in the hall of a new player who stands on a level, on the check's log.</summary>
            public GameSession SessionInTheHall(int levelNumber) =>
                new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(StoryStudio.StandingOn(Level(levelNumber), _levels)), Log, Screen, SessionOpening.InTheMuseum);

            /// <summary>The game's own session at the board of a level, for a new player who has tried it so many times before, on the check's log, with the settings of a store.</summary>
            public GameSession SessionAt(int levelNumber, int attempt, ISettingsStore settings)
            {
                PlayerProgress standing = StoryStudio.StandingOn(Level(levelNumber), _levels);
                var progress = new PlayerProgress(standing.LevelsWon, attempt).WithWallet(standing.Wallet);
                return new GameSession(
                    _levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), Log, settings, DeviceAbilities.OfThisBuild,
                    CuePlayer.Silent, Screen, SessionOpening.AtTheBoard);
            }

            /// <summary>The game's own session at the board of a level, for a new player who has tried it so many times before, on the check's log.</summary>
            public GameSession SessionAt(int levelNumber, int attempt)
            {
                PlayerProgress standing = StoryStudio.StandingOn(Level(levelNumber), _levels);
                var progress = new PlayerProgress(standing.LevelsWon, attempt).WithWallet(standing.Wallet);
                return new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), Log, Screen, SessionOpening.AtTheBoard);
            }

            /// <summary>A new player who stands on a level, never having tried it.</summary>
            public PlayerProgress StandingOn(int levelNumber) => StoryStudio.StandingOn(Level(levelNumber), _levels);

            /// <summary>The game's own session at the board of the level the store's progress stands on, on a log that forgets.</summary>
            public GameSession SessionOver(IProgressStore store) =>
                new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, store, StoryStudio.AttemptsKeptInMemory(), Screen, SessionOpening.AtTheBoard);
        }

        /// <summary>Where a game stood when its attempt ended, as far as a replay must come to the same.</summary>
        private sealed class Ending : IEquatable<Ending>
        {
            private Ending(int level, GameStatus status, int movesUsed, string goalsDone, ulong board, int actions, string what, bool isInTheTesterMode = false)
            {
                IsInTheTesterMode = isInTheTesterMode;
                Level = level;
                Status = status;
                MovesUsed = movesUsed;
                GoalsDone = goalsDone;
                Board = board;
                Actions = actions;
                What = what;
            }

            public int Level { get; }

            public GameStatus Status { get; }

            public int MovesUsed { get; }

            public string GoalsDone { get; }

            public ulong Board { get; }

            public int Actions { get; }

            /// <summary>The attempt in words, for the log.</summary>
            public string What { get; }

            /// <summary>True for an attempt played in the tester mode, whose line must say so; no part of where the game stood.</summary>
            public bool IsInTheTesterMode { get; }

            /// <summary>The same ending, of an attempt played in the tester mode.</summary>
            public Ending InTheTesterMode() => new Ending(Level, Status, MovesUsed, GoalsDone, Board, Actions, What, isInTheTesterMode: true);

            public static Ending Of(Game game, string what) => new Ending(
                game.Level.Id.Value,
                game.Status,
                game.MovesUsed,
                string.Join("/", Enumerable.Range(0, game.Goals.Count).Select(goal => game.Goals[goal].Done)),
                game.Board.ComputeHash(),
                game.Actions.Count,
                what);

            public bool Equals(Ending? other) =>
                other != null && (Level, Status, MovesUsed, GoalsDone, Board, Actions) == (other.Level, other.Status, other.MovesUsed, other.GoalsDone, other.Board, other.Actions);

            public override bool Equals(object? obj) => Equals(obj as Ending);

            public override int GetHashCode() => HashCode.Combine(Level, Status, MovesUsed, GoalsDone, Board, Actions);

            public override string ToString() =>
                $"level {Level} {Status} after {MovesUsed} moves, goals {GoalsDone}, board {Board:x16}, {Actions} actions";
        }
    }

#pragma warning disable IDE1006, CA1051 // public camelCase fields are the line's layout: Unity's JSON reader maps fields, not properties

    /// <summary>
    /// A line of the attempt log as Unity's reader fills it, the way a tool on the phone would read the file: the
    /// fields of the wire form (Puzzle.Tooling's AttemptRecordJson), made into the record they spell. Unity's reader
    /// makes a zero or an empty list of what a line does not have; the game's lines always have their attempt, and a
    /// content version of zero is the first.
    /// </summary>
    [Serializable]
    internal sealed class RecordLineDocument
    {
        public int schema;
        public int level;
        public int contentVersion;
        public int attempt;
        public ulong boardSeed;
        public ulong playSeed;
        public int moveLimit;
        public string result = string.Empty;
        public int movesUsed;
        public GoalLineDocument[] goals = Array.Empty<GoalLineDocument>();
        public bool extraMovesBought;
        public string[] boostersUsed = Array.Empty<string>();
        public string deviceId = string.Empty;
        public string client = string.Empty;
        public string startedUtc = string.Empty;
        public string endedUtc = string.Empty;

        /// <summary>True for an attempt played in the tester mode; Unity's reader makes false of a line that does not say so, as every other line.</summary>
        public bool testerMode;

        /// <summary>The shuffle booster's rule by its number; Unity's reader makes 0 of a line that names none, which is the first rule.</summary>
        public int shuffleRule;
        public ActionLineDocument[] actions = Array.Empty<ActionLineDocument>();

        /// <exception cref="ArgumentException">A value is not one a record can have.</exception>
        /// <exception cref="LevelFormatException">A goal is not in the level files' words.</exception>
        /// <exception cref="InvalidOperationException">The line names an action this game does not know.</exception>
        public AttemptRecord ToRecord()
        {
            var start = new AttemptStart(
                new LevelId(level),
                contentVersion == 0 ? ContentVersion.First : new ContentVersion(contentVersion),
                moveLimit,
                AttemptSeeds.WithFixedBoard(boardSeed, playSeed),
                attempt,
                new DeviceId(deviceId),
                client,
                Instant(startedUtc),
                shuffleRule == 0 ? ShuffleBoosterRule.RandomOrders : (ShuffleBoosterRule)shuffleRule);
            var outcome = new AttemptOutcome(
                ResultNamed(result),
                movesUsed,
                goals.Select(goal => goal.ToOutcome()).ToArray(),
                extraMovesBought,
                boostersUsed,
                Instant(endedUtc));
            return new AttemptRecord(start, outcome, lab: null, actions.Select(action => action.ToAction()).ToArray(), testerMode);
        }

        private static DateTimeOffset Instant(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);

        private static AttemptResult ResultNamed(string name)
        {
            switch (name)
            {
                case "won":
                    return AttemptResult.Won;
                case "outOfMoves":
                    return AttemptResult.OutOfMoves;
                case "noMovesPossible":
                    return AttemptResult.NoMovesPossible;
                case "abandoned":
                    return AttemptResult.Abandoned;
                default:
                    throw new ArgumentException($"Unknown result '{name}'.");
            }
        }
    }

    [Serializable]
    internal sealed class GoalLineDocument
    {
        public string type = string.Empty;
        public string color = string.Empty;
        public int target;
        public int done;

        public GoalOutcome ToOutcome() =>
            new GoalOutcome(LevelFileMapper.ParseGoal(new GoalFile { type = type, color = string.IsNullOrEmpty(color) ? null : color, count = target }), done);
    }

    [Serializable]
    internal sealed class ActionLineDocument
    {
        public string act = string.Empty;
        public int[] at = Array.Empty<int>();

        public AttemptAction ToAction() =>
            AttemptActionNames.Named(act, at != null && at.Length > 0 ? at : null)
            ?? throw new InvalidOperationException($"The line names an action this game does not know: {act}.");
    }
}
