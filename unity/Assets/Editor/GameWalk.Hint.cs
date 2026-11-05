#nullable enable
using System;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
using Puzzle.Client.Stage;
using Puzzle.Core.Engine;
using Puzzle.Core.Model;
using Puzzle.Meta;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// The walk of the game's hint, once on each screen, on the game's own session and by touches alone, for a new
    /// player who stands on the retried level: through the level's card into the level, the board is left untouched;
    /// no hint shows before the settings' wait is over, and one shows then, its pieces those of a move the game takes
    /// (pictured as its light comes up and at its nudge). A touch on the wall takes it away at once, its pieces at
    /// rest (pictured), and the wait starts again from the touch. A booster armed on the shelf and the card that asks
    /// whether to leave each hold every hint back however long the board is left. The second hint's move is played by
    /// a finger's swipe, and every hint after it, up to the settings' cap, is touched away; then the attempt has shown
    /// all its hints, and a board left untouched for twice the wait shows none more (pictured) (<c>walk-hint-...</c>).
    /// </summary>
    internal static partial class GameWalk
    {
        /// <summary>A hint is pictured this long after it showed: as its light comes up, and at the farthest of its first nudge.</summary>
        private const float HintComingUpMoment = 0.12f;
        private const float HintNudgeMoment = 0.6f;

        public static void RunTheHintWalk(ScreenShape screen, Studio studio)
        {
            HintRules rules = StoryStudio.Economy.Hints;
            ThroughTheCard.Require(rules.PerAttempt >= 2, $"the settings allow {rules.PerAttempt} hints an attempt; the walk shows one touched away and one played");
            PlayerProgress standing = StoryStudio.StandingOn(LevelPack.Numbered(studio.Levels, RetriedLevel), studio.Levels);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(standing), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
            var reel = new Reel(session, screen, $"walk-hint-{screen}{studio.FileNameEnding}", studio.Paintings.Catalogue);
            ThroughTheCard.IntoTheLevel(session, Array.Empty<BoosterKind>());
            reel.Shoot("board");

            float wait = (float)rules.Idle.TotalSeconds;
            Move first = WaitForTheHint(reel, wait, "the board left untouched");
            reel.Shoot("hint-touched-away");
            HoldsNoHintWhile(reel, wait, "a booster is armed", ArmTheFirstBoosterHeld, PutTheBoosterDown);
            HoldsNoHintWhile(reel, wait, "the card that asks whether to leave hangs", AskWhetherToLeave, StayOnTheBoard);

            Move second = WaitForTheHint(reel, wait, "the board left untouched again", touchAway: false);
            Swipe(session, second);
            reel.Settle();
            ThroughTheCard.Require(session.Stage.Game.MovesUsed == 1 && session.Stage.HintShown == null, $"the swipe along the hinted move {second} played no move, or left the hint showing");
            while (session.Stage.HintsLeft > 0)
            {
                WaitForTheHint(reel, wait, "the board left untouched after a move");
            }

            reel.Advance(2f * wait);
            ThroughTheCard.Require(
                session.Stage.HintShown == null && session.Stage.HintsShown == rules.PerAttempt && session.Stage.HintsLeft == 0,
                $"after {rules.PerAttempt} hints a board left untouched for {2f * wait} s shows a hint, or counts {session.Stage.HintsShown} shown and {session.Stage.HintsLeft} left");
            reel.Shoot("no-hint-left");
            reel.WriteSheet();
            Debug.Log(
                $"Screen shots: walk {reel.Name}: on level {RetriedLevel} a hint after {wait} s untouched ({first}), touched away, none while a booster was armed or the leaving card hung, "
                + $"the second ({second}) played by a swipe, the third touched away, and none more: {session.Stage.HintsShown} shown, as the attempt's record will count.");
        }

        /// <summary>
        /// Leaves the board untouched: no hint before the wait is over, one when it is, of a move the game takes. It is
        /// pictured coming up and at its nudge, and then, unless asked not to, a touch on the wall takes it away at once.
        /// </summary>
        /// <returns>The move the hint showed.</returns>
        private static Move WaitForTheHint(Reel reel, float wait, string what, bool touchAway = true)
        {
            GameStage stage = reel.Session.Stage;
            int shownBefore = stage.HintsShown;
            reel.Advance(wait - 0.1f);
            ThroughTheCard.Require(stage.HintShown == null, $"a hint showed before {wait} s of {what}");
            for (float waited = 0f; stage.HintShown == null; waited += StoryStudio.FrameSeconds)
            {
                ThroughTheCard.Require(waited <= 0.2f, $"no hint showed after {wait} s of {what}");
                reel.Session.Advance(StoryStudio.FrameSeconds);
            }

            Move hint = stage.HintShown ?? throw new InvalidOperationException("The hint went before it was looked at.");
            ThroughTheCard.Require(stage.Game.IsLegal(hint) && stage.HintsShown == shownBefore + 1, $"the hint shows {hint}, which the game does not take, or counted {stage.HintsShown} after {shownBefore}");
            reel.Advance(HintComingUpMoment);
            reel.Shoot($"hint-{shownBefore + 1}-coming-up");
            reel.Advance(HintNudgeMoment - HintComingUpMoment);
            reel.Shoot($"hint-{shownBefore + 1}");
            if (touchAway)
            {
                TouchTheWall(reel.Session);
                ThroughTheCard.Require(stage.HintShown == null, "a touch on the wall did not take the hint away at once");
            }

            return hint;
        }

        /// <summary>While something holds the board, a hint does not show however long it is left; once it is let go, the wait starts again.</summary>
        private static void HoldsNoHintWhile(Reel reel, float wait, string what, Action<GameSession> hold, Action<GameSession> letGo)
        {
            hold(reel.Session);
            reel.Settle();
            reel.Advance(2f * wait);
            ThroughTheCard.Require(reel.Session.Stage.HintShown == null, $"a hint showed while {what}");
            letGo(reel.Session);
            reel.Settle();
        }

        private static void ArmTheFirstBoosterHeld(GameSession session)
        {
            BoosterKind[] armable = BoosterKinds.InLevel.Where(booster => booster != BoosterKind.Shuffle && session.Progress.Wallet.Shelf.Has(booster)).ToArray();
            ThroughTheCard.Require(armable.Length > 0, $"the player on level {RetriedLevel} holds no booster that is armed: {session.Progress.Wallet.Shelf}");
            BoosterKind held = armable[0];
            ThroughTheCard.Press(session, session.Stage.BoosterDiscPixel(held), $"the shelf's disc of {held.Name()}");
            ThroughTheCard.Require(session.Stage.ArmedBooster == held, $"a press on the disc of {held.Name()} did not arm it");
        }

        private static void PutTheBoosterDown(GameSession session)
        {
            BoosterKind armed = session.Stage.ArmedBooster ?? throw new InvalidOperationException("No booster is armed to be put down.");
            ThroughTheCard.Press(session, session.Stage.BoosterDiscPixel(armed), $"the shelf's disc of {armed.Name()}");
            ThroughTheCard.Require(session.Stage.ArmedBooster == null, "a second press on the armed disc did not put it down");
        }

        private static void AskWhetherToLeave(GameSession session)
        {
            ThroughTheCard.Press(session, session.Stage.PauseButtonPixel, "the shelf's pause disc");
            StoryStudio.AdvanceUntilSettled(session);
            ThroughTheCard.Require(session.Stage.IsAskingWhetherToLeave, "the pause disc did not ask whether to leave");
        }

        private static void StayOnTheBoard(GameSession session)
        {
            session.GoBack();
            StoryStudio.AdvanceUntilSettled(session);
            ThroughTheCard.Require(session.Stage.CardTells == null, "the way back did not take the leaving card up");
        }

        /// <summary>A finger comes down on the wall above the board, where nothing takes it, and lifts.</summary>
        private static void TouchTheWall(GameSession session)
        {
            Vector2 aboveTheBoard = session.Stage.PixelOf(new Position(0, 0)) + new Vector2(0f, 3f * CellPixels(session));
            session.PointerDown(aboveTheBoard);
            session.PointerUp();
        }

        /// <summary>A finger swipes from the move's first cell into its second.</summary>
        private static void Swipe(GameSession session, Move move)
        {
            session.PointerDown(session.Stage.PixelOf(move.From));
            session.PointerMoved(session.Stage.PixelOf(move.To));
            session.PointerUp();
        }

        private static float CellPixels(GameSession session) =>
            Vector2.Distance(session.Stage.PixelOf(new Position(0, 0)), session.Stage.PixelOf(new Position(1, 0)));
    }
}
