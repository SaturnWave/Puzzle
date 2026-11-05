#nullable enable
using System;
using System.IO;
using System.Linq;
using Puzzle.Client.Engine;
using Puzzle.Client.Flow;
using Puzzle.Client.Layout;
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
    /// Pictures of the game's hint for <see cref="ScreenShots"/> (<c>-screens hint</c>), on the game's own session at
    /// the board of the level the state's player stands on, left untouched until the hint shows: the hint at the
    /// farthest of its first nudge (<c>screen-hint-...</c>); eight moments of its first beat, cut out around its cells,
    /// on one strip, from the light coming up to the rest after the nudge (<c>screen-hint-strip-...</c>); and the
    /// hint beside a special piece: from the player's level on, each level is opened through its card with the rocket
    /// and the bomb chosen until its first hint lies on or next to a special piece (<c>screen-hint-special-...</c>).
    /// </summary>
    internal static class HintShots
    {
        /// <summary>The moments of the strip, in seconds after the hint showed: up to the nudge's farthest, back, and at rest.</summary>
        private static readonly float[] StripMoments = { 0.05f, 0.18f, 0.35f, 0.5f, 0.6f, 0.72f, 0.9f, 1.5f };

        /// <summary>The whole board is pictured this long after the hint showed: the farthest of its first nudge.</summary>
        private const float NudgeMoment = 0.6f;

        /// <summary>A moment of the strip is cut out this many cells across, around the middle of the hinted move.</summary>
        private const float CellsAcrossACut = 4.5f;

        /// <summary>Levels tried, from the player's own on, for a hint beside a special piece before the picture is given up.</summary>
        private const int MostLevelsForASpecial = 40;

        /// <returns>How many pictures were written.</returns>
        /// <exception cref="InvalidOperationException">The board showed no hint after the wait, or no level showed one beside a special piece.</exception>
        public static int Shoot(PlayerState state, ScreenShape screen, Studio studio)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using var session = new GameSession(
                studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(state.Progress), StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.AtTheBoard);
            StoryStudio.AdvanceUntilSettled(session);
            Move hint = WaitForTheHint(session);
            var strip = new ContactSheet(shrinkBy: 1);
            float shownFor = 0f;
            foreach (float moment in StripMoments)
            {
                StoryStudio.AdvanceBy(moment - shownFor, session);
                shownFor = moment;
                if (Mathf.Approximately(moment, NudgeMoment))
                {
                    ScreenShots.WritePicture(session.Front, screen, studio.FileName("hint", state, screen));
                }

                Texture2D picture = StoryStudio.Take(session.Front, screen);
                Texture2D? cut = null;
                try
                {
                    cut = CutAround(picture, session.Stage, hint);
                    strip.Add(cut);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(picture);
                    if (cut != null)
                    {
                        UnityEngine.Object.DestroyImmediate(cut);
                    }
                }
            }

            StoryStudio.WriteSheet(strip, studio.FileName("hint-strip", state, screen));
            ShootTheHintBesideASpecial(state, screen, studio);
            Debug.Log($"Screen shots: the hint {hint} on level {session.Stage.Game.Level.Id}, at its nudge, on a strip of {StripMoments.Length} moments, and beside a special piece.");
            return 3;
        }

        /// <summary>Opens each level from the player's own on with the rocket and the bomb on its board, until the first hint lies on or next to a special piece.</summary>
        private static void ShootTheHintBesideASpecial(PlayerState state, ScreenShape screen, Studio studio)
        {
            int first = state.Progress.LevelIndexIn(studio.Levels.Count);
            foreach (LevelDefinition level in studio.Levels.Skip(first).Take(MostLevelsForASpecial))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                using var session = new GameSession(
                    studio.Levels, studio.Art, studio.Paintings, StoryStudio.Economy, StoryStudio.Clock, new ProgressInMemory(StoryStudio.StandingOn(level, studio.Levels)),
                    StoryStudio.AttemptsKeptInMemory(), screen, SessionOpening.InTheMuseum);
                ThroughTheCard.IntoTheLevel(session, new[] { BoosterKind.StartRocketAndBomb });
                Move hint = WaitForTheHint(session);
                if (LiesBesideASpecial(session.Stage.Game.Board, hint))
                {
                    StoryStudio.AdvanceBy(NudgeMoment, session);
                    ScreenShots.WritePicture(session.Front, screen, studio.FileName("hint-special", state, screen));
                    Debug.Log($"Screen shots: level {level.Id} with the rocket and the bomb hints {hint} beside a special piece.");
                    return;
                }
            }

            throw new InvalidOperationException($"None of {MostLevelsForASpecial} levels from the player's own on, opened with the rocket and the bomb, hinted a move beside a special piece.");
        }

        /// <summary>Leaves the board untouched until the hint shows, no longer than the settings' wait and a second.</summary>
        /// <exception cref="InvalidOperationException">No hint showed in that time.</exception>
        private static Move WaitForTheHint(GameSession session)
        {
            float longest = (float)StoryStudio.Economy.Hints.Idle.TotalSeconds + 1f;
            for (float waited = 0f; session.Stage.HintShown == null; waited += StoryStudio.FrameSeconds)
            {
                if (waited > longest)
                {
                    throw new InvalidOperationException($"Level {session.Stage.Game.Level.Id} left untouched for {longest} s showed no hint.");
                }

                session.Advance(StoryStudio.FrameSeconds);
            }

            return session.Stage.HintShown ?? throw new InvalidOperationException("The hint went before it was looked at.");
        }

        /// <summary>Whether a special piece lies in a cell of the move or in a cell that touches one, diagonally too.</summary>
        private static bool LiesBesideASpecial(Board board, Move move) =>
            new[] { move.From, move.To }
                .SelectMany(cell => Enumerable.Range(-1, 3).SelectMany(dx => Enumerable.Range(-1, 3).Select(dy => new Position(cell.X + dx, cell.Y + dy))))
                .Any(cell => board.Shape.IsPlayable(cell) && board.TileAt(cell).Kind.IsSpecial());

        /// <summary>A square of the picture, <see cref="CellsAcrossACut"/> cells across, around the middle of the move, kept inside the picture. The caller owns it.</summary>
        private static Texture2D CutAround(Texture2D picture, GameStage stage, Move move)
        {
            Vector2 from = stage.PixelOf(move.From);
            Vector2 middle = (from + stage.PixelOf(move.To)) / 2f;
            float cell = Vector2.Distance(stage.PixelOf(new Position(0, 0)), stage.PixelOf(new Position(1, 0)));
            int size = Mathf.Min(Mathf.RoundToInt(CellsAcrossACut * cell), Mathf.Min(picture.width, picture.height));
            int x = Mathf.Clamp(Mathf.RoundToInt(middle.x - size / 2f), 0, picture.width - size);
            int y = Mathf.Clamp(Mathf.RoundToInt(middle.y - size / 2f), 0, picture.height - size);
            var cut = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            cut.SetPixels(picture.GetPixels(x, y, size, size));
            cut.Apply();
            return cut;
        }
    }
}
