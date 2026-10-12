#nullable enable
using System;
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
    /// Checks from the command line, with no game running and no picture taken, what the stage makes of a won
    /// game's finale. Watched to its end it leaves no fault and the card hangs; the shelf under the board says
    /// nothing meanwhile, and the lift of the finger that made the winning move does not end it. What it throws
    /// over the stage is drawn where it is meant to: the light it opens with on the wall or over the plaques and
    /// never on the board between them, and every picture a thrown move is made of at a place of its own. A finger
    /// that comes down in the middle of it ends it in that same call, on the engine's board, and the card is in
    /// its place as soon after as after any win; the finger presses nothing of that card. A second finger does the
    /// same while the first one rests, and so does the way back. The shelf's round button does not listen
    /// meanwhile. A win on the last move and a lost game have no finale, and their card comes as it always did.
    /// And a finale that cannot be shown costs its picture and not the card, in two ways. The check plays the
    /// engine's finale behind the stage's back, so the stage's own is refused. And it wins a game on a stage that
    /// was never laid out for a screen: the engine plays that finale and its script is written, but the stage
    /// cannot put it on its timeline. Both must be reported as a fault and got over. Those two parts write the
    /// stage's error lines to the log on purpose: three of them.
    /// usage: Unity.exe -batchmode -quit -projectPath unity -executeMethod Puzzle.Client.EditorTools.FinaleCheck.Run
    /// </summary>
    public static class FinaleCheck
    {
        /// <summary>A level with a hole in its board and crates, which a strong bot wins.</summary>
        private const int LevelNumber = 40;

        private const int MovesLeft = 12;

        /// <summary>The finale is ended so long after its first frame: its rockets stand on the board by then.</summary>
        private const float MidFinale = 1.2f;

        /// <summary>From the end of a finale, or of a turn that decides a game without one, the card hangs in its place within this.</summary>
        private const float CardIsDownWithin = Tempo.PauseBeforeResult + Tempo.CardAfterDark + Tempo.CardComesDown + 2f * StoryStudio.FrameSeconds;

        /// <summary>A stage that still moves after this long never comes to rest: a mistake, not a long turn.</summary>
        private const float LongestSettleSeconds = 30f;

        private static readonly ScreenShape Screen = new ScreenShape(1080, 2340);

        public static void Run()
        {
            try
            {
                ArtCatalogue art = AssetDatabase.LoadAssetAtPath<ArtCatalogue>(ArtCatalogueBuilder.CataloguePath);
                if (art == null)
                {
                    throw new InvalidOperationException($"{ArtCatalogueBuilder.CataloguePath} is missing; run ProjectSetup.Run first.");
                }

                if (!BotPolicies.TryCreate(SkillProfile.Expert.Name, out IBotPolicy bot))
                {
                    throw new InvalidOperationException($"The bots' list has no {SkillProfile.Expert.Name}.");
                }

                LevelDefinition level = LevelPack.Numbered(LevelPack.LoadPacked(), LevelNumber);
                var bench = new Bench(RehearsedGame.Of(level, bot), art, PaintingGallery.OfThePackedMuseum());

                Vector2 greenButton = AFinaleWatchedToItsEndLeavesNoFaultAndTheCardHangs(bench);
                WhatTheFinaleThrowsIsDrawnWhereItIsMeantTo(bench);
                AFingerEndsTheFinaleAtOnceAndPressesNothing(bench, greenButton);
                ASecondFingerEndsTheFinaleWhileTheFirstRests(bench);
                TheWayBackEndsTheFinaleAtOnce(bench);
                AWinOnTheLastMoveHasNoFinale(bench);
                ALostGameHasNoFinale(bench);
                AFinaleTheEngineRefusesCostsItsPictureAndNotTheCard(bench);
                AFinaleThatCannotBeStagedCostsItsPictureAndNotTheCard(bench);
                Debug.Log($"Finale check passed on level {LevelNumber} with {MovesLeft} moves left: watched to its end the finale left no fault and the card hung, "
                    + "the shelf said nothing meanwhile and a finger that lifted did not end it; its light lay on the wall or over the plaques and every part of a thrown move at a place of its own; "
                    + "a finger, a second finger beside a resting one, and the way back each ended it in the same call, on the engine's board, with the card in its place in time and nothing of it pressed; "
                    + "the shelf's round button did not listen meanwhile; a win on the last move and a lost game had no finale; "
                    + "and a finale the engine refused, and one the stage could not put on its timeline, were each reported as a fault and the card came all the same.");
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is ArgumentException
                || failure is IOException || failure is LevelFormatException || failure is CatalogueFormatException)
            {
                Debug.LogError($"Finale check failed: {failure.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <returns>The pixel in the middle of the card's green button, which is the same on every card of this check.</returns>
        private static Vector2 AFinaleWatchedToItsEndLeavesNoFaultAndTheCardHangs(Bench bench)
        {
            using Trial trial = bench.WonWith(MovesLeft);
            trial.Require(trial.Stage.WordsOnTheShelf == AttemptWords.HowToPlay, $"the shelf of a first try says \"{trial.Stage.WordsOnTheShelf}\" while the winning move is shown");

            // The finger of the winning move is still down when the finale begins, and lifts then: that is no touch.
            trial.Stage.PointerDown(trial.Stage.PixelOf(new Position(0, 0)));
            trial.WaitForTheFinale();
            trial.Stage.PointerUp();
            trial.Require(trial.Stage.IsShowingTheFinale, "the finger of the winning move lifted, and that ended the finale");
            trial.Require(trial.Stage.WordsOnTheShelf.Length == 0, $"the shelf says \"{trial.Stage.WordsOnTheShelf}\" while the finale is shown, where any touch ends it");
            trial.Require(trial.Stage.LeaveButtonPixel == null, "the shelf's round button listens while the finale is shown");
            int thrown = trial.Stage.Finale?.Cues.OfType<Cue.MoveThrown>().Count() ?? 0;
            trial.Require(thrown == MovesLeft, $"{thrown} moves are thrown in the finale's script; {MovesLeft} were left");

            float shown = trial.WatchTheFinaleToItsEnd();
            trial.Require(shown <= Tempo.FinaleLongest + Tempo.Land, $"the finale was shown for {StoryStudio.Seconds(shown)} s");
            trial.RequireNoFault("a finale watched to its end");
            trial.RequireTheCardWithin(CardIsDownWithin, "the end of a finale watched to its end");
            trial.Require(trial.Stage.Game.MovesLeft == MovesLeft, "the game no longer says how many moves were left");
            return trial.Stage.ResultButtonPixel ?? throw new InvalidOperationException("The card hangs and has no green button.");
        }

        /// <summary>
        /// Sprites that share a place in the draw order are drawn in whatever order the device takes them, so what
        /// lies on one another needs places of its own; and light that is meant for the wall must not be drawn
        /// over the board, which a plaque hangs close above on a wide screen.
        /// </summary>
        private static void WhatTheFinaleThrowsIsDrawnWhereItIsMeantTo(Bench bench)
        {
            using Trial trial = bench.WonWith(MovesLeft);
            trial.WaitForTheFinale();

            // The medallion lights up; no move has left it yet.
            trial.Advance(Tempo.FinaleLead / 2f);
            SpriteRenderer[] opening = trial.EffectSprites();
            trial.Require(opening.Length > 0, "nothing lights up as the finale opens");
            foreach (SpriteRenderer light in opening)
            {
                bool isOnTheWall = light.sortingOrder < (int)DrawOrder.FrameShadow;
                bool isOverThePlaques = light.sortingOrder >= (int)DrawOrder.Plaque;
                trial.Require(isOnTheWall || isOverThePlaques, $"the light \"{light.name}\" of the finale's opening is drawn at {light.sortingOrder}: over the board and its frame, under the plaques");
            }

            // The first move is half its way to the board.
            trial.Advance(Tempo.FinaleLead / 2f + Tempo.MoveFlies / 2f);
            SpriteRenderer[] flying = trial.EffectSprites()
                .Where(sprite => sprite.sortingOrder >= (int)DrawOrder.ThrownMove && sprite.sortingOrder < (int)DrawOrder.PageButton)
                .ToArray();
            foreach (ThrownMovePart part in Enum.GetValues(typeof(ThrownMovePart)))
            {
                trial.Require(flying.Any(sprite => sprite.sortingOrder == (int)DrawOrder.ThrownMove + (int)part), $"no {part} of a thrown move is drawn at its place while a move flies");
            }

            foreach (IGrouping<int, SpriteRenderer> atOnePlace in flying.GroupBy(sprite => sprite.sortingOrder))
            {
                int kinds = atOnePlace.Select(sprite => (sprite.sprite, sprite.color.r, sprite.color.g, sprite.color.b)).Distinct().Count();
                trial.Require(kinds == 1, $"{kinds} different pictures of a thrown move share the place {atOnePlace.Key} in the draw order: which lies on top is left to chance");
            }
        }

        private static void AFingerEndsTheFinaleAtOnceAndPressesNothing(Bench bench, Vector2 greenButton)
        {
            using Trial trial = bench.WonWith(MovesLeft);
            trial.WaitForTheFinale();
            trial.Advance(MidFinale);
            trial.Require(trial.Stage.IsShowingTheFinale, $"the finale is over {StoryStudio.Seconds(MidFinale)} s after it began; the check needs it shown");

            // The finger comes down where the card's green button will be and stays until the card hangs there:
            // it skipped the finale, and must not be taken for a press of that button when it lifts.
            trial.Stage.PointerDown(greenButton);
            trial.Require(!trial.Stage.IsShowingTheFinale, "a finger came down and the finale went on");
            trial.RequireNoFault("a finale ended by a finger");
            trial.RequireTheCardWithin(CardIsDownWithin, "a finger ended the finale");
            trial.Stage.PointerMoved(greenButton);
            trial.Stage.PointerUp();
            trial.Advance(2f * Tempo.CardGoesUp);
            trial.Require(trial.Stage.PlayerChose == null, $"the finger that ended the finale chose {trial.Stage.PlayerChose} on the card");
        }

        private static void ASecondFingerEndsTheFinaleWhileTheFirstRests(Bench bench)
        {
            using Trial trial = bench.WonWith(MovesLeft);

            // The finger that made the winning move stays on the glass through the turn and into the finale.
            Vector2 resting = trial.Stage.PixelOf(new Position(0, 0));
            trial.Stage.PointerDown(resting);
            trial.WaitForTheFinale();
            trial.Advance(MidFinale);
            trial.Stage.PointerMoved(resting);
            trial.Require(trial.Stage.IsShowingTheFinale, "a finger that rested on the screen since the winning move ended the finale by resting there");

            trial.Stage.AnotherFingerDown();
            trial.Require(!trial.Stage.IsShowingTheFinale, "a second finger came down while the first rested, and the finale went on");
            trial.RequireNoFault("a finale ended by a second finger");
            trial.RequireTheCardWithin(CardIsDownWithin, "a second finger ended the finale");
            trial.Stage.PointerUp();
            trial.Advance(2f * Tempo.CardGoesUp);
            trial.Require(trial.Stage.PlayerChose == null, $"the finger that rested through the finale chose {trial.Stage.PlayerChose} on the card when it lifted");
        }

        private static void TheWayBackEndsTheFinaleAtOnce(Bench bench)
        {
            using Trial trial = bench.WonWith(MovesLeft);
            trial.WaitForTheFinale();
            trial.Advance(MidFinale);
            trial.Stage.GoBack();
            trial.Require(!trial.Stage.IsShowingTheFinale, "the way back was asked for and the finale went on");
            trial.Require(!trial.Stage.IsAskingWhetherToLeave, "the way back asked whether to leave a game that is won");
            trial.RequireNoFault("a finale ended by the way back");
            trial.RequireTheCardWithin(CardIsDownWithin, "the way back ended the finale");
        }

        private static void AWinOnTheLastMoveHasNoFinale(Bench bench)
        {
            using Trial trial = bench.WonWith(movesLeft: 0);
            trial.RequireTheCardWithNoFinaleBeforeIt("a win on the last move");
            trial.Require(trial.Stage.Finale == null, "a win on the last move has a finale's script");
            trial.Require(trial.Stage.WordsOnTheShelf == AttemptWords.HowToPlay, "the shelf of a win on the last move does not say what it always said");
            trial.RequireNoFault("a win on the last move");
        }

        private static void ALostGameHasNoFinale(Bench bench)
        {
            using Trial trial = bench.LostOneMoveShortOfTheWin();
            trial.RequireTheCardWithNoFinaleBeforeIt("a lost game");
            trial.Require(trial.Stage.Finale == null, "a lost game has a finale's script");
            trial.Require(trial.Stage.WordsOnTheShelf == AttemptWords.HowToPlay, "the shelf of a lost game does not say what it always said");
            trial.RequireNoFault("the last move of a lost game");
        }

        private static void AFinaleTheEngineRefusesCostsItsPictureAndNotTheCard(Bench bench)
        {
            using Trial trial = bench.WonWith(MovesLeft);

            // Behind the stage's back: the engine's finale is played, so the stage's own call will be refused. The
            // winning turn then does not end on the engine's board either, which the stage reports first.
            trial.Stage.Game.PlayFinale();
            trial.RequireTheCardWithNoFinaleBeforeIt("a finale that the engine refused");
            trial.RequireTheFaultOfAFinaleThatCouldNotBeShown("a finale that the engine refused");
        }

        private static void AFinaleThatCannotBeStagedCostsItsPictureAndNotTheCard(Bench bench)
        {
            // A stage that was never laid out for a screen does not know where its medallion hangs. The engine
            // plays the finale and its script is written, so the view's ledger is at the finale's end; but the
            // first cue cannot be put on the timeline. The stage must end on the engine's board and hang the card.
            using Trial trial = bench.WonWithOnAStageWithoutAScreen(MovesLeft);
            trial.RequireTheStageSettlesWithNoFinaleShown("a finale that cannot be staged");
            trial.Require(trial.Stage.Game.FinalePlayed, "the engine did not play the finale that the stage then could not stage");
            trial.RequireTheFaultOfAFinaleThatCouldNotBeShown("a finale that cannot be staged");
            trial.Require(trial.Stage.LastTurnFaults.Count == 1, $"a finale that cannot be staged left more than its one fault: {string.Join("; ", trial.Stage.LastTurnFaults)}");

            // The card hangs, as the screen shows once the stage has one.
            trial.Stage.Fit(Screen);
            trial.Require(trial.Stage.ResultButtonPixel != null, "after a finale that could not be staged the result's card does not hang in its place");
        }

        /// <summary>What every part of the check starts from: the rehearsed game, and what a stage is built of.</summary>
        private sealed class Bench
        {
            private readonly RehearsedGame _rehearsed;
            private readonly ArtCatalogue _art;
            private readonly PaintingGallery _paintings;

            public Bench(RehearsedGame rehearsed, ArtCatalogue art, PaintingGallery paintings)
            {
                _rehearsed = rehearsed;
                _art = art;
                _paintings = paintings;
            }

            /// <summary>A stage on which the winning move has just been played, with so many moves left over.</summary>
            public Trial WonWith(int movesLeft) => Decided(_rehearsed.MovesToWin + movesLeft, GameStatus.Won, isLaidOut: true);

            /// <summary>The same on a stage that was never laid out for a screen, as no stage of the game is.</summary>
            public Trial WonWithOnAStageWithoutAScreen(int movesLeft) => Decided(_rehearsed.MovesToWin + movesLeft, GameStatus.Won, isLaidOut: false);

            /// <summary>A stage on which the last move of a game has just been played that had one move too few to be won.</summary>
            public Trial LostOneMoveShortOfTheWin() => Decided(_rehearsed.MovesToWin - 1, GameStatus.LostOutOfMoves, isLaidOut: true);

            /// <exception cref="InvalidOperationException">The game did not end as it was meant to.</exception>
            private Trial Decided(int moves, GameStatus ending, bool isLaidOut)
            {
                // A scene of its own that is never saved: nothing of an earlier part is left in this one.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EffectArt effectArt = EffectArt.Paint();
                Game game = _rehearsed.BeforeItsDecidingMoveWith(moves);
                GameStage stage = isLaidOut
                    ? FinaleStory.OpenAtRest(game, _art, effectArt, _paintings, Screen)
                    : OpenAtRestWithoutAScreen(game, effectArt);
                var trial = new Trial(stage, effectArt);
                if (!stage.TryPlay(_rehearsed.DecidingMoveWith(moves)) || stage.Game.Status != ending)
                {
                    trial.Dispose();
                    throw new InvalidOperationException($"With {moves} moves level {LevelNumber} is {stage.Game.Status} after its deciding move, not {ending}.");
                }

                return trial;
            }

            private GameStage OpenAtRestWithoutAScreen(Game game, EffectArt effectArt)
            {
                GameStage stage = GameStage.Open(game, _art, effectArt, _paintings, AttemptBrief.FirstTry);
                StageSteps.Settle(stage);
                return stage;
            }
        }

        /// <summary>One stage of the check, with the deciding move just played on it.</summary>
        private sealed class Trial : IDisposable
        {
            private readonly EffectArt _effectArt;

            public Trial(GameStage stage, EffectArt effectArt)
            {
                Stage = stage;
                _effectArt = effectArt;
            }

            public GameStage Stage { get; }

            public void Dispose()
            {
                Stage.Dispose();
                _effectArt.Dispose();
            }

            public void Advance(float seconds) => StageSteps.Advance(Stage, seconds);

            /// <summary>How long the deciding move's turn is shown at the most.</summary>
            private float LongestTurnSeconds => StageSteps.ATurnIsOverWithin(Stage.LastTurn?.Seconds ?? 0f);

            /// <summary>Moves the clock to the step in which the winning move's turn ends and the finale begins.</summary>
            /// <exception cref="InvalidOperationException">No finale began.</exception>
            public void WaitForTheFinale() =>
                StageSteps.AdvanceToTheFinale(Stage, () => Stage.Advance(StoryStudio.FrameSeconds), Stage.LastTurn?.Seconds ?? 0f);

            /// <summary>The sprites of what is thrown over the board just now: the effects' own layer of the one stage in the scene.</summary>
            /// <exception cref="InvalidOperationException">The scene has no such layer.</exception>
            public SpriteRenderer[] EffectSprites()
            {
                GameObject layer = GameObject.Find(BoardEffects.LayerName);
                if (layer == null)
                {
                    throw new InvalidOperationException($"The scene has no object called \"{BoardEffects.LayerName}\"; the check looks for the effects' sprites under it.");
                }

                return layer.GetComponentsInChildren<SpriteRenderer>();
            }

            /// <summary>
            /// Watches the deciding move's turn and what follows it, step by step: no finale is shown at any time,
            /// and the card hangs in its place no later than it does after a turn that takes all its time.
            /// </summary>
            /// <exception cref="InvalidOperationException">A finale was shown, or the card did not come in time.</exception>
            public void RequireTheCardWithNoFinaleBeforeIt(string what)
            {
                float longest = LongestTurnSeconds + CardIsDownWithin;
                for (float waited = 0f; Stage.ResultButtonPixel == null; waited += StoryStudio.FrameSeconds)
                {
                    Require(!Stage.IsShowingTheFinale, $"{what} has a finale");
                    Require(waited < longest, $"{StoryStudio.Seconds(longest)} s after the deciding move of {what} the result's card does not hang in its place");
                    Stage.Advance(StoryStudio.FrameSeconds);
                }

                Require(!Stage.IsShowingTheFinale, $"{what} has a finale");
            }

            /// <summary>
            /// The same for a stage that has no screen, whose card has no pixel to be found at: step by step until
            /// nothing on it moves any more, and no finale is shown at any time.
            /// </summary>
            /// <exception cref="InvalidOperationException">A finale was shown, or the stage did not come to rest.</exception>
            public void RequireTheStageSettlesWithNoFinaleShown(string what)
            {
                for (float waited = 0f; !Stage.IsSettled; waited += StoryStudio.FrameSeconds)
                {
                    Require(!Stage.IsShowingTheFinale, $"{what} is shown all the same");
                    Require(waited < LongestSettleSeconds, $"{StoryStudio.Seconds(LongestSettleSeconds)} s after the deciding move of {what} the stage is still moving");
                    Stage.Advance(StoryStudio.FrameSeconds);
                }
            }

            /// <returns>How long the finale was shown from here on.</returns>
            /// <exception cref="InvalidOperationException">The finale does not end.</exception>
            public float WatchTheFinaleToItsEnd()
            {
                float watched = 0f;
                while (Stage.IsShowingTheFinale)
                {
                    Require(watched < 2f * Tempo.FinaleLongest, $"the finale is still shown after {StoryStudio.Seconds(watched)} s");
                    Stage.Advance(StoryStudio.FrameSeconds);
                    watched += StoryStudio.FrameSeconds;
                }

                return watched;
            }

            /// <exception cref="InvalidOperationException">The card does not hang in its place, listening, so long after now.</exception>
            public void RequireTheCardWithin(float seconds, string after)
            {
                Advance(seconds);
                Require(Stage.ResultButtonPixel != null, $"{StoryStudio.Seconds(seconds)} s after {after} the result's card does not hang in its place");
            }

            /// <exception cref="InvalidOperationException">The stage reported a fault.</exception>
            public void RequireNoFault(string after) =>
                Require(Stage.LastTurnFaults.Count == 0, $"after {after} the stage reports: {string.Join("; ", Stage.LastTurnFaults)}");

            /// <exception cref="InvalidOperationException">The stage did not report that its finale could not be shown.</exception>
            public void RequireTheFaultOfAFinaleThatCouldNotBeShown(string what) =>
                Require(
                    Stage.LastTurnFaults.Any(fault => fault.StartsWith("finale: the finale could not be shown", StringComparison.Ordinal)),
                    $"{what} was not reported: the stage's faults are \"{string.Join("; ", Stage.LastTurnFaults)}\"");

            /// <exception cref="InvalidOperationException">What must hold does not.</exception>
            public void Require(bool holds, string otherwise)
            {
                if (!holds)
                {
                    throw new InvalidOperationException(otherwise);
                }
            }
        }
    }
}
