using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Core.Levels;

namespace Puzzle.Museum
{
    /// <summary>
    /// How far a player has restored the museum. Two facts decide all of it: how many levels they have won, which
    /// gives them pieces, and which of those pieces they have put in place. A piece that is won but not placed
    /// waits on its painting's tray; the player places it at once or whenever they like.
    /// </summary>
    /// <remarks>
    /// Pieces are won by the levels alone, so a player who won levels before the museum existed has the pieces of
    /// all of them waiting. A painting's pieces are won in the order of their numbers: the n-th level of a painting
    /// gives the pieces that follow those of the levels before it.
    /// </remarks>
    public sealed class MuseumProgress : IEquatable<MuseumProgress>
    {
        /// <param name="levelsWon">How many levels, from the first on, the player has won. Levels beyond the museum's thousand win no piece.</param>
        /// <exception cref="ArgumentOutOfRangeException">The count of levels is negative.</exception>
        /// <exception cref="ArgumentException">A piece is said to be in place that the levels won have not given yet.</exception>
        public MuseumProgress(int levelsWon, PlacedPieces placed)
        {
            if (levelsWon < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelsWon), levelsWon, "A player cannot have won fewer than no levels.");
            }

            if (!IsPossible(levelsWon, placed))
            {
                throw new ArgumentException($"{levelsWon} levels won do not give every piece that is said to be in place.", nameof(placed));
            }

            LevelsWon = CountedForTheMuseum(levelsWon);
            Placed = placed;
        }

        /// <summary>A new player: no level won, no piece.</summary>
        public static MuseumProgress None { get; } = new MuseumProgress(levelsWon: 0, PlacedPieces.None);

        /// <summary>The levels won that count for the museum: at most its thousand.</summary>
        public int LevelsWon { get; }

        public PlacedPieces Placed { get; }

        public int PiecesWon => PaintingNumber.All.Sum(PiecesWonIn);

        public int PiecesPlaced => Placed.Count;

        /// <summary>How many pieces lie on the trays, won and not yet placed.</summary>
        public int PiecesWaiting => PiecesWon - PiecesPlaced;

        /// <summary>How many paintings hang whole.</summary>
        public int PaintingsCompleted => PaintingNumber.All.Count(IsComplete);

        public int RoomsOpened => RoomNumber.All.Count(IsOpen);

        /// <summary>How many rooms have all their five paintings whole.</summary>
        public int RoomsRestored => RoomNumber.All.Count(IsRestored);

        /// <summary>The painting whose pieces the player's next level gives; the last painting once every level is won.</summary>
        public PaintingNumber PaintingOfTheNextLevel =>
            PaintingNumber.OfLevel(new LevelId(Math.Min(LevelsWon + 1, MuseumPlan.LevelCount)));

        /// <summary>The numbers of the pieces of its painting that winning a level gives, ascending.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The level lies beyond the museum's last painting.</exception>
        public static IReadOnlyList<int> PiecesGivenBy(LevelId level)
        {
            PaintingNumber painting = PaintingNumber.OfLevel(level);
            int perLevel = painting.Size.PiecesPerLevel;
            int levelsBefore = level.Value - painting.FirstLevel.Value;
            return Enumerable.Range(levelsBefore * perLevel, perLevel).ToArray();
        }

        /// <summary>
        /// True when pieces can be in place as said after so many levels won: every one of them is a piece those
        /// levels give. A saved progress that fails this was not written by the game.
        /// </summary>
        public static bool IsPossible(int levelsWon, PlacedPieces placed)
        {
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                IReadOnlyList<int> inPlace = placed.In(painting);
                if (inPlace.Count > 0 && inPlace[inPlace.Count - 1] >= PiecesWonIn(painting, CountedForTheMuseum(levelsWon)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>How many of the painting's ten levels are won.</summary>
        public int LevelsWonIn(PaintingNumber painting) => LevelsWonIn(painting, LevelsWon);

        /// <summary>How many of the painting's pieces the player has won; they are the pieces numbered below this.</summary>
        public int PiecesWonIn(PaintingNumber painting) => PiecesWonIn(painting, LevelsWon);

        public int PiecesPlacedIn(PaintingNumber painting) => Placed.CountIn(painting);

        /// <summary>The pieces on the painting's tray: won and not yet placed, ascending.</summary>
        public IReadOnlyList<int> WaitingIn(PaintingNumber painting) =>
            Enumerable.Range(0, PiecesWonIn(painting)).Where(piece => !Placed.IsPlaced(painting, piece)).ToArray();

        /// <summary>True when every piece of the painting is in place: it hangs whole.</summary>
        public bool IsComplete(PaintingNumber painting) => PiecesPlacedIn(painting) == painting.Size.PieceCount;

        /// <summary>True when the player may enter the room: every level of the rooms before it is won.</summary>
        public bool IsOpen(RoomNumber room) => LevelsWon >= room.LevelsWonToOpen;

        /// <summary>True when all five paintings of the room hang whole.</summary>
        public bool IsRestored(RoomNumber room) => room.Paintings.All(IsComplete);

        /// <summary>The player put a piece from the tray in its place.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The painting has no piece of that number.</exception>
        /// <exception cref="InvalidOperationException">The piece is not on the tray: not won yet, or in place already.</exception>
        public MuseumProgress WithPiecePlaced(PaintingNumber painting, int piece)
        {
            PlacedPieces placed = Placed.With(painting, piece);
            if (piece >= PiecesWonIn(painting))
            {
                throw new InvalidOperationException($"Piece {piece} of painting {painting} is not won yet.");
            }

            return new MuseumProgress(LevelsWon, placed);
        }

        /// <summary>Every piece on the painting's tray goes to its place at once: the "complete it for me" button.</summary>
        public MuseumProgress WithTrayPlaced(PaintingNumber painting)
        {
            PlacedPieces placed = Placed;
            foreach (int piece in WaitingIn(painting))
            {
                placed = placed.With(painting, piece);
            }

            return new MuseumProgress(LevelsWon, placed);
        }

        private static int CountedForTheMuseum(int levelsWon) => Math.Min(levelsWon, MuseumPlan.LevelCount);

        private static int LevelsWonIn(PaintingNumber painting, int levelsWon) =>
            Math.Max(0, Math.Min(levelsWon - (painting.FirstLevel.Value - 1), MuseumPlan.LevelsPerPainting));

        private static int PiecesWonIn(PaintingNumber painting, int levelsWon) => LevelsWonIn(painting, levelsWon) * painting.Size.PiecesPerLevel;

        public bool Equals(MuseumProgress? other) => other is not null && LevelsWon == other.LevelsWon && Placed.Equals(other.Placed);

        public override bool Equals(object? obj) => Equals(obj as MuseumProgress);

        public override int GetHashCode() => HashCode.Combine(LevelsWon, Placed);

        public override string ToString() => $"{LevelsWon} levels won, {PiecesPlaced} of {PiecesWon} pieces placed";
    }
}
