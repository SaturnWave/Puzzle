#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Client.Stage;
using Puzzle.Museum;
using UnityEngine;

namespace Puzzle.Client.Collections
{
    /// <summary>
    /// The collection, as the design canvas draws it (CollectionTR): the wall of one room with its five paintings,
    /// each as far restored as the player has it, under a plaque that counts the museum's restored paintings and
    /// names the room. The round buttons on the plaque turn to the rooms before and after, and so does a swipe on the
    /// plaque, which the stage hands over (<see cref="IsOnThePlaque"/>): the room's paintings follow the finger and
    /// the next room's come in beside them. A restored painting comes forward to be looked at when it is touched; a
    /// painting whose pieces wait leads to its jigsaw.
    /// </summary>
    /// <remarks>
    /// Over the plaque the museum's stage shows the player's coins and lives (<see cref="WalletRow"/>). Left out of
    /// the design, because the game has nothing behind them yet: the wallet row's plus buttons and the settings'
    /// button, and the row of room tabs that scrolls sideways (the two round buttons turn the rooms instead).
    /// </remarks>
    public sealed class CollectionPage : IMuseumPage
    {
        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly PaintingGallery _paintings;
        private readonly MuseumVisit _visit;
        private readonly Timeline _timeline;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly CollectionPlaque _plaque;
        private readonly DiscButton _previous;
        private readonly DiscButton _next;

        private ScreenShape? _screen;

        /// <summary>Where the plaque lies on the screen, in the layout's points; nothing before the first <see cref="Fit"/>.</summary>
        private Box? _plaqueBox;

        /// <summary>The paintings of the room that is shown.</summary>
        private HungRoom _room;

        /// <summary>The room a finger on the plaque drags in beside the shown one; nothing otherwise.</summary>
        private HungRoom? _roomBeside;

        /// <summary>The room that slides out of sight after a swipe let it go; nothing otherwise.</summary>
        private HungRoom? _roomLeaving;

        /// <summary>The painting that is looked at by itself, and the one that is on its way back to the wall; nothing otherwise.</summary>
        private PaintingViewer? _viewer;
        private PaintingViewer? _goingBack;
        private PaintingNumber? _viewed;

        private CollectionPage(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit, Timeline timeline, RoomNumber room)
        {
            _art = art;
            _paintings = paintings;
            _visit = visit;
            _timeline = timeline;
            _root = new GameObject("Collection").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wall = Wall.Create(_root, art);
            _trim = RoomTrim.Create(_root, art);
            _plaque = CollectionPlaque.Create(_root, art, CollectionRooms.PaintingsWhole(visit.Progress, paintings.Catalogue));
            _previous = new DiscButton("Room before", _root, art, DiscGlyph.Back, DrawOrder.PlaqueArt);
            _next = new DiscButton("Room after", _root, art, DiscGlyph.Forward, DrawOrder.PlaqueArt);
            _room = Hang(room);
            NameTheRoom();
        }

        public Transform Root => _root;

        /// <summary>The room whose wall is shown.</summary>
        public RoomNumber Room => _room.Number;

        /// <summary>How many rooms the collection shows this player: the rooms 1 to this one.</summary>
        public int RoomsToVisit => CollectionRooms.LastToVisit(_visit.Progress, _paintings.Catalogue).Value;

        /// <summary>The painting that is looked at by itself; nothing while the wall is shown.</summary>
        public PaintingNumber? Viewed => _viewer != null ? _viewed : null;

        /// <summary>The middles of the round buttons that turn the rooms, in the layout's points.</summary>
        public Vector2 PreviousRoomMiddle => _previous.Middle;

        public Vector2 NextRoomMiddle => _next.Middle;

        /// <summary>The middle of the plaque, in the layout's points: where a finger swipes the rooms; nothing before the first <see cref="Fit"/>.</summary>
        public Vector2? PlaqueMiddle => _plaqueBox is Box plaque ? new Vector2(plaque.MiddleX, plaque.MiddleY) : (Vector2?)null;

        /// <param name="timeline">The clock of the stage the page is shown on: a painting that comes forward takes its time from it.</param>
        /// <param name="room">The room whose wall is shown first; nothing for the room the player stands in.</param>
        /// <exception cref="ArgumentOutOfRangeException">The collection does not show that room to this player yet.</exception>
        public static CollectionPage Create(
            Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit, Timeline timeline, RoomNumber? room = null)
        {
            RoomNumber shown = room ?? visit.Room;
            if (!CollectionRooms.Shows(shown, visit.Progress, paintings.Catalogue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(room), shown, $"The collection shows this player the rooms 1 to {CollectionRooms.LastToVisit(visit.Progress, paintings.Catalogue)}.");
            }

            return new CollectionPage(parent, art, paintings, visit, timeline, shown);
        }

        public void Fit(ScreenShape screen)
        {
            _screen = screen;
            CollectionLayout layout = _room.Fit(screen);
            _roomBeside?.Fit(screen);
            _roomLeaving?.Fit(screen);
            ScreenView view = layout.Page.View;
            _wall.Fit(view);
            _trim.Show(layout.Page, floorLine: null);
            _plaque.Place(layout.Page);
            _plaqueBox = layout.Page.Plaque;
            PlaceTheButton(_previous, layout.PreviousRoom);
            PlaceTheButton(_next, layout.NextRoom);
            if (_viewer != null && _viewed is PaintingNumber viewed && ExhibitOf(viewed).Picture is Box onTheWall)
            {
                _viewer.Fit(screen, onTheWall);
            }
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            presses.Add(_previous, new MuseumPress.OnThePage(() => TurnTo(new RoomNumber(Room.Value - 1))));
            presses.Add(_next, new MuseumPress.OnThePage(() => TurnTo(new RoomNumber(Room.Value + 1))));
            foreach (ExhibitFrame exhibit in _room.Exhibits)
            {
                PaintingNumber painting = exhibit.Painting.Number;
                if (exhibit.Painting.Standing == PaintingStanding.Whole)
                {
                    presses.Add(exhibit, new MuseumPress.OnThePage(() => BringForward(painting)));
                }
                else if (exhibit.Painting.PiecesWaiting > 0)
                {
                    presses.Add(exhibit, new MuseumPress.Restore(painting));
                }
            }

            // A painting that hangs forward takes every touch: it lies over all the page's controls, and is asked first.
            if (_viewer != null)
            {
                presses.Add(_viewer, new MuseumPress.OnThePage(PutBack));
            }
        }

        /// <summary>A painting that hangs forward goes back to the wall.</summary>
        public bool TryGoBack()
        {
            if (_viewer == null)
            {
                return false;
            }

            PutBack();
            return true;
        }

        /// <summary>True when a point of the screen, in the layout's points, lies on the plaque: a swipe from there turns the rooms, not the page.</summary>
        public bool IsOnThePlaque(Vector2 point) => _plaqueBox is Box plaque && plaque.Contains(point.x, point.y);

        /// <summary>
        /// A finger on the plaque drags the rooms: the shown room's paintings follow it, and the paintings of the room
        /// it drags in hang beside them, a screen's width away; beyond the first and the last room the paintings only
        /// stretch a little way. Rooms are told by their places in the row, room 1 at place 0.
        /// </summary>
        public void DragTheRooms(SwipeReading.Drag drag, float width)
        {
            RoomNumber? beside = drag.Beside is int place ? new RoomNumber(place + 1) : (RoomNumber?)null;
            if (_roomBeside?.Number != beside)
            {
                _roomBeside?.Dispose();
                _roomBeside = beside is RoomNumber room ? Hang(room) : null;
            }

            _room.SlideTo(drag.By);
            _roomBeside?.SlideTo(drag.By - Math.Sign(drag.By) * width);
        }

        /// <summary>
        /// The finger let go of the rooms: they go the rest of the way to the room the swipe settled on, which the
        /// plaque names at once, as a press of an arrow would; once the paintings stand still, <paramref name="settled"/> is called.
        /// </summary>
        public void SettleTheRooms(SwipeReading.Settle settle, float width, Action settled)
        {
            HungRoom shown = _room;
            HungRoom? beside = _roomBeside;
            _roomBeside = null;
            bool turns = beside != null && beside.Number.Value == settle.Place + 1;
            float from = settle.From;
            float side = from < 0f ? -1f : 1f;
            float to = turns ? side * width : 0f;
            HungRoom? leaving = turns ? shown : beside;
            if (turns && beside != null)
            {
                _room = beside;
                NameTheRoom();
            }

            _roomLeaving = leaving;
            _timeline.Add(
                0f,
                Tempo.PageSettlesIn((to - from) / width),
                progress =>
                {
                    float at = from + (to - from) * Easing.Out(progress);
                    shown.SlideTo(at);
                    beside?.SlideTo(at - side * width);
                },
                () =>
                {
                    leaving?.Dispose();
                    _roomLeaving = null;
                    settled();
                });
        }

        /// <summary>A drag of the rooms is given up: the shown room's paintings stand in their places at once, and the room beside is taken down.</summary>
        public void ForgetTheRoomsDrag()
        {
            _roomBeside?.Dispose();
            _roomBeside = null;
            _room.SlideTo(0f);
        }

        /// <summary>The middle of a painting of the shown room on the wall, in the layout's points: where a finger touches it.</summary>
        /// <exception cref="ArgumentException">The painting does not hang in the room that is shown.</exception>
        public Vector2 MiddleOf(PaintingNumber painting) => ExhibitOf(painting).Middle;

        public void Dispose()
        {
            _room.Dispose();
            _roomBeside?.Dispose();
            _roomLeaving?.Dispose();
            _viewer?.Dispose();
            _goingBack?.Dispose();
            _wall.Dispose();
            _trim.Dispose();
            _plaque.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>Shows another room's wall.</summary>
        private void TurnTo(RoomNumber room)
        {
            _room.Dispose();
            _room = Hang(room);
            NameTheRoom();
            if (_screen is ScreenShape screen)
            {
                Fit(screen);
            }
        }

        /// <summary>Hangs the paintings of a room, fitted to the screen once there is one.</summary>
        private HungRoom Hang(RoomNumber room)
        {
            var hung = new HungRoom(_root, _art, _paintings, RoomWall.Of(room, _visit.Progress, _paintings.Catalogue));
            if (_screen is ScreenShape screen)
            {
                hung.Fit(screen);
            }

            return hung;
        }

        /// <summary>Names the shown room on the plaque, and lights the round buttons that lead to a room before or after it.</summary>
        private void NameTheRoom()
        {
            _plaque.Show(_room.Wall, _visit.Progress.IsOpen(Room));
            _previous.LeadSomewhere(Room.Value > 1);
            _next.LeadSomewhere(Room.Value < RoomsToVisit);
        }

        /// <summary>A restored painting comes forward from the wall to be looked at by itself.</summary>
        private void BringForward(PaintingNumber painting)
        {
            ExhibitFrame exhibit = ExhibitOf(painting);
            if (_screen is not ScreenShape screen || exhibit.Picture is not Box onTheWall)
            {
                return;
            }

            PaintingViewer viewer = PaintingViewer.Create(_root, _art, _room.Looks[painting.PlaceInRoom - 1], onTheWall);
            viewer.Fit(screen, onTheWall);
            _viewer = viewer;
            _viewed = painting;
            _timeline.Add(0f, Tempo.PaintingComesForward, progress => viewer.ComeForward(Easing.Out(progress)));
        }

        /// <summary>The painting that hangs forward goes back to the wall; the page's own controls are the player's again at once.</summary>
        private void PutBack()
        {
            if (_viewer is not PaintingViewer viewer)
            {
                return;
            }

            _viewer = null;
            _goingBack = viewer;
            _timeline.Add(0f, Tempo.PaintingComesForward, progress => viewer.ComeForward(1f - Easing.InOut(progress)), () =>
            {
                viewer.Dispose();
                _goingBack = null;
            });
        }

        private ExhibitFrame ExhibitOf(PaintingNumber painting) => painting.Room == Room
            ? _room.Exhibits[painting.PlaceInRoom - 1]
            : throw new ArgumentException($"Painting {painting} hangs in room {painting.Room}; the collection shows room {Room}.", nameof(painting));

        private static void PlaceTheButton(DiscButton button, Box place) =>
            button.Place(new Vector2(place.MiddleX, place.MiddleY), place.Width / DiscButton.Size);

        /// <summary>
        /// The paintings of one room on the wall, under a root of their own that a swipe on the plaque slides
        /// sideways. Where a finger touches a painting does not move with them: nothing is pressed while they slide.
        /// </summary>
        private sealed class HungRoom : IDisposable
        {
            private readonly Transform _root;
            private readonly List<ExhibitFrame> _exhibits = new List<ExhibitFrame>();
            private readonly List<PaintingLook> _looks = new List<PaintingLook>();

            public HungRoom(Transform page, ArtCatalogue art, PaintingGallery paintings, RoomWall wall)
            {
                Wall = wall;
                _root = new GameObject($"Room {wall.Number.Value}").transform;
                _root.SetParent(page, worldPositionStays: false);
                foreach (HungPainting painting in wall.Paintings)
                {
                    PaintingLook look = paintings.LookOf(painting.Number);
                    _looks.Add(look);
                    _exhibits.Add(ExhibitFrame.Create(_root, art, look, painting));
                }
            }

            public RoomWall Wall { get; }

            public RoomNumber Number => Wall.Number;

            public IReadOnlyList<ExhibitFrame> Exhibits => _exhibits;

            public IReadOnlyList<PaintingLook> Looks => _looks;

            /// <summary>Hangs the paintings where the room's layout puts them on a screen; the layout is the page's too.</summary>
            public CollectionLayout Fit(ScreenShape screen)
            {
                CollectionLayout layout = CollectionLayout.Of(screen, _looks.Select(look => look.Aspect).ToArray());
                for (int painting = 0; painting < _exhibits.Count; painting++)
                {
                    _exhibits[painting].Place(layout.Exhibits[painting], layout.Page.View, layout.Page.RailBottom);
                }

                return layout;
            }

            /// <summary>Moves the paintings this far to the right of their places (less than nothing: to the left).</summary>
            public void SlideTo(float across) => _root.localPosition = new Vector3(across, 0f, 0f);

            public void Dispose()
            {
                foreach (ExhibitFrame exhibit in _exhibits)
                {
                    exhibit.Dispose();
                }

                _exhibits.Clear();
                _looks.Clear();
                SceneObjects.DestroyObjectOf(_root);
            }
        }
    }
}
