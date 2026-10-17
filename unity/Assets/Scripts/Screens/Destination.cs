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
        /// <summary>The shop: sets of boosters for coins, coins for an ad, and the coin packs a test build does not sell.</summary>
        Shop,

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
        /// <summary>
        /// The destinations as they stand on the bar, from left to right, as on the design canvas: the shop, the
        /// collection, the hall, the map. The canvas' fifth place, the events' calendar, has nothing behind it.
        /// </summary>
        public static readonly IReadOnlyList<Destination> OnTheBar = new[] { Destination.Shop, Destination.Collection, Destination.Home, Destination.Map };

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
            Destination.Shop => MuseumWords.ShopTab,
            Destination.Home => MuseumWords.HomeTab,
            Destination.Map => MuseumWords.MapTab,
            Destination.Collection => MuseumWords.CollectionTab,
            _ => throw Unknown(destination),
        };

        public static GalleryArt MarkOf(Destination destination) => destination switch
        {
            Destination.Shop => GalleryArt.IconShop,
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
    /// and goes into it, buys a booster or a set of them, asks for coins (a pack, an ad) or for the day's gift, opens
    /// a painting's jigsaw, opens or closes a card that hangs over the page, flips a setting or deletes the attempt
    /// records after a question, or does something that stays on the page that is shown.
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

        /// <summary>
        /// The green button of a set of boosters in the shop: the set, for its price. A button the coins do not cover
        /// is dimmed and is pressed all the same: the rules then say why nothing was bought.
        /// </summary>
        public sealed class BuyASet : MuseumPress
        {
            public BuyASet(BoosterSet set)
            {
                Set = set ?? throw new ArgumentNullException(nameof(set));
            }

            public BoosterSet Set { get; }
        }

        /// <summary>The button of a pack of coins in the shop: the rules say whether this build sells it.</summary>
        public sealed class BuyACoinPack : MuseumPress
        {
            public BuyACoinPack(CoinPack pack)
            {
                Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            }

            public CoinPack Pack { get; }
        }

        /// <summary>The blue button of the shop's free strip: an ad, and coins once it is over.</summary>
        public sealed class WatchAnAdForCoins : MuseumPress
        {
        }

        /// <summary>The hall's gift frame: the daily reward, or when the next one comes.</summary>
        public sealed class TakeTheGift : MuseumPress
        {
        }

        /// <summary>A place on the navigation bar, the coins' plus, or a card's way to the shop: to that page.</summary>
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

        /// <summary>The gear at the wallet row's right end: the settings card.</summary>
        public sealed class OpenTheSettings : MuseumPress
        {
        }

        /// <summary>The settings card's row "Gizlilik ve veri": the privacy card, in front of it.</summary>
        public sealed class OpenThePrivacy : MuseumPress
        {
        }

        /// <summary>The settings card's row "Destek": the support card, in front of it.</summary>
        public sealed class OpenTheSupport : MuseumPress
        {
        }

        /// <summary>A setting's switch on the settings card: it slides to the other state, which is kept at once.</summary>
        public sealed class FlipASwitch : MuseumPress
        {
            public FlipASwitch(DeviceSetting setting)
            {
                Setting = setting;
            }

            public DeviceSetting Setting { get; }
        }

        /// <summary>The privacy card's "Kayıtları sil": the card asks first.</summary>
        public sealed class AskToDeleteTheRecords : MuseumPress
        {
        }

        /// <summary>The question's "Vazgeç": nothing is deleted, and the card is as it was.</summary>
        public sealed class KeepTheRecords : MuseumPress
        {
        }

        /// <summary>The question's "Sil": every attempt record on the phone is deleted.</summary>
        public sealed class DeleteTheRecords : MuseumPress
        {
        }

        /// <summary>The support card's version row, in a test build with the tester mode off: the seventh tap in a row asks whether to turn it on.</summary>
        public sealed class TapTheVersion : MuseumPress
        {
        }

        /// <summary>The tester mode question's "Aç": the mode is turned on, and every card goes up.</summary>
        public sealed class TurnOnTheTesterMode : MuseumPress
        {
        }

        /// <summary>The tester mode question's "Vazgeç": nothing changes, and the support card is as it was.</summary>
        public sealed class LeaveTheTesterModeOff : MuseumPress
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
    /// What a press in the museum or on a board asks of the player's wallet, which the stages show and do not hold:
    /// whoever owns the stage applies it by the economy's rules, keeps the wallet after it, and tells the stage of it.
    /// A closed set (private constructor).
    /// </summary>
    public abstract class WalletRequest
    {
        private WalletRequest()
        {
        }

        /// <summary>One booster of a kind, for its single price.</summary>
        public sealed class BuyABooster : WalletRequest
        {
            public BuyABooster(BoosterKind booster)
            {
                Booster = booster;
            }

            public BoosterKind Booster { get; }

            public override string ToString() => $"buy one {Booster.Name()}";
        }

        /// <summary>A set of boosters of the shop's, for its price.</summary>
        public sealed class BuyASet : WalletRequest
        {
            public BuyASet(BoosterSet set)
            {
                Set = set ?? throw new ArgumentNullException(nameof(set));
            }

            public BoosterSet Set { get; }

            public override string ToString() => $"buy the {Set}";
        }

        /// <summary>A pack of coins for real money.</summary>
        public sealed class BuyACoinPack : WalletRequest
        {
            public BuyACoinPack(CoinPack pack)
            {
                Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            }

            public CoinPack Pack { get; }

            public override string ToString() => $"buy the {Pack}";
        }

        /// <summary>The coins of an ad for coins that was watched to its end.</summary>
        public sealed class CoinsForAnAd : WalletRequest
        {
            public override string ToString() => "coins for an ad";
        }

        /// <summary>The daily reward, if one is due.</summary>
        public sealed class TakeTheDailyGift : WalletRequest
        {
            public override string ToString() => "the daily gift";
        }
    }

    /// <summary>
    /// What a press on the settings card or a card it opens asks of the phone: whoever owns the museum keeps it
    /// (the settings in the device file, the records' deletion in the attempt log) and tells the museum the phone
    /// as it is after it. A closed set (private constructor).
    /// </summary>
    public abstract class DeviceRequest
    {
        private DeviceRequest()
        {
        }

        /// <summary>A setting turned on or off.</summary>
        public sealed class TurnASetting : DeviceRequest
        {
            public TurnASetting(DeviceSetting setting, bool isOn)
            {
                Setting = setting;
                IsOn = isOn;
            }

            public DeviceSetting Setting { get; }

            public bool IsOn { get; }

            public override string ToString() => $"turn {Setting} {(IsOn ? "on" : "off")}";
        }

        /// <summary>Every attempt record on the phone deleted, at the player's wish.</summary>
        public sealed class DeleteTheRecords : DeviceRequest
        {
            public override string ToString() => "delete the attempt records";
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
