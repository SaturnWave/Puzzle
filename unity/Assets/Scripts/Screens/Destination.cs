#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Museum;

namespace Puzzle.Client.Screens
{
    /// <summary>
    /// The pages of the museum the navigation bar leads to. A closed set: to add a page, add it here, give it a
    /// place on the bar in <see cref="Destinations"/>, and build its page in <see cref="MuseumStage"/>; the compiler
    /// then names every switch that has to learn of it.
    /// </summary>
    public enum Destination
    {
        /// <summary>The hall: the painting being restored, and the way into the next level.</summary>
        Home,

        /// <summary>The museum's floor plan.</summary>
        Map,

        /// <summary>The paintings the player has restored and is restoring, room by room.</summary>
        Collection,
    }

    /// <summary>What the navigation bar shows of each destination.</summary>
    public static class Destinations
    {
        /// <summary>The destinations as they stand on the bar, from left to right, as on the design canvas: the collection, the hall, the map.</summary>
        public static readonly IReadOnlyList<Destination> OnTheBar = new[] { Destination.Collection, Destination.Home, Destination.Map };

        /// <summary>A destination's place on the bar, counted from 0 at the left.</summary>
        public static int PlaceOf(Destination destination)
        {
            for (int place = 0; place < OnTheBar.Count; place++)
            {
                if (OnTheBar[place] == destination)
                {
                    return place;
                }
            }

            throw Unknown(destination);
        }

        public static string NameOf(Destination destination) => destination switch
        {
            Destination.Home => MuseumWords.HomeTab,
            Destination.Map => MuseumWords.MapTab,
            Destination.Collection => MuseumWords.CollectionTab,
            _ => throw Unknown(destination),
        };

        public static GalleryArt MarkOf(Destination destination) => destination switch
        {
            Destination.Home => GalleryArt.IconMuseum,
            Destination.Map => GalleryArt.IconMap,
            Destination.Collection => GalleryArt.IconCollection,
            _ => throw Unknown(destination),
        };

        private static ArgumentOutOfRangeException Unknown(Destination destination) =>
            new ArgumentOutOfRangeException(nameof(destination), destination, "A destination the navigation bar does not know.");
    }

    /// <summary>
    /// What a press on a page of the museum asks for. A closed set (private constructor): the player goes to
    /// another page, starts the level they stand on, opens a painting's jigsaw, opens or closes a card that hangs
    /// over the page, or does something that stays on the page that is shown.
    /// </summary>
    public abstract class MuseumPress
    {
        private MuseumPress()
        {
        }

        /// <summary>The play button: into the level.</summary>
        public sealed class Play : MuseumPress
        {
        }

        /// <summary>A place on the navigation bar: to that page.</summary>
        public sealed class Visit : MuseumPress
        {
            public Visit(Destination to)
            {
                To = to;
            }

            public Destination To { get; }
        }

        /// <summary>A painting whose pieces wait: to its jigsaw.</summary>
        public sealed class Restore : MuseumPress
        {
            public Restore(PaintingNumber painting)
            {
                Painting = painting;
            }

            public PaintingNumber Painting { get; }
        }

        /// <summary>The button under a room that has just been restored: on to the hall as the player's progress has it.</summary>
        public sealed class GoOnFromTheRestoredRoom : MuseumPress
        {
        }

        /// <summary>The heart's pill of the wallet row, or the play button of a player without a life: the card that tells of the lives.</summary>
        public sealed class OpenTheLivesCard : MuseumPress
        {
        }

        /// <summary>A card's button that closes it, or its "Tamam": the card goes up and the page is the player's again.</summary>
        public sealed class CloseTheCard : MuseumPress
        {
        }

        /// <summary>
        /// Something the page does by itself (it turns to another room, it brings a painting forward): the page
        /// does it, and the stage then asks the page anew what can be pressed.
        /// </summary>
        public sealed class OnThePage : MuseumPress
        {
            public OnThePage(Action act)
            {
                Act = act;
            }

            public Action Act { get; }
        }
    }

    /// <summary>
    /// What a player leaves the museum for. A closed set (private constructor): a level's board, or a painting's jigsaw.
    /// </summary>
    public abstract class MuseumExit
    {
        private MuseumExit()
        {
        }

        /// <summary>To the board of the level the player stands on.</summary>
        public sealed class ToPlay : MuseumExit
        {
        }

        /// <summary>To the jigsaw of a painting; its way back leads to the page it was opened from.</summary>
        public sealed class ToAJigsaw : MuseumExit
        {
            public ToAJigsaw(PaintingNumber painting)
            {
                Painting = painting;
            }

            public PaintingNumber Painting { get; }
        }
    }

    /// <summary>
    /// Where in the museum a visit begins: a page; on the collection's page the room it is opened at; and in the
    /// hall, the room that has just been restored, if the visit begins with that.
    /// </summary>
    public readonly struct MuseumDoor
    {
        public MuseumDoor(Destination page, RoomNumber? collectionRoom = null)
            : this(page, collectionRoom, restored: null)
        {
        }

        private MuseumDoor(Destination page, RoomNumber? collectionRoom, RoomNumber? restored)
        {
            Page = page;
            CollectionRoom = collectionRoom;
            Restored = restored;
        }

        /// <summary>The hall, where the game opens and where a level's result leads back to.</summary>
        public static MuseumDoor Home => new MuseumDoor(Destination.Home);

        /// <summary>The hall of a room whose fifth painting has just become whole: it is shown restored before the hall moves on.</summary>
        public static MuseumDoor AtTheRestored(RoomNumber room) => new MuseumDoor(Destination.Home, collectionRoom: null, restored: room);

        /// <summary>The room the hall shows restored as the visit begins; nothing for the hall as the player's progress has it.</summary>
        public RoomNumber? Restored { get; }

        public Destination Page { get; }

        /// <summary>The room the collection shows first; nothing for the room the player stands in.</summary>
        public RoomNumber? CollectionRoom { get; }
    }
}
