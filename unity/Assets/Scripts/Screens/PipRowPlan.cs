#nullable enable
using System;
using System.Collections.Generic;

namespace Puzzle.Client.Screens
{
    /// <summary>What one pip of a row says of its piece. A closed set.</summary>
    public enum PipFace
    {
        /// <summary>The piece was won before: a gilt pip.</summary>
        Won,

        /// <summary>The piece has just been won: a gilt pip in a ring of light and of red.</summary>
        JustWon,

        /// <summary>The piece is not won yet: a hollow.</summary>
        NotWon,
    }

    /// <summary>
    /// A painting's pieces as a row of pips on a cream card, as the design canvas sets it under a won level's new
    /// piece (WinTR): one pip a piece, the pieces won before first, then the ones just won, then the ones still to
    /// win. The pips stand side by side with a gap between them; a pip just won has a little more room at each
    /// side for its rings. Lengths are in points at the row's full size, along the row from its middle.
    /// No Unity dependency.
    /// </summary>
    public sealed class PipRowPlan
    {
        /// <summary>A pip and the gap to the next (WinTR: 19 by 13 points, 5.5 apart).</summary>
        public const float PipWidth = 19f;
        public const float PipHeight = 13f;
        public const float Gap = 5.5f;

        /// <summary>The room a pip just won has more than the others at each of its sides (WinTR: 3.2).</summary>
        public const float RoomBesideAPipJustWon = 3.2f;

        private readonly float[] _middles;

        private PipRowPlan(PipFace[] faces, float[] middles, float width)
        {
            Faces = faces;
            _middles = middles;
            Width = width;
        }

        /// <summary>Every piece's pip, in the order of the pieces' winning.</summary>
        public IReadOnlyList<PipFace> Faces { get; }

        /// <summary>From the first pip's left edge to the last one's right edge, with the room a pip just won has at the row's ends.</summary>
        public float Width { get; }

        /// <summary>The row of a painting of so many pieces, of which so many are won, the last of those just now.</summary>
        /// <param name="won">How many pieces are won, the ones just won among them.</param>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece, or more are won than it has, or more just now than in all.</exception>
        public static PipRowPlan Of(int pieceCount, int won, int justWon)
        {
            if (pieceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceCount), pieceCount, "A painting has a piece at least.");
            }

            if (won < 0 || won > pieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(won), won, $"Of {pieceCount} pieces, none to {pieceCount} can be won.");
            }

            if (justWon < 0 || justWon > won)
            {
                throw new ArgumentOutOfRangeException(nameof(justWon), justWon, $"Of {won} pieces won, none to {won} can have been won just now.");
            }

            var faces = new PipFace[pieceCount];
            var lefts = new float[pieceCount];
            float right = 0f;
            for (int pip = 0; pip < pieceCount; pip++)
            {
                faces[pip] = pip < won - justWon ? PipFace.Won : pip < won ? PipFace.JustWon : PipFace.NotWon;
                float roomBeside = faces[pip] == PipFace.JustWon ? RoomBesideAPipJustWon : 0f;
                lefts[pip] = right + (pip > 0 ? Gap : 0f) + roomBeside;
                right = lefts[pip] + PipWidth + roomBeside;
            }

            var middles = new float[pieceCount];
            for (int pip = 0; pip < pieceCount; pip++)
            {
                middles[pip] = lefts[pip] + PipWidth / 2 - right / 2;
            }

            return new PipRowPlan(faces, middles, right);
        }

        /// <summary>Where the middle of a pip lies along the row, from the row's middle.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The row has no such pip.</exception>
        public float MiddleOf(int pip) => pip >= 0 && pip < _middles.Length
            ? _middles[pip]
            : throw new ArgumentOutOfRangeException(nameof(pip), pip, $"A row of {_middles.Length} pips has no pip {pip}.");
    }
}
