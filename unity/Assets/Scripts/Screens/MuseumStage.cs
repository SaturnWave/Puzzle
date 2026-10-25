#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Avatar;
using Puzzle.Client.Canvases;
using Puzzle.Client.Collections;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Client.Play;
using Puzzle.Client.Purchases;
using Puzzle.Client.Shops;
using Puzzle.Client.Stage;
using Puzzle.Client.Words;
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
    /// pressed, and the way back closes it. In the bare hall, from where no way leads further back, the way back
    /// brings down the card that asks whether to close the game (<see cref="QuitCard"/>); its "Çık" the stage passes
    /// on to its owner (<see cref="TakeTheWishToQuit"/>). Like the board's stage the museum has a clock that only
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
    /// The gear of the wallet row brings the settings card down, whose rows open the language card, the privacy card
    /// and the support card in front of it. What those ask of the phone (a setting turned, a language chosen, the
    /// attempt records deleted after a question) the stage passes on to its owner too
    /// (<see cref="TakeWhatWasAskedOfThePhone"/>), who keeps it and tells the stage the phone as it is after it
    /// (<see cref="Tell(DeviceView)"/>); after a language it has the stage speak anew (<see cref="SpeakAnew"/>), and
    /// every page and card is built again in it where it stands. In a test build the support card's version
    /// row asks, after seven taps in a row, whether the tester mode is to be turned on; its "Aç" asks the owner to turn
    /// it on and takes every card up, so the row shows at once the wallet the owner tells after it, without an end.
    /// </para>
    /// <para>
    /// The card before a level is the one door into it. The play button opens it, or, for a player without a life,
    /// the lives card; on it the player chooses the start boosters they hold, and its green button leads into the
    /// level with them, unless the player has no life, whom the lives card tells why not. Once the card has gone up
    /// and the room is dark, <see cref="PlayerLeftFor"/> says what for (the level with its start boosters, or a
    /// painting's jigsaw), and whoever owns the stage opens it, out of the same dark.
    /// </para>
    /// <para>
    /// The first time the card before a level comes down for a level the player tried and did not win (lost, or left
    /// after a move), the tutorial shows the start boosters on it (#7): the card is dimmed but for one booster's disc
    /// and name, and a pill under the card says what it is and what choosing it does; a touch anywhere lights the next,
    /// and after the third the card is the player's. Nothing on the card is pressed meanwhile; the way back takes the
    /// card up and the step comes again on the next card. The step done is reported (<see cref="TakeTheTutorialStepsDone"/>).
    /// A finger held on a start booster's disc for <see cref="Tempo.LongPress"/> chooses nothing: a card comes down in
    /// front that shows what the booster does on a tiny board of its own (<see cref="BoosterCard{TPress}"/>), which a
    /// touch anywhere or the way back takes up.
    /// </para>
    /// <para>
        /// A page is also turned by a swipe (<see cref="PageSwipe"/>): a finger that goes clearly sideways drags the page
    /// that is shown, and its neighbour on the bar comes in beside it; let go, the row settles on the neighbour or
    /// springs back, and the bar marks the page it settled on. Beyond the bar's first and last page the page only
    /// stretches. A swipe on the collection's plaque turns its rooms instead, the same way; one on the world map's
    /// strip of eras scrolls the strip, and runs on with a flick. A touch stays a tap until the finger leaves the
    /// slop (<see cref="TapSlop"/>); after that it presses nothing, whether it swipes or not, on a page or on a card
    /// (<see cref="PressSet{TAction}"/>). No swipe is taken while a card hangs, while a painting of the collection
    /// hangs forward or a painting's card is open on the map, nor from the screen's side edges, where the phone's
    /// own back gesture begins.
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

        /// <summary>The tutorial's dark over the card before a level comes in over this long.</summary>
        private const float LessonComesIn = 0.35f;

        /// <summary>The tutorial's pill hangs this many of the card's points under what rides under the card (its green button), and keeps this far from the screen's sides.</summary>
        private const float LessonPillUnderTheCard = 14f;
        private const float LessonPillFromTheSides = 16f;

        /// <summary>How large the mark of a card that tells what a request came to is drawn.</summary>
        private const float NoticeMarkSize = 100f;

        private readonly GameObject _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly MuseumVisit _visit;
        private readonly LevelBrief _level;
        private NavigationBar _bar;
        private WalletRow _wallet;
        private readonly Veil _veil;

        /// <summary>The dark that falls over the first card when a second hangs in front of it.</summary>
        private readonly Veil _secondVeil;

        /// <summary>The shop and the gift as the rules had them at the last step, for the pages that show them.</summary>
        private ShopView _shop;

        /// <summary>The phone as the stage was last told of it, for the settings card and the cards it opens.</summary>
        private DeviceView _device;

        /// <summary>The stand-in ad for coins while it hangs over the shop; nothing otherwise.</summary>
        private AdInTheMuseum? _ad;

        /// <summary>The room the hall shows restored while it is the one shown so; nothing once the player went on from it.</summary>
        private RoomNumber? _restored;

        /// <summary>Where the player goes once the cards that hang have gone up (a card's way to the shop); nothing otherwise.</summary>
        private Destination? _turnsToOnceTheCardsAreUp;
        private readonly Timeline _timeline = new Timeline();
        private readonly PressSet<MuseumPress> _presses = new PressSet<MuseumPress>();

        /// <summary>What a finger on the page means: a tap, or a swipe that drags the pages or the collection's rooms.</summary>
        private readonly PageSwipe _swipe = new PageSwipe();

        /// <summary>The cards that hang over the page, coming down or going up, from the one at the back to the one in front: none, one or two.</summary>
        private readonly List<IMuseumCard> _cards = new List<IMuseumCard>();

        /// <summary>What presses asked of the wallet that the stage's owner has not taken yet, in the order they were made.</summary>
        private readonly List<WalletRequest> _asked = new List<WalletRequest>();

        /// <summary>What presses asked of the phone that the stage's owner has not taken yet, in the order they were made.</summary>
        private readonly List<DeviceRequest> _askedOfThePhone = new List<DeviceRequest>();

        /// <summary>What presses asked of the store that the stage's owner has not taken yet, in the order they were made.</summary>
        private readonly List<StoreRequest> _askedOfTheStore = new List<StoreRequest>();

        /// <summary>
        /// The card that shows where a purchase for money stands, with what it shows: a state of the purchase's course,
        /// or the end of a restore. Nothing while no such card hangs, or once it is on its way up.
        /// </summary>
        private (NoticeCard Card, object ShowsFor)? _purchaseCard;

        /// <summary>What the player closed the purchase card of (or the cancel the stage passed over), until the course moves on: no card comes down for it again.</summary>
        private object? _closedFor;

        /// <summary>What the stage sounded that its owner has not taken yet: presses, switches, cards and coins.</summary>
        private readonly CuesSounded _sounded = new CuesSounded();

        /// <summary>The page that is shown, or that is coming in while a page is turned.</summary>
        private IMuseumPage _page;

        /// <summary>The page that is leaving while a page is turned or settles after a swipe; nothing otherwise.</summary>
        private IMuseumPage? _leaving;

        /// <summary>What the finger that is down swipes, should it swipe; nothing while it may not swipe at all.</summary>
        private SwipedRow? _swiped;

        /// <summary>The neighbouring page a swipe drags in beside the shown one, with its destination; nothing otherwise.</summary>
        private (IMuseumPage Page, Destination Destination)? _pageBeside;

        /// <summary>The screen the stage was last fitted to; nothing before the first <see cref="Fit"/>.</summary>
        private ScreenShape? _screen;

        /// <summary>True from the press that leads out of the museum on: the room is going dark, or is dark.</summary>
        private bool _isLeaving;

        /// <summary>True from the "Çık" of the hall's question until whoever runs the game has taken the wish (<see cref="TakeTheWishToQuit"/>).</summary>
        private bool _isAskedToQuit;

        /// <summary>How long the page that is shown has been the one shown.</summary>
        private float _secondsOnThePage;

        /// <summary>The museum's own clock, which only <see cref="Advance"/> moves: how long a finger has held a start booster's disc.</summary>
        private float _clock;

        /// <summary>When on the clock the finger came down on a control of the card before a level that it still holds; nothing while it holds none.</summary>
        private float? _heldSince;

        /// <summary>True once a finger came down on the card a long press brought: its lift takes that card up. The lift of the finger whose long press brought it does not.</summary>
        private bool _isATouchOnTheBoosterCard;

        /// <summary>Paints the effects of the card a long press brings, once: the game's own when the session lends them, painted here otherwise.</summary>
        private EffectArt? _effectArt;
        private readonly bool _ownsTheEffectArt;

        /// <summary>True while the start boosters are still to be shown on the card before the level (the tutorial's step); false once shown, or for a player who has seen them.</summary>
        private bool _startBoostersToShow;

        /// <summary>Which of the start boosters the tutorial lights on the card now, by its place on the card's shelf; nothing while none is lit.</summary>
        private int? _startBoosterLit;

        /// <summary>When on the clock the start boosters' step began to show, for its dark to come in.</summary>
        private float _startBoostersSince;

        /// <summary>The steps of the tutorial shown to their end here that the stage's owner has not taken yet.</summary>
        private readonly List<TutorialStep> _stepsDone = new List<TutorialStep>();
        private readonly Spotlight _spotlight;
        private readonly ShelfPill _lessonPill;

        private MuseumStage(
            MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet, ShopView shop, DeviceView device, LevelBrief level,
            bool showsTheStartBoosters, EffectArt? effectArt)
        {
            _startBoostersToShow = showsTheStartBoosters;
            _effectArt = effectArt;
            _ownsTheEffectArt = effectArt == null;
            _visit = visit;
            _paintings = paintings;
            _art = art;
            _level = level;
            _shop = shop;
            _device = device;
            Showing = door.Page;
            _restored = door.Restored;
            _root = new GameObject("Museum");
            Camera = StageCamera.Add(_root.transform);
            _page = door.Restored is RoomNumber restored
                ? HomePage.Create(_root.transform, art, paintings, visit, restored)
                : BuildPage(door.Page, door.CollectionRoom);
            _bar = NavigationBar.Create(_root.transform, art, door.Page);
            _wallet = WalletRow.Create(_root.transform, art, wallet);
            _veil = Veil.Create(_root.transform, art);
            _secondVeil = Veil.Create(_root.transform, art, DrawOrder.SecondVeil);
            _spotlight = Spotlight.CreateOverACard(_root.transform, art);
            _lessonPill = ShelfPill.CreateForLessonsOnACard(_root.transform, art, paintings.Painter);
            (_page as IShopPage)?.Tell(shop);
            ListenToThePage();
        }

        public Camera Camera { get; }

        /// <summary>The destination whose page is shown, or is coming in.</summary>
        public Destination Showing { get; private set; }

        /// <summary>True when no page is turning or being dragged, no card is on its way, no ad is being shown, and the room is neither lighting up nor going dark.</summary>
        public bool IsSettled => IsStill && !_swipe.IsDragging && !IsAPurchaseCardDue;

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

        /// <summary>The start booster the tutorial lights on the card before the level now; nothing while none is lit.</summary>
        public BoosterKind? StartBoosterLit => _startBoosterLit is int lit ? LevelBrief.StartBoosters[lit] : (BoosterKind?)null;

        /// <summary>What the tutorial's pill on the card before the level says now; <see cref="PillWords.None"/> while it says nothing.</summary>
        public PillWords WhatTheLessonSays => _lessonPill.Words;

        /// <summary>The booster the card in front shows after a long press; nothing while no such card is in front.</summary>
        public BoosterKind? BoosterCardShown => (TopCard as BoosterCardOverTheCard)?.Shown.Booster;

        /// <summary>How many times the card in front has shown its booster's effect through; 0 while no such card is in front.</summary>
        public int BoosterCardTimesShown => (TopCard as BoosterCardOverTheCard)?.Shown.TimesShown ?? 0;

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

        /// <summary>The pixel in the middle of the collection's plaque, where a swipe turns the rooms; nothing on another page.</summary>
        public Vector2? PlaquePixel => _page is CollectionPage collection ? PixelWhileListening(collection.PlaqueMiddle) : null;

        /// <summary>The painting of the collection that is looked at by itself; nothing while none is.</summary>
        public PaintingNumber? PaintingViewed => (_page as CollectionPage)?.Viewed;

        /// <summary>The world map's page while it is the one shown; nothing on another page.</summary>
        public MapPage? Map => _page as MapPage;

        /// <summary>The pixel of a point of the world map's page that is shown (an era's medallion, a pin, a button, a button of the card); nothing elsewhere or while the stage does not listen.</summary>
        public Vector2? PixelOnTheMap(Func<MapPage, Vector2?> point) =>
            _page is MapPage map && point(map) is Vector2 at && ListensToThePlayer && !_swipe.IsDragging && _cards.Count == 0 && _screen is ScreenShape screen
                ? PixelOf(at, screen)
                : (Vector2?)null;

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
        public Vector2? NoticeDonePixel => TopCard is NoticeCard { DoneButtonMiddle: Vector2 done } ? PixelOnTheCard(done) : null;

        /// <summary>The pixels in the middle of the green "Satın al" and the blue "Vazgeç" of the card that asks before paying; nothing while it is not down in front.</summary>
        public Vector2? NoticeBuyPixel => TopCard is NoticeCard { BuyButtonMiddle: Vector2 buy } ? PixelOnTheCard(buy) : null;

        public Vector2? NoticeDeclinePixel => TopCard is NoticeCard { DeclineButtonMiddle: Vector2 decline } ? PixelOnTheCard(decline) : null;

        /// <summary>The pixel in the middle of the round button of the notice card in front; nothing while none is, or while it has none.</summary>
        public Vector2? NoticeClosePixel => TopCard is NoticeCard { CanBeClosed: true } notice ? PixelOnTheCard(notice.Card.CloseButtonMiddle) : null;

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

        /// <summary>The pixels in the middle of the starter pack's button and of the row that asks for the purchases back; nothing while the shop shows neither.</summary>
        public Vector2? ShopStarterPixel => _page is ShopPage shop ? PixelWhileListening(shop.StarterButtonMiddle) : null;

        public Vector2? ShopRestorePixel => _page is ShopPage shop ? PixelWhileListening(shop.RestoreRowMiddle) : null;

        /// <summary>True while a pack's button in the shop can be pressed to buy it: the store gave its price; false otherwise, and on another page.</summary>
        public bool IsShopPackForSale(int pack) => _page is ShopPage shop && shop.IsPackForSale(pack);

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
            LanguageCard _ => SettingsCardKind.Language,
            CreditsCard _ => SettingsCardKind.Credits,
            _ => null,
        };

        /// <summary>The pixel in the middle of the settings card's row that opens the language card; nothing while that card is not down in front.</summary>
        public Vector2? LanguageRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.LanguageRowMiddle) : null;

        /// <summary>The pixel in the middle of a language's row on the language card; nothing while that card is not down in front, or does not offer the language.</summary>
        public Vector2? LanguagePixel(Language language) => TopCard is LanguageCard languages ? PixelOnTheCard(languages.RowMiddle(language)) : null;

        /// <summary>The pixel in the middle of the language card's "Tamam"; nothing while that card is not down in front.</summary>
        public Vector2? LanguageDonePixel => TopCard is LanguageCard languages ? PixelOnTheCard(languages.DoneButtonMiddle) : null;

        /// <summary>The language the language card ticks; nothing while that card does not hang.</summary>
        public Language? LanguageTicked => (_cards.Find(card => card is LanguageCard) as LanguageCard)?.Ticked;

        /// <summary>What the bar names the page that is shown, as it is drawn now: what a language just chosen shows at once.</summary>
        public string BarSays => _bar.Says;

        /// <summary>The pixels in the middle of the settings card's rows that open the privacy card and the support card, and of a setting's switch; nothing while that card is not down in front.</summary>
        public Vector2? PrivacyRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.PrivacyRowMiddle) : null;

        public Vector2? SupportRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.SupportRowMiddle) : null;

        public Vector2? CreditsRowPixel => TopCard is SettingsCard settings ? PixelOnTheCard(settings.CreditsRowMiddle) : null;

        public Vector2? SwitchPixel(DeviceSetting setting) =>
            TopCard is SettingsCard settings && settings.Switches.Contains(setting) ? PixelOnTheCard(settings.SwitchMiddle(setting)) : null;

        /// <summary>The settings the settings card's switches show, in its order; none while that card does not hang.</summary>
        public IReadOnlyList<(DeviceSetting Setting, bool IsOn)> SwitchesShown =>
            _cards.Find(card => card is SettingsCard) is SettingsCard settings
                ? settings.Switches.Select(setting => (setting, settings.ShowsOn(setting))).ToArray()
                : Array.Empty<(DeviceSetting, bool)>();

        /// <summary>What the settings card's privacy row says under its name, while the card hangs; nothing otherwise.</summary>
        public string? PrivacyLineShown => (_cards.Find(card => card is SettingsCard) as SettingsCard)?.PrivacyLineSays;

        /// <summary>The pixel in the middle of the round button that closes the card in front, whichever it is; nothing while no card is down in front.</summary>
        public Vector2? CardClosePixel => TopCard is IMuseumCard card ? PixelOnTheCard(card.Card.CloseButtonMiddle) : null;

        /// <summary>What the privacy card's count says, and whether it asks the question; nothing while that card is not in front.</summary>
        public (string Count, bool Asks)? PrivacyShown => TopCard is PrivacyCard privacy ? (privacy.CountSays, privacy.AsksTheQuestion) : ((string, bool)?)null;

        /// <summary>The pixels in the middle of the privacy card's "Kayıtları sil", and of the question's "Vazgeç" and "Sil"; nothing while each is not there to press.</summary>
        public Vector2? DeleteRecordsPixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.DeleteButtonMiddle) : null;

        public Vector2? KeepRecordsPixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.KeepButtonMiddle) : null;

        public Vector2? ConfirmDeletePixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.ConfirmButtonMiddle) : null;

        /// <summary>Which page of its text the privacy card in front shows, of how many; nothing while it is not in front.</summary>
        public (int Page, int Pages)? PrivacyPageShown => TopCard is PrivacyCard privacy ? privacy.PageShown : ((int, int)?)null;

        /// <summary>The pixels in the middle of the privacy card's arrows to the next page and to the one before; nothing where there is no such page.</summary>
        public Vector2? PrivacyNextPagePixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.NextPageMiddle) : null;

        public Vector2? PrivacyPreviousPagePixel => TopCard is PrivacyCard privacy ? PixelOnTheCard(privacy.PreviousPageMiddle) : null;

        /// <summary>The pixel in the middle of the support card's "Tamam"; nothing while that card is not down in front, or asks about the tester mode.</summary>
        public Vector2? SupportDonePixel => TopCard is SupportCard support ? PixelOnTheCard(support.DoneButtonMiddle) : null;

        /// <summary>Which page of the credits card's text is shown, of how many; nothing while that card is not in front.</summary>
        public (int Page, int Pages)? CreditsPageShown => TopCard is CreditsCard credits ? credits.PageShown : ((int, int)?)null;

        /// <summary>The pixels of the credits card's arrows and its "Tamam"; nothing while that card is not in front, or where there is no such page.</summary>
        public Vector2? CreditsNextPagePixel => TopCard is CreditsCard credits ? PixelOnTheCard(credits.NextPageMiddle) : null;

        public Vector2? CreditsPreviousPagePixel => TopCard is CreditsCard credits ? PixelOnTheCard(credits.PreviousPageMiddle) : null;

        public Vector2? CreditsDonePixel => TopCard is CreditsCard credits ? PixelOnTheCard(credits.DoneButtonMiddle) : null;

        /// <summary>The pixel in the middle of the support card's version row; nothing while that card is not down in front, or its row opens no door.</summary>
        public Vector2? VersionRowPixel => TopCard is SupportCard support ? PixelOnTheCard(support.VersionRowMiddle) : null;

        /// <summary>True while the support card in front asks whether the tester mode is to be turned on.</summary>
        public bool AsksAboutTheTesterMode => TopCard is SupportCard { AsksAboutTheTesterMode: true };

        /// <summary>What the card the way back brings in the hall asks on its nameplate, while it hangs or is on its way down or up; nothing otherwise.</summary>
        public string? QuitQuestionShown => (_cards.Find(card => card is QuitCard) as QuitCard)?.Question;

        /// <summary>The pixels in the middle of that card's "Vazgeç" and "Çık"; nothing while it is not down in front.</summary>
        public Vector2? StayInTheGamePixel => TopCard is QuitCard quit ? PixelOnTheCard(quit.StayButtonMiddle) : null;

        public Vector2? QuitTheGamePixel => TopCard is QuitCard quit ? PixelOnTheCard(quit.QuitButtonMiddle) : null;

        /// <summary>True while the question about the play statistics hangs, or is on its way down or up.</summary>
        public bool ShowsTheStatisticsQuestion => _cards.Exists(card => card is ConsentCard);

        /// <summary>The pixels in the middle of that question's "İzin ver" and "İzin verme"; nothing while it is not down in front.</summary>
        public Vector2? AllowTheStatisticsPixel => TopCard is ConsentCard consent ? PixelOnTheCard(consent.AllowButtonMiddle) : null;

        public Vector2? DeclineTheStatisticsPixel => TopCard is ConsentCard consent ? PixelOnTheCard(consent.DeclineButtonMiddle) : null;

        /// <summary>The pixels in the middle of the tester mode question's "Vazgeç" and "Aç"; nothing while the question is not asked in front.</summary>
        public Vector2? LeaveTheTesterModeOffPixel => TopCard is SupportCard support ? PixelOnTheCard(support.LeaveOffButtonMiddle) : null;

        public Vector2? TurnOnTheTesterModePixel => TopCard is SupportCard support ? PixelOnTheCard(support.TurnOnButtonMiddle) : null;

        /// <summary>
        /// The museum listens to the player while nothing of it moves by itself and the player has not left it; a
        /// page the finger drags moves with the finger, which the museum goes on listening to.
        /// </summary>
        private bool ListensToThePlayer => IsStill && !_isLeaving && PlayerLeftFor == null;

        /// <summary>True when no page is turning or settling, no card is on its way, no ad is being shown, and the room is neither lighting up nor going dark.</summary>
        private bool IsStill => _timeline.IsDone && _ad == null && _turnsToOnceTheCardsAreUp == null;

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
        /// <param name="showsTheStartBoosters">True when the card before the level shows the start boosters one after another the first time it comes down (TutorialPlan.IntroducesTheStartBoosters).</param>
        /// <param name="effectArt">The pictures of what is thrown over a board, which the card a long press brings plays with; nothing paints them when that card first comes.</param>
        /// <param name="asksAboutTheStatistics">
        /// True when the visit begins with the question about the play statistics coming down over the hall, as the
        /// game's first start after its entry page has it (Statistics/PlayStatistics.AsksOn): the room stays dark
        /// under it until it is answered.
        /// </param>
        public static MuseumStage Open(
            MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet, ShopView shop, DeviceView device, LevelBrief level,
            StageOpening opening = StageOpening.InTheLitRoom, bool showsTheStartBoosters = false, EffectArt? effectArt = null, bool asksAboutTheStatistics = false)
        {
            var stage = new MuseumStage(visit, paintings, art, door, wallet, shop, device, level, showsTheStartBoosters, effectArt);
            bool isDark = opening == StageOpening.OutOfTheDark;
            if (isDark)
            {
                stage._veil.Darken(1f);
            }

            if (asksAboutTheStatistics)
            {
                // Before anything else of the hall, and alone: the player answers before the first press of the visit.
                stage.Hang(ConsentCard.Create(stage._root.transform, art), roomIsDark: isDark);
            }
            else if (door.HangsTheStartCard)
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
            ForgetTheSwipe(); // a page dragged on the screen before has no place on this one
            _screen = screen;
            PageLayout layout = PageLayout.Of(screen);
            StageCamera.Show(Camera, layout.View);
            _page.Fit(screen);
            _leaving?.Fit(screen);
            _bar.Place(layout);
            _wallet.Place(layout);
            _veil.Fit(layout.View);
            _secondVeil.Fit(layout.View);
            _spotlight.Fit(layout.View);
            foreach (IMuseumCard card in _cards)
            {
                Place(card, layout); // the card at the back first: one in front stands on its foot
            }

            if (_startBoosterLit is int lit && StartCard is LevelStartCard start)
            {
                LightUpTheStartBooster(start, lit, layout);
            }

            _ad?.Card.Place(layout.PlaceOfACard(_ad.Card.Width, _ad.Card.Height), layout.View);
        }

        public void Advance(float deltaSeconds)
        {
            _clock += deltaSeconds;
            _timeline.Advance(deltaSeconds);
            _secondsOnThePage += deltaSeconds;
            (_page as IMovingPage)?.Move(_secondsOnThePage);
            _wallet.Advance(deltaSeconds);
            ShowTheAd(deltaSeconds);
            (TopCard as BoosterCardOverTheCard)?.Shown.Advance(deltaSeconds);
            TakeALongPress();
            ShowTheStartBoosters();
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

            // Done here too, after the step: the card of a purchase that comes next hangs in the step the card before it is gone.
            FollowTheStore();
        }

        /// <summary>
        /// Shows the player's wallet as it is at this instant, on the row and on the cards that tell of it, and the
        /// shop and the gift as the rules have them for it on the pages that show them: the owner of the stage tells
        /// both at every step, settled by the rules at the time of the step.
        /// </summary>
        public void Tell(WalletView wallet, ShopView shop)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            if (wallet.Coins != _wallet.Wallet.Coins)
            {
                _sounded.Add(SoundCue.Coin); // as the row starts to count them in, or out
            }

            _wallet.Tell(wallet);
            foreach (IMuseumCard card in _cards)
            {
                card.Tell(wallet);
            }

            (_page as IShopPage)?.Tell(shop);
            (_leaving as IShopPage)?.Tell(shop);
            (_pageBeside?.Page as IShopPage)?.Tell(shop);
            FollowTheStore();
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

        /// <summary>What presses asked of the store since the owner last took it, in order: the owner hands it to the purchase's course and tells the stage where it stands after it.</summary>
        public IReadOnlyList<StoreRequest> TakeWhatWasAskedOfTheStore()
        {
            if (_askedOfTheStore.Count == 0)
            {
                return Array.Empty<StoreRequest>();
            }

            StoreRequest[] taken = _askedOfTheStore.ToArray();
            _askedOfTheStore.Clear();
            return taken;
        }

        /// <summary>The cues the stage sounded since this was asked last, in the order they fell, each once.</summary>
        public IReadOnlyList<SoundCue> TakeWhatSounded() => _sounded.TakeAll();

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
        /// The game speaks another language: every page and card is built anew in it, where it stands, at once and
        /// without moving. The page that is shown (the collection at its room, the hall of a room just restored), the
        /// wallet row and the bar, and the cards that hang, each down in its place; the darks behind them stay as they
        /// are. Only the settings card and the cards it opens can hang while a language is chosen.
        /// </summary>
        /// <param name="device">The phone as it is in the new language.</param>
        /// <exception cref="InvalidOperationException">A card hangs that no language is chosen on.</exception>
        public void SpeakAnew(DeviceView device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            ForgetTheSwipe();
            IMuseumPage page = _page is HomePage { ShowsARestoredRoom: true } && _restored is RoomNumber restored
                ? HomePage.Create(_root.transform, _art, _paintings, _visit, restored)
                : BuildPage(Showing, (_page as CollectionPage)?.Room);
            _page.Dispose();
            _page = page;
            _secondsOnThePage = 0f;
            (_page as IShopPage)?.Tell(_shop);

            WalletView wallet = _wallet.Wallet;
            _wallet.Dispose();
            _wallet = WalletRow.Create(_root.transform, _art, wallet);
            _bar.Dispose();
            _bar = NavigationBar.Create(_root.transform, _art, Showing);

            IMuseumCard[] hanging = _cards.ToArray();
            _cards.Clear();
            foreach (IMuseumCard card in hanging)
            {
                IMuseumCard again = card switch
                {
                    SettingsCard _ => SettingsCard.Create(_root.transform, _art, device),
                    LanguageCard _ => LanguageCard.Create(_root.transform, _art, device, (int)DrawOrder.OverACard),
                    PrivacyCard _ => PrivacyCard.Create(_root.transform, _art, device, (int)DrawOrder.OverACard),
                    SupportCard _ => SupportCard.Create(_root.transform, _art, device, (int)DrawOrder.OverACard),
                    CreditsCard _ => CreditsCard.Create(_root.transform, _art, (int)DrawOrder.OverACard),
                    _ => throw new InvalidOperationException($"A {card.GetType().Name} hangs, and no language is chosen on it."),
                };
                card.Dispose();
                _cards.Add(again);
            }

            if (_screen is ScreenShape screen)
            {
                Fit(screen);
            }

            foreach (IMuseumCard card in _cards)
            {
                card.Card.ComeDown(1f);
            }

            if (_cards.Count == 0)
            {
                ListenToThePage();
            }
        }

        /// <summary>
        /// The device's way back: the card in front goes up (a question on it with it); a painting that hangs forward
        /// goes back to the wall; from the shop, the map and the collection it leads to the hall; in the hall, which
        /// has no way further back, the card comes down that asks whether to close the game; while a finger drags a
        /// page, nothing: the page is the finger's until it lets go.
        /// </summary>
        public void GoBack()
        {
            if (!ListensToThePlayer || _swipe.IsDragging)
            {
                return;
            }

            if (TopCard is NoticeCard { CanBeClosed: false })
            {
                return; // the card of a payment the store is taking stands until the store answers
            }

            if (TopCard is ConsentCard)
            {
                Follow(new MuseumPress.DeclineTheStatistics()); // the way back is a "no", never a "yes"
                return;
            }

            if (_cards.Count > 0)
            {
                StopShowingTheStartBoosters(); // cut short: the step comes again on the next card
                AskOnClosingThePurchaseCard();
                TakeTheCardInFrontUp();
            }
            else if (_page.TryGoBack())
            {
                ListenToThePage();
            }
            else if (Showing == Destination.Home)
            {
                Hang(QuitCard.Create(_root.transform, _art), roomIsDark: false);
            }
            else
            {
                TurnTo(Destination.Home);
            }
        }

        /// <summary>
        /// True once since this was asked last when the player answered the hall's question of the way back with "Çık":
        /// the game is to close. The card stays as it hangs; whoever runs the game closes it, and the Editor's tools
        /// only read that it was asked.
        /// </summary>
        public bool TakeTheWishToQuit()
        {
            bool isWished = _isAskedToQuit;
            _isAskedToQuit = false;
            return isWished;
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

            Vector2 point = PointAt(pixel, screen);
            if (_startBoosterLit != null || TopCard is BoosterCardOverTheCard)
            {
                _isATouchOnTheBoosterCard = TopCard is BoosterCardOverTheCard;
                return; // a touch goes on with the step, or takes the card of a long press up, as it lifts
            }

            if (TopCard is IMuseumCard card)
            {
                card.Card.FingerDown(point);
                _heldSince = card is LevelStartCard && card.Card.TryGetHeld(out _) ? _clock : (float?)null;
            }
            else
            {
                _presses.FingerDown(point);
                FollowASwipeFrom(point, ScreenView.Of(screen).Width);
            }
        }

        public void PointerMoved(Vector2 pixel)
        {
            if (!ListensToThePlayer || !(_screen is ScreenShape screen))
            {
                return;
            }

            Vector2 point = PointAt(pixel, screen);
            if (TopCard is IMuseumCard card)
            {
                card.Card.FingerMovedTo(point);
            }
            else if (_swipe.MovedTo(point.x, point.y, _timeline.Now) is SwipeReading.Drag drag)
            {
                _presses.FingerGone(); // a swipe presses nothing it began on
                Drag(drag, point, ScreenView.Of(screen).Width);
            }
            else
            {
                _presses.FingerMovedTo(point);
            }
        }

        public void PointerUp()
        {
            if (_swipe.Up(_timeline.Now) is SwipeReading.Settle settle)
            {
                Settle(settle);
                return;
            }

            _heldSince = null;
            if (!ListensToThePlayer)
            {
                return;
            }

            if (_startBoosterLit is int lit)
            {
                LightTheNextStartBooster(lit);
                return;
            }

            if (TopCard is BoosterCardOverTheCard)
            {
                if (_isATouchOnTheBoosterCard)
                {
                    TakeTheCardInFrontUp();
                }

                _isATouchOnTheBoosterCard = false;
                return;
            }

            MuseumPress press;
            bool isPressed = TopCard is IMuseumCard card ? card.Card.FingerUp(out press) : _presses.FingerUp(out press);
            if (isPressed)
            {
                _sounded.Add(press is MuseumPress.FlipASwitch ? SoundCue.Toggle : SoundCue.Press);
                Follow(press);
            }
        }

        public void PointerCancelled()
        {
            _heldSince = null;
            _isATouchOnTheBoosterCard = false;
            _presses.FingerGone();
            TopCard?.Card.FingerGone();
            if (_swipe.Cancel() is SwipeReading.Settle settle)
            {
                Settle(settle); // springs back to the page that was shown
            }
        }

        public void Dispose()
        {
            _page.Dispose();
            _leaving?.Dispose();
            _pageBeside?.Page.Dispose();
            _bar.Dispose();
            _wallet.Dispose();
            foreach (IMuseumCard card in _cards)
            {
                card.Dispose();
            }

            _ad?.Card.Dispose();
            _veil.Dispose();
            _secondVeil.Dispose();
            _spotlight.Dispose();
            _lessonPill.Dispose();
            if (_ownsTheEffectArt)
            {
                _effectArt?.Dispose();
            }

            SceneObjects.Destroy(_root);
        }

        /// <summary>The steps of the tutorial shown here to their end since this was asked last, each once: the start boosters.</summary>
        public IReadOnlyList<TutorialStep> TakeTheTutorialStepsDone()
        {
            TutorialStep[] done = _stepsDone.ToArray();
            _stepsDone.Clear();
            return done;
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
                    AskOnClosingThePurchaseCard();
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
                case MuseumPress.BuyFromTheStore _ when _shop.Store.Status is StoreStatus.NotSold:
                    // A test build sells nothing for money: no course is asked, and the card says so.
                    Hang(
                        NoticeCard.Create(
                            _root.transform, _art, ShopWords.PacksTitle, new NoticeExhibit.Mark(GalleryArt.CoinChest, NoticeMarkSize), ShopWords.NotOnSale,
                            ShopWords.WhereCoinsComeFrom),
                        roomIsDark: false);
                    break;
                case MuseumPress.BuyFromTheStore buy:
                    _askedOfTheStore.Add(new StoreRequest.Ask(buy.Product));
                    break;
                case MuseumPress.PayInTheStore _ when TopCard is NoticeCard { Answers: NoticeAnswers.BuyOrDecline }:
                    // The card stays until the course moves on: then it goes up, and the card of the payment comes down.
                    _askedOfTheStore.Add(new StoreRequest.Pay());
                    break;
                case MuseumPress.RestorePurchases _:
                    _askedOfTheStore.Add(new StoreRequest.Restore());
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
                case MuseumPress.OpenTheCredits _ when TopCard is SettingsCard:
                    Hang(CreditsCard.Create(_root.transform, _art, (int)DrawOrder.OverACard), roomIsDark: false);
                    break;
                case MuseumPress.TurnTheCreditsPage turn when TopCard is CreditsCard credits:
                    credits.TurnThePage(turn.IsForward);
                    break;
                case MuseumPress.OpenTheLanguages _ when TopCard is SettingsCard:
                    Hang(LanguageCard.Create(_root.transform, _art, _device, (int)DrawOrder.OverACard), roomIsDark: false);
                    break;
                case MuseumPress.ChooseALanguage choose when TopCard is LanguageCard:
                    // The game speaks it once the owner has kept it: the owner has the stage speak anew (SpeakAnew).
                    if (choose.Language != _device.Speaking)
                    {
                        _askedOfThePhone.Add(new DeviceRequest.ChooseALanguage(choose.Language));
                    }

                    break;
                case MuseumPress.FlipASwitch flip when TopCard is SettingsCard settings:
                    // The switch slides at once; the setting is kept by the stage's owner, who tells the phone after it.
                    bool isOn = settings.Flip(flip.Setting);
                    _askedOfThePhone.Add(new DeviceRequest.TurnASetting(flip.Setting, isOn));
                    _timeline.Add(0f, Tempo.SwitchSlides, progress => settings.Slide(flip.Setting, progress));
                    break;
                case MuseumPress.TurnThePrivacyPage turn when TopCard is PrivacyCard privacy:
                    privacy.TurnThePage(turn.IsForward);
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
                case MuseumPress.TapTheVersion _ when TopCard is SupportCard support:
                    support.CountATap();
                    break;
                case MuseumPress.LeaveTheTesterModeOff _ when TopCard is SupportCard support:
                    support.TakeTheQuestionBack();
                    break;
                case MuseumPress.TurnOnTheTesterMode _ when TopCard is SupportCard:
                    // The owner keeps the mode and tells the wallet without an end; once every card is up, the page is the player's again.
                    _askedOfThePhone.Add(new DeviceRequest.TurnASetting(DeviceSetting.TesterMode, isOn: true));
                    TakeEveryCardUp();
                    _turnsToOnceTheCardsAreUp = Showing;
                    break;
                case MuseumPress.QuitTheGame _ when TopCard is QuitCard:
                    // The card stays: on Android the game is gone with the next step, and elsewhere its "Vazgeç" or the way back takes it up.
                    _isAskedToQuit = true;
                    break;
                case MuseumPress.AllowTheStatistics _ when TopCard is ConsentCard:
                    // The owner keeps the answer in the device file and lets the notes through from the next step on.
                    _askedOfThePhone.Add(new DeviceRequest.TurnASetting(DeviceSetting.Statistics, isOn: true));
                    TakeTheCardInFrontUp();
                    break;
                case MuseumPress.DeclineTheStatistics _ when TopCard is ConsentCard:
                    _askedOfThePhone.Add(new DeviceRequest.TurnASetting(DeviceSetting.Statistics, isOn: false));
                    TakeTheCardInFrontUp();
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
                case MuseumPress.VisitTheCollection visit:
                    // A restored painting's card on the map: the collection opens at the painting's room.
                    Turn(BuildPage(Destination.Collection, visit.Room), Destination.Collection,
                        comesFromTheRight: Destinations.PlaceOf(Destination.Collection) > Destinations.PlaceOf(Showing));
                    break;
                case MuseumPress.GoOnFromTheRestoredRoom _:
                    _restored = null;
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

        // ---------- the booster intro ----------

        /// <summary>
        /// The start boosters' step begins once the card before the level hangs in its place and nothing is held on it;
        /// the step that shows is seen dimming in.
        /// </summary>
        private void ShowTheStartBoosters()
        {
            if (_startBoosterLit == null && _startBoostersToShow && TopCard is LevelStartCard start && ListensToThePlayer && !start.Card.TryGetHeld(out _))
            {
                start.Card.FingerGone();
                _heldSince = null;
                _startBoostersSince = _clock;
                _startBoosterLit = 0;
                if (_screen is ScreenShape screen)
                {
                    LightUpTheStartBooster(start, 0, PageLayout.Of(screen));
                }
            }

            _spotlight.Show(_startBoosterLit == null ? 0f : Easing.Out(Mathf.Clamp01((_clock - _startBoostersSince) / LessonComesIn)));
        }

        /// <summary>A touch went on: the next start booster is lit, or after the last the step is done and the card is the player's.</summary>
        private void LightTheNextStartBooster(int lit)
        {
            _sounded.Add(SoundCue.Press);
            if (lit + 1 < LevelBrief.StartBoosters.Count && StartCard is LevelStartCard start && _screen is ScreenShape screen)
            {
                _startBoosterLit = lit + 1;
                LightUpTheStartBooster(start, lit + 1, PageLayout.Of(screen));
                return;
            }

            StopShowingTheStartBoosters();
            _startBoostersToShow = false;
            _stepsDone.Add(TutorialStep.StartBoosters);
        }

        /// <summary>The dark over the card is cut open around one start booster's disc and name, and the pill under the card says it.</summary>
        private void LightUpTheStartBooster(LevelStartCard start, int lit, PageLayout layout)
        {
            BoosterKind booster = LevelBrief.StartBoosters[lit];
            _spotlight.LightUp(new[] { start.PlaceOf(booster) });
            _lessonPill.ShowThePainter(PainterPictures.MoodOf(TutorialStep.StartBoosters));
            _lessonPill.Show(TutorialWords.OfTheStartBooster(booster, _level.ExtraMoves));
            float widest = Mathf.Min(ScreenLayout.WidestShelf, layout.View.Width - 2f * LessonPillFromTheSides);
            float under = start.Card.Foot - start.Card.Scale * (LevelStartLayout.RidesBelow + LessonPillUnderTheCard);
            _lessonPill.Stand(under, ShelfPill.PillEdge.Head, 1f, widest);
        }

        private void StopShowingTheStartBoosters()
        {
            _startBoosterLit = null;
            _spotlight.Show(0f);
            _lessonPill.Show(PillWords.None);
            _lessonPill.ShowThePainter(null);
        }

        /// <summary>
        /// A finger held on a start booster's disc for <see cref="Tempo.LongPress"/>: it chooses nothing (the lift
        /// presses nothing), and the card that shows the booster comes down in front.
        /// </summary>
        private void TakeALongPress()
        {
            if (!(_heldSince is float since) || _clock - since < Tempo.LongPress)
            {
                return;
            }

            _heldSince = null;
            if (TopCard is LevelStartCard start && start.Card.TryGetHeld(out MuseumPress held) && held is MuseumPress.ChooseStartBooster choose)
            {
                start.Card.FingerGone();
                _effectArt ??= EffectArt.Paint();
                _isATouchOnTheBoosterCard = false;
                Hang(new BoosterCardOverTheCard(new BoosterCard<MuseumPress>(_root.transform, _art, _effectArt, choose.Booster, _level.ExtraMoves, (int)DrawOrder.OverACard)), roomIsDark: false);
            }
        }

        /// <summary>The card of a long press, hung in front of the card before a level as the museum hangs its cards.</summary>
        private sealed class BoosterCardOverTheCard : IMuseumCard
        {
            public BoosterCardOverTheCard(BoosterCard<MuseumPress> shown)
            {
                Shown = shown;
            }

            public BoosterCard<MuseumPress> Shown { get; }

            public HangingCard<MuseumPress> Card => Shown.Card;

            public void Tell(WalletView wallet)
            {
                // The card shows no wallet.
            }

            public void Dispose() => Shown.Dispose();
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
            ForgetTheSwipe(); // no page moves under a card
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

            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
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
        /// of the card behind stays in sight above it and its rim under it. A language card of more languages than fit
        /// there hangs in the page's place for a card too, in front of the dark over the card behind it.
        /// </summary>
        private void Place(IMuseumCard card, PageLayout layout)
        {
            HangingCard<MuseumPress> behind = _cards[0].Card;
            bool hangsByItself = ReferenceEquals(card, _cards[0]) || card is LanguageCard { HangsByItself: true };
            CardPlace place = hangsByItself
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

        // ---------- the store ----------

        /// <summary>
        /// The card of where a purchase for money stands follows the purchase's course at every step: the card that
        /// asks before paying, the card of the payment the store is taking (no answer: it goes when the store answers),
        /// and the cards of what came of it, a restore's included. When the course moves on, the card that shows the
        /// step before goes up, and the next comes down once it is gone, over a page with no other card in front: a
        /// payment approved while the settings card hangs waits for it to close. A purchase the player cancelled on the
        /// store's own sheet, which said so, has no card: the course is told so and goes back to idle. A card the player
        /// closed tells the course (declined, seen) and does not come down again.
        /// </summary>
        private void FollowTheStore()
        {
            StoreView store = _shop.Store;
            if (store.Purchase is PurchaseState.Cancelled cancelled && !ReferenceEquals(_closedFor, cancelled))
            {
                _askedOfTheStore.Add(new StoreRequest.Dismiss());
                _closedFor = cancelled;
            }

            object? due = DuePurchaseCard(store);
            if (_purchaseCard is (NoticeCard shown, object showsFor))
            {
                if (!ReferenceEquals(showsFor, due) && ReferenceEquals(TopCard, shown) && IsStill)
                {
                    _purchaseCard = null;
                    TakeTheCardInFrontUp();
                }

                return;
            }

            if (due != null && _cards.Count == 0 && ListensToThePlayer)
            {
                NoticeCard card = PurchaseCardFor(due, store);
                _purchaseCard = (card, due);
                Hang(card, roomIsDark: false);
            }
        }

        /// <summary>True while a card of a purchase is due and has not come down yet: the museum is not settled until it hangs.</summary>
        private bool IsAPurchaseCardDue => _purchaseCard == null && _cards.Count == 0 && !_isLeaving && PlayerLeftFor == null && DuePurchaseCard(_shop.Store) != null;

        /// <summary>What a purchase card is due to show now: where the purchase stands, else the end of a restore; nothing for what the player closed.</summary>
        private object? DuePurchaseCard(StoreView store)
        {
            object? due = store.Purchase is PurchaseState.Nothing || store.Purchase is PurchaseState.Cancelled
                ? store.Restore as RestoreState.Ended
                : store.Purchase;
            return ReferenceEquals(due, _closedFor) ? null : due;
        }

        /// <summary>The player closes the purchase card in front: the course is told (declined, seen), and the card does not come down again.</summary>
        private void AskOnClosingThePurchaseCard()
        {
            if (_purchaseCard is (NoticeCard card, object showsFor) && ReferenceEquals(TopCard, card))
            {
                _askedOfTheStore.Add(showsFor switch
                {
                    PurchaseState.Asking _ => new StoreRequest.Decline(),
                    RestoreState.Ended _ => new StoreRequest.DismissTheRestore(),
                    _ => (StoreRequest)new StoreRequest.Dismiss(),
                });
                _closedFor = showsFor;
                _purchaseCard = null;
            }
        }

        /// <summary>
        /// The card of a step of a purchase for money: the product's goods on the card that asks, on the card of the
        /// payment and on the card of what came; the chest of coins on a card of nothing bought and of a restore.
        /// </summary>
        private NoticeCard PurchaseCardFor(object due, StoreView store)
        {
            switch (due)
            {
                case PurchaseState.Asking asking:
                    PurchaseCardWords ask = PurchaseWords.Asking(asking.Bought, asking.Price);
                    return NoticeCard.Create(_root.transform, _art, ask.Title, GoodsOf(asking.Bought), ask.FirstLine, ask.SecondLine, raisedBy: 0, NoticeAnswers.BuyOrDecline);
                case PurchaseState state:
                    PurchaseCardWords said = PurchaseWords.Of(state) ?? throw new InvalidOperationException($"A purchase that stands at {state} has no card.");
                    NoticeExhibit exhibit = state is PurchaseState.Failed ? new NoticeExhibit.Mark(GalleryArt.CoinChest, NoticeMarkSize)
                        : state is PurchaseState.Delivered delivered ? new NoticeExhibit.Goods(GoodsItem.Of(delivered.What))
                        : GoodsOf(state.Product ?? throw new InvalidOperationException($"A purchase that stands at {state} is of no product."));
                    return NoticeCard.Create(
                        _root.transform, _art, said.Title, exhibit, said.FirstLine, said.SecondLine, raisedBy: 0,
                        state is PurchaseState.Pending ? NoticeAnswers.None : NoticeAnswers.Done);
                case RestoreState restore:
                    PurchaseCardWords back = PurchaseWords.Of(restore, store.StarterPackProduct) ?? throw new InvalidOperationException($"A restore that stands at {restore} has no card.");
                    return NoticeCard.Create(
                        _root.transform, _art, back.Title, new NoticeExhibit.Mark(GalleryArt.CoinChest, NoticeMarkSize), back.FirstLine, back.SecondLine);
                default:
                    throw new ArgumentOutOfRangeException(nameof(due), due, "The store's course has no card for that.");
            }
        }

        private static NoticeExhibit GoodsOf(StoreProduct product) => new NoticeExhibit.Goods(GoodsItem.Of(product.Contents));

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
            _timeline.At(Tempo.CardAfterDark, () => _sounded.Add(SoundCue.CardDown));
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
        /// A finger came down on the page: should it swipe, it turns the pages of the bar, or from the collection's
        /// plaque its rooms. Nothing swipes while a painting of the collection hangs forward: it takes every touch.
        /// </summary>
        private void FollowASwipeFrom(Vector2 point, float width)
        {
            CollectionPage? collection = _page as CollectionPage;
            MapPage? map = _page as MapPage;
            if (collection?.Viewed != null || map?.ShowsACard == true)
            {
                _swipe.Cancel();
                _swiped = null;
                return;
            }

            bool turnsTheRooms = collection != null && collection.IsOnThePlaque(point);
            bool scrollsTheEras = map != null && map.IsOnTheStrip(point);
            _swiped = turnsTheRooms ? SwipedRow.Rooms : scrollsTheEras ? SwipedRow.Eras : SwipedRow.Pages;
            if (scrollsTheEras && map != null)
            {
                map.HoldTheStrip(point.x);
            }

            SwipeRow row = turnsTheRooms && collection != null
                ? new SwipeRow(collection.RoomsToVisit, collection.Room.Value - 1, width)
                : new SwipeRow(Destinations.OnTheBar.Count, Destinations.PlaceOf(Showing), width);
            _swipe.Down(point.x, point.y, _timeline.Now, row);
        }

        private void Drag(SwipeReading.Drag drag, Vector2 point, float width)
        {
            if (_swiped == SwipedRow.Rooms && _page is CollectionPage collection)
            {
                collection.DragTheRooms(drag, width);
            }
            else if (_swiped == SwipedRow.Eras && _page is MapPage map)
            {
                map.DragTheStrip(point.x); // the strip follows the finger, not the row of pages
            }
            else
            {
                DragThePages(drag, width);
            }
        }

        private void Settle(SwipeReading.Settle settle)
        {
            SwipedRow? swiped = _swiped;
            _swiped = null;
            if (_screen is not ScreenShape screen)
            {
                return; // nothing is dragged before there is a screen to drag it on
            }

            float width = ScreenView.Of(screen).Width;
            if (swiped == SwipedRow.Rooms && _page is CollectionPage collection)
            {
                _presses.Clear();
                collection.SettleTheRooms(settle, width, settled: ListenToThePage);
            }
            else if (swiped == SwipedRow.Eras && _page is MapPage map)
            {
                map.LetGoOfTheStrip();
                ListenToThePage();
            }
            else
            {
                SettleThePages(settle, width);
            }
        }

        /// <summary>
        /// The finger drags the pages: the shown one follows it, and the neighbour it drags in stands beside it, built
        /// the first time it is needed; beyond the bar's ends the page only stretches, with nothing beside it.
        /// </summary>
        private void DragThePages(SwipeReading.Drag drag, float width)
        {
            Destination? beside = drag.Beside is int place ? Destinations.OnTheBar[place] : (Destination?)null;
            if (_pageBeside?.Destination != beside)
            {
                TakeDownThePageBeside();
                if (beside is Destination destination && _screen is ScreenShape screen)
                {
                    IMuseumPage page = BuildPage(destination, collectionRoom: null);
                    page.Fit(screen);
                    (page as IShopPage)?.Tell(_shop);
                    _pageBeside = (page, destination);
                }
            }

            _page.Root.localPosition = new Vector3(drag.By, 0f, 0f);
            if (_pageBeside is { } pageBeside)
            {
                pageBeside.Page.Root.localPosition = new Vector3(drag.By - Math.Sign(drag.By) * width, 0f, 0f);
            }
        }

        /// <summary>
        /// The finger let go of the pages: they go the rest of their way, already moving. Settled on the neighbour, the
        /// bar marks it at once and the page that was shown is taken down once it is out of sight; sprung back, the
        /// neighbour is. Meanwhile nothing is pressed.
        /// </summary>
        private void SettleThePages(SwipeReading.Settle settle, float width)
        {
            IMuseumPage shown = _page;
            IMuseumPage? beside = _pageBeside?.Page;
            Destination settledOn = Destinations.OnTheBar[settle.Place];
            bool turns = _pageBeside?.Destination == settledOn && settledOn != Showing;
            _pageBeside = null;
            float from = settle.From;
            float side = from < 0f ? -1f : 1f;
            float to = turns ? side * width : 0f;
            if (turns && beside != null)
            {
                _page = beside;
                _leaving = shown;
                _secondsOnThePage = 0f;
                Showing = settledOn;
                _bar.Choose(settledOn);
            }
            else
            {
                _leaving = beside;
            }

            _presses.Clear();
            _timeline.Add(
                0f,
                Tempo.PageSettlesIn((to - from) / width),
                progress =>
                {
                    float at = from + (to - from) * Easing.Out(progress);
                    shown.Root.localPosition = new Vector3(at, 0f, 0f);
                    if (beside != null)
                    {
                        beside.Root.localPosition = new Vector3(at - side * width, 0f, 0f);
                    }
                },
                () =>
                {
                    _leaving?.Dispose();
                    _leaving = null;
                    ListenToThePage();
                });
        }

        /// <summary>A swipe is given up without settling, as when a card comes down or the screen changes: the page, or the room, stands in its place at once.</summary>
        private void ForgetTheSwipe()
        {
            if (_swipe.Cancel() is SwipeReading.Settle)
            {
                if (_swiped == SwipedRow.Rooms && _page is CollectionPage collection)
                {
                    collection.ForgetTheRoomsDrag();
                }

                if (_swiped == SwipedRow.Eras && _page is MapPage map)
                {
                    map.ForgetTheStripDrag();
                }

                _page.Root.localPosition = Vector3.zero;
                TakeDownThePageBeside();
            }

            _swiped = null;
        }

        private void TakeDownThePageBeside()
        {
            _pageBeside?.Page.Dispose();
            _pageBeside = null;
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
            point is Vector2 at && ListensToThePlayer && !_swipe.IsDragging && _cards.Count == 0 && _screen is ScreenShape screen ? PixelOf(at, screen) : (Vector2?)null;

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

        /// <summary>What a swipe on the museum's pages moves: the pages of the bar, the collection's rooms from its plaque, or the world map's strip of eras.</summary>
        private enum SwipedRow
        {
            Pages,
            Rooms,
            Eras,
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
