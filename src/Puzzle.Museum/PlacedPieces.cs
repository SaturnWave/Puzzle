using System;
using System.Collections.Generic;

namespace Puzzle.Museum
{
    /// <summary>
    /// Which jigsaw pieces a player has put in their place, painting by painting. It is the one thing about the
    /// museum that has to be saved: everything else follows from the number of levels won. A value that never
    /// changes; placing a piece gives a new one.
    /// </summary>
    public sealed class PlacedPieces : IEquatable<PlacedPieces>
    {
        /// <summary>One entry per painting of the museum; bit n is set when piece n is in place. The largest painting has forty pieces.</summary>
        private readonly ulong[] _inPainting;

        private PlacedPieces(ulong[] inPainting)
        {
            _inPainting = inPainting;
        }

        /// <summary>Nothing placed: a new player, or one whose saved progress is older than the museum.</summary>
        public static PlacedPieces None { get; } = new PlacedPieces(new ulong[MuseumPlan.PaintingCount]);

        /// <summary>How many pieces are in place in the whole museum.</summary>
        public int Count
        {
            get
            {
                int count = 0;
                foreach (PaintingNumber painting in PaintingNumber.All)
                {
                    count += CountIn(painting);
                }

                return count;
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece of that number.</exception>
        public bool IsPlaced(PaintingNumber painting, int piece) => (_inPainting[painting.Value - 1] & BitOf(painting, piece)) != 0;

        public int CountIn(PaintingNumber painting) => In(painting).Count;

        /// <summary>The numbers of the painting's pieces that are in place, ascending.</summary>
        public IReadOnlyList<int> In(PaintingNumber painting)
        {
            var pieces = new List<int>();
            ulong bits = _inPainting[painting.Value - 1];
            for (int piece = 0; bits != 0; piece++, bits >>= 1)
            {
                if ((bits & 1) != 0)
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        /// <summary>These pieces, and one more in place.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece of that number.</exception>
        /// <exception cref="InvalidOperationException">The piece is in place already.</exception>
        public PlacedPieces With(PaintingNumber painting, int piece)
        {
            ulong bit = BitOf(painting, piece);
            if ((_inPainting[painting.Value - 1] & bit) != 0)
            {
                throw new InvalidOperationException($"Piece {piece} of painting {painting} is in place already.");
            }

            var inPainting = (ulong[])_inPainting.Clone();
            inPainting[painting.Value - 1] |= bit;
            return new PlacedPieces(inPainting);
        }

        public bool Equals(PlacedPieces? other)
        {
            if (other is null)
            {
                return false;
            }

            for (int index = 0; index < _inPainting.Length; index++)
            {
                if (_inPainting[index] != other._inPainting[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as PlacedPieces);

        public override int GetHashCode()
        {
            var hash = default(HashCode);
            foreach (ulong bits in _inPainting)
            {
                hash.Add(bits);
            }

            return hash.ToHashCode();
        }

        private static ulong BitOf(PaintingNumber painting, int piece)
        {
            if (piece < 0 || piece >= painting.Size.PieceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(piece), piece, $"Painting {painting} has the pieces 0 to {painting.Size.PieceCount - 1}.");
            }

            return 1UL << piece;
        }
    }
}
