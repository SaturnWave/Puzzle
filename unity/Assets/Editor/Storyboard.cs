#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Art;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Core.Randomness;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Plays a level with a bot, with no game running, and takes pictures in the middle of chosen turns: a burst in
    /// progress, pieces on their way down, an effect at its height, and the board when the turn is over. The stage's
    /// clock is moved frame by frame as a phone would move it, so a picture shows what a frame of the game shows; the
    /// same level, bot and moments always give the same pictures. After every turn the stage is asked whether it
    /// shows the engine's board, and a turn that does not fails the command. With -storyFlow it tells the way from
    /// one level to the next instead (FlowStory): a level's start, its result's card, the button, the next board.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.Storyboard.Run
    ///            [-storyLevels 1,40] [-storySize 1080x1920] [-storyBot expert] [-storyTurns 1,2,3] [-storyTimes 0.08,0.2,0.32,0.44,0.6,end]
    ///            [-storyTouches tap:3:4,drag:3:4:4:4] [-storyFlow won,lost]
    ///   -storyBot    random, novice, casual, average, expert, or another name the bots' list knows
    ///   -storyTurns  the turns to picture, counted from 1 (0 for none); the game is played up to the last of them, and every turn
    ///                on the way is described in the log (its move, its length, what happened in it), which is how
    ///                turns worth a picture are found
    ///   -storyTimes  seconds after the move, or "end" for the moment the turn has come to rest; a moment after the
    ///                turn's end is left out
    ///   -storyTouches  what a finger does after those turns: tap:column:row, or drag:column:row:column:row from the
    ///                middle of one cell to the middle of another (columns and rows from 0); each touch is pictured
    ///                a few times in its first third of a second (...-touch-NN-tS.SS.png and a sheet)
    ///   -storyFlow   instead of turns and touches: for each level, play it until it is won (with -storyBot) or until
    ///                it is lost (with the random bot, whatever -storyBot says), and picture the level's start, the
    ///                result's card coming down, its button pressed, and the board that comes next; the progress is
    ///                saved to a file of the story's own and read back as after a restart
    /// Pictures go to out/unity/shots/storyboard-level-NNNN-turn-NN-tS.SS.png, with one sheet of all of a turn's
    /// pictures side by side (…-sheet.png); a flow's go to flow-level-NNNN-SIZE-won-NN-what.png (or -lost-). Do not
    /// pass -nographics: the pictures need a graphics device.
    /// </summary>
    public static class Storyboard
    {
        private const ulong BotSeed = 20261007;
        private const ulong BotStream = 7;

        /// <summary>The moments after a touch that are pictured: a refused trade on its way out, at its turn and back, and what stays.</summary>
        private static readonly float[] TouchMoments = { 0.07f, 0.13f, 0.2f, 0.33f };

        public static void Run()
        {
            try
            {
                Plan plan = Plan.FromCommandLine(Environment.GetCommandLineArgs());
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                IReadOnlyList<LevelDefinition> levels = LevelPack.LoadPacked();
                foreach (int levelNumber in plan.LevelNumbers)
                {
                    LevelDefinition level = LevelPack.Numbered(levels, levelNumber);
                    if (plan.Flow.Count == 0)
                    {
                        Tell(level, levels, plan, art);
                    }

                    foreach (FlowEnding ending in plan.Flow)
                    {
                        IBotPolicy player = ending == FlowEnding.Won ? plan.Bot : new RandomPolicy();
                        FlowStory.Tell(levels, level, ending, player, plan.Screen, art);
                    }
                }
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is LevelFormatException)
            {
                Debug.LogError($"Storyboard failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Plays the level up to the last turn of the plan, pictures the turns the plan names, then the plan's touches.</summary>
        /// <exception cref="InvalidOperationException">A turn did not end on the engine's board, or never came to rest.</exception>
        private static void Tell(LevelDefinition level, IReadOnlyList<LevelDefinition> levels, Plan plan, ArtCatalogue art)
        {
            // A scene of its own that is never saved: nothing of an earlier run is left in this one.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The game's own session, on progress that is forgotten with it: a first try at the level.
            using GameSession session = StoryStudio.AtTheBoard(levels, art, new ProgressInMemory(StoryStudio.StandingOn(level, levels)), plan.Screen);
            GameStage stage = session.Stage;
            StoryStudio.AdvanceUntilSettled(session);

            string story = $"storyboard-level-{level.Id.Value:D4}";
            var rng = new Rng(BotSeed, BotStream);
            for (int turn = 1; turn <= plan.LastTurn && stage.Game.Status == GameStatus.Playing; turn++)
            {
                Move move = plan.Bot.ChooseMove(stage.Game, stage.Game.FindLegalMoves(), rng);
                if (!session.TryPlay(move) || stage.LastTurn is not TurnScript script)
                {
                    throw new InvalidOperationException($"The stage refused the bot's move {move} in turn {turn}.");
                }

                Debug.Log($"Storyboard: level {level.Id.Value} turn {turn}: {move}, {Seconds(script.Seconds)} s, {Describe(script)}");
                if (plan.Turns.Contains(turn))
                {
                    PictureTheTurn(session, script, $"{story}-turn-{turn:D2}", plan);
                }

                ComeToRestOnTheEnginesBoard(session, $"turn {turn}");
            }

            for (int touch = 0; touch < plan.Touches.Count && stage.Game.Status == GameStatus.Playing; touch++)
            {
                PictureTheTouch(session, plan.Touches[touch], $"{story}-touch-{touch + 1:D2}", plan);
                ComeToRestOnTheEnginesBoard(session, $"touch {touch + 1}");
            }

            Debug.Log($"Storyboard: level {level.Id.Value} played to move {stage.Game.MovesUsed}, {stage.Game.Status}; every turn ended on the engine's board.");
        }

        /// <exception cref="InvalidOperationException">What the stage shows once it has come to rest is not the engine's board.</exception>
        private static void ComeToRestOnTheEnginesBoard(GameSession session, string after)
        {
            StoryStudio.AdvanceUntilSettled(session);
            if (session.Stage.LastTurnFaults.Count > 0)
            {
                throw new InvalidOperationException($"After {after} the stage did not show the engine's board: {string.Join("; ", session.Stage.LastTurnFaults)}");
            }
        }

        /// <summary>The pictures of one turn: one per moment of the plan, and a sheet of them all.</summary>
        private static void PictureTheTurn(GameSession session, TurnScript script, string name, Plan plan)
        {
            var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
            float clock = 0f;
            foreach (Moment moment in plan.Moments)
            {
                if (!moment.IsTheEnd && (session.Stage.IsSettled || moment.Seconds > script.Seconds + Tempo.Land))
                {
                    Debug.Log($"Storyboard: {name} is over before {moment.Label}; no picture.");
                    continue;
                }

                clock = moment.IsTheEnd ? clock + StoryStudio.AdvanceUntilSettled(session) : StoryStudio.AdvanceTo(moment.Seconds, session, clock);
                StoryStudio.Shoot(session, plan.Screen, sheet, $"{name}-{moment.Label}.png");
            }

            StoryStudio.WriteSheet(sheet, $"{name}-sheet.png");
        }

        /// <summary>
        /// Touches the screen as a finger would and pictures what the stage makes of it: a picked piece in its ring,
        /// a refused trade on its way out and back, or the first moments of the turn a legal move starts.
        /// </summary>
        private static void PictureTheTouch(GameSession session, Touch touch, string name, Plan plan)
        {
            GameStage stage = session.Stage;
            session.PointerDown(stage.PixelOf(touch.From));
            session.PointerMoved(stage.PixelOf(touch.To));
            session.PointerUp();
            Debug.Log($"Storyboard: {name}: a finger went from {touch.From} to {touch.To}; the game is at move {stage.Game.MovesUsed}");

            var sheet = new ContactSheet(StoryStudio.SheetShrinksBy);
            float clock = 0f;
            foreach (float seconds in TouchMoments)
            {
                clock = StoryStudio.AdvanceTo(seconds, session, clock);
                StoryStudio.Shoot(session, plan.Screen, sheet, $"{name}-t{Seconds(seconds)}.png");
            }

            StoryStudio.WriteSheet(sheet, $"{name}-sheet.png");
        }

        /// <summary>What happened in a turn, in a line: how many rounds of matches, which effects, what else of note.</summary>
        private static string Describe(TurnScript script)
        {
            var parts = new List<string>();
            int bursts = script.Cues.OfType<Cue.Leave>().Count();
            parts.Add($"{bursts} pieces gone");
            foreach (IGrouping<string, Cue.Effect> effects in script.Cues.OfType<Cue.Effect>().GroupBy(effect => effect.Kind.ToString()))
            {
                parts.Add($"{effects.Key} x{effects.Count()}");
            }

            AddCount(parts, script.Cues.OfType<Cue.Appear>().Count(), "specials made");
            AddCount(parts, script.Cues.OfType<Cue.Flight>().Count(), "flights");
            AddCount(parts, script.Cues.OfType<Cue.Slip>().Count(), "slips");
            AddCount(parts, script.Cues.OfType<Cue.Reface>().Count(), "crates hit");
            AddCount(parts, script.Cues.OfType<Cue.TapeHit>().Count(), "tape hits");
            AddCount(parts, script.Cues.OfType<Cue.SlabHit>().Count(), "slab hits");
            AddCount(parts, script.Cues.OfType<Cue.Leave>().Count(leave => leave.Manner == LeaveManner.Collect), "items out");
            AddCount(parts, script.Cues.OfType<Cue.Reshuffle>().Count(), "reshuffles");
            return string.Join(", ", parts);
        }

        private static void AddCount(List<string> parts, int count, string what)
        {
            if (count > 0)
            {
                parts.Add($"{count} {what}");
            }
        }

        private static string Seconds(float seconds) => StoryStudio.Seconds(seconds);

        /// <summary>A moment of a turn to take a picture at: so many seconds after the move, or when the turn has come to rest.</summary>
        private readonly struct Moment
        {
            private Moment(bool isTheEnd, float seconds, string label)
            {
                IsTheEnd = isTheEnd;
                Seconds = seconds;
                Label = label;
            }

            public bool IsTheEnd { get; }

            /// <summary>Only meaningful when the moment is not the end.</summary>
            public float Seconds { get; }

            /// <summary>How the moment is written in a file name.</summary>
            public string Label { get; }

            /// <exception cref="ArgumentException">The text is neither "end" nor a number of seconds from 0 up.</exception>
            public static Moment Parse(string text)
            {
                if (text == "end")
                {
                    return new Moment(isTheEnd: true, seconds: 0f, label: "end");
                }

                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) || seconds < 0f)
                {
                    throw new ArgumentException($"\"{text}\" is not a moment: write seconds such as 0.25, or end.");
                }

                return new Moment(isTheEnd: false, seconds, $"t{Storyboard.Seconds(seconds)}");
            }
        }

        /// <summary>A finger that comes down in the middle of one cell and lifts in the middle of another, or of the same one: a tap.</summary>
        private readonly struct Touch
        {
            private Touch(Position from, Position to)
            {
                From = from;
                To = to;
            }

            public Position From { get; }

            public Position To { get; }

            /// <exception cref="ArgumentException">The text is neither tap:x:y nor drag:x:y:x:y.</exception>
            public static Touch Parse(string text)
            {
                string[] parts = text.Split(':');
                bool isTap = parts.Length == 3 && parts[0] == "tap";
                bool isDrag = parts.Length == 5 && parts[0] == "drag";
                if (!isTap && !isDrag)
                {
                    throw new ArgumentException($"\"{text}\" is not a touch: write tap:3:4 for a tap on column 3, row 4 (both from 0), or drag:3:4:4:4.");
                }

                var from = new Position(Plan.WholeNumber(parts[1]), Plan.WholeNumber(parts[2]));
                return new Touch(from, isTap ? from : new Position(Plan.WholeNumber(parts[3]), Plan.WholeNumber(parts[4])));
            }
        }

        /// <summary>What one run tells: which levels, on which screen, played by which bot, pictured in which turns at which moments, touched where.</summary>
        private sealed class Plan
        {
            private Plan(
                IReadOnlyList<int> levelNumbers,
                ScreenShape screen,
                IBotPolicy bot,
                IReadOnlyCollection<int> turns,
                IReadOnlyList<Moment> moments,
                IReadOnlyList<Touch> touches,
                IReadOnlyList<FlowEnding> flow)
            {
                LevelNumbers = levelNumbers;
                Screen = screen;
                Bot = bot;
                Turns = turns;
                Moments = moments;
                Touches = touches;
                Flow = flow;
            }

            public IReadOnlyList<int> LevelNumbers { get; }

            public ScreenShape Screen { get; }

            public IBotPolicy Bot { get; }

            /// <summary>The turns to picture, counted from 1; a 0 among them names no turn.</summary>
            public IReadOnlyCollection<int> Turns { get; }

            public int LastTurn => Turns.Max();

            /// <summary>In the order the clock reaches them: the timed ones from the earliest on, the end last.</summary>
            public IReadOnlyList<Moment> Moments { get; }

            /// <summary>What a finger does once the turns are played, one touch after another.</summary>
            public IReadOnlyList<Touch> Touches { get; }

            /// <summary>The endings whose way to the next level is told instead of turns and touches; none tells turns and touches.</summary>
            public IReadOnlyList<FlowEnding> Flow { get; }

            /// <exception cref="ArgumentException">An argument is not written as the usage says.</exception>
            public static Plan FromCommandLine(string[] arguments)
            {
                string botName = After("-storyBot", arguments, SkillProfile.Expert.Name);
                if (!BotPolicies.TryCreate(botName, out IBotPolicy bot))
                {
                    throw new ArgumentException($"\"{botName}\" is not a bot; the bots are {string.Join(", ", BotPolicies.Names)}.");
                }

                string touches = After("-storyTouches", arguments, string.Empty);
                string flow = After("-storyFlow", arguments, string.Empty);
                return new Plan(
                    After("-storyLevels", arguments, "1").Split(',').Select(WholeNumber).ToArray(),
                    ScreenFrom(After("-storySize", arguments, "1080x1920")),
                    bot,
                    After("-storyTurns", arguments, "1,2,3").Split(',').Select(WholeNumber).ToArray(),
                    After("-storyTimes", arguments, "0.08,0.2,0.32,0.44,0.6,end").Split(',').Select(Moment.Parse)
                        .OrderBy(moment => moment.IsTheEnd).ThenBy(moment => moment.Seconds).ToArray(),
                    touches.Length == 0 ? Array.Empty<Touch>() : touches.Split(',').Select(Touch.Parse).ToArray(),
                    flow.Length == 0 ? Array.Empty<FlowEnding>() : flow.Split(',').Select(Ending).ToArray());
            }

            /// <exception cref="ArgumentException">The text names no ending.</exception>
            private static FlowEnding Ending(string text) => text switch
            {
                "won" => FlowEnding.Won,
                "lost" => FlowEnding.Lost,
                _ => throw new ArgumentException($"\"{text}\" is not an ending of a level: write won or lost."),
            };

            public static int WholeNumber(string text) =>
                int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    ? number
                    : throw new ArgumentException($"\"{text}\" is not a whole number.");

            /// <summary>The value that follows a switch, or the default when the switch is not given.</summary>
            private static string After(string name, string[] arguments, string byDefault)
            {
                int at = Array.IndexOf(arguments, name);
                if (at >= 0 && at + 1 >= arguments.Length)
                {
                    throw new ArgumentException($"{name} needs a value.");
                }

                return at >= 0 ? arguments[at + 1] : byDefault;
            }

            private static ScreenShape ScreenFrom(string text)
            {
                string[] sides = text.Split('x');
                if (sides.Length != 2)
                {
                    throw new ArgumentException($"\"{text}\" is not a screen: write 1080x1920.");
                }

                return new ScreenShape(WholeNumber(sides[0]), WholeNumber(sides[1]));
            }
        }
    }
}
