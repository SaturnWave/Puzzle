#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Museum.Catalogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Takes pictures of the game from the command line, with no game running: it builds a level's stage as the game
    /// does, moves the stage's clock to the moments asked for, and writes what the game's camera sees as PNG files
    /// into out/unity/shots. The same level, size and moment always give the same picture.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.RenderHarness.Run
    ///            [-shotLevels 1,40,52,114] [-shotSizes 1080x2340,1080x1920,1038x1076] [-shotTimes settled] [-shotMoves 0] [-shotPiece 46]
    ///   -shotSizes  width x height in pixels; "+top+bottom" adds a cutout and a system bar, e.g. 1080x2340+96+48
    ///   -shotTimes  seconds after the stage opens, or "settled" for the moment everything has come to rest
    ///   -shotMoves  moves a random player makes before the picture, to see a board in the middle of a game
    ///   -shotPiece  the level gives the jigsaw piece a player with so many levels won is given, which its plaques
    ///               show; left out, it gives none, as a level won before or beyond the museum's paintings
    /// Do not pass -nographics: the pictures need a graphics device.
    /// </summary>
    public static class RenderHarness
    {
        private const string ShotsFolder = "../out/unity/shots";

        /// <summary>The clock is moved in the steps a phone would move it, so a picture shows what a frame would.</summary>
        private const float FrameSeconds = 1f / 60f;

        /// <summary>A stage that still moves after this long never comes to rest: a mistake, not a long animation.</summary>
        private const float LongestSettleSeconds = 20f;

        private const ulong RandomPlayerSeed = 20261007;
        private const ulong RandomPlayerStream = 7;

        public static void Run()
        {
            try
            {
                ShotPlan plan = ShotPlan.FromCommandLine(Environment.GetCommandLineArgs());
                ArtCatalogue art = LoadArt();
                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                PaintingGallery paintings = PaintingGallery.OfThePackedMuseum();
                AttemptBrief brief = plan.BriefOfTheAttempt(levels.Count, paintings);
                foreach (int levelNumber in plan.LevelNumbers)
                {
                    LevelDefinition level = LevelPack.Numbered(levels, levelNumber);
                    foreach (ScreenShape screen in plan.Screens)
                    {
                        Shoot(level, screen, plan, brief, art, paintings);
                    }
                }
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Render harness failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        private static ArtCatalogue LoadArt()
        {
            ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
            if (art == null)
            {
                throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
            }

            return art;
        }

        /// <summary>One stage on one screen, pictured at every moment of the plan in turn.</summary>
        private static void Shoot(LevelDefinition level, ScreenShape screen, ShotPlan plan, AttemptBrief brief, ArtCatalogue art, PaintingGallery paintings)
        {
            // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using EffectArt effectArt = EffectArt.Paint();
            using GameStage stage = GameStage.Open(GameAfter(plan.MovesPlayed, level), art, effectArt, paintings, brief);
            stage.Fit(screen);
            float clock = 0f;
            foreach (Moment moment in plan.Moments)
            {
                clock = AdvanceTo(moment, stage, clock);
                string piece = plan.PieceAfterLevelsWon is int levelsWon ? $"-piece{levelsWon}" : string.Empty;
                string file = $"level-{level.Id.Value:D4}{piece}-{screen}{(plan.MovesPlayed > 0 ? $"-move{plan.MovesPlayed}" : "")}-{moment.Label}.png";
                CameraPicture.Write(stage.Camera, screen.PixelWidth, screen.PixelHeight, Path.Combine(ShotsFolder, file));
                Debug.Log($"Render harness: {file} at {clock.ToString("F2", CultureInfo.InvariantCulture)} s");
            }
        }

        private static Game GameAfter(int moves, LevelDefinition level)
        {
            Game game = LevelStart.FirstAttempt(level);
            var player = new RandomPolicy();
            var rng = new Rng(RandomPlayerSeed, RandomPlayerStream);
            var legalMoves = new List<Move>();
            for (int played = 0; played < moves && game.Status == GameStatus.Playing; played++)
            {
                legalMoves.Clear();
                game.CollectLegalMoves(legalMoves);
                game.TryPlay(player.ChooseMove(game, legalMoves, rng));
            }

            return game;
        }

        /// <summary>Moves the stage's clock frame by frame up to the moment; returns where the clock then stands.</summary>
        private static float AdvanceTo(Moment moment, GameStage stage, float clock)
        {
            while (moment.IsSettled ? !stage.IsSettled : clock + FrameSeconds / 2f < moment.Seconds)
            {
                if (clock > LongestSettleSeconds)
                {
                    throw new InvalidOperationException($"The stage is still moving after {LongestSettleSeconds} s.");
                }

                stage.Advance(FrameSeconds);
                clock += FrameSeconds;
            }

            return clock;
        }

        /// <summary>A moment to take a picture at: so many seconds after the stage opened, or when it has come to rest.</summary>
        private readonly struct Moment
        {
            private Moment(bool isSettled, float seconds, string label)
            {
                IsSettled = isSettled;
                Seconds = seconds;
                Label = label;
            }

            public bool IsSettled { get; }

            /// <summary>Only meaningful when the moment is not "settled".</summary>
            public float Seconds { get; }

            /// <summary>How the moment is written in a file name.</summary>
            public string Label { get; }

            public static Moment Settled => new Moment(isSettled: true, seconds: 0f, label: "settled");

            /// <exception cref="ArgumentException">The text is neither "settled" nor a number of seconds from 0 up.</exception>
            public static Moment Parse(string text)
            {
                if (text == "settled")
                {
                    return Settled;
                }

                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) || seconds < 0f)
                {
                    throw new ArgumentException($"\"{text}\" is not a moment: write seconds such as 0.25, or settled.");
                }

                return new Moment(isSettled: false, seconds, $"t{seconds.ToString("F2", CultureInfo.InvariantCulture)}");
            }
        }

        /// <summary>What one run pictures: which levels, on which screens, at which moments, after how many moves.</summary>
        private sealed class ShotPlan
        {
            private static readonly Regex ScreenPattern = new Regex(@"^(\d+)x(\d+)(?:\+(\d+)\+(\d+))?$");

            private ShotPlan(IReadOnlyList<int> levelNumbers, IReadOnlyList<ScreenShape> screens, IReadOnlyList<Moment> moments, int movesPlayed, int? pieceAfterLevelsWon)
            {
                LevelNumbers = levelNumbers;
                Screens = screens;
                Moments = moments;
                MovesPlayed = movesPlayed;
                PieceAfterLevelsWon = pieceAfterLevelsWon;
            }

            public IReadOnlyList<int> LevelNumbers { get; }

            public IReadOnlyList<ScreenShape> Screens { get; }

            /// <summary>In the order the clock reaches them: the timed ones from the earliest on, "settled" last.</summary>
            public IReadOnlyList<Moment> Moments { get; }

            public int MovesPlayed { get; }

            /// <summary>The level gives the piece a player with so many levels won is given; nothing when it gives none.</summary>
            public int? PieceAfterLevelsWon { get; }

            /// <summary>What the stage is told of the attempt: a first try, paid by the game's economy, with the piece asked for.</summary>
            /// <exception cref="ArgumentException">No piece is given for the levels won that were named.</exception>
            public AttemptBrief BriefOfTheAttempt(int levelCount, PaintingGallery paintings)
            {
                if (PieceAfterLevelsWon is not int levelsWon)
                {
                    return AttemptBrief.FirstTry(StoryStudio.Economy.Win);
                }

                PieceReward reward = PieceReward.ForWinning(new PlayerProgress(levelsWon, attemptsOnLevel: 0), levelCount, paintings.Catalogue)
                    ?? throw new ArgumentException($"A player with {levelsWon} levels won is given no piece: -shotPiece names one who is.");
                return new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, StoryStudio.Economy.Win, reward);
            }

            /// <summary>
            /// Without arguments: the levels that show a plain board, a hole, a shaped board with exits and crates of
            /// every strength, on a tall phone, a 9:16 phone and the half-size square of a foldable, once settled.
            /// </summary>
            /// <exception cref="ArgumentException">An argument is not written as the usage says.</exception>
            public static ShotPlan FromCommandLine(string[] arguments) => new ShotPlan(
                ListAfter("-shotLevels", arguments, "1,40,52,114").Select(WholeNumber).ToArray(),
                ListAfter("-shotSizes", arguments, "1080x2340,1080x1920,1038x1076").Select(ScreenFrom).ToArray(),
                ListAfter("-shotTimes", arguments, "settled").Select(Moment.Parse)
                    .OrderBy(moment => moment.IsSettled).ThenBy(moment => moment.Seconds).ToArray(),
                ListAfter("-shotMoves", arguments, "0").Select(WholeNumber).Single(),
                Array.IndexOf(arguments, "-shotPiece") >= 0 ? ListAfter("-shotPiece", arguments, "0").Select(WholeNumber).Single() : (int?)null);

            /// <summary>The comma-separated values that follow a switch, or the default when the switch is not given.</summary>
            private static string[] ListAfter(string name, string[] arguments, string byDefault)
            {
                int at = Array.IndexOf(arguments, name);
                if (at >= 0 && at + 1 >= arguments.Length)
                {
                    throw new ArgumentException($"{name} needs a value.");
                }

                return (at >= 0 ? arguments[at + 1] : byDefault).Split(',');
            }

            private static int WholeNumber(string text) =>
                int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    ? number
                    : throw new ArgumentException($"\"{text}\" is not a whole number.");

            private static ScreenShape ScreenFrom(string text)
            {
                Match match = ScreenPattern.Match(text);
                if (!match.Success)
                {
                    throw new ArgumentException($"\"{text}\" is not a screen: write 1080x2340, or 1080x2340+96+48 with top and bottom insets.");
                }

                int Number(int group) => match.Groups[group].Success ? WholeNumber(match.Groups[group].Value) : 0;
                return new ScreenShape(Number(1), Number(2), topInsetPixels: Number(3), bottomInsetPixels: Number(4));
            }
        }
    }
}
