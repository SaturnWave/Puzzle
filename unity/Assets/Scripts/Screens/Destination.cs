#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Flow;
using Puzzle.Client.Words;
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
        /// <summary>The shop: what the store sells for money (coin packs, the starter pack), sets of boosters for coins, and coins for an ad.</summary>
        Shop,

        /// <summary>The hall: the painting being restored, and the way into the next level.</summary>
        Home,

        /// <summary>The world map of the paintings' places (MapWorldTR).</summary>
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
    /// a painting's jigsaw, opens or closes a card that hangs over the page, flips a setting, chooses the language or
    /// deletes the attempt records after a question, closes the game after a question, answers the question about the
    /// play statistics, or does something that stays on the page that is shown.
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

        /// <summary>
        /// The button of a product of the store in the shop (a pack of coins, the starter pack): in a build that sells
        /// for money, the card that asks before paying; in a test build, the card that says it sells nothing.
        /// </summary>
        public sealed class BuyFromTheStore : MuseumPress
        {
            public BuyFromTheStore(StoreProduct product)
            {
                Product = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Product { get; }
        }

        /// <summary>The green button of the card that asks before paying: the store's own payment sheet, for the product the card shows.</summary>
        public sealed class PayInTheStore : MuseumPress
        {
        }

        /// <summary>The shop's row that asks the store for the player's purchases back (the App Store's restore).</summary>
        public sealed class RestorePurchases : MuseumPress
        {
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

        /// <summary>A restored painting's card on the world map: to the collection, at the painting's room.</summary>
        public sealed class VisitTheCollection : MuseumPress
        {
            public VisitTheCollection(RoomNumber room)
            {
                Room = room;
            }

            public RoomNumber Room { get; }
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

        /// <summary>The settings card's row "Künye": the credits card, in front of it.</summary>
        public sealed class OpenTheCredits : MuseumPress
        {
        }

        /// <summary>One of the credits card's arrows: its text turns to the next page, or to the one before.</summary>
        public sealed class TurnTheCreditsPage : MuseumPress
        {
            public TurnTheCreditsPage(bool isForward)
            {
                IsForward = isForward;
            }

            /// <summary>True for the next page, false for the one before.</summary>
            public bool IsForward { get; }
        }

        /// <summary>The settings card's row "Dil": the language card, in front of it.</summary>
        public sealed class OpenTheLanguages : MuseumPress
        {
        }

        /// <summary>A language's row on the language card: the game speaks it from now on, on every page and card.</summary>
        public sealed class ChooseALanguage : MuseumPress
        {
            public ChooseALanguage(Language language)
            {
                Language = language;
            }

            public Language Language { get; }
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

        /// <summary>One of the privacy card's arrows: its text turns to the next page, or to the one before.</summary>
        public sealed class TurnThePrivacyPage : MuseumPress
        {
            public TurnThePrivacyPage(bool isForward)
            {
                IsForward = isForward;
            }

            /// <summary>True for the next page, false for the one before.</summary>
            public bool IsForward { get; }
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

        /// <summary>The "Çık" of the card the way back brings in the hall: the game is to close.</summary>
        public sealed class QuitTheGame : MuseumPress
        {
        }

        /// <summary>The statistics question's "İzin ver": the play notes may leave the phone from now on, and the card goes up.</summary>
        public sealed class AllowTheStatistics : MuseumPress
        {
        }

        /// <summary>The statistics question's "İzin verme", or the way back on it: nothing is sent, and the card goes up.</summary>
        public sealed class DeclineTheStatistics : MuseumPress
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
    /// What a press in the museum asks of the store, which the museum shows and does not hold: whoever owns the
    /// museum hands it to the purchase's course (Checkout), which says what it came to at the next step. Money is only
    /// ever paid by <see cref="Pay"/>, the answer of the card that asks before paying: no single press pays. A closed
    /// set (private constructor).
    /// </summary>
    public abstract class StoreRequest
    {
        private StoreRequest()
        {
        }

        /// <summary>A product of the store was pressed: the card that asks before paying, if it can be bought now.</summary>
        public sealed class Ask : StoreRequest
        {
            public Ask(StoreProduct product)
            {
                Product = product ?? throw new ArgumentNullException(nameof(product));
            }

            public StoreProduct Product { get; }

            public override string ToString() => $"ask to buy {Product.Name}";
        }

        /// <summary>The card that asks was answered with the green button: to the store's payment sheet.</summary>
        public sealed class Pay : StoreRequest
        {
            public override string ToString() => "pay";
        }

        /// <summary>The card that asks was closed without paying.</summary>
        public sealed class Decline : StoreRequest
        {
            public override string ToString() => "decline";
        }

        /// <summary>The card that told what a purchase came to was closed.</summary>
        public sealed class Dismiss : StoreRequest
        {
            public override string ToString() => "dismiss";
        }

        /// <summary>The shop's row that asks for the player's purchases back.</summary>
        public sealed class Restore : StoreRequest
        {
            public override string ToString() => "restore";
        }

        /// <summary>The card that told what asking for the purchases back came to was closed.</summary>
        public sealed class DismissTheRestore : StoreRequest
        {
            public override string ToString() => "dismiss the restore";
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

        /// <summary>The language the game speaks, chosen on the language card: kept in the device file and spoken at once.</summary>
        public sealed class ChooseALanguage : DeviceRequest
        {
            public ChooseALanguage(Language language)
            {
                Language = language;
            }

            public Language Language { get; }

            public override string ToString() => $"speak {Languages.CodeOf(Language)}";
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
