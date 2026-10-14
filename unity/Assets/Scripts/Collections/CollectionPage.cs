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
    /// names the room. The round buttons on the plaque turn to the rooms before and after; a restored painting
    /// comes forward to be looked at when it is touched; a painting whose pieces wait leads to its jigsaw.
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
        private readonly List<ExhibitFrame> _exhibits = new List<ExhibitFrame>();
        private readonly List<PaintingLook> _looks = new List<PaintingLook>();

        private ScreenShape? _screen;

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
            _plaque = CollectionPlaque.Create(
                _root, art, CollectionRooms.PaintingsWhole(visit.Progress, paintings.Catalogue), paintings.Catalogue.Rooms.Select(MuseumWords.NameOf).ToArray());
            _previous = new DiscButton("Room before", _root, art, DiscGlyph.Back, DrawOrder.PlaqueArt);
            _next = new DiscButton("Room after", _root, art, DiscGlyph.Forward, DrawOrder.PlaqueArt);
            Room = room;
            HangTheRoom();
        }

        public Transform Root => _root;

        /// <summary>The room whose wall is shown.</summary>
        public RoomNumber Room { get; private set; }

        /// <summary>The painting that is looked at by itself; nothing while the wall is shown.</summary>
        public PaintingNumber? Viewed => _viewer != null ? _viewed : null;

        /// <summary>The middles of the round buttons that turn the rooms, in the layout's points.</summary>
        public Vector2 PreviousRoomMiddle => _previous.Middle;

        public Vector2 NextRoomMiddle => _next.Middle;

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
            CollectionLayout layout = CollectionLayout.Of(screen, _looks.Select(look => look.Aspect).ToArray());
            ScreenView view = layout.Page.View;
            _wall.Fit(view);
            _trim.Show(layout.Page, floorLine: null);
            _plaque.Place(layout.Page);
            PlaceTheButton(_previous, layout.PreviousRoom);
            PlaceTheButton(_next, layout.NextRoom);
            for (int painting = 0; painting < _exhibits.Count; painting++)
            {
                _exhibits[painting].Place(layout.Exhibits[painting], view, layout.Page.RailBottom);
            }

            if (_viewer != null && _viewed is PaintingNumber viewed && ExhibitOf(viewed).Picture is Box onTheWall)
            {
                _viewer.Fit(screen, onTheWall);
            }
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            presses.Add(_previous, new MuseumPress.OnThePage(() => TurnTo(new RoomNumber(Room.Value - 1))));
            presses.Add(_next, new MuseumPress.OnThePage(() => TurnTo(new RoomNumber(Room.Value + 1))));
            foreach (ExhibitFrame exhibit in _exhibits)
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

        /// <summary>The middle of a painting of the shown room on the wall, in the layout's points: where a finger touches it.</summary>
        /// <exception cref="ArgumentException">The painting does not hang in the room that is shown.</exception>
        public Vector2 MiddleOf(PaintingNumber painting) => ExhibitOf(painting).Middle;

        public void Dispose()
        {
            TakeDownTheRoom();
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
            TakeDownTheRoom();
            Room = room;
            HangTheRoom();
            if (_screen is ScreenShape screen)
            {
                Fit(screen);
            }
        }

        /// <summary>Hangs the paintings of the room that is shown and names the room on the plaque.</summary>
        private void HangTheRoom()
        {
            RoomWall wall = RoomWall.Of(Room, _visit.Progress, _paintings.Catalogue);
            foreach (HungPainting painting in wall.Paintings)
            {
                PaintingLook look = _paintings.LookOf(painting.Number);
                _looks.Add(look);
                _exhibits.Add(ExhibitFrame.Create(_root, _art, look, painting));
            }

            _plaque.Show(wall, _visit.Progress.IsOpen(Room));
            _previous.LeadSomewhere(Room.Value > 1);
            _next.LeadSomewhere(Room.Value < CollectionRooms.LastToVisit(_visit.Progress, _paintings.Catalogue).Value);
        }

        private void TakeDownTheRoom()
        {
            foreach (ExhibitFrame exhibit in _exhibits)
            {
                exhibit.Dispose();
            }

            _exhibits.Clear();
            _looks.Clear();
        }

        /// <summary>A restored painting comes forward from the wall to be looked at by itself.</summary>
        private void BringForward(PaintingNumber painting)
        {
            ExhibitFrame exhibit = ExhibitOf(painting);
            if (_screen is not ScreenShape screen || exhibit.Picture is not Box onTheWall)
            {
                return;
            }

            PaintingViewer viewer = PaintingViewer.Create(_root, _art, _looks[painting.PlaceInRoom - 1], onTheWall);
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
            ? _exhibits[painting.PlaceInRoom - 1]
            : throw new ArgumentException($"Painting {painting} hangs in room {painting.Room}; the collection shows room {Room}.", nameof(painting));

        private static void PlaceTheButton(DiscButton button, Box place) =>
            button.Place(new Vector2(place.MiddleX, place.MiddleY), place.Width / DiscButton.Size);
    }
}
