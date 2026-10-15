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
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Museum.Catalogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Pictures the finale of a won game from the command line, with no game running: a level is won with a chosen
    /// count of moves left over, and what the stage shows from the finale's first frame on is pictured at chosen
    /// moments, with one sheet of them all. The clock is moved in the steps of a 60 Hz screen, so a picture shows
    /// what a frame of the game shows. The command fails when the finale does not begin, when the stage reports a
    /// fault in the winning turn or in the finale, and when the finale is shown for longer than it may be.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.FinaleStory.Run
    ///            [-finaleLevels 40,147] [-finaleLeft 3,12] [-finaleSize 1080x2340] [-finaleBot expert]
    ///            [-finaleTimes 0,0.1,0.3,0.5,0.8,1.1,1.5,2.0,2.6,3.2,end,card] [-finaleBefore 2]
    ///            [-finaleStrip 1.9:12 | -finaleStrip aftermath:16] [-finaleSkipAt 1.6] [-finaleEarly 0.2]
    ///   -finaleLeft   how many moves are left over when the level is won; a bot plays the level once to find the
    ///                 moves of a win, and the same game is then given that many more moves than the win took.
    ///                 With 0 the level is won on its last move, which has no finale: pictured are the board at
    ///                 rest at the end of the winning turn and the card, and the command fails if a finale is shown
    ///   -finaleTimes  seconds after the finale's first frame (0 is that frame), "end" for the frame that ends it
    ///                 (the board at rest, before the room darkens) and "card" for the result's card in its place;
    ///                 a moment after the finale's end is left out
    ///   -finaleBefore so many of the winning turn's last frames are pictured too, before the finale's first: the
    ///                 hand-over from the turn to the finale, frame by frame
    ///   -finaleStrip  instead of the moments: so many frames in a row, one for every step of a 60 Hz screen, from
    ///                 a moment of the finale on; "aftermath" for the moment begins a few frames before the board
    ///                 has come down after the last rockets, where the finale's pace may change. What moves is
    ///                 judged from these, since a still every third of a second cannot show it
    ///   -finaleSkipAt instead of the moments: a finger comes down in the middle of the board so long after the
    ///                 finale's first frame; pictured are the frame before, the frame of the touch, a moment after
    ///                 it, and the card
    ///   -finaleEarly  the level's goals are cut to this part of them (above 0, up to 1), so it is won while most
    ///                 of its crates, tapes and slabs still stand. No level of the game ends like that; this shows
    ///                 what the finale's rockets make of what stands in their way
    ///   -finalePiece  the win gives the jigsaw piece a player with so many levels won is given (46: the fifth
    ///                 painting's seventh piece; 49: its last), on the plaque and the card; left out, it gives none
    /// Pictures go to out/unity/shots/finale/finale-level-NNNN-left-NN-SIZE-tS.SS.png (…-before-N.png, …-end.png,
    /// …-card.png) with a sheet (…-sheet.png); an early win has -earlyNN before the size, a skip -skip and a strip
    /// -stripS.SS after it, with its frames as …-fNN.png. Do not pass -nographics: the pictures need a graphics device.
    /// </summary>
    public static class FinaleStory
    {
        private const string Folder = "finale";

        /// <summary>A stage that still moves after this long never comes to rest: a mistake, not a long finale.</summary>
        private const float LongestSettleSeconds = 30f;

        /// <summary>A finale has more moments than a row can show at a size worth looking at: its sheet begins a new row after so many.</summary>
        private const int PicturesInARowOfTheSheet = 6;

        /// <summary>A skipped finale is pictured once more this long after the touch, while the light over its board fades.</summary>
        private const float AfterTheTouch = 3f * StoryStudio.FrameSeconds;

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
                PaintingGallery paintings = PaintingGallery.OfThePackedMuseum();
                foreach (int levelNumber in plan.LevelNumbers)
                {
                    LevelDefinition level = LevelPack.Numbered(levels, levelNumber);
                    RehearsedGame rehearsed = RehearsedGame.Of(plan.GoalsPart is float part ? EarlyWin.Of(level, part) : level, plan.Bot);
                    AttemptBrief brief = plan.BriefOfTheAttempt(levels.Count, paintings);
                    foreach (int movesLeft in plan.MovesLeft)
                    {
                        Tell(rehearsed, movesLeft, plan, brief, art, paintings);
                    }
                }
            }
            catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Finale story failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// A stage on a game that its next move decides, laid out for a screen and at rest: the board's entrance is
        /// over. The caller plays the deciding move on it and disposes of it.
        /// </summary>
        internal static GameStage OpenAtRest(Game game, ArtCatalogue art, EffectArt effectArt, PaintingGallery paintings, ScreenShape screen, AttemptBrief brief)
        {
            GameStage stage = GameStage.Open(game, art, effectArt, paintings, brief, StoryStudio.ShelfOf(game));
            stage.Fit(screen);
            stage.Tell(StoryStudio.CanvasWallet);
            StageSteps.Settle(stage);
            return stage;
        }

        /// <summary>Wins the rehearsed game on a stage with so many moves left, and pictures its finale as the plan says.</summary>
        /// <exception cref="InvalidOperationException">The finale did not begin, was at fault, or was shown for too long.</exception>
        private static void Tell(RehearsedGame rehearsed, int movesLeft, Plan plan, AttemptBrief brief, ArtCatalogue art, PaintingGallery paintings)
        {
            // A scene of its own that is never saved: nothing of an earlier story is left in this one.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int level = rehearsed.Level.Id.Value;
            int moves = rehearsed.MovesToWin + movesLeft;
            using EffectArt effectArt = EffectArt.Paint();
            using GameStage stage = OpenAtRest(rehearsed.BeforeItsDecidingMoveWith(moves), art, effectArt, paintings, plan.Screen, brief);
            if (!stage.TryPlay(rehearsed.DecidingMoveWith(moves)) || stage.Game.Status != GameStatus.Won || stage.Game.MovesLeft != movesLeft)
            {
                throw new InvalidOperationException($"Level {level} was not won with {movesLeft} moves left: it is {stage.Game.Status} with {stage.Game.MovesLeft} left.");
            }

            var film = new Film(stage, plan.Screen, plan.NameOf(level, movesLeft));
            if (movesLeft == 0)
            {
                TellAWinOnTheLastMove(film, stage);
                return;
            }

            film.WaitForTheFinale(plan.FramesBefore);
            TurnScript script = stage.Finale ?? throw new InvalidOperationException($"The finale of level {level} is shown and has no script.");
            if (plan.SkipAt is float skipAt)
            {
                PictureTheSkip(film, skipAt);
            }
            else if (plan.Strip is Strip strip)
            {
                PictureTheStrip(film, strip, script);
            }
            else
            {
                PictureTheMoments(film, plan.Moments);
            }

            film.Settle();
            film.WriteSheet();
            if (stage.LastTurnFaults.Count > 0)
            {
                throw new InvalidOperationException($"The stage did not show level {level} as the game played it: {string.Join("; ", stage.LastTurnFaults)}");
            }

            float shown = film.FinaleSeconds ?? throw new InvalidOperationException($"The finale of level {level} never ended.");
            if (shown > Tempo.FinaleLongest + Tempo.Land)
            {
                throw new InvalidOperationException($"The finale of level {level} with {movesLeft} moves left was shown for {StoryStudio.Seconds(shown)} s; it may take {StoryStudio.Seconds(Tempo.FinaleLongest)} s.");
            }

            int thrown = script.Cues.OfType<Cue.MoveThrown>().Count();
            FinalePace pace = FinalePace.Of(script, thrown);
            Debug.Log($"Finale story: {film.Name}: the win took {rehearsed.MovesToWin} moves; {Describe(script)}; "
                + $"script {StoryStudio.Seconds(script.Seconds)} s, of which the show {StoryStudio.Seconds(script.ShowSeconds ?? script.Seconds)} s "
                + $"(played at {StoryStudio.Seconds(pace.OfTheShow)}, the aftermath at {StoryStudio.Seconds(pace.OfTheAftermath)}); "
                + $"shown for {StoryStudio.Seconds(shown)} s{(plan.SkipAt.HasValue ? " (ended by a touch)" : string.Empty)} where {thrown} moves may take {StoryStudio.Seconds(Tempo.FinaleMayTake(thrown))} s; "
                + "no fault, and the card hangs.");
        }

        /// <summary>
        /// A level won on its last move has no finale and must look as it did before there was one: the board at
        /// rest when the winning turn is over, then the card.
        /// </summary>
        /// <exception cref="InvalidOperationException">A finale was shown, or the stage reported a fault.</exception>
        private static void TellAWinOnTheLastMove(Film film, GameStage stage)
        {
            film.Advance(StageSteps.ATurnIsOverWithin(stage.LastTurn?.Seconds ?? 0f));
            film.Shoot("end");
            film.Settle();
            film.Shoot("card");
            film.WriteSheet();
            if (film.SawTheFinale || stage.Finale != null)
            {
                throw new InvalidOperationException($"{film.Name} was won on its last move and a finale was shown.");
            }

            if (stage.LastTurnFaults.Count > 0)
            {
                throw new InvalidOperationException($"The stage did not show {film.Name} as the game played it: {string.Join("; ", stage.LastTurnFaults)}");
            }

            Debug.Log($"Finale story: {film.Name}: won on its last move; no finale, no fault, and the card hangs.");
        }

        /// <summary>
        /// Frames in a row from a moment of the finale on, one for every step of the stage's clock: what moves
        /// between two stills of the story, and whether the finale's pace changes where the eye can catch it.
        /// </summary>
        private static void PictureTheStrip(Film film, Strip strip, TurnScript script)
        {
            if (!(strip.BeginsIn(script) is float from))
            {
                Debug.Log($"Finale story: {film.Name} has no aftermath; no strip.");
                return;
            }

            film.AdvanceWithinTheFinaleTo(from);
            for (int frame = 0; frame < strip.Frames; frame++)
            {
                if (film.FinaleSeconds.HasValue)
                {
                    Debug.Log($"Finale story: {film.Name} is over {frame} frames into the strip; no more pictures.");
                    return;
                }

                film.Shoot($"f{frame:D2}");
                film.StepAFrame();
            }
        }

        private static void PictureTheMoments(Film film, IReadOnlyList<Moment> moments)
        {
            foreach (Moment moment in moments)
            {
                switch (moment.Kind)
                {
                    case MomentKind.Seconds:
                        film.AdvanceWithinTheFinaleTo(moment.Seconds);
                        if (film.FinaleSeconds.HasValue)
                        {
                            Debug.Log($"Finale story: {film.Name} is over before {moment.Label}; no picture.");
                            continue;
                        }

                        break;
                    case MomentKind.TheEnd:
                        film.AdvanceToTheEndOfTheFinale();
                        break;
                    case MomentKind.TheCard:
                        film.Settle();
                        break;
                    default:
                        throw new InvalidOperationException($"A moment of the kind {moment.Kind} is not known.");
                }

                film.Shoot(moment.Label);
            }
        }

        /// <summary>A finger comes down in the middle of the board in mid-finale: the frame before, the frame of the touch, a moment after, the card.</summary>
        /// <exception cref="InvalidOperationException">The finale was over before the touch, or the touch did not end it.</exception>
        private static void PictureTheSkip(Film film, float skipAt)
        {
            film.AdvanceWithinTheFinaleTo(skipAt);
            if (film.FinaleSeconds.HasValue)
            {
                throw new InvalidOperationException($"{film.Name} was over before {StoryStudio.Seconds(skipAt)} s; touch it earlier.");
            }

            film.Shoot("1-before");
            film.TouchTheMiddleOfTheBoard();
            if (!film.FinaleSeconds.HasValue)
            {
                throw new InvalidOperationException($"A finger came down in the finale {film.Name} and the finale went on.");
            }

            film.Shoot("2-touch");
            film.Advance(AfterTheTouch);
            film.Shoot("3-after");
            film.Settle();
            film.Shoot("4-card");
        }

        /// <summary>What a finale's script holds, in a line.</summary>
        private static string Describe(TurnScript script)
        {
            Cue.MoveThrown[] thrown = script.Cues.OfType<Cue.MoveThrown>().ToArray();

            // The moves of one wave leave closer together than one of them takes to fly.
            int waves = thrown.Where((move, index) => index == 0 || move.Start - thrown[index - 1].Start > Tempo.MoveFlies).Count();
            IEnumerable<string> effects = script.Cues.OfType<Cue.Effect>().GroupBy(effect => effect.Kind).Select(kind => $"{kind.Key} x{kind.Count()}");
            return $"{thrown.Length} moves thrown in {waves} wave(s), {script.Cues.OfType<Cue.Leave>().Count()} pieces gone, "
                + $"{script.Cues.OfType<Cue.Reface>().Count()} crates hit, {script.Cues.OfType<Cue.TapeHit>().Count()} tape hits, "
                + $"{script.Cues.OfType<Cue.SlabHit>().Count()} slab hits ({string.Join(", ", effects)})";
        }

        /// <summary>The stage of one finale with a clock that counts from the finale's first frame, and the pictures taken of it.</summary>
        private sealed class Film
        {
            private readonly GameStage _stage;
            private readonly ScreenShape _screen;
            private readonly ContactSheet _sheet = new ContactSheet(StoryStudio.SheetShrinksBy, PicturesInARowOfTheSheet);

            public Film(GameStage stage, ScreenShape screen, string name)
            {
                _stage = stage;
                _screen = screen;
                Name = name;
            }

            public string Name { get; }

            /// <summary>Seconds since the finale's first frame.</summary>
            public float Clock { get; private set; }

            /// <summary>How long the finale was shown; nothing while it is still shown, and for a game that has none.</summary>
            public float? FinaleSeconds { get; private set; }

            /// <summary>True once a step of the clock has found the stage showing a finale.</summary>
            public bool SawTheFinale { get; private set; }

            /// <summary>
            /// Moves the clock on to the step in which the winning move's turn ends and the finale begins: its
            /// first frame. The last frames of the turn before that are pictured too, as many as are asked for:
            /// which they are is only known once the finale has begun, so every frame of the turn is taken and
            /// the last ones are kept.
            /// </summary>
            /// <exception cref="InvalidOperationException">The turn came to its end and no finale began.</exception>
            public void WaitForTheFinale(int framesBefore)
            {
                var lastFrames = new Queue<Texture2D>();
                try
                {
                    StageSteps.AdvanceToTheFinale(_stage, () => StepTheTurn(lastFrames, framesBefore), _stage.LastTurn?.Seconds ?? 0f);
                    int before = lastFrames.Count;
                    foreach (Texture2D frame in lastFrames)
                    {
                        StoryStudio.Keep(frame, _sheet, $"{Folder}/{Name}-before-{before--}.png");
                    }
                }
                finally
                {
                    foreach (Texture2D frame in lastFrames)
                    {
                        UnityEngine.Object.DestroyImmediate(frame);
                    }
                }

                SawTheFinale = true;
                Clock = 0f;
            }

            /// <summary>One frame of the winning turn: what is seen before the step is taken, when frames are kept, and the step.</summary>
            private void StepTheTurn(Queue<Texture2D> lastFrames, int kept)
            {
                if (kept > 0)
                {
                    lastFrames.Enqueue(StoryStudio.Take(_stage, _screen));
                    if (lastFrames.Count > kept)
                    {
                        UnityEngine.Object.DestroyImmediate(lastFrames.Dequeue());
                    }
                }

                _stage.Advance(StoryStudio.FrameSeconds);
            }

            /// <summary>Moves the clock one frame on.</summary>
            public void StepAFrame() => Step();

            public void Advance(float seconds)
            {
                float until = Clock + seconds;
                while (Clock + StoryStudio.FrameSeconds / 2f < until)
                {
                    Step();
                }
            }

            /// <summary>Moves the clock to a moment, or to the finale's end if that comes first.</summary>
            public void AdvanceWithinTheFinaleTo(float seconds)
            {
                while (_stage.IsShowingTheFinale && Clock + StoryStudio.FrameSeconds / 2f < seconds)
                {
                    Step();
                }
            }

            public void AdvanceToTheEndOfTheFinale() => StepUntil(() => !_stage.IsShowingTheFinale);

            public void Settle() => StepUntil(() => _stage.IsSettled);

            public void TouchTheMiddleOfTheBoard()
            {
                BoardShape board = _stage.Game.Board.Shape;
                _stage.PointerDown(_stage.PixelOf(new Position(board.Width / 2, board.Height / 2)));
                NoteTheFinalesEnd();
                _stage.PointerUp();
            }

            public void Shoot(string what) => StoryStudio.Shoot(_stage, _screen, _sheet, $"{Folder}/{Name}-{what}.png");

            public void WriteSheet() => StoryStudio.WriteSheet(_sheet, $"{Folder}/{Name}-sheet.png");

            /// <exception cref="InvalidOperationException">The stage does not get there.</exception>
            private void StepUntil(Func<bool> isThere)
            {
                float waited = 0f;
                while (!isThere())
                {
                    if (waited > LongestSettleSeconds)
                    {
                        throw new InvalidOperationException($"The stage of {Name} is still moving after {LongestSettleSeconds} s.");
                    }

                    Step();
                    waited += StoryStudio.FrameSeconds;
                }
            }

            private void Step()
            {
                _stage.Advance(StoryStudio.FrameSeconds);
                Clock += StoryStudio.FrameSeconds;
                SawTheFinale |= _stage.IsShowingTheFinale;
                NoteTheFinalesEnd();
            }

            private void NoteTheFinalesEnd()
            {
                if (SawTheFinale && !FinaleSeconds.HasValue && !_stage.IsShowingTheFinale)
                {
                    FinaleSeconds = Clock;
                }
            }
        }

        /// <summary>Frames in a row to be pictured, from a moment of a finale on.</summary>
        private readonly struct Strip
        {
            /// <summary>The strip that begins with the aftermath starts so many frames before it: the last of the show's own pace.</summary>
            private const int FramesBeforeTheAftermath = 3;

            /// <summary>The moment the strip begins at, in seconds after the finale's first frame; nothing for a strip that begins with the aftermath.</summary>
            private readonly float? _fromSeconds;

            private Strip(float? fromSeconds, int frames)
            {
                _fromSeconds = fromSeconds;
                Frames = frames;
            }

            public int Frames { get; }

            /// <summary>How the strip's beginning is written in a file name.</summary>
            public string Label => _fromSeconds is float seconds ? StoryStudio.Seconds(seconds) : "aftermath";

            /// <summary>
            /// When the strip begins, in seconds after the finale's first frame; nothing for a strip of the
            /// aftermath in a finale that has none. The show before an aftermath is played as written but for a
            /// finale beyond its longest time, so the script's seconds are the clock's up to there.
            /// </summary>
            public float? BeginsIn(TurnScript script)
            {
                if (_fromSeconds is float seconds)
                {
                    return seconds;
                }

                return script.ShowSeconds is float show
                    ? Mathf.Max(0f, show - FramesBeforeTheAftermath * StoryStudio.FrameSeconds)
                    : (float?)null;
            }

            /// <exception cref="ArgumentException">The text is not "seconds:frames" or "aftermath:frames" with a frame at least.</exception>
            public static Strip Parse(string text)
            {
                string[] parts = text.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int frames) || frames < 1)
                {
                    throw new ArgumentException($"\"{text}\" is not a strip: write 1.9:12 or aftermath:16.");
                }

                return new Strip(parts[0] == "aftermath" ? (float?)null : Plan.NumberFromNothingUp(parts[0]), frames);
            }
        }

        /// <summary>What a moment of a finale's story is told by. A closed set.</summary>
        private enum MomentKind
        {
            /// <summary>So many seconds after the finale's first frame.</summary>
            Seconds,

            /// <summary>The frame that ends the finale: the board at rest, before the room darkens.</summary>
            TheEnd,

            /// <summary>The result's card in its place.</summary>
            TheCard,
        }

        /// <summary>A moment of a finale to take a picture at.</summary>
        private readonly struct Moment
        {
            private Moment(MomentKind kind, float seconds, string label)
            {
                Kind = kind;
                Seconds = seconds;
                Label = label;
            }

            public MomentKind Kind { get; }

            /// <summary>Only meaningful for a moment told in seconds.</summary>
            public float Seconds { get; }

            /// <summary>How the moment is written in a file name.</summary>
            public string Label { get; }

            /// <exception cref="ArgumentException">The text is neither "end", "card" nor a number of seconds from 0 up.</exception>
            public static Moment Parse(string text)
            {
                switch (text)
                {
                    case "end":
                        return new Moment(MomentKind.TheEnd, 0f, "end");
                    case "card":
                        return new Moment(MomentKind.TheCard, 0f, "card");
                    default:
                        return new Moment(MomentKind.Seconds, Plan.NumberFromNothingUp(text), $"t{StoryStudio.Seconds(Plan.NumberFromNothingUp(text))}");
                }
            }
        }

        /// <summary>What one run tells: which levels, won with how many moves left, on which screen, pictured when.</summary>
        private sealed class Plan
        {
            private Plan(
                IReadOnlyList<int> levelNumbers,
                IReadOnlyList<int> movesLeft,
                ScreenShape screen,
                IBotPolicy bot,
                IReadOnlyList<Moment> moments,
                int framesBefore,
                Strip? strip,
                float? skipAt,
                float? goalsPart,
                int? pieceAfterLevelsWon)
            {
                LevelNumbers = levelNumbers;
                MovesLeft = movesLeft;
                Screen = screen;
                Bot = bot;
                Moments = moments;
                FramesBefore = framesBefore;
                Strip = strip;
                SkipAt = skipAt;
                GoalsPart = goalsPart;
                PieceAfterLevelsWon = pieceAfterLevelsWon;
            }

            /// <summary>The win gives the piece that a player with so many levels won is given; nothing when it gives none.</summary>
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
                    ?? throw new ArgumentException($"A player with {levelsWon} levels won is given no piece: -finalePiece names one who is.");
                return new AttemptBrief(attemptNumber: 1, isOnLastLevel: false, StoryStudio.Economy.Win, reward);
            }

            public IReadOnlyList<int> LevelNumbers { get; }

            public IReadOnlyList<int> MovesLeft { get; }

            public ScreenShape Screen { get; }

            public IBotPolicy Bot { get; }

            /// <summary>In the order the clock reaches them: the timed ones from the earliest on, then the end, then the card.</summary>
            public IReadOnlyList<Moment> Moments { get; }

            /// <summary>How many of the winning turn's last frames are pictured before the finale's first; none unless asked for.</summary>
            public int FramesBefore { get; }

            /// <summary>The frames in a row that are pictured in place of the moments; nothing when the moments are.</summary>
            public Strip? Strip { get; }

            /// <summary>When a finger skips the finale, in seconds after its first frame; nothing when it is watched to its end.</summary>
            public float? SkipAt { get; }

            /// <summary>The part the levels' goals are cut to, for a win with obstacles standing; nothing for the levels as they are.</summary>
            public float? GoalsPart { get; }

            /// <summary>The name of the pictures of one finale.</summary>
            public string NameOf(int level, int movesLeft)
            {
                string early = GoalsPart is float part ? $"-early{Mathf.RoundToInt(100f * part):D2}" : string.Empty;
                string piece = PieceAfterLevelsWon is int levelsWon ? $"-piece{levelsWon}" : string.Empty;
                string skip = SkipAt.HasValue ? "-skip" : string.Empty;
                string strip = Strip is Strip frames ? $"-strip{frames.Label}" : string.Empty;
                return $"finale-level-{level:D4}-left-{movesLeft:D2}{early}{piece}-{Screen}{skip}{strip}";
            }

            /// <exception cref="ArgumentException">An argument is not written as the usage says.</exception>
            public static Plan FromCommandLine(string[] arguments)
            {
                string botName = After("-finaleBot", arguments, SkillProfile.Expert.Name);
                if (!BotPolicies.TryCreate(botName, out IBotPolicy bot))
                {
                    throw new ArgumentException($"\"{botName}\" is not a bot; the bots are {string.Join(", ", BotPolicies.Names)}.");
                }

                string skipAt = After("-finaleSkipAt", arguments, string.Empty);
                string strip = After("-finaleStrip", arguments, string.Empty);
                string early = After("-finaleEarly", arguments, string.Empty);
                string piece = After("-finalePiece", arguments, string.Empty);
                if (skipAt.Length > 0 && strip.Length > 0)
                {
                    throw new ArgumentException("A finale is skipped or pictured as a strip, not both: give -finaleSkipAt or -finaleStrip.");
                }

                return new Plan(
                    After("-finaleLevels", arguments, "40").Split(',').Select(WholeNumber).ToArray(),
                    After("-finaleLeft", arguments, "3,12").Split(',').Select(WholeNumber).ToArray(),
                    ScreenFrom(After("-finaleSize", arguments, "1080x2340")),
                    bot,
                    After("-finaleTimes", arguments, "0,0.1,0.3,0.5,0.8,1.1,1.5,2.0,2.6,3.2,end,card").Split(',').Select(Moment.Parse)
                        .OrderBy(moment => moment.Kind).ThenBy(moment => moment.Seconds).ToArray(),
                    WholeNumber(After("-finaleBefore", arguments, "0")),
                    strip.Length == 0 ? (Strip?)null : FinaleStory.Strip.Parse(strip),
                    skipAt.Length == 0 ? (float?)null : NumberFromNothingUp(skipAt),
                    early.Length == 0 ? (float?)null : NumberFromNothingUp(early),
                    piece.Length == 0 ? (int?)null : WholeNumber(piece));
            }

            /// <exception cref="ArgumentException">The text is not a number from 0 up.</exception>
            public static float NumberFromNothingUp(string text) =>
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds >= 0f
                    ? seconds
                    : throw new ArgumentException($"\"{text}\" is not a number from 0 up, such as 0.25.");

            private static int WholeNumber(string text) =>
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
                    throw new ArgumentException($"\"{text}\" is not a screen: write 1080x2340.");
                }

                return new ScreenShape(WholeNumber(sides[0]), WholeNumber(sides[1]));
            }
        }
    }
}
