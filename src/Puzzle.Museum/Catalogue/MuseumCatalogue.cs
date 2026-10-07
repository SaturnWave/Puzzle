using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Catalogue
{
    /// <summary>One room of the museum: an art movement, and the works chosen for it so far.</summary>
    public sealed class MuseumRoom
    {
        public MuseumRoom(RoomNumber number, string movement, bool isNameProvisional, IReadOnlyList<Artwork> artworks)
        {
            Number = number;
            Movement = movement;
            IsNameProvisional = isNameProvisional;
            Artworks = artworks;
        }

        public RoomNumber Number { get; }

        /// <summary>The art movement the room is named after, in Turkish.</summary>
        public string Movement { get; }

        /// <summary>True while the movement is only pencilled in: the room's place in the museum is planned, its theme is not decided.</summary>
        public bool IsNameProvisional { get; }

        /// <summary>
        /// The works chosen for the room's paintings, in the order they are restored; the last of five is the room's finale.
        /// Fewer than five, or none, while the room is still being put together.
        /// </summary>
        public IReadOnlyList<Artwork> Artworks { get; }
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
