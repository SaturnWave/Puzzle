#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Flow;
using Puzzle.Client.Jigsaws;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Museum;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle.Client.EditorTools
{
    /// <summary>The moments of a painting's jigsaw the picture command can show. A closed set.</summary>
    internal enum JigsawMoment
    {
        /// <summary>The screen as it opens: the pieces in place, the pieces that wait in the tray.</summary>
        Rest,

        /// <summary>The first piece of the tray in the player's hand, halfway to the painting.</summary>
        Carried,

        /// <summary>That piece let go away from its place, on its way back to the tray.</summary>
        GoingBack,

        /// <summary>That piece let go on its place, just dropped in: its edge is still lit.</summary>
        Placed,

        /// <summary>The blue button pressed: the waiting pieces on their way to their places.</summary>
        Flying,

        /// <summary>The blue button pressed and every waiting piece in place; if that makes the painting whole, in the middle of its celebration.</summary>
        Restored,

        /// <summary>The same once everything is at rest: a painting made whole there shows in the tray what completing it gave.</summary>
        Rewarded,
    }

    /// <summary>
    /// Pictures of a painting's jigsaw at chosen moments, for <see cref="ScreenShots"/>: each moment is reached on a
    /// stage of its own, by the touches a player would make and the time that passes, so what is pictured is what
    /// the game shows.
    /// </summary>
    internal static class JigsawShots
    {
        /// <summary>A carried piece is pictured this far from its place: clear of it, so the marked place is seen beside the piece.</summary>
        private static readonly Vector2 CarriedAwayFromItsPlace = new Vector2(-34f, -40f);

        /// <summary>A piece that goes back is let go this far from its place, and pictured this long after.</summary>
        private static readonly Vector2 LetGoAwayFromItsPlace = new Vector2(-70f, -120f);
        private const float GoingBackMoment = 0.09f;

        /// <summary>A piece that dropped in is pictured this long after it arrived; flying pieces this long after the button; a celebration this far into it.</summary>
        private const float PlacedMoment = 0.07f;
        private const float FlyingMoment = 0.34f;
        private const float CelebrationMoment = 0.34f;

        private const float LongestPlacingSeconds = 20f;

        /// <returns>How many pictures were taken: a moment that the player's state cannot reach (no piece waits) gives none.</returns>
        public static int Shoot(PaintingNumber painting, IReadOnlyList<JigsawMoment> moments, PlayerState state, ScreenShape screen, Studio studio)
        {
            int pictures = 0;
            foreach (JigsawMoment moment in moments)
            {
                // A scene of its own that is never saved: nothing of an earlier picture is left in the next.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                using JigsawStage jigsaw = JigsawStage.Open(
                    painting, state.Progress.Museum, studio.Paintings, studio.Art, reward: new WalletLedger(StoryStudio.Economy).RewardForCompleting(state.Progress, painting));
                jigsaw.Fit(screen);
                if (Reach(moment, jigsaw, screen))
                {
                    ScreenShots.WritePicture(jigsaw, screen, studio.FileName($"jigsaw-p{painting.Value:D2}-{moment.ToString().ToLowerInvariant()}", state, screen));
                    pictures++;
                }
                else
                {
                    Debug.Log($"Screen shots: no piece of painting {painting} waits for the player {state.Label}; its jigsaw has no moment \"{moment}\".");
                }
            }

            return pictures;
        }

        /// <summary>Brings a freshly opened jigsaw to a moment.</summary>
        /// <returns>False when the moment needs a waiting piece and none waits.</returns>
        private static bool Reach(JigsawMoment moment, JigsawStage jigsaw, ScreenShape screen)
        {
            switch (moment)
            {
                case JigsawMoment.Rest:
                    return true;
                case JigsawMoment.Carried:
                    return PickUpAndCarry(jigsaw, CarriedAwayFromItsPlace, screen);
                case JigsawMoment.GoingBack:
                    return LetGo(jigsaw, LetGoAwayFromItsPlace, GoingBackMoment, screen);
                case JigsawMoment.Placed:
                    return LetGo(jigsaw, Vector2.zero, Tempo.PieceDropsIn + PlacedMoment, screen);
                case JigsawMoment.Flying:
                    return PressPlaceThemAll(jigsaw, FlyingMoment);
                case JigsawMoment.Restored:
                    return PlaceThemAll(jigsaw);
                case JigsawMoment.Rewarded:
                    if (!PlaceThemAll(jigsaw))
                    {
                        return false;
                    }

                    StageSteps.Settle(jigsaw);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(moment), moment, "A moment of the jigsaw the picture command does not know.");
            }
        }

        /// <summary>
        /// A finger takes the tray's first piece and carries it to a point near its place; the piece is in the hand
        /// when this returns. The point lies at the side of the place that is nearer the middle of the screen, so
        /// the piece is pictured whole.
        /// </summary>
        /// <returns>False when the tray shows no piece.</returns>
        private static bool PickUpAndCarry(JigsawStage jigsaw, Vector2 awayFromItsPlace, ScreenShape screen)
        {
            if (jigsaw.PiecesInTheTray.Count == 0)
            {
                return false;
            }

            int piece = jigsaw.PiecesInTheTray[0];
            Vector2 atOneSide = PixelOrFail(jigsaw.PixelToLetGoAt(piece, awayFromItsPlace), "the place of the piece");
            Vector2 atTheOther = PixelOrFail(jigsaw.PixelToLetGoAt(piece, new Vector2(-awayFromItsPlace.x, awayFromItsPlace.y)), "the place of the piece");
            float middle = screen.PixelWidth / 2f;
            jigsaw.PointerDown(PixelOrFail(jigsaw.PixelOfTheTrayPiece(piece), "the piece in the tray"));
            StageSteps.Settle(jigsaw);
            jigsaw.PointerMoved(Mathf.Abs(atOneSide.x - middle) <= Mathf.Abs(atTheOther.x - middle) ? atOneSide : atTheOther);
            return true;
        }

        private static bool LetGo(JigsawStage jigsaw, Vector2 awayFromItsPlace, float picturedAfter, ScreenShape screen)
        {
            if (!PickUpAndCarry(jigsaw, awayFromItsPlace, screen))
            {
                return false;
            }

            jigsaw.PointerUp();
            StageSteps.Advance(jigsaw, picturedAfter);
            return true;
        }

        private static bool PressPlaceThemAll(JigsawStage jigsaw, float picturedAfter)
        {
            if (jigsaw.Table.Waiting.Count == 0)
            {
                return false;
            }

            jigsaw.PointerDown(PixelOrFail(jigsaw.TrayButtonPixel, "the button under the tray"));
            jigsaw.PointerUp();
            StageSteps.Advance(jigsaw, picturedAfter);
            return true;
        }

        /// <summary>The blue button places every piece; a painting that is whole then is pictured in its celebration, any other at rest.</summary>
        /// <exception cref="InvalidOperationException">The button did not place the pieces.</exception>
        internal static bool PlaceThemAll(JigsawStage jigsaw)
        {
            if (!PressPlaceThemAll(jigsaw, picturedAfter: 0f))
            {
                return false;
            }

            for (float waited = 0f; jigsaw.Table.Waiting.Count > 0; waited += StoryStudio.FrameSeconds)
            {
                if (waited > LongestPlacingSeconds)
                {
                    throw new InvalidOperationException($"After {LongestPlacingSeconds} s the button under the tray has not placed every piece.");
                }

                jigsaw.Advance(StoryStudio.FrameSeconds);
            }

            if (jigsaw.Table.IsWhole)
            {
                StageSteps.Advance(jigsaw, Tempo.PauseBeforeRestored + CelebrationMoment);
            }
            else
            {
                StageSteps.Settle(jigsaw);
            }

            return true;
        }

        private static Vector2 PixelOrFail(Vector2? pixel, string what) =>
            pixel ?? throw new InvalidOperationException($"The jigsaw does not show {what}, which the picture needs touched.");
    }
}
