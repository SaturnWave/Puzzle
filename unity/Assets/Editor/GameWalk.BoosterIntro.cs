#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Levels;
using Puzzle.Core.Model;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the booster intro (#7), once on each screen, on the game's own session and by touches alone, in the
    /// language the tools speak (<c>walk-boosters-...</c>). On the level each booster used during play opens on (7, 12,
    /// 16, 20), a player who has seen every other step is asked for it by hand: the disc is lit and ringed, a touch on
    /// another disc or a swipe on the board does nothing, and no hint shows however long the board is left; the disc
    /// arms it (the shuffle is used at once), the target is lit with its aim, an aim elsewhere is refused, and the aim
    /// on the target (a brush's anywhere on its row or column) uses it, free: the player's stock is as it was, the step
    /// is kept. After the first of them (the spatula) its disc is lit once more with the long press's sentence, which a
    /// touch ends. The free use begins no attempt: leaving right after it costs nothing and writes no record; after a
    /// move of the player's own (on the row brush's level) the record names it as guided. Then a long press on each
    /// of the four discs on level 40 brings the card that plays the booster on its tiny board, arms nothing and is taken
    /// up by a touch (no hint shows under it); a level given up there brings the card before the level, on which the
    /// start boosters are lit one after another with a touch between them (a touch on its "Oyna" plays nothing), kept
    /// once; and a long press on each start booster brings its card in front and chooses nothing.
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>A step of the booster intro is pictured this long after it began, its dark fully down.</summary>
        private const float IntroMoment = 0.6f;

        /// <summary>A finger is held on a disc this long for a long press: a little longer than the game's.</summary>
        private const float HeldLong = Tempo.LongPress + 0.1f;

        /// <summary>The board is left alone this long while a step or a card shows, longer than the hint waits.</summary>
        private const float LeftAlone = 10f;

        /// <summary>The card of a long press is pictured at these moments after it came down: the aim, the strike, the rest after it.</summary>
        private static readonly float[] CardMoments = { 0.7f, 1.35f, 2.2f };

        /// <summary>The level where every booster is open and long presses are walked: the retries' level.</summary>
        private const int EveryBoosterOpen = RetriedLevel;

        /// <returns>How many pictures the walk took.</returns>
        public static int RunTheBoosterIntroWalk(ScreenShape screen, Studio studio)
        {
            var reel = new BoosterReel(screen, studio);
            foreach (BoosterKind booster in BoosterKinds.InLevel)
            {
                UseTheBoosterByHand(reel, booster);
            }

            PressLongOnTheShelf(reel);
            ShowTheStartBoostersAfterALoss(reel);
            reel.WriteSheet();
            Debug.Log(
                $"Screen shots: walk {reel.Name}: the spatula, the row brush, the column brush and the shuffle were each used by hand, free, on the level they open on; "
                + "a long press on each disc of the board and of the level's card brought its card and chose nothing; the start boosters were shown after a level given up.");
            return reel.Taken;
        }

        /// <summary>
        /// On the level the booster opens on: the step shows on the disc, nothing else is taken, no hint comes; the
        /// disc arms it, an aim elsewhere is refused, the aim on the target uses it free; leaving writes the record.
        /// </summary>
        private static void UseTheBoosterByHand(BoosterReel reel, BoosterKind booster)
        {
            TutorialStep step = TutorialSteps.Teaching(booster);
            int level = StoryStudio.Economy.Unlocks.LevelOf(booster);
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(reel.Studio.Levels, level), reel.Studio.Levels);
            var records = new AttemptsInMemory(StoryStudio.Device);
            bool teachesTheLongPress = booster == BoosterKind.HitCell; // the first booster used by hand
            TutorialSeen seen = TutorialSeen.Of(TutorialSteps.All.Where(each => each != step && (each != TutorialStep.LongPress || !teachesTheLongPress)));
            using GameSession session = reel.Open(Seeing(standing, seen), records, SessionOpening.InTheMuseum);
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            reel.Settle(session);
            GameStage stage = session.Stage;
            string name = booster.Name();
            int held = session.Progress.Wallet.Shelf.Count(booster);

            var lesson = stage.LessonShown as Lesson.Booster ?? throw new InvalidOperationException($"Level {level} does not ask for {name} by hand: \"{stage.LessonShown}\".");
            ThroughTheCard.Require(lesson.Kind == booster && stage.RingedDisc == booster, $"level {level} asks for {lesson.Kind.Name()}, ringing {stage.RingedDisc}");
            ThroughTheCard.Require(stage.WhatTheLessonSays.Equals(TutorialWords.OfTheBooster(booster, isArmed: false)), $"the pill of {name} says \"{stage.WhatTheLessonSays}\"");
            reel.Advance(session, IntroMoment);
            reel.Shoot(session, $"{name}-disc");

            int movesBefore = stage.Game.MovesUsed;
            int usedBefore = stage.Game.BoostersUsed.Count;
            Move another = stage.Game.FindLegalMoves().First(move => move.Kind == MoveKind.Swap);
            Swipe(session, another);
            reel.Settle(session);
            BoosterKind other = ShelfBrief.Boosters.First(each => each != booster);
            Tap(session, stage.DiscPixel(other), $"the disc of {other.Name()}");
            reel.Settle(session);

            reel.Advance(session, LeftAlone);
            ThroughTheCard.Require(
                stage.Game.MovesUsed == movesBefore && stage.Game.BoostersUsed.Count == usedBefore && stage.ArmedBooster == null && stage.LessonShown is Lesson.Booster,
                $"while {name} was asked for, a swipe or another disc did something, or the step went");
            ThroughTheCard.Require(stage.HintShown == null, $"a hint showed while {name} was asked for");

            Tap(session, stage.DiscPixel(booster), $"the disc of {name}");
            if (booster == BoosterKind.Shuffle)
            {
                reel.Advance(session, 0.2f);
                reel.Shoot(session, $"{name}-used");
            }
            else
            {
                reel.Advance(session, 0.1f);
                ThroughTheCard.Require(stage.ArmedBooster == booster && stage.RingedDisc == null, $"the disc of {name} did not arm it by hand ({stage.ArmedBooster})");
                ThroughTheCard.Require(stage.WhatTheLessonSays.Equals(TutorialWords.OfTheBooster(booster, isArmed: true)), $"the pill of the armed {name} says \"{stage.WhatTheLessonSays}\"");
                reel.Advance(session, IntroMoment);
                reel.Shoot(session, $"{name}-target");

                Tap(session, stage.PixelOf(ElsewhereThan(lesson.Use, stage.Game.Board.Shape)), "a cell off the target");
                reel.Settle(session);
                ThroughTheCard.Require(
                    stage.Game.BoostersUsed.Count == usedBefore && stage.ArmedBooster == booster && stage.LessonShown is Lesson.Booster,
                    $"an aim off the target used {name}, or put it down");

                Tap(session, stage.PixelOf(OnTheTarget(lesson.Use, stage.Game.Board.Shape)), "the target");
                reel.Advance(session, 0.25f);
                reel.Shoot(session, $"{name}-used");
            }

            reel.Settle(session);
            ThroughTheCard.Require(stage.Game.BoostersUsed.SequenceEqual(new[] { name }) && !(stage.LessonShown is Lesson.Booster), $"the target did not use {name}: \"{string.Join(", ", stage.Game.BoostersUsed)}\"");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(step) && !session.HasUnsavedProgress, $"the step of {name} is not kept: {session.Progress.Tutorial}");
            ThroughTheCard.Require(session.Progress.Wallet.Shelf.Count(booster) == held, $"{name} used by hand took one from the stock: {session.Progress.Wallet.Shelf.Count(booster)} of {held} left");
            if (teachesTheLongPress)
            {
                TeachTheLongPress(reel, session, booster);
            }

            ThroughTheCard.Require(stage.LessonShown == null, $"after {name} by hand \"{stage.LessonShown}\" showed");
            reel.Shoot(session, $"{name}-done");

            // The free use began no attempt: leaving after it alone costs nothing and writes nothing; after a move of the player's own, the record names it.
            bool playsAMoveFirst = booster == BoosterKind.HitRow;
            if (playsAMoveFirst)
            {
                ThroughTheCard.Require(session.TryPlay(stage.Game.FindLegalMoves().First()), $"the board refused a move after {name} by hand");
                reel.Settle(session);
            }

            PlayerProgress beforeLeaving = session.Progress;
            LeaveTheLevel(session, reel);
            if (playsAMoveFirst)
            {
                AttemptLineNamesTheGuidedBooster(records, name);
            }
            else
            {
                ThroughTheCard.Require(
                    records.Records.Count == 0 && session.Progress.Equals(beforeLeaving),
                    $"leaving after {name} by hand alone wrote {records.Records.Count} records, or changed the progress to \"{session.Progress}\"");
            }
        }

        /// <summary>Right after the first booster used by hand its disc is lit once more, the pill says a long press shows what a booster does, and a touch ends it.</summary>
        private static void TeachTheLongPress(BoosterReel reel, GameSession session, BoosterKind booster)
        {
            GameStage stage = session.Stage;
            ThroughTheCard.Require(
                stage.LessonShown is Lesson.Look { Step: TutorialStep.LongPress, Spot: LessonSpot.Disc lit } && lit.Booster == booster && stage.RingedDisc == booster,
                $"after the first booster by hand \"{stage.LessonShown}\" showed, ringing {stage.RingedDisc}");
            ThroughTheCard.Require(stage.WhatTheLessonSays.Equals(TutorialWords.Of(TutorialStep.LongPress, asksForATouch: true)), $"the long press's pill says \"{stage.WhatTheLessonSays}\"");
            ThroughTheCard.Require(stage.HintShown == null, "a hint showed beside the long press's pill");
            reel.Advance(session, IntroMoment);
            reel.Shoot(session, "long-press");
            TouchTheWall(session);
            reel.Advance(session, StoryStudio.FrameSeconds);
            ThroughTheCard.Require(
                stage.LessonShown == null && stage.ArmedBooster == null && session.Progress.Tutorial.Has(TutorialStep.LongPress) && !session.HasUnsavedProgress,
                $"a touch did not end the long press's step, or it is not kept: {session.Progress.Tutorial}");
        }

        /// <summary>The record the leaving wrote names the booster among those used and as guided.</summary>
        private static void AttemptLineNamesTheGuidedBooster(AttemptsInMemory records, string booster)
        {
            ThroughTheCard.Require(records.Records.Count == 1, $"leaving after {booster} by hand wrote {records.Records.Count} records");
            string line = AttemptLine.Of(records.Records[0]);
            ThroughTheCard.Require(
                records.Records[0].GuidedBoosters.SequenceEqual(new[] { booster }) && line.Contains($"\"guidedBoosters\":[\"{booster}\"]") && line.Contains($"\"boostersUsed\":[\"{booster}\"]"),
                $"the record does not name {booster} as guided: {line}");
        }

        /// <summary>
        /// On level 40, where every booster is open: a long press on each disc brings its card, which plays the
        /// booster on its tiny board; the lift arms nothing, no hint shows under the card, and a touch takes it up.
        /// </summary>
        private static void PressLongOnTheShelf(BoosterReel reel)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(reel.Studio.Levels, EveryBoosterOpen), reel.Studio.Levels);
            using GameSession session = reel.Open(standing, StoryStudio.AttemptsKeptInMemory(), SessionOpening.AtTheBoard);
            reel.Settle(session);
            GameStage stage = session.Stage;
            foreach (BoosterKind booster in ShelfBrief.Boosters)
            {
                string name = booster.Name();
                PlayerProgress before = session.Progress;
                session.PointerDown(PixelOrFail(stage.DiscPixel(booster), $"the disc of {name}"));
                reel.Advance(session, HeldLong);
                session.PointerUp();
                WaitFor(session, () => stage.IsBoosterCardDown, $"a long press on {name} brought no card down");
                ThroughTheCard.Require(stage.BoosterCardShown == booster && stage.ArmedBooster == null, $"a long press on {name} showed {stage.BoosterCardShown} and armed {stage.ArmedBooster}");
                ShootTheCard(reel, session, name, () => stage.BoosterCardTimesShown);
                reel.Advance(session, LeftAlone);
                ThroughTheCard.Require(stage.HintShown == null && stage.BoosterCardTimesShown > 0, $"a hint showed under the card of {name}, or it showed nothing through");
                Tap(session, stage.PixelOf(new Position(0, 0)), "the card of a long press");
                WaitFor(session, () => stage.BoosterCardShown == null, $"a touch did not take the card of {name} up");
                reel.Settle(session);
                ThroughTheCard.Require(stage.ArmedBooster == null && stage.Game.BoostersUsed.Count == 0 && session.Progress.Equals(before), $"the long press on {name} used or spent something");
            }
        }

        /// <summary>
        /// A player who has seen every step but the start boosters' plays a move on level 40 and leaves: the hall's play
        /// button brings the level's card down, on which the three start boosters are lit one after another, a touch
        /// on "Oyna" going on and playing nothing; the step is kept and comes no more. A long press on each start
        /// booster brings its card in front and chooses nothing.
        /// </summary>
        private static void ShowTheStartBoostersAfterALoss(BoosterReel reel)
        {
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(reel.Studio.Levels, EveryBoosterOpen), reel.Studio.Levels);
            TutorialSeen allButTheStartBoosters = TutorialSeen.Of(TutorialSteps.All.Where(each => each != TutorialStep.StartBoosters));
            using GameSession session = reel.Open(Seeing(standing, allButTheStartBoosters), StoryStudio.AttemptsKeptInMemory(), SessionOpening.InTheMuseum);
            ThroughTheCard.BringDownTheCard(session);
            ThroughTheCard.Require(MuseumOf(session).StartBoosterLit == null, "the start boosters were shown on the card of a level never tried");
            ThroughTheCard.PressPlay(session);
            reel.Settle(session);
            ThroughTheCard.Require(session.TryPlay(session.Stage.Game.FindLegalMoves().First()), "the board of level 40 refused its first move");
            reel.Settle(session);
            LeaveTheLevel(session, reel);

            Tap(session, MuseumOf(session).PlayButtonPixel, "the hall's play button");
            WaitFor(session, () => MuseumOf(session).StartBoosterLit != null, "the card of a level given up did not show the start boosters");
            foreach (BoosterKind booster in LevelBrief.StartBoosters)
            {
                MuseumStage museum = MuseumOf(session);
                ThroughTheCard.Require(museum.StartBoosterLit == booster, $"the card lights {museum.StartBoosterLit} where {booster.Name()} was due");
                ThroughTheCard.Require(
                    museum.WhatTheLessonSays.Equals(TutorialWords.OfTheStartBooster(booster, LevelStart.Rules.StartBoosterExtraMoves)),
                    $"the pill of {booster.Name()} says \"{museum.WhatTheLessonSays}\"");
                reel.Advance(session, IntroMoment);
                reel.Shoot(session, $"start-{booster.Name()}");
                Tap(session, museum.StartCardPlayPixel, "the card's Oyna");
                reel.Advance(session, StoryStudio.FrameSeconds);
                reel.Settle(session);
                ThroughTheCard.Require(session.Museum != null && museum.ShowsTheStartCard, "a touch while the start boosters were shown left the card");
            }

            ThroughTheCard.Require(MuseumOf(session).StartBoosterLit == null && !MuseumOf(session).StartChoice.Boosters.Any(), "the start boosters' step did not end, or chose something");
            ThroughTheCard.Require(session.Progress.Tutorial.Has(TutorialStep.StartBoosters) && !session.HasUnsavedProgress, $"the start boosters' step is not kept: {session.Progress.Tutorial}");
            reel.Shoot(session, "start-done");

            foreach (BoosterKind booster in LevelBrief.StartBoosters)
            {
                string name = booster.Name();
                MuseumStage museum = MuseumOf(session);
                session.PointerDown(PixelOrFail(museum.StartCardDiscPixel(booster), $"the card's disc of {name}"));
                reel.Advance(session, HeldLong);
                session.PointerUp();
                reel.Settle(session);
                ThroughTheCard.Require(museum.BoosterCardShown == booster && !museum.StartChoice.Boosters.Any(), $"a long press on {name} showed {museum.BoosterCardShown} and chose {museum.StartChoice}");
                ShootTheCard(reel, session, name, () => museum.BoosterCardTimesShown);
                Tap(session, reel.Middle, "the card of a long press");
                reel.Settle(session);
                ThroughTheCard.Require(museum.BoosterCardShown == null && museum.ShowsTheStartCard && !museum.StartChoice.Boosters.Any(), $"a touch did not take the card of {name} up, or chose it");
            }

            Tap(session, MuseumOf(session).StartCardClosePixel, "the card's round button");
            reel.Settle(session);
            Tap(session, MuseumOf(session).PlayButtonPixel, "the hall's play button");
            reel.Settle(session);
            reel.Advance(session, IntroMoment);
            ThroughTheCard.Require(MuseumOf(session).ShowsTheStartCard, "the hall's play button brought no card down a second time");
            ThroughTheCard.Require(MuseumOf(session).StartBoosterLit == null, "the start boosters were shown a second time");
        }

        /// <summary>The card of a long press, pictured at its moments: its aim, its strike, its rest; it is required to have shown its booster through by then.</summary>
        private static void ShootTheCard(BoosterReel reel, GameSession session, string name, Func<int> timesShown)
        {
            float shown = 0f;
            foreach (float moment in CardMoments)
            {
                reel.Advance(session, moment - shown);
                shown = moment;
                reel.Shoot(session, $"card-{name}-{moment:0.0#}s");
            }

            reel.Advance(session, 3f);
            ThroughTheCard.Require(timesShown() > 0, $"the card of {name} did not show its booster through in {shown + 3f} s");
        }

        /// <summary>The pause disc, and the leaving card's museum button: the session is left in the hall.</summary>
        private static void LeaveTheLevel(GameSession session, BoosterReel reel)
        {
            Tap(session, session.Stage.PauseButtonPixel, "the shelf's pause disc");
            reel.Settle(session);
            Tap(session, session.Stage.MuseumButtonPixel, "the leaving card's museum button");
            WaitFor(session, () => session.Museum != null, "the leaving card's museum button opened no museum");
            reel.Settle(session);
        }

        /// <summary>A cell the booster does not strike where it is shown: beside the spatula's cell, or on the next row or column.</summary>
        private static Position ElsewhereThan(BoosterUse use, BoardShape shape) => use switch
        {
            BoosterUse.HitCell cell => PlayableNear(shape, cell.At, cell.At.X == 0 ? 1 : -1, 0),
            BoosterUse.HitRow row => PlayableNear(shape, row.At, 0, row.At.Y == 0 ? 1 : -1),
            BoosterUse.HitColumn column => PlayableNear(shape, column.At, column.At.X == 0 ? 1 : -1, 0),
            _ => throw new ArgumentException($"The use {use} has no target to miss.", nameof(use)),
        };

        /// <summary>Where the walk aims at the target: the spatula's own cell, a brush's playable cell farthest along its row or column from the one shown.</summary>
        private static Position OnTheTarget(BoosterUse use, BoardShape shape) => use switch
        {
            BoosterUse.HitCell cell => cell.At,
            BoosterUse.HitRow row => Enumerable.Range(0, shape.Width).Select(x => new Position(x, row.At.Y)).Where(cell => shape.IsPlayable(cell)).OrderByDescending(cell => Math.Abs(cell.X - row.At.X)).First(),
            BoosterUse.HitColumn column => Enumerable.Range(0, shape.Height).Select(y => new Position(column.At.X, y)).Where(cell => shape.IsPlayable(cell)).OrderByDescending(cell => Math.Abs(cell.Y - column.At.Y)).First(),
            _ => throw new ArgumentException($"The use {use} has no target to aim at.", nameof(use)),
        };

        /// <summary>The nearest playable cell from a cell, stepping one way.</summary>
        private static Position PlayableNear(BoardShape shape, Position from, int stepX, int stepY)
        {
            for (var cell = new Position(from.X + stepX, from.Y + stepY); cell.X >= 0 && cell.Y >= 0 && cell.X < shape.Width && cell.Y < shape.Height; cell = new Position(cell.X + stepX, cell.Y + stepY))
            {
                if (shape.IsPlayable(cell))
                {
                    return cell;
                }
            }

            throw new InvalidOperationException($"No playable cell lies beside {from} on the board.");
        }

        /// <summary>A finger comes down and lifts at a pixel.</summary>
        private static void Tap(GameSession session, Vector2? pixel, string what)
        {
            session.PointerDown(PixelOrFail(pixel, what));
            session.PointerUp();
        }

        /// <summary>The pictures of the booster intro's walk, of several sessions one after another, on one sheet.</summary>
        private sealed class BoosterReel
        {
            private readonly ScreenShape _screen;
            private readonly ContactSheet _sheet = new ContactSheet(StoryStudio.SheetShrinksBy);

            public BoosterReel(ScreenShape screen, Studio studio)
            {
                _screen = screen;
                Studio = studio;
                Name = $"walk-boosters-{screen}{studio.FileNameEnding}";
                foreach (string earlier in System.IO.Directory.Exists(StoryStudio.ShotsFolder)
                    ? System.IO.Directory.GetFiles(StoryStudio.ShotsFolder, $"{Name}-??-*.png")
                    : Array.Empty<string>())
                {
                    System.IO.File.Delete(earlier);
                }
            }

            public Studio Studio { get; }

            public string Name { get; }

            /// <summary>The pixel in the middle of the screen: a touch there lands on whatever card hangs.</summary>
            public Vector2 Middle => new Vector2(_screen.PixelWidth / 2f, _screen.PixelHeight / 2f);

            public int Taken { get; private set; }

            /// <summary>The game's own session over a progress, in a scene of its own.</summary>
            public GameSession Open(PlayerProgress progress, IAttemptLog log, SessionOpening opening)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return new GameSession(Studio.Levels, Studio.Art, Studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(progress), log, _screen, opening);
            }

            public void Advance(GameSession session, float seconds) => StoryStudio.AdvanceBy(seconds, session);

            public void Settle(GameSession session) => StoryStudio.AdvanceUntilSettled(session);

            public void Shoot(GameSession session, string what)
            {
                Taken++;
                StoryStudio.Shoot(session, _screen, _sheet, $"{Name}-{Taken:D2}-{what}.png");
            }

            public void WriteSheet() => StoryStudio.WriteSheet(_sheet, $"{Name}-sheet.png");
        }
    }
}
