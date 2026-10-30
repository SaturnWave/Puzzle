#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Core.Levels;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// The jigsaw pieces a level gives the player who wins it: which painting they belong to and which of its pieces
    /// they are. Which level gives which pieces is the museum's rule (<see cref="MuseumProgress.PiecesGivenBy"/>);
    /// this is that rule for the level a player stands on, as the result's card tells it. No Unity dependency.
    /// </summary>
    public sealed class PieceReward
    {
        private PieceReward(PaintingNumber painting, IReadOnlyList<int> pieces, int waitingWithThem)
        {
            Painting = painting;
            Pieces = pieces;
            WaitingWithThem = waitingWithThem;
        }

        public PaintingNumber Painting { get; }

        /// <summary>The numbers of the pieces, in the order they are won; at least one.</summary>
        public IReadOnlyList<int> Pieces { get; }

        /// <summary>How many of the painting's pieces the player has won once these are theirs: pieces are won in number order.</summary>
        public int WonWithThem => Pieces[Pieces.Count - 1] + 1;

        /// <summary>
        /// How many pieces of the painting wait to be placed once these are won: these, and the ones the player won
        /// before and has not placed. It is what the card's button to the jigsaw speaks of.
        /// </summary>
        public int WaitingWithThem { get; }

        /// <summary>How many pieces the painting has in all.</summary>
        public int PieceCount => Painting.Size.PieceCount;

        /// <summary>
        /// What winning the level a player stands on gives them to hold and to place. Nothing when that level gives
        /// nothing new (the last level of the pack played again after it was won, or a level beyond the museum's
        /// paintings), and nothing when no work is chosen yet for the painting the level belongs to: the player is
        /// never handed a piece of a blank canvas. The levels won keep such a piece all the same
        /// (<see cref="MuseumProgress"/>), so it waits on its tray the day the painting is there.
        /// </summary>
        /// <param name="levelCount">How many levels the pack has.</param>
        /// <param name="catalogue">What hangs where in the museum.</param>
        /// <exception cref="ArgumentOutOfRangeException">The pack has no level.</exception>
        public static PieceReward? ForWinning(PlayerProgress progress, int levelCount, MuseumCatalogue catalogue)
        {
            if (levelCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelCount), levelCount, "A pack without a level has no level to win.");
            }

            bool isWonAlready = progress.LevelsWon >= levelCount;
            bool isBeyondTheMuseum = progress.LevelsWon >= MuseumPlan.LevelCount;
            if (isWonAlready || isBeyondTheMuseum)
            {
                return null;
            }

            var level = new LevelId(progress.LevelsWon + 1);
            PaintingNumber painting = PaintingNumber.OfLevel(level);
            if (catalogue.ArtworkAt(painting) == null)
            {
                return null;
            }

            IReadOnlyList<int> pieces = MuseumProgress.PiecesGivenBy(level);
            return new PieceReward(painting, pieces, progress.Museum.WaitingIn(painting).Count + pieces.Count);
        }
    }
}
