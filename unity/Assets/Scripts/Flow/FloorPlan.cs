#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Museum;
using Puzzle.Museum.Catalogue;

namespace Puzzle.Client.Flow
{
    /// <summary>How far a room of the museum is, as the map colours it.</summary>
    public enum RoomAccess
    {
        /// <summary>All five paintings hang whole.</summary>
        Restored,

        /// <summary>The player's levels have opened the room; paintings are still to be restored.</summary>
        Open,

        /// <summary>The room opens with a level the player has not reached.</summary>
        Locked,

        /// <summary>No work is chosen for the room yet: it stays closed whatever levels are won, and says that it is being prepared.</summary>
        ToCome,
    }

    /// <summary>Several rooms that the map draws as one stretch, with their numbers only: "7–20".</summary>
    public readonly struct RoomStretch
    {
        /// <exception cref="ArgumentException">The stretch ends before it begins.</exception>
        public RoomStretch(RoomNumber first, RoomNumber last)
        {
            if (last.Value < first.Value)
            {
                throw new ArgumentException($"A stretch of rooms cannot run from room {first} back to room {last}.", nameof(last));
            }

            First = first;
            Last = last;
        }

        public RoomNumber First { get; }

        public RoomNumber Last { get; }
    }

    /// <summary>How much of the museum around the player a map has room to draw room by room.</summary>
    public readonly struct PlanDepth
    {
        /// <exception cref="ArgumentOutOfRangeException">A map draws no, one or two rows of rooms ahead.</exception>
        public PlanDepth(bool showsTheRoomBefore, int rowsAhead)
        {
            if (rowsAhead < 0 || rowsAhead > FloorPlan.MostRowsAhead)
            {
                throw new ArgumentOutOfRangeException(nameof(rowsAhead), rowsAhead, $"A map draws 0 to {FloorPlan.MostRowsAhead} rows of rooms ahead.");
            }

            ShowsTheRoomBefore = showsTheRoomBefore;
            RowsAhead = rowsAhead;
        }

        /// <summary>True when the room the player left last is drawn with its name and paintings.</summary>
        public bool ShowsTheRoomBefore { get; }

        /// <summary>How many rows of two rooms are drawn past the next room.</summary>
        public int RowsAhead { get; }
    }

    /// <summary>
    /// The museum's floor plan as the map draws it for a player, from the entrance at the foot of the sheet upwards:
    /// the rooms left behind as one stretch, the room left last, the room the player stands in, the next room, a few
    /// rooms ahead two to a row, and the rest of the museum as one stretch at the top. The design canvas (MapTR)
    /// draws a player in room 1; this is the same plan for a player in any room: every room of the museum is on
    /// it, by itself or in a stretch. No Unity dependency.
    /// </summary>
    public sealed class FloorPlan
    {
        public const int RoomsInARow = 2;
        public const int MostRowsAhead = 2;

        private FloorPlan(RoomNumber here, PlanDepth depth)
        {
            Here = here;

            int roomBefore = here.Value - 1;
            Before = depth.ShowsTheRoomBefore && roomBefore >= 1 ? new RoomNumber(roomBefore) : (RoomNumber?)null;
            int lastBehind = Before == null ? roomBefore : roomBefore - 1;
            Behind = lastBehind >= 1 ? new RoomStretch(new RoomNumber(1), new RoomNumber(lastBehind)) : (RoomStretch?)null;

            int next = here.Value + 1;
            Next = next <= MuseumPlan.RoomCount ? new RoomNumber(next) : (RoomNumber?)null;

            var ahead = new List<RoomNumber>();
            int lastAhead = Math.Min(next + depth.RowsAhead * RoomsInARow, MuseumPlan.RoomCount);
            for (int room = next + 1; room <= lastAhead; room++)
            {
                ahead.Add(new RoomNumber(room));
            }

            Ahead = ahead;
            int firstBeyond = Math.Max(next, lastAhead) + 1;
            Beyond = firstBeyond <= MuseumPlan.RoomCount
                ? new RoomStretch(new RoomNumber(firstBeyond), new RoomNumber(MuseumPlan.RoomCount))
                : (RoomStretch?)null;
        }

        /// <summary>The room the player stands in.</summary>
        public RoomNumber Here { get; }

        /// <summary>The room before it, drawn in full; nothing in the first room, or on a map too short for it.</summary>
        public RoomNumber? Before { get; }

        /// <summary>The rooms before those, as one stretch; nothing when there are none.</summary>
        public RoomStretch? Behind { get; }

        /// <summary>The room after the player's; nothing in the last room.</summary>
        public RoomNumber? Next { get; }

        /// <summary>The rooms after the next one that are drawn by themselves, in order: none, or up to two rows of two.</summary>
        public IReadOnlyList<RoomNumber> Ahead { get; }

        /// <summary>The rest of the museum, as one stretch; nothing when every room is drawn already.</summary>
        public RoomStretch? Beyond { get; }

        /// <param name="here">The room the player stands in (<see cref="MuseumStanding.Room"/>).</param>
        public static FloorPlan Of(RoomNumber here, PlanDepth depth) => new FloorPlan(here, depth);

        /// <summary>How many rows the rooms ahead take, two to a row.</summary>
        public int RowsAhead => (Ahead.Count + RoomsInARow - 1) / RoomsInARow;

        /// <summary>How far a room is for a player.</summary>
        public static RoomAccess AccessOf(RoomNumber room, MuseumProgress progress, MuseumCatalogue catalogue) =>
            catalogue.Room(room).Artworks.Count == 0 ? RoomAccess.ToCome
            : progress.IsRestored(room) ? RoomAccess.Restored
            : progress.IsOpen(room) ? RoomAccess.Open
            : RoomAccess.Locked;

        /// <summary>True for a room the player cannot enter: one their levels have not opened, or one that is not there yet.</summary>
        public static bool IsClosed(RoomAccess access) => access == RoomAccess.Locked || access == RoomAccess.ToCome;

        /// <summary>How far a stretch of rooms is: restored when every room of it is, and otherwise as far as its last room, since rooms open in order.</summary>
        public static RoomAccess AccessOf(RoomStretch stretch, MuseumProgress progress, MuseumCatalogue catalogue)
        {
            for (int room = stretch.First.Value; room <= stretch.Last.Value; room++)
            {
                if (AccessOf(new RoomNumber(room), progress, catalogue) != RoomAccess.Restored)
                {
                    return AccessOf(stretch.Last, progress, catalogue);
                }
            }

            return RoomAccess.Restored;
        }
    }
}
