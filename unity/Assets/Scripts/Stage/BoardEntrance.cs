using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Boards;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Core.Engine.Events;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Stage
{
    /// <summary>
    /// How a board comes on: its pieces pop up in a wave that runs from the top left corner to the bottom right one,
    /// each with the tape that lies over it. The floor and the slabs are there from the start, so the board's shape
    /// is seen before its pieces. The special pieces the player's start boosters put on the board come in after the
    /// wave, one after the other, each with a blink of warm light and a ring that widens from it, so the player sees
    /// what they took into the level and where it lies.
    /// </summary>
    public static class BoardEntrance
    {
        /// <summary>Each diagonal of the board starts this long after the one before it.</summary>
        private const float SecondsPerDiagonal = 0.014f;

        private const float PopSeconds = 0.3f;

        /// <summary>The first special comes in this long after the last gem began to pop, the next ones this long after each other.</summary>
        private const float SpecialsAfterTheGems = 0.18f;
        private const float BetweenSpecials = 0.16f;

        /// <summary>
        /// The blink of gilt light over a special that comes in, so many cells across, and the two rings that widen
        /// from it, the second a little later and wider: a twinkle the eye finds on a full board.
        /// </summary>
        private const float BlinkAcross = 2.4f;
        private const float BlinkStrength = 0.85f;
        private const float RingAcross = 2.6f;
        private const float SecondRingAfter = 0.12f;
        private const float SecondRingAcross = 3.6f;

        private static readonly Color SpecialLight = Tint.Of(0xFFD36B);

        /// <param name="startEvents">What the engine did as the game started (Game.LastEvents of a game just started): the specials of its start boosters.</param>
        public static void Play(BoardView board, TweenSet tweens, BoardEffects effects, IReadOnlyList<GameEvent> startEvents)
        {
            List<Position> specials = startEvents.OfType<GameEvent.SpecialCreated>().Select(created => created.At).ToList();
            int lastDiagonal = board.Pieces.Select(piece => piece.Cell.X + piece.Cell.Y).DefaultIfEmpty(0).Max();
            float specialsBegin = DelayOf(lastDiagonal) + SpecialsAfterTheGems;
            foreach (PieceSprite piece in board.Pieces)
            {
                PieceSprite popping = piece;
                int special = specials.IndexOf(piece.Cell);
                float delay = special < 0 ? DelayOf(piece.Cell.X + piece.Cell.Y) : specialsBegin + special * BetweenSpecials;
                tweens.Start(new Tween(delay, PopSeconds, Easing.Overshoot, grown => popping.Size(grown, grown)));
                if (special >= 0)
                {
                    Vector2 place = PieceSprite.MiddleOf(piece.Cell);
                    effects.Flash(delay, place, BlinkAcross, SpecialLight, BlinkStrength);
                    effects.Wave(delay, place, RingAcross);
                    effects.Wave(delay + SecondRingAfter, place, SecondRingAcross);
                }
            }

            foreach (CellSprite tape in board.Tapes)
            {
                Transform popping = tape.Transform;
                Vector3 atRest = popping.localScale;
                tweens.Start(new Tween(DelayOf(tape.Cell.X + tape.Cell.Y), PopSeconds, Easing.Overshoot, grown => popping.localScale = atRest * grown));
            }
        }

        private static float DelayOf(int diagonal) => diagonal * SecondsPerDiagonal;
    }
}
