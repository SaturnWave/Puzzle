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
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using Puzzle.Core.Recording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Checks from the command line that the phone's attempt records do what the game relies on, with Unity's own file
    /// calls and JSON reader. The device file makes a random id on the first start, in the layout the tests pin, and a
    /// restart reads the same id; a damaged device file is moved aside for a new id, and a later game's file is left as
    /// it is. Then a won, a lost and a left attempt are played by bot through the game's own session, on a log of its
    /// own: each is a line in the log the moment it ends (the won and the lost one at their deciding move, before any of
    /// it is shown; the left one at the press of the leaving card's museum button), and a level left before its first
    /// move leaves no line. An attempt with every booster and the extra moves, which no screen offers yet, is played on
    /// the engine alone and logged the same way. Last, the file is read back with Unity's reader, and every record is
    /// played again on the engine (Puzzle.Core's AttemptReplay) to the end that was seen: the same board, status, moves
    /// used and goals. It works in a folder of its own (out/unity/attempt-log). The damaged and the later device file
    /// write two warnings to the log on purpose.
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

        /// <summary>A later game's device file, with an id this game reads and something it does not know.</summary>
        private const string LaterDeviceDocument = "{\"version\":2,\"id\":\"6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a\",\"sound\":false}";

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
                DeviceId device = TheFirstStartMakesTheIdAndARestartReadsIt();

                var bench = new Bench(device);
                var ended = new List<Ending>();
                PlayUntil(bench, WonLevel, status => status == GameStatus.Won, Bot(SkillProfile.Expert.Name), ended);
                PlayUntil(bench, LostLevel, status => status.IsLoss(), new RandomPolicy(), ended);
                LeaveBeforeAndAfterAMove(bench, ended);
                PlayWithEveryBoosterOnTheEngine(bench, ended);

                int replayed = EveryLineReadsBackAndReplaysToTheEndSeen(bench, ended);
                Debug.Log($"Attempt log check passed: device {device}; {replayed} records in {LogPath} ({string.Join("; ", ended.Select(end => end.What))}), each read back with Unity's reader and replayed on the engine to the end seen.");
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

        /// <exception cref="InvalidOperationException">The device file was not written as the tests pin it, or a restart read another id.</exception>
        private static DeviceId TheFirstStartMakesTheIdAndARestartReadsIt()
        {
            DeviceId made = new DeviceFile(Folder).LoadOrMake();
            string written = File.ReadAllText(Path.Combine(Folder, DeviceFile.FileName));
            Require(written == "{\"version\":1,\"id\":\"" + made.Value + "\"}", $"the first start wrote the device file as {written}");
            DeviceId afterARestart = new DeviceFile(Folder).LoadOrMake();
            Require(afterARestart.Equals(made), $"a restart read the id {afterARestart}, not {made}");
            return made;
        }

        /// <exception cref="InvalidOperationException">A damaged file was written over or kept its id, or a later game's file was changed.</exception>
        private static void ADamagedDeviceFileGivesANewIdAndALaterOneIsLeftAsItIs()
        {
            const string damagedDocument = "{\"version\":1,\"id\":\"ada@example.com\"}";
            string damaged = Path.Combine(Folder, "device-damaged");
            Directory.CreateDirectory(damaged);
            File.WriteAllText(Path.Combine(damaged, DeviceFile.FileName), damagedDocument);
            DeviceId made = new DeviceFile(damaged).LoadOrMake();
            Require(File.ReadAllText(Path.Combine(damaged, DeviceFile.FileName + ".damaged")) == damagedDocument, "the damaged device file was not kept aside as it was");
            Require(new DeviceFile(damaged).LoadOrMake().Equals(made), "the id made in place of a damaged file did not read back");

            string later = Path.Combine(Folder, "device-later");
            Directory.CreateDirectory(later);
            string laterFile = Path.Combine(later, DeviceFile.FileName);
            File.WriteAllText(laterFile, LaterDeviceDocument);
            DeviceId kept = new DeviceFile(later).LoadOrMake();
            Require(kept.Value == "6f1c0e9a2b7d4c3e8a5f0b1d2c3e4f5a", $"a later game's id read as {kept}");
            File.WriteAllText(laterFile, "{\"version\":2,\"id\":\"a later spelling\"}");
            new DeviceFile(later).LoadOrMake();
            Require(File.ReadAllText(laterFile) == "{\"version\":2,\"id\":\"a later spelling\"}", "a later game's device file was written over");
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
                ended.Add(Ending.Of(game, $"level {levelNumber} {game.Status} after {game.MovesUsed} moves"));
                Require(LinesInTheLog() == linesBefore + 1, $"no line was written at the move that decided attempt {attempt} of level {levelNumber}");
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

        /// <summary>The shelf's round button asks whether to leave; the card's museum button leaves for the hall.</summary>
        private static void Leave(GameSession session)
        {
            StoryStudio.AdvanceUntilSettled(session);
            Press(session, session.Stage.LeaveButtonPixel, "the shelf's round button");
            StoryStudio.AdvanceUntilSettled(session);
            Require(session.Stage.IsAskingWhetherToLeave, "the shelf's round button did not ask whether to leave the level");
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
        /// An attempt as no screen lets a player play it yet: all three start boosters, every booster of play, the
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
            var start = new AttemptStart(level.Id, level.ContentVersion, level.MoveLimit, seeds, attempt: 0, bench.Log.Device, "unity", now);
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

            public LevelDefinition Level(int number) => LevelPack.Numbered(_levels, number);

            /// <summary>The game's own session at the board of a level, for a new player who has tried it so many times before, on the check's log.</summary>
            public GameSession SessionAt(int levelNumber, int attempt)
            {
                PlayerProgress standing = StoryStudio.StandingOn(Level(levelNumber), _levels);
                var progress = new PlayerProgress(standing.LevelsWon, attempt).WithWallet(standing.Wallet);
                return new GameSession(_levels, _art, _paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), Log, Screen, SessionOpening.AtTheBoard);
            }
        }

        /// <summary>Where a game stood when its attempt ended, as far as a replay must come to the same.</summary>
        private sealed class Ending : IEquatable<Ending>
        {
            private Ending(int level, GameStatus status, int movesUsed, string goalsDone, ulong board, int actions, string what)
            {
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
                Instant(startedUtc));
            var outcome = new AttemptOutcome(
                ResultNamed(result),
                movesUsed,
                goals.Select(goal => goal.ToOutcome()).ToArray(),
                extraMovesBought,
                boostersUsed,
                Instant(endedUtc));
            return new AttemptRecord(start, outcome, lab: null, actions.Select(action => action.ToAction()).ToArray());
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
