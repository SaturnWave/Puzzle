#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Collections;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Client.Play;
using Puzzle.Client.Shops;
using Puzzle.Client.Stage;
using Puzzle.Meta;
using Puzzle.Museum;
using UnityEngine;

namespace Puzzle.Client.Screens
{
    /// <summary>A page of the museum: everything of one destination that is seen and touched, under one root.</summary>
    public interface IMuseumPage : IDisposable
    {
        /// <summary>What the stage moves to turn the page: everything of the page hangs under it.</summary>
        Transform Root { get; }

        /// <summary>Lays the page out for a screen; call again whenever the screen changes.</summary>
        void Fit(ScreenShape screen);

        /// <summary>Tells a set of pressable controls about the page's own, each with what pressing it asks for.</summary>
        void AddPressables(PressSet<MuseumPress> presses);

        /// <summary>
        /// The player asked for the way back. A page that has something of its own to go back from (a painting that
        /// hangs forward) does that.
        /// </summary>
        /// <returns>False when the page had nothing to go back from: the way back then leads to the hall.</returns>
        bool TryGoBack();
    }

    /// <summary>A page on which something moves by itself while it is looked at: the stage tells it the time.</summary>
    public interface IMovingPage
    {
        /// <param name="secondsOnThePage">How long the page has been the one that is shown.</param>
        void Move(float secondsOnThePage);
    }

    /// <summary>A page that shows something of the shop's view (the hall's gift frame, the shop itself): the stage tells it at every step.</summary>
    public interface IShopPage
    {
        void Tell(ShopView shop);
    }

    /// <summary>A card that shows something of the phone (the settings card, the privacy card): the stage tells it whenever that changes.</summary>
    public interface IDeviceCard
    {
        void Tell(DeviceView device);
    }

    /// <summary>
    /// A card that hangs over a museum page, in front of the room's dark: the lives card, the card before a level, the
    /// card that offers a booster, the card that tells what a request of the shop or of the gift came to, the settings
    /// card and the cards it opens. It shows the wallet as the stage is told of it at every step.
    /// </summary>
    public interface IMuseumCard : IDisposable
    {
        /// <summary>The card itself: where it hangs, how far it has come down, and what a finger on it presses.</summary>
        HangingCard<MuseumPress> Card { get; }

        /// <summary>Shows the wallet as it is at this step.</summary>
        void Tell(WalletView wallet);
    }

    /// <summary>
    /// The museum as the player walks through it between two levels: one page at a time (the shop, the hall, the map,
    /// the collection), over them the row of the player's coins and lives, and at their foot the navigation bar that
    /// leads from one page to the other. A page is turned like a page: the new one comes in from the side its
    /// destination stands on the bar while the old one leaves to the other side, under the row and the bar, which
    /// stay. A card may hang over the page, the row and the bar, in front of the room's dark (the lives card, the
    /// card before a level), and a second one in front of that card behind a dark of its own (the card that offers a
    /// booster, or the lives card for a player who asks for the level without a life); only the card in front is
    /// pressed, and the way back closes it. Like the board's stage the museum has a clock that only
    /// <see cref="Advance"/> moves, and everything of it is drawn by its one camera, so the Editor pictures any page
    /// at any moment with no game running.
    /// </summary>
    /// <remarks>
    /// The stage shows the museum as the player's progress has it when the stage is opened; it does not change that
    /// progress. The player's wallet is what its owner tells it (<see cref="Tell"/>), at every step: the wallet as
    /// the rules have it at that instant, so the lives come back and the waits count down as the clock runs, and
    /// coins that arrive or go count on the row. What a press asks of the wallet (a booster or a set bought, a pack of
    /// coins, the coins of an ad, the day's gift) the stage passes on to its owner (<see cref="TakeWhatWasAskedFor"/>),
    /// who applies it by the rules, tells the wallet after it, and tells the stage what the rules said
    /// (<see cref="Hear"/>): a card then says what came of it, a refusal and why included. The shop's ad for coins is
    /// the test build's stand-in card, shown here; its coins are asked for once it is over.
    /// <para>
    /// The gear of the wallet row brings the settings card down, whose rows open the privacy card and the support card
    /// in front of it. What those ask of the phone (a setting turned, the attempt records deleted after a question)
    /// the stage passes on to its owner too (<see cref="TakeWhatWasAskedOfThePhone"/>), who keeps it and tells the
    /// stage the phone as it is after it (<see cref="Tell(DeviceView)"/>).
    /// </para>
    /// <para>
    /// The card before a level is the one door into it. The play button opens it, or, for a player without a life,
    /// the lives card; on it the player chooses the start boosters they hold, and its green button leads into the
    /// level with them, unless the player has no life, whom the lives card tells why not. Once the card has gone up
    /// and the room is dark, <see cref="PlayerLeftFor"/> says what for (the level with its start boosters, or a
    /// painting's jigsaw), and whoever owns the stage opens it, out of the same dark.
    /// </para>
    /// <para>
    /// To add a page: add its <see cref="Destination"/>, build it in <see cref="BuildPage"/>, and give it a render
    /// state in the Editor's ScreenShots command. A page needs no clock of its own while nothing on it moves; one
    /// on which something does is given the stage's timeline when it is built.
    /// </para>
    /// </remarks>
    public sealed class MuseumStage : IStage
    {
        /// <summary>A card in front of another stands this far over the foot of the one behind, in that card's points.</summary>
        private const float InFrontOverTheFoot = 16f;

        /// <summary>How large the mark of a card that tells what a request came to is drawn.</summary>
        private const float NoticeMarkSize = 100f;

        private readonly GameObject _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly MuseumVisit _visit;
        private readonly LevelBrief _level;
        private readonly NavigationBar _bar;
        private readonly WalletRow _wallet;
        private readonly Veil _veil;

        /// <summary>The dark that falls over the first card when a second hangs in front of it.</summary>
        private readonly Veil _secondVeil;

        /// <summary>The shop and the gift as the rules had them at the last step, for the pages that show them.</summary>
        private ShopView _shop;

        /// <summary>The phone as the stage was last told of it, for the settings card and the cards it opens.</summary>
        private DeviceView _device;

        /// <summary>The stand-in ad for coins while it hangs over the shop; nothing otherwise.</summary>
        private AdInTheMuseum? _ad;

        /// <summary>Where the player goes once the cards that hang have gone up (a card's way to the shop); nothing otherwise.</summary>
        private Destination? _turnsToOnceTheCardsAreUp;
        private readonly Timeline _timeline = new Timeline();
        private readonly PressSet<MuseumPress> _presses = new PressSet<MuseumPress>();

        /// <summary>The cards that hang over the page, coming down or going up, from the one at the back to the one in front: none, one or two.</summary>
        private readonly List<IMuseumCard> _cards = new List<IMuseumCard>();

        /// <summary>What presses asked of the wallet that the stage's owner has not taken yet, in the order they were made.</summary>
        private readonly List<WalletRequest> _asked = new List<WalletRequest>();

        /// <summary>What presses asked of the phone that the stage's owner has not taken yet, in the order they were made.</summary>
        private readonly List<DeviceRequest> _askedOfThePhone = new List<DeviceRequest>();

        /// <summary>The page that is shown, or that is coming in while a page is turned.</summary>
        private IMuseumPage _page;

        /// <summary>The page that is leaving while a page is turned; nothing otherwise.</summary>
        private IMuseumPage? _leaving;

        /// <summary>The screen the stage was last fitted to; nothing before the first <see cref="Fit"/>.</summary>
        private ScreenShape? _screen;

        /// <summary>True from the press that leads out of the museum on: the room is going dark, or is dark.</summary>
        private bool _isLeaving;

        /// <summary>How long the page that is shown has been the one shown.</summary>
        private float _secondsOnThePage;

        private MuseumStage(
            MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet, ShopView shop, DeviceView device, LevelBrief level)
        {
            _visit = visit;
            _paintings = paintings;
            _art = art;
            _level = level;
            _shop = shop;
            _device = device;
            Showing = door.Page;
            _root = new GameObject("Museum");
            Camera = StageCamera.Add(_root.transform);
            _page = door.Restored is RoomNumber restored
                ? HomePage.Create(_root.transform, art, paintings, visit, restored)
                : BuildPage(door.Page, door.CollectionRoom);
            _bar = NavigationBar.Create(_root.transform, art, door.Page);
            _wallet = WalletRow.Create(_root.transform, art, wallet);
            _veil = Veil.Create(_root.transform, art);
            _secondVeil = Veil.Create(_root.transform, art, DrawOrder.SecondVeil);
            (_page as IShopPage)?.Tell(shop);
            ListenToThePage();
        }

        public Camera Camera { get; }

        /// <summary>The destination whose page is shown, or is coming in.</summary>
        public Destination Showing { get; private set; }

        /// <summary>True when no page is turning, no card is on its way, no ad is being shown, and the room is neither lighting up nor going dark.</summary>
        public bool IsSettled => _timeline.IsDone && _ad == null && _turnsToOnceTheCardsAreUp == null;

        /// <summary>
        /// What the player has left the museum for, once they pressed what leads there and the room has gone dark:
        /// this stage is then over, and the room is dark for the board or the jigsaw to come out of. Nothing before.
        /// </summary>
        public MuseumExit? PlayerLeftFor { get; private set; }

        /// <summary>True once the player has left for the level they stand on.</summary>
        public bool PlayerWentToPlay => PlayerLeftFor is MuseumExit.ToPlay;

        /// <summary>True while the lives card hangs over the page or over the card before a level, or is on its way down or up.</summary>
        public bool ShowsTheLivesCard => _cards.Exists(card => card is LivesCard);

        /// <summary>True while the card before a level hangs over the page, or is on its way.</summary>
        public bool ShowsTheStartCard => StartCard != null;

        /// <summary>The booster the card in front offers; nothing while no such card hangs.</summary>
        public BoosterKind? OffersABooster => (TopCard as GetMoreCard)?.Booster;

        /// <summary>The title of the card in front that tells what a request came to; nothing while no such card hangs.</summary>
        public string? NoticeShown => (TopCard as NoticeCard)?.Title;

        /// <summary>True while the stand-in ad for coins hangs over the shop or is on its way.</summary>
        public bool IsShowingTheAd => _ad != null;

        /// <summary>The start boosters chosen on the card before a level; none while no such card hangs.</summary>
        public StartChoice StartChoice => StartCard?.Choice ?? StartChoice.None;

        /// <summary>The wallet as the stage was last told of it.</summary>
        public WalletView Wallet => _wallet.Wallet;

        /// <summary>The coins the wallet row shows now: coins that came in count up to <see cref="Wallet"/>'s amount.</summary>
        public Coins CoinsShown => _wallet.CoinsShown;

        /// <summary>Where the player is in the museum: the page that is shown, and on the collection's page the room.</summary>
        public MuseumDoor Door => new MuseumDoor(Showing, (_page as CollectionPage)?.Room);

        /// <summary>
        /// The pixel of the screen in the middle of the play button, counted from the screen's lower left corner:
        /// where a finger presses it. Nothing while the page that is shown has no such button, or is still turning.
        /// </summary>
        public Vector2? PlayButtonPixel => _page is HomePage { ShowsARestoredRoom: false } home ? PixelWhileListening(home.PlayButtonMiddle) : null;

        /// <summary>The pixel in the middle of the green button under a room that has just been restored, which leads on to the hall as it is; nothing otherwise.</summary>
        public Vector2? GoOnButtonPixel => _page is HomePage { ShowsARestoredRoom: true } home ? PixelWhileListening(home.PlayButtonMiddle) : null;

        /// <summary>The pixel in the middle of the hall's button that leads to the jigsaw; nothing while no piece waits there, or another page is shown.</summary>
        public Vector2? JigsawButtonPixel => _page is HomePage home ? PixelWhileListening(home.JigsawButtonMiddle) : null;

        /// <summary>The pixel in the middle of a painting on the collection's wall; nothing while the collection does not show that painting's room.</summary>
        public Vector2? PixelOfExhibit(PaintingNumber painting) =>
            _page is CollectionPage collection && collection.Room == painting.Room ? PixelWhileListening(collection.MiddleOf(painting)) : null;

        /// <summary>The pixels in the middle of the collection's round buttons that turn to the room before and the room after; nothing on another page.</summary>
        public Vector2? PreviousRoomPixel => _page is CollectionPage collection ? PixelWhileListening(collection.PreviousRoomMiddle) : null;

        public Vector2? NextRoomPixel => _page is CollectionPage collection ? PixelWhileListening(collection.NextRoomMiddle) : null;

        /// <summary>The painting of the collection that is looked at by itself; nothing while none is.</summary>
        public PaintingNumber? PaintingViewed => (_page as CollectionPage)?.Viewed;

        /// <summary>The pixel in the middle of the wallet row's heart and its pill, which open the lives card; nothing while the page's controls are not pressed.</summary>
        public Vector2? LivesPixel => PixelWhileListening(_wallet.LivesButtonMiddle);

        /// <summary>The pixels in the middle of the lives card's "Tamam" and of its round button that closes it; nothing while the lives card is not down in front.</summary>
        public Vector2? LivesCardDonePixel => TopCard is LivesCard lives ? PixelOnTheCard(lives.DoneButtonMiddle) : null;

        public Vector2? LivesCardClosePixel => TopCard is LivesCard lives ? PixelOnTheCard(lives.Card.CloseButtonMiddle) : null;

        /// <summary>The pixels in the middle of the start card's green "Oyna", of its round button that closes it, and of a start booster's disc; nothing while that card is not down in front.</summary>
        public Vector2? StartCardPlayPixel => TopCard is LevelStartCard start ? PixelOnTheCard(start.PlayButtonMiddle) : null;

        public Vector2? StartCardClosePixel => TopCard is LevelStartCard start ? PixelOnTheCard(start.Card.CloseButtonMiddle) : null;

        public Vector2? StartCardDiscPixel(BoosterKind booster) => TopCard is LevelStartCard start ? PixelOnTheCard(start.DiscMiddle(booster)) : null;

        /// <summary>The pixels in the middle of the green button of the card that offers a booster, and of its round button that closes it; nothing while that card is not down in front.</summary>
        public Vector2? GetMoreBuyPixel => TopCard is GetMoreCard offer ? PixelOnTheCard(offer.BuyButtonMiddle) : null;

        public Vector2? GetMoreClosePixel => TopCard is GetMoreCard offer ? PixelOnTheCard(offer.Card.CloseButtonMiddle) : null;

        /// <summary>The pixel in the middle of the way to the shop under the dimmed button of the card that offers a booster; nothing while it is not there.</summary>
        public Vector2? GetMoreToTheShopPixel => TopCard is GetMoreCard offer ? PixelOnTheCard(offer.ToTheShopMiddle) : null;

        /// <summary>The pixel in the middle of the "Tamam" of the card that tells what a request came to; nothing while it is not down in front.</summary>
        public Vector2? NoticeDonePixel => TopCard is NoticeCard notice ? PixelOnTheCard(notice.DoneButtonMiddle) : null;

        /// <summary>The pixel in the middle of the wallet row's coins' plus, which leads to the shop; nothing while the page's controls are not pressed.</summary>
        public Vector2? CoinsPlusPixel => PixelWhileListening(_wallet.CoinsPlusMiddle);

        /// <summary>The pixel in the middle of the hall's gift frame; nothing on another page, or in the hall of a room just restored.</summary>
        public Vector2? GiftPixel => _page is HomePage { Gift: GiftFrame gift } ? PixelWhileListening(gift.Middle) : null;

        /// <summary>What the hall's gift frame shows: whether a gift waits, and what its pill says; nothing on another page.</summary>
        public (bool Waits, string Says)? GiftShown => _page is HomePage { Gift: GiftFrame gift } ? (gift.ShowsAGiftWaiting, gift.Says) : ((bool, string)?)null;

        /// <summary>The pixels in the middle of the shop's buttons: a pack's, a set's (lit or dimmed), the free strip's; nothing on another page.</summary>
        public Vector2? ShopPackPixel(int pack) => _page is ShopPage shop ? PixelWhileListening(shop.PackButtonMiddle(pack)) : null;

        public Vector2? ShopSetPixel(int set) => _page is ShopPage shop ? PixelWhileListening(shop.SetButtonMiddle(set)) : null;

        public Vector2? ShopAdPixel => _page is ShopPage shop ? PixelWhileListening(shop.AdButtonMiddle) : null;

        /// <summary>True while the shop's set's button is lit; false while it is dimmed, and on another page.</summary>
        public bool IsShopSetLit(int set) => _page is ShopPage shop && shop.IsSetButtonLit(set);

        /// <summary>True while the shop's free strip offers an ad: the day has one left; false otherwise, and on another page.</summary>
        public bool ShopOffersAnAd => _page is ShopPage { OffersAnAd: true };

        /// <summary>The pixel in the middle of the wallet row's gear, which opens the settings; nothing while the page's controls are not pressed.</summary>
        public Vector2? GearPixel => PixelWhileListening(_wallet.GearMiddle);

        /// <summary>True while the settings card hangs over the page, or is on its way down or up.</summary>
        public bool ShowsTheSettings => _cards.Exists(card => card is SettingsCard);

        /// <summary>The card that hangs in front: the settings card, the privacy card, the support card; nothing while none of them is in front.</summary>
        public SettingsCardKind? SettingsCardInFront => TopCard switch
        {
            SettingsCard _ => SettingsCardKind.Settings,
            PrivacyCard _ => SettingsCardKind.Privacy,
            SupportCard _ => SettingsCardKind.Support,
            _ => null,
        };

        /// <summary>The pixels in the middle of the settings card's rows that open the privacy card and the support card, and of a setting's switch; nothing while that card is not down in front.</summary>
        public Vector2? PrivacyRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.PrivacyRowMiddle) : null;

        public Vector2? SupportRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.SupportRowMiddle) : null;

        public Vector2? SwitchPixel(DeviceSetting setting) =>
            TopCard is SettingsCard settings && settings.Switches.Contains(setting) ? PixelOnTheCard(settings.SwitchMiddle(setting)) : null;

        /// <summary>The settings the settings card's switches show, in its order; none while that card does not hang.</summary>
        public IReadOnlyList<(DeviceSetting Setting, bool IsOn)> SwitchesShown =>
            _cards.Find(card => card is SettingsCard) is SettingsCard settings
                ? settings.Switches.Select(setting => (setting, settings.ShowsOn(setting))).ToArray()
                : Array.Empty<(DeviceSetting, bool)>();

        /// <summary>The pixel in the middle of the round button that closes the card in front, whichever it is; nothing while no card is down in front.</summary>
        public Vector2? CardClosePixel => TopCard is IMuseumCard card ? PixelOnTheCard(card.Card.CloseButtonMiddle) : null;

        /// <summary>What the privacy card's count says, and whether it asks the question; nothing while that card is not in front.</summary>
        public (string Count, bool Asks)? PrivacyShown => TopCard is PrivacyCard privacy ? (privacy.CountSays, privacy.AsksTheQuestion) : ((string, bool)?)null;

        /// <summary>The pixels in the middle of the privacy card's "Kayıtları sil", and of the question's "Vazgeç" and "Sil"; nothing while each is not there to press.</summary>
        public Vector2? DeleteRecordsPixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.DeleteButtonMiddle) : null;

        public Vector2? KeepRecordsPixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.KeepButtonMiddle) : null;

        public Vector2? ConfirmDeletePixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.ConfirmButtonMiddle) : null;

        /// <summary>The pixel in the middle of the support card's "Tamam"; nothing while that card is not down in front.</summary>
        public Vector2? SupportDonePixel => TopCard is SupportCard support ? PixelOnTheCard(support.DoneButtonMiddle) : null;

        /// <summary>The museum listens to the player while nothing of it moves and the player has not left it.</summary>
        private bool ListensToThePlayer => IsSettled && !_isLeaving && PlayerLeftFor == null;

        /// <summary>The card in front, which takes the presses; nothing while no card hangs.</summary>
        private IMuseumCard? TopCard => _cards.Count > 0 ? _cards[_cards.Count - 1] : null;

        private LevelStartCard? StartCard => _cards.Count > 0 ? _cards[0] as LevelStartCard : null;

        /// <summary>Builds the museum for a visit, with one page shown. Call <see cref="Fit"/> before the first picture.</summary>
        /// <param name="door">The page the visit begins on, and whether the card before the level comes down over it.</param>
        /// <param name="wallet">The player's wallet as the row shows it first, with nothing counting; <see cref="Tell"/> it anew at every step.</param>
        /// <param name="shop">The shop and the gift as the rules have them for that wallet; told anew at every step with it.</param>
        /// <param name="device">The phone as the settings card shows it; told anew when the player changes something of it.</param>
        /// <param name="level">What the card before the level the player stands on tells of it and offers for it.</param>
        /// <param name="opening">In the lit room, as the game starts; or out of the dark a result's card or a jigsaw left behind.</param>
        public static MuseumStage Open(
            MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet, ShopView shop, DeviceView device, LevelBrief level,
            StageOpening opening = StageOpening.InTheLitRoom)
        {
            var stage = new MuseumStage(visit, paintings, art, door, wallet, shop, device, level);
            bool isDark = opening == StageOpening.OutOfTheDark;
            if (isDark)
            {
                stage._veil.Darken(1f);
            }

            if (door.HangsTheStartCard)
            {
                // The room stays dark, or goes dark, and the level's card comes down as it does after the play button.
                stage.Hang(stage.TheLevelsCardOrTheLivesCard(), roomIsDark: isDark);
            }
            else if (isDark)
            {
                stage._timeline.Add(0f, Tempo.RoomLightens, progress => stage._veil.Darken(1f - Easing.Out(progress)));
            }

            return stage;
        }

        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            PageLayout layout = PageLayout.Of(screen);
            StageCamera.Show(Camera, layout.View);
            _page.Fit(screen);
            _leaving?.Fit(screen);
            _bar.Place(layout);
            _wallet.Place(layout);
            _veil.Fit(layout.View);
            _secondVeil.Fit(layout.View);
            foreach (IMuseumCard card in _cards)
            {
                Place(card, layout); // the card at the back first: one in front stands on its foot
            }

            _ad?.Card.Place(layout.PlaceOfACard(_ad.Card.Width, _ad.Card.Height), layout.View);
        }

        public void Advance(float deltaSeconds)
        {
            _timeline.Advance(deltaSeconds);
            _secondsOnThePage += deltaSeconds;
            (_page as IMovingPage)?.Move(_secondsOnThePage);
            _wallet.Advance(deltaSeconds);
            ShowTheAd(deltaSeconds);
            if (_turnsToOnceTheCardsAreUp is Destination to && _timeline.IsDone)
            {
                // Done here, after the step: turning a page adds to the timeline, which an act of it may not.
                _turnsToOnceTheCardsAreUp = null;
                if (to == Showing)
                {
                    ListenToThePage();
                }
                else
                {
                    TurnTo(to);
                }
            }
        }

        /// <summary>
        /// Shows the player's wallet as it is at this instant, on the row and on the cards that tell of it, and the
        /// shop and the gift as the rules have them for it on the pages that show them: the owner of the stage tells
        /// both at every step, settled by the rules at the time of the step.
        /// </summary>
        public void Tell(WalletView wallet, ShopView shop)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _wallet.Tell(wallet);
            foreach (IMuseumCard card in _cards)
            {
                card.Tell(wallet);
            }

            (_page as IShopPage)?.Tell(shop);
            (_leaving as IShopPage)?.Tell(shop);
        }

        /// <summary>
        /// What the rules said to a request the stage asked for: a card comes down that says what came of it (a set
        /// bought, the day's gift), or why nothing did (too few coins, nothing sold for money, no ad or gift left
        /// today). A booster bought on a card, and the coins of an ad, need no card: the row counts them.
        /// </summary>
        public void Hear(WalletRequest request, WalletAnswer answer)
        {
            if (NoticeFor(request, answer) is NoticeCard notice)
            {
                Hang(notice, roomIsDark: false);
            }
        }

        /// <summary>What presses asked of the wallet since the owner last took it, in order: the owner applies it by the rules and tells the stage the wallet after it.</summary>
        public IReadOnlyList<WalletRequest> TakeWhatWasAskedFor()
        {
            if (_asked.Count == 0)
            {
                return Array.Empty<WalletRequest>();
            }

            WalletRequest[] taken = _asked.ToArray();
            _asked.Clear();
            return taken;
        }

        /// <summary>What presses asked of the phone since the owner last took it, in order: the owner keeps it and tells the stage the phone after it.</summary>
        public IReadOnlyList<DeviceRequest> TakeWhatWasAskedOfThePhone()
        {
            if (_askedOfThePhone.Count == 0)
            {
                return Array.Empty<DeviceRequest>();
            }

            DeviceRequest[] taken = _askedOfThePhone.ToArray();
            _askedOfThePhone.Clear();
            return taken;
        }

        /// <summary>Shows the phone as it is now on the cards that show it: the settings kept, the records left after a deletion.</summary>
        public void Tell(DeviceView device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            foreach (IMuseumCard card in _cards)
            {
                (card as IDeviceCard)?.Tell(device);
            }
        }

        /// <summary>
        /// The device's way back: the card in front goes up (a question on it with it); a painting that hangs forward
        /// goes back to the wall; from the map and the collection it leads to the hall; in the hall it leads nowhere.
        /// </summary>
        public void GoBack()
        {
            if (!ListensToThePlayer)
            {
                return;
            }

            if (_cards.Count > 0)
            {
                TakeTheCardInFrontUp();
            }
            else if (_page.TryGoBack())
            {
                ListenToThePage();
            }
            else
            {
                TurnTo(Destination.Home);
            }
        }

        /// <summary>The pixel of the screen in the middle of a destination's place on the bar: where a finger presses it.</summary>
        /// <exception cref="InvalidOperationException">The stage was not fitted to a screen yet.</exception>
        public Vector2 PixelOf(Destination destination) => _screen is ScreenShape screen
            ? PixelOf(_bar.MiddleOf(destination), screen)
            : throw new InvalidOperationException("The museum has no screen yet; call Fit first.");

        public void PointerDown(Vector2 pixel)
        {
            if (!ListensToThePlayer || !(_screen is ScreenShape screen))
            {
                return;
            }

            if (TopCard is IMuseumCard card)
            {
                card.Card.FingerDown(PointAt(pixel, screen));
            }
            else
            {
                _presses.FingerDown(PointAt(pixel, screen));
            }
        }

        public void PointerMoved(Vector2 pixel)
        {
            if (!ListensToThePlayer || !(_screen is ScreenShape screen))
            {
                return;
            }

            if (TopCard is IMuseumCard card)
            {
                card.Card.FingerMovedTo(PointAt(pixel, screen));
            }
            else
            {
                _presses.FingerMovedTo(PointAt(pixel, screen));
            }
        }

        public void PointerUp()
        {
            if (!ListensToThePlayer)
            {
                return;
            }

            MuseumPress press;
            bool isPressed = TopCard is IMuseumCard card ? card.Card.FingerUp(out press) : _presses.FingerUp(out press);
            if (isPressed)
            {
                Follow(press);
            }
        }

        public void PointerCancelled()
        {
            _presses.FingerGone();
            TopCard?.Card.FingerGone();
        }

        public void Dispose()
        {
            _page.Dispose();
            _leaving?.Dispose();
            _bar.Dispose();
            _wallet.Dispose();
            foreach (IMuseumCard card in _cards)
            {
                card.Dispose();
            }

            _ad?.Card.Dispose();
            _veil.Dispose();
            _secondVeil.Dispose();
            SceneObjects.Destroy(_root);
        }

        private void Follow(MuseumPress press)
        {
            switch (press)
            {
                case MuseumPress.Play _:
                    // No level opens without a life: the lives card says why, and when the next one comes.
                    Hang(TheLevelsCardOrTheLivesCard(), roomIsDark: false);
                    break;
                case MuseumPress.OpenTheLivesCard _:
                    Hang(LivesCard.Create(_root.transform, _art, _wallet.Wallet), roomIsDark: false);
                    break;
                case MuseumPress.CloseTheCard _ when _cards.Count > 0:
                    TakeTheCardInFrontUp();
                    break;
                case MuseumPress.ChooseStartBooster choose when StartCard is LevelStartCard start:
                    Choose(choose.Booster, start);
                    break;
                case MuseumPress.StartTheLevel _ when StartCard is LevelStartCard start:
                    StartTheLevel(start);
                    break;
                case MuseumPress.BuyABooster buy:
                    _asked.Add(new WalletRequest.BuyABooster(buy.Booster));
                    TakeTheCardInFrontUp();
                    break;
                case MuseumPress.BuyASet buy:
                    _asked.Add(new WalletRequest.BuyASet(buy.Set));
                    break;
                case MuseumPress.BuyACoinPack buy:
                    _asked.Add(new WalletRequest.BuyACoinPack(buy.Pack));
                    break;
                case MuseumPress.WatchAnAdForCoins _ when _shop.CanWatchAnAd:
                    ShowAnAdForCoins();
                    break;
                case MuseumPress.WatchAnAdForCoins _:
                    // The day's last ad was watched in the step before the button went: nothing is shown, nothing is asked.
                    break;
                case MuseumPress.TakeTheGift _:
                    _asked.Add(new WalletRequest.TakeTheDailyGift());
                    break;
                case MuseumPress.OpenTheSettings _:
                    Hang(SettingsCard.Create(_root.transform, _art, _device), roomIsDark: false);
                    break;
                case MuseumPress.OpenThePrivacy _ when TopCard is SettingsCard:
                    Hang(PrivacyCard.Create(_root.transform, _art, _device, (int)DrawOrder.OverACard), roomIsDark: false);
                    break;
                case MuseumPress.OpenTheSupport _ when TopCard is SettingsCard:
                    Hang(SupportCard.Create(_root.transform, _art, _device, (int)DrawOrder.OverACard), roomIsDark: false);
                    break;
                case MuseumPress.FlipASwitch flip when TopCard is SettingsCard settings:
                    // The switch slides at once; the setting is kept by the stage's owner, who tells the phone after it.
                    bool isOn = settings.Flip(flip.Setting);
                    _askedOfThePhone.Add(new DeviceRequest.TurnASetting(flip.Setting, isOn));
                    _timeline.Add(0f, Tempo.SwitchSlides, progress => settings.Slide(flip.Setting, progress));
                    break;
                case MuseumPress.AskToDeleteTheRecords _ when TopCard is PrivacyCard privacy:
                    privacy.Ask();
                    break;
                case MuseumPress.KeepTheRecords _ when TopCard is PrivacyCard privacy:
                    privacy.TakeTheQuestionBack();
                    break;
                case MuseumPress.DeleteTheRecords _ when TopCard is PrivacyCard privacy:
                    // The question goes at once; the count the card shows next is what the owner tells after the deletion.
                    privacy.TakeTheQuestionBack();
                    _askedOfThePhone.Add(new DeviceRequest.DeleteTheRecords());
                    break;
                case MuseumPress.Visit visit when _cards.Count > 0:
                    // A card's way to the shop: every card goes up, and then the page turns.
                    TakeEveryCardUp();
                    _turnsToOnceTheCardsAreUp = visit.To;
                    break;
                case MuseumPress.Restore restore:
                    LeaveFor(new MuseumExit.ToAJigsaw(restore.Painting));
                    break;
                case MuseumPress.Visit visit:
                    TurnTo(visit.To);
                    break;
                case MuseumPress.GoOnFromTheRestoredRoom _:
                    Turn(BuildPage(Destination.Home, collectionRoom: null), Destination.Home, comesFromTheRight: true);
                    break;
                case MuseumPress.OnThePage onThePage:
                    onThePage.Act();
                    ListenToThePage();
                    break;
                default:
                    throw new InvalidOperationException($"A press of the kind {press.GetType().Name} leads nowhere here.");
            }
        }

        /// <summary>The card before the level the player stands on; for a player without a life, the lives card that says why not.</summary>
        private IMuseumCard TheLevelsCardOrTheLivesCard() => _wallet.Wallet.HasALife
            ? LevelStartCard.Create(_root.transform, _art, _level, _wallet.Wallet)
            : LivesCard.Create(_root.transform, _art, _wallet.Wallet);

        /// <summary>A start booster's disc was pressed: the card chooses it or puts it back; one the player holds none of is offered in front of the card.</summary>
        private void Choose(BoosterKind booster, LevelStartCard start)
        {
            switch (start.Press(booster))
            {
                case StartToggle.Changed _:
                    break;
                case StartToggle.NoneOwned _:
                    Hang(GetMoreCard.Create(_root.transform, _art, booster, _level, _wallet.Wallet, (int)DrawOrder.OverACard), roomIsDark: false);
                    break;
                case StartToggle.NoMoreThisAttempt _:
                    // The rules take no more start boosters into one attempt: the disc stays as it is, as the rules have it.
                    break;
                default:
                    throw new InvalidOperationException($"A press on a start booster did something the card does not know: {booster.Name()}.");
            }
        }

        /// <summary>
        /// The card's green button: a player with a life goes into the level with what they chose; one without is
        /// shown the lives card in front of the level's card, and stays.
        /// </summary>
        private void StartTheLevel(LevelStartCard start)
        {
            if (!_wallet.Wallet.HasALife)
            {
                Hang(LivesCard.Create(_root.transform, _art, _wallet.Wallet, (int)DrawOrder.OverACard), roomIsDark: false);
                return;
            }

            // The room is dark already: the card goes up, and the level comes out of the same dark.
            var exit = new MuseumExit.ToPlay(start.Choice);
            _isLeaving = true;
            _presses.Clear();
            start.Card.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, start.Card.GoUp, () => PlayerLeftFor = exit);
        }

        /// <summary>
        /// A card comes down in front of the room's dark, or, where a card hangs already, in front of a second dark
        /// that falls over that card; meanwhile nothing is pressed, and from then on only the card in front is.
        /// </summary>
        /// <param name="roomIsDark">True when the room is dark already: the card comes down without the room going dark first.</param>
        private void Hang(IMuseumCard card, bool roomIsDark)
        {
            Veil dark = _cards.Count == 0 ? _veil : _secondVeil;
            TopCard?.Card.FingerGone();
            _cards.Add(card);
            if (_screen is ScreenShape screen)
            {
                Place(card, PageLayout.Of(screen));
            }

            _presses.Clear();
            if (!roomIsDark)
            {
                _timeline.Add(0f, Tempo.RoomDarkens, progress => dark.Darken(Easing.Out(progress)));
            }

            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.Card.ComeDown);
        }

        /// <summary>The card in front goes up, the dark behind it lifts, and once it is gone the card behind it, or the page, is the player's again.</summary>
        private void TakeTheCardInFrontUp()
        {
            IMuseumCard card = TopCard ?? throw new InvalidOperationException("No card hangs to be taken up.");
            Veil dark = _cards.Count > 1 ? _secondVeil : _veil;
            card.Card.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, card.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => dark.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    card.Dispose();
                    _cards.Remove(card);
                    if (_cards.Count == 0)
                    {
                        ListenToThePage();
                    }
                });
        }

        /// <summary>
        /// Hangs a card where it belongs: the card at the back in the page's place for a card; one in front of it, a
        /// shorter card, at the same size and standing a little over the foot of the one behind, so that the nameplate
        /// of the card behind stays in sight above it and its rim under it.
        /// </summary>
        private void Place(IMuseumCard card, PageLayout layout)
        {
            HangingCard<MuseumPress> behind = _cards[0].Card;
            CardPlace place = ReferenceEquals(card, _cards[0])
                ? layout.PlaceOfACard(card.Card.Width, card.Card.Height)
                : card.Card.PlaceOnAFoot(behind.Foot + behind.Scale * InFrontOverTheFoot, behind.Scale);
            card.Card.Place(place, layout.View);
        }

        /// <summary>Every card that hangs goes up at once and the darks behind them lift; once they are gone, nothing hangs over the page.</summary>
        private void TakeEveryCardUp()
        {
            IMuseumCard[] cards = _cards.ToArray();
            bool hasASecondDark = cards.Length > 1;
            foreach (IMuseumCard card in cards)
            {
                card.Card.FingerGone();
                _timeline.Add(0f, Tempo.CardGoesUp, card.Card.GoUp);
            }

            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress =>
                {
                    float dark = 1f - Easing.Out(progress);
                    _veil.Darken(dark);
                    if (hasASecondDark)
                    {
                        _secondVeil.Darken(dark);
                    }
                },
                () =>
                {
                    foreach (IMuseumCard card in cards)
                    {
                        card.Dispose();
                        _cards.Remove(card);
                    }
                });
        }

        /// <summary>
        /// The card that says what a request came to, or why it came to nothing; nothing where the row says it (a
        /// booster bought on a card, the coins of an ad) or where the card that asked says it already.
        /// </summary>
        private NoticeCard? NoticeFor(WalletRequest request, WalletAnswer answer)
        {
            int raisedBy = _cards.Count > 0 ? (int)DrawOrder.OverACard : 0;
            switch (answer)
            {
                case WalletAnswer.Received received when request is WalletRequest.BuyASet bought:
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.BoughtTitle, new NoticeExhibit.Goods(GoodsItem.Of(received.What)),
                        ShopWords.InYourBag(bought.Set), ShopWords.HowToUse(received.What), raisedBy);
                case WalletAnswer.Received received when request is WalletRequest.TakeTheDailyGift:
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.GiftTitle, new NoticeExhibit.Goods(GoodsItem.Of(received.What)),
                        ShopWords.WhatCame(received.What), ShopWords.NextGiftAfter(_shop.GiftEveryDays), raisedBy);
                case WalletAnswer.Received _:
                    return null;
                case WalletAnswer.Refused { Why: WalletRefusal.CoinsShort coinsShort } when request is WalletRequest.BuyASet set:
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.CoinsShortTitle, new NoticeExhibit.Mark(GalleryArt.CoinPile, NoticeMarkSize),
                        ShopWords.CoinsShortFor(set.Set, coinsShort.Missing), ShopWords.GatherCoins, raisedBy);
                case WalletAnswer.Refused { Why: WalletRefusal.CoinsShort _ }:
                    return null; // the card that offered one booster dims its button and says so itself
                case WalletAnswer.Refused { Why: WalletRefusal.NotOnSale _ }:
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.PacksTitle, new NoticeExhibit.Mark(GalleryArt.CoinChest, NoticeMarkSize),
                        ShopWords.NotOnSale, ShopWords.WhereCoinsComeFrom, raisedBy);
                case WalletAnswer.Refused { Why: WalletRefusal.NoAdLeftToday _ }:
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.NoAdTitle, new NoticeExhibit.Mark(GalleryArt.CoinPile, NoticeMarkSize),
                        ShopWords.NoAdLeft, ShopWords.ComeTomorrow, raisedBy);
                case WalletAnswer.Refused { Why: WalletRefusal.GiftNotDueYet _ }:
                    string when = _shop.Gift is GiftView.Waiting waiting ? ShopWords.NextGiftIn(waiting.Left) : ShopWords.NextGiftAfter(_shop.GiftEveryDays);
                    return NoticeCard.Create(
                        _root.transform, _art, ShopWords.GiftTitle, new NoticeExhibit.Framed(GalleryArt.GiftPicture, NoticeMarkSize),
                        ShopWords.GiftTaken, when, raisedBy);
                default:
                    throw new InvalidOperationException($"The museum has no card for the answer \"{answer}\" to \"{request}\".");
            }
        }

        /// <summary>
        /// The stand-in ad for coins: the room darkens and its card comes down; its seconds run once it hangs, and
        /// the coins are asked for when it is over (<see cref="ShowTheAd"/>).
        /// </summary>
        private void ShowAnAdForCoins()
        {
            var card = new SimulatedAdCard(_root.transform, _art, new AdPrize.CoinsInTheShop(_shop.CoinsForAnAd), raisedBy: 0);
            if (_screen is ScreenShape screen)
            {
                PageLayout layout = PageLayout.Of(screen);
                card.Place(layout.PlaceOfACard(card.Width, card.Height), layout.View);
            }

            var ad = new AdInTheMuseum(card, Ads.Show(_shop.Ads));
            _ad = ad;
            _presses.Clear();
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.ComeDown, () => ad.IsDown = true);
        }

        /// <summary>
        /// The ad's seconds run while its card hangs in its place, and its bar shows them. When it is over its coins
        /// are asked for, its card goes up, the dark lifts, and the shop is the player's again.
        /// </summary>
        private void ShowTheAd(float deltaSeconds)
        {
            if (!(_ad is AdInTheMuseum ad) || !ad.IsDown || ad.HasPaid)
            {
                return;
            }

            ad.Ad.Advance(deltaSeconds);
            ad.Card.Show(ad.Ad);
            if (!ad.Ad.IsOver)
            {
                return;
            }

            ad.HasPaid = true;
            _asked.Add(new WalletRequest.CoinsForAnAd());
            _timeline.Add(0f, Tempo.CardGoesUp, ad.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    ad.Card.Dispose();
                    _ad = null;
                    ListenToThePage();
                });
        }

        /// <summary>The room goes dark; once it is, this stage is over.</summary>
        private void LeaveFor(MuseumExit exit)
        {
            _isLeaving = true;
            _presses.Clear();
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)), () => PlayerLeftFor = exit);
        }

        /// <summary>
        /// Turns to another destination's page: it comes in from the side it stands on the bar, the page that was
        /// shown leaves to the other side and is taken down once it is out of sight.
        /// </summary>
        private void TurnTo(Destination destination)
        {
            if (destination != Showing)
            {
                Turn(
                    BuildPage(destination, collectionRoom: null), destination,
                    comesFromTheRight: Destinations.PlaceOf(destination) > Destinations.PlaceOf(Showing));
            }
        }

        /// <summary>Turns to a page that was built for it; the page that was shown leaves to the other side.</summary>
        private void Turn(IMuseumPage coming, Destination destination, bool comesFromTheRight)
        {
            if (_screen is not ScreenShape screen)
            {
                coming.Dispose();
                return;
            }

            float width = ScreenView.Of(screen).Width;
            float comesFrom = comesFromTheRight ? width : -width;
            IMuseumPage leaving = _page;
            coming.Fit(screen);
            (coming as IShopPage)?.Tell(_shop);
            coming.Root.localPosition = new Vector3(comesFrom, 0f, 0f); // out of sight until the first step of the turn
            _leaving = leaving;
            _page = coming;
            _secondsOnThePage = 0f;
            Showing = destination;
            _bar.Choose(destination);
            _presses.Clear();

            _timeline.Add(
                0f,
                Tempo.PageTurns,
                progress =>
                {
                    float turned = Easing.InOut(progress);
                    coming.Root.localPosition = new Vector3(comesFrom * (1f - turned), 0f, 0f);
                    leaving.Root.localPosition = new Vector3(-comesFrom * turned, 0f, 0f);
                },
                () =>
                {
                    leaving.Dispose();
                    _leaving = null;
                    ListenToThePage();
                });
        }

        /// <summary>
        /// Asks the bar and the page what can be pressed. The page's controls are asked first where they lie over
        /// the bar's: a painting that hangs forward on the collection's page takes every touch.
        /// </summary>
        private void ListenToThePage()
        {
            _presses.Clear();
            _bar.AddPressables(_presses);
            _wallet.AddPressables(_presses);
            _page.AddPressables(_presses);
        }

        /// <param name="collectionRoom">The room the collection's page shows first; nothing for the room the player stands in.</param>
        private IMuseumPage BuildPage(Destination destination, RoomNumber? collectionRoom) => destination switch
        {
            Destination.Shop => ShopPage.Create(_root.transform, _art, _shop),
            Destination.Home => HomePage.Create(_root.transform, _art, _paintings, _visit),
            Destination.Map => MapPage.Create(_root.transform, _art, _paintings, _visit),
            Destination.Collection => CollectionPage.Create(_root.transform, _art, _paintings, _visit, _timeline, collectionRoom),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "A destination without a page."),
        };

        /// <summary>The pixel of a point of the page that is shown; nothing while the stage has no screen, does not listen to the player, or a card hangs over the page.</summary>
        private Vector2? PixelWhileListening(Vector2? point) =>
            point is Vector2 at && ListensToThePlayer && _cards.Count == 0 && _screen is ScreenShape screen ? PixelOf(at, screen) : (Vector2?)null;

        /// <summary>The pixel of a point of the card in front; nothing while a card is on its way, or the stage has no screen.</summary>
        private Vector2? PixelOnTheCard(Vector2? point) =>
            point is Vector2 at && ListensToThePlayer && _screen is ScreenShape screen ? PixelOf(at, screen) : (Vector2?)null;

        private static Vector2 PointAt(Vector2 pixel, ScreenShape screen)
        {
            (float x, float y) = ScreenView.Of(screen).PointAt(pixel.x, pixel.y);
            return new Vector2(x, y);
        }

        private static Vector2 PixelOf(Vector2 point, ScreenShape screen)
        {
            (float pixelX, float pixelY) = ScreenView.Of(screen).PixelOfPoint(point.x, point.y);
            return new Vector2(pixelX, pixelY);
        }

        /// <summary>The stand-in ad for coins over the shop: its card, and the ad as it runs.</summary>
        private sealed class AdInTheMuseum
        {
            public AdInTheMuseum(SimulatedAdCard card, IAdShowing ad)
            {
                Card = card;
                Ad = ad;
            }

            public SimulatedAdCard Card { get; }

            public IAdShowing Ad { get; }

            /// <summary>True once the card hangs in its place: the ad's seconds run from then.</summary>
            public bool IsDown { get; set; }

            /// <summary>True once the ad was over and its coins asked for: the card is on its way up.</summary>
            public bool HasPaid { get; set; }
        }
    }
}
