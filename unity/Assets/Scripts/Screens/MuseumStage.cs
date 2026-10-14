#nullable enable
using System;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Collections;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Maps;
using Puzzle.Client.Play;
using Puzzle.Client.Stage;
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

    /// <summary>
    /// The museum as the player walks through it between two levels: one page at a time (the hall, the map, the
    /// collection), over them the row of the player's coins and lives, and at their foot the navigation bar that
    /// leads from one page to the other. A page is turned like a page: the new one comes in from the side its
    /// destination stands on the bar while the old one leaves to the other side, under the row and the bar, which
    /// stay. One card at a time may hang over the page, the row and the bar, in front of the room's dark (the lives
    /// card); while it hangs, only the card is pressed, and the way back closes it. Like the board's stage the
    /// museum has a clock that only <see cref="Advance"/> moves, and everything of it is drawn by its one camera, so
    /// the Editor pictures any page at any moment with no game running.
    /// </summary>
    /// <remarks>
    /// The stage shows the museum as the player's progress has it when the stage is opened; it does not change that
    /// progress. The player's wallet is what its owner tells it (<see cref="Tell"/>), at every step: the wallet as
    /// the rules have it at that instant, so the lives come back and the waits count down as the clock runs, and
    /// coins that arrive count up on the row. The play button of a player without a life opens the lives card
    /// instead of the level. When the player has pressed the play button with a life, or a painting whose pieces
    /// wait, and the room has gone dark, <see cref="PlayerLeftFor"/> says what for, and whoever owns the stage opens
    /// the level's board or the painting's jigsaw, which comes out of the same dark.
    /// <para>
    /// To add a page: add its <see cref="Destination"/>, build it in <see cref="BuildPage"/>, and give it a render
    /// state in the Editor's ScreenShots command. A page needs no clock of its own while nothing on it moves; one
    /// on which something does is given the stage's timeline when it is built.
    /// </para>
    /// </remarks>
    public sealed class MuseumStage : IStage
    {
        private readonly GameObject _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly MuseumVisit _visit;
        private readonly NavigationBar _bar;
        private readonly WalletRow _wallet;
        private readonly Veil _veil;
        private readonly Timeline _timeline = new Timeline();
        private readonly PressSet<MuseumPress> _presses = new PressSet<MuseumPress>();

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

        /// <summary>The card that hangs over the page, coming down or going up; nothing while none does.</summary>
        private LivesCard? _card;

        private MuseumStage(MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet)
        {
            _visit = visit;
            _paintings = paintings;
            _art = art;
            Showing = door.Page;
            _root = new GameObject("Museum");
            Camera = StageCamera.Add(_root.transform);
            _page = door.Restored is RoomNumber restored
                ? HomePage.Create(_root.transform, art, paintings, visit, restored)
                : BuildPage(door.Page, door.CollectionRoom);
            _bar = NavigationBar.Create(_root.transform, art, door.Page);
            _wallet = WalletRow.Create(_root.transform, art, wallet);
            _veil = Veil.Create(_root.transform, art);
            ListenToThePage();
        }

        public Camera Camera { get; }

        /// <summary>The destination whose page is shown, or is coming in.</summary>
        public Destination Showing { get; private set; }

        /// <summary>True when no page is turning and the room is neither lighting up nor going dark.</summary>
        public bool IsSettled => _timeline.IsDone;

        /// <summary>
        /// What the player has left the museum for, once they pressed what leads there and the room has gone dark:
        /// this stage is then over, and the room is dark for the board or the jigsaw to come out of. Nothing before.
        /// </summary>
        public MuseumExit? PlayerLeftFor { get; private set; }

        /// <summary>True once the player has left for the level they stand on.</summary>
        public bool PlayerWentToPlay => PlayerLeftFor is MuseumExit.ToPlay;

        /// <summary>True while the lives card hangs over the page, or is on its way down or up.</summary>
        public bool ShowsTheLivesCard => _card != null;

        /// <summary>The wallet as the stage was last told of it.</summary>
        public WalletView Wallet => _wallet.Wallet;

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

        /// <summary>The pixels in the middle of the lives card's "Tamam" and of its round button that closes it; nothing while the card is not down.</summary>
        public Vector2? LivesCardDonePixel => _card != null ? PixelOnTheCard(_card.DoneButtonMiddle) : null;

        public Vector2? LivesCardClosePixel => _card != null ? PixelOnTheCard(_card.Card.CloseButtonMiddle) : null;

        /// <summary>The museum listens to the player while nothing of it moves and the player has not left it.</summary>
        private bool ListensToThePlayer => IsSettled && !_isLeaving && PlayerLeftFor == null;

        /// <summary>Builds the museum for a visit, with one page shown. Call <see cref="Fit"/> before the first picture.</summary>
        /// <param name="door">The page the visit begins on.</param>
        /// <param name="wallet">The player's wallet as the row shows it first, with nothing counting; <see cref="Tell"/> it anew at every step.</param>
        /// <param name="opening">In the lit room, as the game starts; or out of the dark a result's card or a jigsaw left behind.</param>
        public static MuseumStage Open(
            MuseumVisit visit, PaintingGallery paintings, ArtCatalogue art, MuseumDoor door, WalletView wallet, StageOpening opening = StageOpening.InTheLitRoom)
        {
            var stage = new MuseumStage(visit, paintings, art, door, wallet);
            if (opening == StageOpening.OutOfTheDark)
            {
                stage._veil.Darken(1f);
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
            _card?.Card.Place(layout.PlaceOfACard(_card.Card.Width, _card.Card.Height), layout.View);
        }

        public void Advance(float deltaSeconds)
        {
            _timeline.Advance(deltaSeconds);
            _secondsOnThePage += deltaSeconds;
            (_page as IMovingPage)?.Move(_secondsOnThePage);
            _wallet.Advance(deltaSeconds);
        }

        /// <summary>
        /// Shows the player's wallet as it is at this instant, on the row and on a card that tells of it: the
        /// owner of the stage tells it at every step, settled by the rules at the time of the step.
        /// </summary>
        public void Tell(WalletView wallet)
        {
            _wallet.Tell(wallet);
            _card?.Tell(wallet);
        }

        /// <summary>
        /// The device's way back: a card that hangs over the page goes up; a painting that hangs forward goes back
        /// to the wall; from the map and the collection it leads to the hall; in the hall it leads nowhere.
        /// </summary>
        public void GoBack()
        {
            if (!ListensToThePlayer)
            {
                return;
            }

            if (_card != null)
            {
                TakeTheCardUp(_card);
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

            if (_card != null)
            {
                _card.Card.FingerDown(PointAt(pixel, screen));
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

            if (_card != null)
            {
                _card.Card.FingerMovedTo(PointAt(pixel, screen));
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
            bool isPressed = _card != null ? _card.Card.FingerUp(out press) : _presses.FingerUp(out press);
            if (isPressed)
            {
                Follow(press);
            }
        }

        public void PointerCancelled()
        {
            _presses.FingerGone();
            _card?.Card.FingerGone();
        }

        public void Dispose()
        {
            _page.Dispose();
            _leaving?.Dispose();
            _bar.Dispose();
            _wallet.Dispose();
            _card?.Dispose();
            _veil.Dispose();
            SceneObjects.Destroy(_root);
        }

        private void Follow(MuseumPress press)
        {
            switch (press)
            {
                case MuseumPress.Play _ when !_wallet.Wallet.HasALife:
                case MuseumPress.OpenTheLivesCard _:
                    HangTheLivesCard();
                    break;
                case MuseumPress.CloseTheCard _ when _card != null:
                    TakeTheCardUp(_card);
                    break;
                case MuseumPress.Play _:
                    LeaveFor(new MuseumExit.ToPlay());
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
                    throw new InvalidOperationException($"A press of the kind {press.GetType().Name} leads nowhere.");
            }
        }

        /// <summary>The room goes dark and the lives card comes down in front of it; meanwhile nothing is pressed.</summary>
        private void HangTheLivesCard()
        {
            if (_card != null || !(_screen is ScreenShape screen))
            {
                return;
            }

            LivesCard card = LivesCard.Create(_root.transform, _art, _wallet.Wallet);
            PageLayout layout = PageLayout.Of(screen);
            card.Card.Place(layout.PlaceOfACard(card.Card.Width, card.Card.Height), layout.View);
            _card = card;
            _presses.Clear();
            _timeline.Add(0f, Tempo.RoomDarkens, progress => _veil.Darken(Easing.Out(progress)));
            _timeline.Add(Tempo.CardAfterDark, Tempo.CardComesDown, card.Card.ComeDown);
        }

        /// <summary>The card goes up, the room lights again behind it, and once it is gone the page is the player's again.</summary>
        private void TakeTheCardUp(LivesCard card)
        {
            card.Card.FingerGone();
            _timeline.Add(0f, Tempo.CardGoesUp, card.Card.GoUp);
            _timeline.Add(
                Tempo.CardGoesUp / 2f,
                Tempo.RoomLightens,
                progress => _veil.Darken(1f - Easing.Out(progress)),
                () =>
                {
                    card.Dispose();
                    _card = null;
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
            Destination.Home => HomePage.Create(_root.transform, _art, _paintings, _visit),
            Destination.Map => MapPage.Create(_root.transform, _art, _paintings, _visit),
            Destination.Collection => CollectionPage.Create(_root.transform, _art, _paintings, _visit, _timeline, collectionRoom),
            _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "A destination without a page."),
        };

        /// <summary>The pixel of a point of the page that is shown; nothing while the stage has no screen, does not listen to the player, or a card hangs over the page.</summary>
        private Vector2? PixelWhileListening(Vector2? point) =>
            point is Vector2 at && ListensToThePlayer && _card == null && _screen is ScreenShape screen ? PixelOf(at, screen) : (Vector2?)null;

        /// <summary>The pixel of a point of the card that hangs over the page; nothing while it is on its way, or the stage has no screen.</summary>
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
    }
}
