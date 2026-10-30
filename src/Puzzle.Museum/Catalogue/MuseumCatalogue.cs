using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Catalogue
{
    /// <summary>One room of the museum: an art movement, and the works chosen for it so far.</summary>
    public sealed class MuseumRoom
    {
        public MuseumRoom(RoomNumber number, string movement, string shortName, bool isNameProvisional, IReadOnlyList<Artwork> artworks)
        {
            Number = number;
            Movement = movement;
            ShortName = shortName;
            IsNameProvisional = isNameProvisional;
            Artworks = artworks;
        }

        public RoomNumber Number { get; }

        /// <summary>The art movement the room is named after, in Turkish.</summary>
        public string Movement { get; }

        /// <summary>The era's short name in Turkish, as the world map's strip of eras writes it under the room's number ("Edo ve Meiji").</summary>
        public string ShortName { get; }

        /// <summary>
        /// The years the room's paintings span, "1867–1906": from the earliest year any of them names to the latest,
        /// a year being any four figures of its date ("y. 1890–91" names 1890). Nothing for a room without a dated painting.
        /// </summary>
        public (int First, int Last)? Years
        {
            get
            {
                int first = int.MaxValue;
                int last = int.MinValue;
                foreach (Artwork artwork in Artworks)
                {
                    foreach (int year in YearsNamedIn(artwork.Caption.Year))
                    {
                        first = Math.Min(first, year);
                        last = Math.Max(last, year);
                    }
                }

                return first <= last ? (first, last) : ((int, int)?)null;
            }
        }

        /// <summary>True while the movement is only pencilled in: the room's place in the museum is planned, its theme is not decided.</summary>
        public bool IsNameProvisional { get; }

        /// <summary>
        /// The works chosen for the room's paintings, in the order they are restored; the last of five is the room's finale.
        /// Fewer than five, or none, while the room is still being put together.
        /// </summary>
        public IReadOnlyList<Artwork> Artworks { get; }

        /// <summary>Every run of exactly four figures in a date: "1785–87" names 1785, "y. 1720–37" names 1720.</summary>
        private static IEnumerable<int> YearsNamedIn(string date)
        {
            int run = 0;
            int value = 0;
            for (int at = 0; at <= date.Length; at++)
            {
                bool isFigure = at < date.Length && date[at] >= '0' && date[at] <= '9';
                if (isFigure)
                {
                    run++;
                    value = value * 10 + (date[at] - '0');
                    continue;
                }

                if (run == 4)
                {
                    yield return value;
                }

                run = 0;
                value = 0;
            }
        }
    }

    /// <summary>
    /// What hangs where: the museum's twenty rooms and the paintings chosen for them. The rooms and the places of
    /// their paintings are fixed (<see cref="MuseumPlan"/>); the works themselves are content and come from a data
    /// file (museum/catalogue.json), so a room can be filled without touching code.
    /// </summary>
    public sealed class MuseumCatalogue
    {
        private readonly IReadOnlyList<MuseumRoom> _rooms;

        /// <param name="rooms">Every room of the museum, in order.</param>
        /// <exception cref="ArgumentException">The rooms are not the museum's twenty, in order.</exception>
        public MuseumCatalogue(IReadOnlyList<MuseumRoom> rooms)
        {
            if (rooms.Count != MuseumPlan.RoomCount)
            {
                throw new ArgumentException($"The museum has {MuseumPlan.RoomCount} rooms, not {rooms.Count}.", nameof(rooms));
            }

            for (int index = 0; index < rooms.Count; index++)
            {
                if (rooms[index].Number.Value != index + 1)
                {
                    throw new ArgumentException($"Room {rooms[index].Number} stands where room {index + 1} belongs.", nameof(rooms));
                }
            }

            _rooms = rooms;
        }

        public IReadOnlyList<MuseumRoom> Rooms => _rooms;

        public MuseumRoom Room(RoomNumber number) => _rooms[number.Value - 1];

        /// <summary>The work that hangs at a place of the museum; nothing while none is chosen for that place.</summary>
        public Artwork? ArtworkAt(PaintingNumber number)
        {
            IReadOnlyList<Artwork> chosen = Room(number.Room).Artworks;
            return number.PlaceInRoom <= chosen.Count ? chosen[number.PlaceInRoom - 1] : null;
        }
    }
}
