#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Meta;
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
    /// What a press on a page of the museum, or on a card that hangs over it, asks for. A closed set (private
    /// constructor): the player goes to another page, asks for the level they stand on, chooses its start boosters
    /// and goes into it, buys a booster, opens a painting's jigsaw, opens or closes a card that hangs over the page,
    /// or does something that stays on the page that is shown.
    /// </summary>
    public abstract class MuseumPress
    {
        private MuseumPress()
        {
        }

        /// <summary>The play button: the card before the level the player stands on.</summary>
        public sealed class Play : MuseumPress
        {
        }

        /// <summary>A start booster's disc on the card before a level: it is chosen or put back, or, when none is held, offered.</summary>
        public sealed class ChooseStartBooster : MuseumPress
        {
            public ChooseStartBooster(BoosterKind booster)
            {
                Booster = booster;
            }

            public BoosterKind Booster { get; }
        }

        /// <summary>The green button of the card before a level: into the level, with the start boosters chosen on the card.</summary>
        public sealed class StartTheLevel : MuseumPress
        {
        }

        /// <summary>The green button of the card that offers a booster the player holds none of: one of it, for its price.</summary>
        public sealed class BuyABooster : MuseumPress
        {
            public BuyABooster(BoosterKind booster)
            {
                Booster = booster;
            }

            public BoosterKind Booster { get; }
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
    /// What a press in the museum asks of the player's wallet, which the museum shows and does not hold: whoever owns
    /// the museum's stage applies it by the economy's rules, keeps the wallet after it, and tells the stage of it. A
    /// closed set (private constructor).
    /// </summary>
    public abstract class MuseumRequest
    {
        private MuseumRequest()
        {
        }

        /// <summary>One booster of a kind, for its single price.</summary>
        public sealed class BuyABooster : MuseumRequest
        {
            public BuyABooster(BoosterKind booster)
            {
                Booster = booster;
            }

            public BoosterKind Booster { get; }

            public override string ToString() => $"buy one {Booster.Name()}";
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

        /// <summary>To the board of the level the player stands on, through its card, with the start boosters chosen there.</summary>
        public sealed class ToPlay : MuseumExit
        {
            public ToPlay(StartChoice startBoosters)
            {
                StartBoosters = startBoosters ?? throw new ArgumentNullException(nameof(startBoosters));
            }

            /// <summary>The start boosters the player takes into the attempt; they leave the shelf with its first move.</summary>
            public StartChoice StartBoosters { get; }
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
    /// Where in the museum a visit begins: a page; on the collection's page the room it is opened at; in the hall,
    /// the room that has just been restored, if the visit begins with that; or the hall with the card before the
    /// level hanging over it, where a level that is to be tried again leads.
    /// </summary>
    public readonly struct MuseumDoor
    {
        public MuseumDoor(Destination page, RoomNumber? collectionRoom = null)
            : this(page, collectionRoom, restored: null, hangsTheStartCard: false)
        {
        }

        private MuseumDoor(Destination page, RoomNumber? collectionRoom, RoomNumber? restored, bool hangsTheStartCard)
        {
            Page = page;
            CollectionRoom = collectionRoom;
            Restored = restored;
            HangsTheStartCard = hangsTheStartCard;
        }

        /// <summary>The hall, where the game opens and where a won level leads back to.</summary>
        public static MuseumDoor Home => new MuseumDoor(Destination.Home);

        /// <summary>
        /// The hall with the card of the level the player stands on coming down over it, as after its play button:
        /// where a level that is to be tried again leads (a lost card's "Tekrar dene"). A player without a life is
        /// shown the lives card instead, as the play button shows it them.
        /// </summary>
        public static MuseumDoor HomeWithTheStartCard => new MuseumDoor(Destination.Home, collectionRoom: null, restored: null, hangsTheStartCard: true);

        /// <summary>The hall of a room whose fifth painting has just become whole: it is shown restored before the hall moves on.</summary>
        public static MuseumDoor AtTheRestored(RoomNumber room) => new MuseumDoor(Destination.Home, collectionRoom: null, restored: room, hangsTheStartCard: false);

        /// <summary>True when the visit begins with the card before the level hanging over the hall.</summary>
        public bool HangsTheStartCard { get; }

        /// <summary>The room the hall shows restored as the visit begins; nothing for the hall as the player's progress has it.</summary>
        public RoomNumber? Restored { get; }

        public Destination Page { get; }

        /// <summary>The room the collection shows first; nothing for the room the player stands in.</summary>
        public RoomNumber? CollectionRoom { get; }
    }
}
