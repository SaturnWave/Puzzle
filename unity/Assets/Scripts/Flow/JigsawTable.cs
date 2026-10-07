#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Museum;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// One painting's jigsaw while the player works on it: which pieces are in their place, and which the player has
    /// won and not placed yet. It begins as the player's progress has the painting and changes as pieces are put
    /// down; whoever owns the progress reads <see cref="InPlace"/> back and keeps it. No Unity dependency.
    /// </summary>
    public sealed class JigsawTable
    {
        private readonly SortedSet<int> _inPlace;
        private readonly SortedSet<int> _waiting;

        public JigsawTable(PaintingNumber painting, MuseumProgress progress)
        {
            Painting = painting;
            _inPlace = new SortedSet<int>(progress.Placed.In(painting));
            _waiting = new SortedSet<int>(progress.WaitingIn(painting));
        }

        public PaintingNumber Painting { get; }

        public int PieceCount => Painting.Size.PieceCount;

        /// <summary>The numbers of the pieces that are in their place, rising.</summary>
        public IReadOnlyCollection<int> InPlace => _inPlace;

        /// <summary>The numbers of the pieces the player has won and not placed yet, rising.</summary>
        public IReadOnlyCollection<int> Waiting => _waiting;

        /// <summary>True when every piece of the painting is in its place.</summary>
        public bool IsWhole => _inPlace.Count == PieceCount;

        /// <summary>The piece went from the tray to its place.</summary>
        /// <exception cref="InvalidOperationException">The piece is not waiting: it is in place already, or not won yet.</exception>
        public void Place(int piece)
        {
            if (!_waiting.Remove(piece))
            {
                throw new InvalidOperationException($"Piece {piece} of painting {Painting} is not on the tray.");
            }

            _inPlace.Add(piece);
        }
    }

    /// <summary>
    /// The row of places in the tray. A tray has room for a few pieces side by side; a player who has won more than
    /// that sees the first ones, and each time one of them leaves for the painting the next one that waited behind
    /// takes its place. A place keeps its piece until that piece is taken, so nothing moves under the player's finger.
    /// No Unity dependency.
    /// </summary>
    public sealed class TrayRow
    {
        private readonly int?[] _places;
        private readonly Queue<int> _behind;

        /// <param name="waiting">The pieces that wait, in the order they are shown.</param>
        /// <param name="placeCount">How many pieces the tray shows side by side.</param>
        /// <exception cref="ArgumentOutOfRangeException">A tray without a place.</exception>
        public TrayRow(IEnumerable<int> waiting, int placeCount)
        {
            if (placeCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(placeCount), placeCount, "A tray shows at least one piece.");
            }

            _behind = new Queue<int>(waiting);
            int shown = Math.Min(placeCount, _behind.Count);
            _places = new int?[shown];
            for (int place = 0; place < shown; place++)
            {
                _places[place] = _behind.Dequeue();
            }
        }

        /// <summary>
        /// The tray's places from left to right, each with the piece that lies in it or nothing. A tray has no more
        /// places than pieces waited when it was laid.
        /// </summary>
        public IReadOnlyList<int?> Places => _places;

        /// <summary>How many pieces wait behind the ones that are shown.</summary>
        public int Behind => _behind.Count;

        /// <summary>The pieces that are shown, from left to right.</summary>
        public IEnumerable<int> Shown => _places.Where(piece => piece.HasValue).Select(piece => piece!.Value);

        /// <summary>The place a piece lies in, counted from 0 at the left; nothing when the tray does not show it.</summary>
        public int? PlaceOf(int piece)
        {
            int place = Array.IndexOf(_places, piece);
            return place >= 0 ? place : (int?)null;
        }

        /// <summary>A shown piece leaves the tray; the next piece from behind, if one waits, takes its place.</summary>
        /// <returns>The piece that came into the place; nothing when none waited behind.</returns>
        /// <exception cref="InvalidOperationException">The tray does not show that piece.</exception>
        public int? Take(int piece)
        {
            int place = PlaceOf(piece) ?? throw new InvalidOperationException($"The tray does not show piece {piece}.");
            _places[place] = _behind.Count > 0 ? _behind.Dequeue() : (int?)null;
            return _places[place];
        }
    }

    /// <summary>
    /// When a piece that is let go drops into its place: when its middle is within reach of the middle of the place
    /// it belongs to. The reach is a share of the place's own size, so a small piece of a large jigsaw has to be
    /// brought as close, measured in pieces, as a large one. No Unity dependency.
    /// </summary>
    public static class JigsawSnap
    {
        /// <summary>A piece let go within this share of its place's width and height from the place's middle drops in.</summary>
        public const float Reach = 0.55f;

        /// <summary>The smallest reach, in points: about half a fingertip, so the small pieces of a forty-piece jigsaw are not fiddly.</summary>
        public const float SmallestReach = 22f;

        /// <param name="awayX">How far the piece's middle is from its place's middle, sideways, in points.</param>
        /// <param name="awayY">The same, upright.</param>
        /// <param name="placeWidth">The width of the piece's place (its cell, knobs aside), in points.</param>
        /// <param name="placeHeight">The height of the piece's place, in points.</param>
        public static bool Catches(float awayX, float awayY, float placeWidth, float placeHeight)
        {
            float reachX = Math.Max(SmallestReach, Reach * placeWidth);
            float reachY = Math.Max(SmallestReach, Reach * placeHeight);
            float shareX = awayX / reachX;
            float shareY = awayY / reachY;
            return shareX * shareX + shareY * shareY <= 1f;
        }
    }
}
