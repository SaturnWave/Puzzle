#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>How far a painting has come back. A closed set: every painting is exactly one of these.</summary>
    public enum PaintingStanding
    {
        /// <summary>
        /// No work is chosen for this place of the museum yet. Its frame hangs covered whatever levels the player has
        /// won: the game shows no piece of it, counts none and lets none be placed, so that no player is handed a
        /// blank canvas as a reward.
        /// </summary>
        ToCome,

        /// <summary>The player has won no piece of it yet: its frame hangs covered.</summary>
        NotReached,

        /// <summary>The player has won pieces of it, and not all of its pieces are in place.</summary>
        BeingRestored,

        /// <summary>Every piece is in place.</summary>
        Whole,
    }

    /// <summary>
    /// One painting of the museum as a player's progress has it: the work that hangs there, if one is chosen, and
    /// which of its jigsaw pieces are in place. A place no work is chosen for has no pieces here, whatever the
    /// player's levels would give: the levels won keep them, and they wait until the work is there. No Unity dependency.
    /// </summary>
    public sealed class HungPainting
    {
        internal HungPainting(PaintingNumber number, Artwork? artwork, IReadOnlyList<int> piecesInPlace, int piecesWaiting)
        {
            Number = number;
            Artwork = artwork;
            PiecesInPlace = piecesInPlace;
            PiecesWaiting = piecesWaiting;
        }

        public PaintingNumber Number { get; }

        /// <summary>The work chosen for this place of the museum; nothing while the place is still to be filled.</summary>
        public Artwork? Artwork { get; }

        /// <summary>The numbers of the pieces the player has put in place, rising.</summary>
        public IReadOnlyList<int> PiecesInPlace { get; }

        /// <summary>The pieces the player has won but not yet put in place.</summary>
        public int PiecesWaiting { get; }

        public int PieceCount => Number.Size.PieceCount;

        /// <summary>True when every piece is in place: the painting hangs restored.</summary>
        public bool IsWhole => PiecesInPlace.Count == PieceCount;

        /// <summary>How much of the painting is in place, from 0 to 1.</summary>
        public float ShareInPlace => (float)PiecesInPlace.Count / PieceCount;

        /// <summary>The pieces the player has won, in place or waiting.</summary>
        public int PiecesWon => PiecesInPlace.Count + PiecesWaiting;

        public PaintingStanding Standing =>
            Artwork == null ? PaintingStanding.ToCome
            : IsWhole ? PaintingStanding.Whole
            : PiecesWon > 0 ? PaintingStanding.BeingRestored
            : PaintingStanding.NotReached;
    }

    /// <summary>
    /// A room of the museum as its screens show it: its name, its five paintings, and how far the player has
    /// restored them. No Unity dependency.
    /// </summary>
    public sealed class RoomWall
    {
        private RoomWall(RoomNumber number, string name, IReadOnlyList<HungPainting> paintings)
        {
            Number = number;
            Name = name;
            Paintings = paintings;
        }

        public RoomNumber Number { get; }

        /// <summary>The room's name as the player reads it: the art movement it is named after, in Turkish, once that is decided.</summary>
        public string Name { get; }

        /// <summary>The room's five paintings, in the order they are restored.</summary>
        public IReadOnlyList<HungPainting> Paintings { get; }

        public int PaintingsWhole => Paintings.Count(painting => painting.Standing == PaintingStanding.Whole);

        /// <summary>True when all five paintings hang whole: the room is restored.</summary>
        public bool IsRestored => PaintingsWhole == Paintings.Count;

        /// <summary>True when no work is chosen for any of the room's places: the room is still being put together.</summary>
        public bool IsToCome => Paintings.All(painting => painting.Standing == PaintingStanding.ToCome);

        public static RoomWall Of(RoomNumber room, MuseumProgress progress, MuseumCatalogue catalogue) => new RoomWall(
            room,
            MuseumWords.NameOf(catalogue.Room(room)),
            room.Paintings.Select(painting => Hung(painting, progress, catalogue)).ToArray());

        public HungPainting Painting(PaintingNumber number) => number.Room == Number
            ? Paintings[number.PlaceInRoom - 1]
            : throw new ArgumentException($"Painting {number} hangs in room {number.Room}, not in room {Number}.", nameof(number));

        private static HungPainting Hung(PaintingNumber painting, MuseumProgress progress, MuseumCatalogue catalogue) =>
            catalogue.ArtworkAt(painting) is Artwork artwork
                ? new HungPainting(painting, artwork, progress.Placed.In(painting), progress.WaitingIn(painting).Count)
                : new HungPainting(painting, artwork: null, Array.Empty<int>(), piecesWaiting: 0);
    }

    /// <summary>What the hall asks of the player. A closed set.</summary>
    public enum HallTask
    {
        /// <summary>The painting in the hall's frame is being restored: the player's levels give its pieces and the player places them.</summary>
        Restoring,

        /// <summary>
        /// Every painting the museum has for the levels the player has reached hangs whole, and no work is chosen
        /// yet for the paintings that follow: the hall keeps the last one restored in its frame and says that more
        /// are on their way.
        /// </summary>
        WaitingForPaintings,

        /// <summary>Every painting of the museum hangs whole: the museum is restored.</summary>
        MuseumRestored,
    }

    /// <summary>
    /// Where a player stands in the museum. The museum is restored in order: the painting the player is restoring
    /// is the first one that is not whole yet, and never one whose levels still lie ahead of them, so a player who
    /// plays on without placing pieces keeps the painting they left unfinished on the wall of the hall. A place no
    /// work is chosen for is passed over: the museum a player walks through ends where its paintings end. The room
    /// they stand in is the room of the painting the hall shows. No Unity dependency.
    /// </summary>
    public readonly struct MuseumStanding
    {
        private MuseumStanding(PaintingNumber painting, HallTask task)
        {
            Painting = painting;
            Task = task;
        }

        /// <summary>The painting the hall shows in its frame.</summary>
        public PaintingNumber Painting { get; }

        /// <summary>What the hall asks of the player about it.</summary>
        public HallTask Task { get; }

        /// <summary>The room the player stands in.</summary>
        public RoomNumber Room => Painting.Room;

        public static MuseumStanding Of(MuseumProgress progress, MuseumCatalogue catalogue)
        {
            PaintingNumber latest = progress.PaintingOfTheNextLevel;
            PaintingNumber? lastWhole = null;
            bool everyPlaceHasAWork = true;
            foreach (PaintingNumber painting in PaintingNumber.All)
            {
                if (painting.Value > latest.Value)
                {
                    break;
                }

                if (catalogue.ArtworkAt(painting) == null)
                {
                    everyPlaceHasAWork = false;
                }
                else if (!progress.IsComplete(painting))
                {
                    return new MuseumStanding(painting, HallTask.Restoring);
                }
                else
                {
                    lastWhole = painting;
                }
            }

            // Nothing is left to restore. The frame keeps the painting restored last; a museum that has no work at
            // all for this player's levels shows its first place, covered.
            bool isAtTheEnd = latest.Value == MuseumPlan.PaintingCount && everyPlaceHasAWork;
            return new MuseumStanding(lastWhole ?? new PaintingNumber(1), isAtTheEnd ? HallTask.MuseumRestored : HallTask.WaitingForPaintings);
        }
    }

    /// <summary>
    /// Which rooms the collection lets a player turn to: every room their levels have opened, and the one after,
    /// so the next room is seen waiting behind its dust sheets; but no further than the first room no work is
    /// chosen for, which is seen being prepared and has nothing to show behind it. No Unity dependency.
    /// </summary>
    public static class CollectionRooms
    {
        /// <summary>The last room the collection shows.</summary>
        public static RoomNumber LastToVisit(MuseumProgress progress, MuseumCatalogue catalogue)
        {
            int afterTheOpenOnes = Math.Min(progress.RoomsOpened + 1, MuseumPlan.RoomCount);
            foreach (MuseumRoom room in catalogue.Rooms)
            {
                if (room.Artworks.Count == 0)
                {
                    return new RoomNumber(Math.Min(afterTheOpenOnes, room.Number.Value));
                }
            }

            return new RoomNumber(afterTheOpenOnes);
        }

        /// <summary>How many of the museum's paintings hang whole: the ones a work is chosen for and whose every piece is in place.</summary>
        public static int PaintingsWhole(MuseumProgress progress, MuseumCatalogue catalogue) =>
            PaintingNumber.All.Count(painting => catalogue.ArtworkAt(painting) != null && progress.IsComplete(painting));

        /// <summary>True when the collection shows a room to a player.</summary>
        public static bool Shows(RoomNumber room, MuseumProgress progress, MuseumCatalogue catalogue) =>
            room.Value <= LastToVisit(progress, catalogue).Value;
    }

    /// <summary>What the museum's pages are told of the player who visits them. No Unity dependency.</summary>
    public sealed class MuseumVisit
    {
        /// <param name="catalogue">What hangs where: the player's standing follows from it and from their progress.</param>
        /// <param name="levelToPlay">The number of the level the player stands on, as the player knows it.</param>
        /// <param name="hasWonEveryLevel">True when the level the player stands on is one they have won already: no level is left to win.</param>
        /// <exception cref="ArgumentOutOfRangeException">Levels are numbered from 1.</exception>
        public MuseumVisit(MuseumProgress progress, MuseumCatalogue catalogue, int levelToPlay, bool hasWonEveryLevel = false)
        {
            if (levelToPlay < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(levelToPlay), levelToPlay, "The levels a player knows are numbered from 1.");
            }

            Progress = progress;
            LevelToPlay = levelToPlay;
            HasWonEveryLevel = hasWonEveryLevel;
            Standing = MuseumStanding.Of(progress, catalogue);
        }

        public MuseumProgress Progress { get; }

        /// <summary>The level the play button opens.</summary>
        public int LevelToPlay { get; }

        /// <summary>True when no level is left to win: the play button offers the last one again.</summary>
        public bool HasWonEveryLevel { get; }

        /// <summary>Where the player stands in the museum.</summary>
        public MuseumStanding Standing { get; }

        /// <summary>The painting the hall shows in its frame.</summary>
        public PaintingNumber Painting => Standing.Painting;

        /// <summary>The painting the player is restoring; nothing when nothing is left to restore just now.</summary>
        public PaintingNumber? BeingRestored => Standing.Task == HallTask.Restoring ? Painting : (PaintingNumber?)null;

        /// <summary>The room the player stands in.</summary>
        public RoomNumber Room => Standing.Room;
    }
}
