#nullable enable
using System;
using System.Linq;
using Puzzle.Bot;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Randomness;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the tutorial, once on each screen, on the game's own session and by touches alone, in the language
    /// the tools speak (<c>walk-tutorial-...</c>). A new player goes from the hall into level 1: the goal plaque is lit
    /// and said (a touch on the wall goes on), the match is shown by hand (a swipe of another trade is refused and
    /// plays nothing, the swipe of the trade shown plays it), and after it the moves medallion is lit and said. The
    /// level is played out (the expert bot), and its card's blue button opens the jigsaw, where the piece won is lit in
    /// the tray with its place and the button under the tray takes no finger; a finger carries it home and the step is
    /// done. Through level 2 (the four shown by hand) and level 3 (the five) the walk goes on; then, each with a player
    /// who has seen the steps before, the five that waits on level 3's second board while its board offers none (it says
    /// nothing, and the board takes the bot's moves, until a five is there), the crates on the first board that has
    /// them (level 11), and the first painting made whole (level 10's piece placed last), celebrated with a word about
    /// the museum that a touch ends. Every step done is in the player's progress, and none comes twice; the records of
    /// levels 1 to 3 each count the move shown by hand (<c>"guidedMoves":1</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>A step is pictured this long after it began: its dark is fully down, and a move shown by hand is at its nudge.</summary>
        private const float LessonMoment = 0.6f;

        /// <summary>At most this many moves are played on level 3's second board while the five waits for its move.</summary>
        private const int MostMovesTheFiveWaits = 12;

        /// <summary>A level played with the tutorial is decided in fewer turns than this (moves, touches and swipes together).</summary>
        private const int MostTurnsOfALevel = 400;

        /// <summary>The first painting's pieces are won on levels 1 to 10; the tenth is placed last.</summary>
        private const int LastLevelOfTheFirstPainting = 10;

        /// <summary>The first level that has crates, as the level files have it (TutorialPlan.FirstLevelWith).</summary>
        private static int FirstLevelWithCrates => TutorialPlan.FirstLevelWith[TutorialStep.Box];

        /// <returns>How many pictures the walk took.</returns>
        public static int RunTheTutorialWalk(ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var records = new AttemptsInMemory(StoryStudio.Device);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock,
                new ProgressInMemory(PlayerProgress.NewPlayer(StoryStudio.Economy.StartingWallet)), records, screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-tutorial-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            reel.Settle();
            reel.Shoot("home");

            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            reel.Settle();
            LookAt(reel, TutorialStep.Goal, "goal");
            ShowTheMoveByHand(reel, TutorialStep.Match, "match", triesAnotherTradeFirst: true);
            LookAt(reel, TutorialStep.Moves, "moves");
            RequireWon(PlayWithTheTutorial(reel), level: 1);
            PlaceTheFirstPiece(reel);
            GoOnFromTheCard(reel, GameStatus.Won);

            foreach ((int level, TutorialStep step, string name) in new[] { (2, TutorialStep.FourInALine, "four"), (3, TutorialStep.FiveInALine, "five") })
            {
                ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
                reel.Settle();
                ShowTheMoveByHand(reel, step, name, triesAnotherTradeFirst: false);
                RequireWon(PlayWithTheTutorial(reel), level);
                GoOnFromTheCard(reel, GameStatus.Won);
            }

            TutorialSeen seen = session.Progress.Tutorial;
            ThroughTheCard.Require(
                seen.Equals(TutorialSeen.Of(new[] { TutorialStep.Goal, TutorialStep.Match, TutorialStep.Moves, TutorialStep.FourInALine, TutorialStep.FiveInALine, TutorialStep.FirstPiece })),
                $"after levels 1 to 3 the player has seen {seen}");
            ThroughTheCard.Require(session.Progress.LevelsWon == 3 && !session.HasUnsavedProgress, $"after levels 1 to 3 the progress is \"{session.Progress}\", or is not saved");
            ThroughTheCard.Require(
                records.Records.Count == 3 && records.Records.All(record => record.GuidedMoves == 1 && AttemptLine.Of(record).Contains("\"guidedMoves\":1")),
                $"the records of levels 1 to 3 count guided moves {string.Join(", ", records.Records.Select(record => record.GuidedMoves))}, not one each");

            reel.WriteSheet();
            (string waited, int waitingPictures) = WaitForTheFive(screen, studio, seen);
            int cratePictures = LookAtTheFirstCrates(screen, studio, seen);
            int paintingPictures = CelebrateTheFirstPainting(screen, studio, seen);
            Debug.Log(
                $"Screen shots: walk {reel.Name}: a new player was shown the goal, the match by hand (another trade refused) and the moves on level 1, the first piece "
                + $"on the jigsaw, the four on level 2 and the five on level 3; {waited}; the crates on level {FirstLevelWithCrates} and the first painting made whole "
                + "were shown to players who had seen the rest. Every step was kept in the progress once.");
            return reel.Taken + waitingPictures + cratePictures + paintingPictures;
        }

        /// <summary>
        /// A step that is looked at shows: it is pictured as it stands, its pill says the step's words, the board takes
        /// no move, and a touch on the wall ends it and is kept in the progress.
        /// </summary>
        private static void LookAt(Reel reel, TutorialStep step, string name)
        {
            GameSession session = reel.Session;
            GameStage stage = session.Stage;
            var look = stage.LessonShown as Lesson.Look ?? throw new InvalidOperationException($"No step was looked at where {step.NameOf()} was due: \"{stage.LessonShown}\".");
            ThroughTheCard.Require(look.Step == step, $"the step {look.Step.NameOf()} showed where {step.NameOf()} was due");
            ThroughTheCard.Require(stage.WhatTheLessonSays.Equals(TutorialWords.Of(step, asksForATouch: true)), $"the pill of {step.NameOf()} says \"{stage.WhatTheLessonSays}\"");
            reel.Advance(LessonMoment);
            reel.Shoot(name);
            int movesBefore = stage.Game.MovesUsed;
            TouchTheWall(session);
            session.Advance(StoryStudio.FrameSeconds);
            ThroughTheCard.Require(stage.LessonShown?.Step != step && stage.Game.MovesUsed == movesBefore, $"a touch on the wall did not end {step.NameOf()}, or played a move");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(step) && !session.HasUnsavedProgress, $"the step {step.NameOf()} is not kept in the progress: {session.Progress.Tutorial}");
        }

        /// <summary>
        /// A move shown by hand: pictured at its nudge, its pill says the step's words; another trade, where one is
        /// asked for, is refused and plays nothing; the swipe of the trade shown plays it, and the step is kept.
        /// </summary>
        private static void ShowTheMoveByHand(Reel reel, TutorialStep step, string name, bool triesAnotherTradeFirst)
        {
            GameSession session = reel.Session;
            GameStage stage = session.Stage;
            var guided = stage.LessonShown as Lesson.Guided ?? throw new InvalidOperationException($"No move was shown by hand where {step.NameOf()} was due: \"{stage.LessonShown}\".");
            ThroughTheCard.Require(guided.Step == step, $"the step {guided.Step.NameOf()} showed where {step.NameOf()} was due");
            ThroughTheCard.Require(stage.WhatTheLessonSays.Equals(TutorialWords.Of(step, asksForATouch: false)), $"the pill of {step.NameOf()} says \"{stage.WhatTheLessonSays}\"");
            ThroughTheCard.Require(stage.HintShown == null, "a hint shows beside a move shown by hand");
            reel.Advance(LessonMoment);
            reel.Shoot(name);
            if (triesAnotherTradeFirst)
            {
                Move other = stage.Game.FindLegalMoves().First(move => move.Kind == MoveKind.Swap && !IsTheTrade(move, guided.Move));
                Swipe(session, other);
                reel.Advance(0.15f);
                reel.Shoot($"{name}-other-refused");
                reel.Settle();
                ThroughTheCard.Require(stage.Game.MovesUsed == 0 && stage.LessonShown is Lesson.Guided, $"the trade {other}, not the one shown, was played or ended the step");
            }

            int movesBefore = stage.Game.MovesUsed;
            Swipe(session, guided.Move);
            reel.Advance(0.3f);
            reel.Shoot($"{name}-played");
            reel.Settle();
            ThroughTheCard.Require(stage.Game.MovesUsed == movesBefore + 1 && stage.GuidedMoves >= 1, $"the swipe of the trade shown, {guided.Move}, played no move");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(step) && !session.HasUnsavedProgress, $"the step {step.NameOf()} is not kept in the progress: {session.Progress.Tutorial}");
        }

        /// <summary>
        /// Plays the level out with the expert bot, as a player who follows what the tutorial shows: a move shown by
        /// hand is swiped, a step that is looked at is touched away.
        /// </summary>
        /// <returns>How the game ended.</returns>
        private static GameStatus PlayWithTheTutorial(Reel reel)
        {
            GameSession session = reel.Session;
            GameStage stage = session.Stage;
            IBotPolicy bot = Expert();
            var rng = new Rng(BotSeed, BotStream);
            for (int turns = 0; stage.Game.Status == GameStatus.Playing && !stage.IsOfferingExtraMoves; turns++)
            {
                ThroughTheCard.Require(turns < MostTurnsOfALevel, $"level {stage.Game.Level.Id.Value} was not decided after {MostTurnsOfALevel} turns: a step of the tutorial was not followed");
                reel.Settle();
                switch (stage.LessonShown)
                {
                    case Lesson.Guided guided:
                        Swipe(session, guided.Move);
                        break;
                    case Lesson.Look _:
                        TouchTheWall(session);
                        break;
                    default:
                        if (stage.Game.Status == GameStatus.Playing && !session.TryPlay(bot.ChooseMove(stage.Game, stage.Game.FindLegalMoves(), rng)))
                        {
                            throw new InvalidOperationException($"Level {stage.Game.Level.Id.Value} refused the bot's move with no step of the tutorial showing.");
                        }

                        break;
                }
            }

            reel.Settle();
            if (stage.IsOfferingExtraMoves)
            {
                AtTheOffer.GiveUp(session);
                reel.Settle();
            }

            return stage.Game.Status;
        }

        /// <summary>
        /// The won card's blue button opens the jigsaw: the piece won is lit in the tray with its place, the pill says
        /// to carry it, and the blue button under the tray takes no finger; a finger carries the piece home and the
        /// step is done and kept. The jigsaw's back button leads to the card again.
        /// </summary>
        private static void PlaceTheFirstPiece(Reel reel)
        {
            GameSession session = reel.Session;
            reel.Shoot("card-won");
            Press(session, session.Stage.PlaceButtonPixel, "the card's jigsaw button", reel, pressedShot: null);
            WaitFor(session, () => session.Jigsaw != null, "the card's jigsaw button opened no jigsaw");
            reel.Settle();
            JigsawStage jigsaw = JigsawOf(session);
            ThroughTheCard.Require(jigsaw.LessonShown == TutorialStep.FirstPiece && jigsaw.GuidedPiece is int, $"the first jigsaw shows the step \"{jigsaw.LessonShown}\"");
            ThroughTheCard.Require(jigsaw.WhatTheLessonSays.Equals(TutorialWords.Of(TutorialStep.FirstPiece, asksForATouch: false)), $"the jigsaw's pill says \"{jigsaw.WhatTheLessonSays}\"");
            reel.Advance(LessonMoment);
            reel.Shoot("first-piece");

            int waiting = jigsaw.Table.Waiting.Count;
            session.PointerDown(PixelOrFail(jigsaw.TrayButtonPixel, "the button under the tray"));
            session.PointerUp();
            reel.Advance(StoryStudio.FrameSeconds);
            ThroughTheCard.Require(jigsaw.Table.Waiting.Count == waiting && jigsaw.LessonShown == TutorialStep.FirstPiece, "the button under the tray placed pieces while the first piece was shown");

            int piece = jigsaw.GuidedPiece ?? throw new InvalidOperationException("The first piece's step lost its piece.");
            Vector2 inTheTray = PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray");
            Vector2 onItsPlace = PixelOrFail(jigsaw.PixelToLetGoAt(piece, Vector2.zero), "the piece's place");
            session.PointerDown(inTheTray);
            reel.Settle();
            session.PointerMoved(Vector2.Lerp(inTheTray, onItsPlace, 0.55f));
            reel.Advance(StoryStudio.FrameSeconds);
            reel.Shoot("first-piece-carried");
            session.PointerMoved(onItsPlace);
            session.PointerUp();
            reel.Advance(Tempo.PieceDropsIn + DroppedMoment);
            reel.Settle();
            reel.Shoot("first-piece-placed");
            ThroughTheCard.Require(jigsaw.Table.InPlace.Contains(piece) && jigsaw.LessonShown == null, $"piece {piece} did not drop in, or its step still shows");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(TutorialStep.FirstPiece) && !session.HasUnsavedProgress, $"the first piece's step is not kept: {session.Progress.Tutorial}");

            Press(session, jigsaw.BackButtonPixel, "the jigsaw's back button", reel, pressedShot: null);
            WaitFor(session, () => session.Board != null, "the jigsaw's back button did not lead back to the card");
            reel.Settle();
        }

        /// <summary>
        /// Level 3's second board offers no five: nothing shows and the board takes the bot's moves, until a five is
        /// there and is shown by hand. A board that offers none in its first moves still shows the rule.
        /// </summary>
        /// <returns>What the walk saw, for the log, and how many pictures it took.</returns>
        private static (string Outcome, int Pictures) WaitForTheFive(ScreenShape screen, Studio studio, TutorialSeen seen)
        {
            LevelDefinition third = LevelPack.Numbered(studio.Levels, 3);
            PlayerProgress standing = Seeing(StoryStudio.StandingOn(third, studio.Levels), WithoutTheFive(seen)).WithAttemptStarted();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var waiting = new Reel(session, screen, $"walk-tutorial-waits-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            waiting.Settle();
            GameStage stage = session.Stage;
            IBotPolicy bot = Expert();
            var rng = new Rng(BotSeed, BotStream);
            int played = 0;
            while (stage.LessonShown == null && stage.Game.Status == GameStatus.Playing && played < MostMovesTheFiveWaits)
            {
                waiting.Shoot($"five-waits-{played}");
                ThroughTheCard.Require(session.TryPlay(bot.ChooseMove(stage.Game, stage.Game.FindLegalMoves(), rng)), "the board refused a move while the five waited");
                played++;
                waiting.Settle();
            }

            string outcome;
            if (stage.LessonShown is Lesson.Guided { Step: TutorialStep.FiveInALine })
            {
                ShowTheMoveByHand(waiting, TutorialStep.FiveInALine, $"five-after-{played}-moves", triesAnotherTradeFirst: false);
                outcome = $"on level 3's second board the five waited, saying nothing, through {played} move(s) and then was shown by hand";
            }
            else
            {
                ThroughTheCard.Require(stage.LessonShown == null, $"\"{stage.LessonShown}\" showed while the five waited");
                outcome = $"on level 3's second board the five waited, saying nothing, through all {played} move(s) the walk played";
            }

            waiting.WriteSheet();
            return (outcome, waiting.Taken);
        }

        /// <summary>The first board with crates points at one and says how they break; a touch ends it and it is kept.</summary>
        /// <returns>How many pictures the walk took.</returns>
        private static int LookAtTheFirstCrates(ScreenShape screen, Studio studio, TutorialSeen seen)
        {
            LevelDefinition level = LevelPack.Numbered(studio.Levels, FirstLevelWithCrates);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(Seeing(StoryStudio.StandingOn(level, studio.Levels), seen)),
                StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var crates = new Reel(session, screen, $"walk-tutorial-crates-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            crates.Settle();
            ThroughTheCard.Require(session.Stage.LessonShown is Lesson.Look { Spot: LessonSpot.Cell }, $"the first board with crates shows \"{session.Stage.LessonShown}\"");
            LookAt(crates, TutorialStep.Box, "crate");
            crates.WriteSheet();
            ThroughTheCard.Require(session.Stage.LessonShown == null, $"after the crates \"{session.Stage.LessonShown}\" showed");
            return crates.Taken;
        }

        /// <summary>
        /// A player who has placed nine of the first painting's pieces wins level 10, carries its piece home, and the
        /// painting is whole: after its celebration it is lit in the dimmed room with a word about the museum, which a
        /// touch ends; the step is kept.
        /// </summary>
        /// <returns>How many pictures the walk took.</returns>
        private static int CelebrateTheFirstPainting(ScreenShape screen, Studio studio, TutorialSeen seen)
        {
            LevelDefinition level = LevelPack.Numbered(studio.Levels, LastLevelOfTheFirstPainting);
            PlayerProgress standing = StoryStudio.StandingOn(level, studio.Levels);
            standing = Seeing(standing.WithWaitingPiecesPlaced(standing.Museum.PiecesWaiting), seen);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var painting = new Reel(session, screen, $"walk-tutorial-painting-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            GameStatus ending = GameStatus.Playing;
            for (int attempt = 0; attempt < 3 && ending != GameStatus.Won; attempt++)
            {
                ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
                painting.Settle();
                ending = PlayWithTheTutorial(painting);
                if (ending != GameStatus.Won)
                {
                    GoOnFromTheCard(painting, ending);
                }
            }

            RequireWon(ending, level.Id.Value);
            Press(session, session.Stage.PlaceButtonPixel, "the card's jigsaw button", painting, pressedShot: null);
            WaitFor(session, () => session.Jigsaw != null, "the card's jigsaw button opened no jigsaw");
            painting.Settle();
            JigsawStage jigsaw = JigsawOf(session);
            ThroughTheCard.Require(jigsaw.LessonShown == null && jigsaw.Table.Waiting.Count == 1, $"the jigsaw of the tenth piece shows \"{jigsaw.LessonShown}\" with {jigsaw.Table.Waiting.Count} pieces waiting");
            CarryThePieceHome(painting);
            ThroughTheCard.Require(jigsaw.Table.IsWhole, "the tenth piece did not make the first painting whole");
            painting.Advance(Tempo.PauseBeforeRestored + CelebrationMoment);
            painting.Shoot("painting-whole");
            ThroughTheCard.Require(jigsaw.LessonShown == null, "the word about the museum came during the celebration");
            WaitFor(session, () => jigsaw.LessonShown == TutorialStep.FirstPainting, "no word about the museum came after the celebration");
            ThroughTheCard.Require(
                jigsaw.WhatTheLessonSays.Equals(TutorialWords.Of(TutorialStep.FirstPainting, asksForATouch: true)), $"the jigsaw's pill says \"{jigsaw.WhatTheLessonSays}\"");
            painting.Advance(LessonMoment);
            painting.Shoot("first-painting");
            // A touch on the back button: it ends the word, and leads nowhere.
            session.PointerDown(PixelOrFail(jigsaw.BackButtonPixel, "the jigsaw's back button"));
            session.PointerUp();
            painting.Advance(StoryStudio.FrameSeconds);
            ThroughTheCard.Require(jigsaw.LessonShown == null && !jigsaw.PlayerLeft, "a touch did not end the word about the museum, or pressed the button it lay on");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(TutorialStep.FirstPainting) && !session.HasUnsavedProgress, $"the first painting's step is not kept: {session.Progress.Tutorial}");
            painting.Settle();
            painting.Shoot("first-painting-done");
            painting.WriteSheet();
            return painting.Taken;
        }

        private static void RequireWon(GameStatus ending, int level) =>
            ThroughTheCard.Require(ending == GameStatus.Won, $"the expert bot did not win level {level} in the walk ({ending}); the walk needs the win to go on");

        private static bool IsTheTrade(Move move, Move trade) =>
            move == trade || (move.Kind == MoveKind.Swap && move.From == trade.To && move.To == trade.From);

        /// <summary>The progress with the tutorial seen as given.</summary>
        private static PlayerProgress Seeing(PlayerProgress progress, TutorialSeen seen) =>
            new PlayerProgress(progress.LevelsWon, progress.AttemptsOnLevel, progress.Museum.Placed, progress.Wallet, seen);

        private static TutorialSeen WithoutTheFive(TutorialSeen seen) => TutorialSeen.Of(seen.Steps.Where(step => step != TutorialStep.FiveInALine));
    }
}
