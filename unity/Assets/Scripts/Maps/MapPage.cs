#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Play;
using Puzzle.Client.Screens;
using Puzzle.Museum;
using Puzzle.Museum.Places;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The museum's map as a world map (#4), as the design canvas draws it (MapWorldTR): under the plaque, in the
    /// gilt frame the paintings have, the world in the look of an old museum map, each painting's pin where it was
    /// made or the place it shows. It opens on the whole world and glides to the player's era. Over the map stand
    /// the legend, the globe and the target, the compass, and across its foot the strip of the twenty eras; a tap on
    /// an era takes the camera there, a tap on a pin opens its painting's card over the strip. An era still ahead
    /// shows its places, titles and painters, its pictures veiled.
    /// </summary>
    /// <remarks>
    /// The page has a clock of its own: the stage tells it how long it has been shown (<see cref="Move"/>), and every
    /// glide, fade and scroll is a function of that clock, so the Editor pictures any moment of it. The camera's
    /// moves cost the map's drawing a few numbers (<see cref="WorldMapDrawing"/>); the pins, dots and labels are built
    /// anew for a view once the camera has come to rest there. The way back closes the card; without one it leads
    /// to the hall. A sideways drag on the strip scrolls the strip (the stage asks <see cref="IsOnTheStrip"/>);
    /// anywhere else it turns the pages, and nothing swipes while the card is open.
    /// </remarks>
    public sealed class MapPage : IMuseumPage, IMovingPage
    {
        /// <summary>The wires meet the frame's top edge this far in from its corners.</summary>
        private const float WireInFromCorner = 42f;

        private readonly Transform _root;
        private readonly ArtCatalogue _art;
        private readonly MapAtlas _atlas;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly MapPlaque _plaque;
        private readonly PaintedShape _wires;
        private readonly SpriteRenderer _frame;
        private readonly WorldMapDrawing _drawing;
        private readonly MapControls _controls;
        private readonly EraStrip _strip;
        private readonly MapButton[] _pins = new MapButton[MuseumPlan.PaintingsPerRoom];

        private MapLayout? _layout;
        private MapOverlay? _overlay;
        private MapCard? _card;

        private MapView _view = MapView.World;
        private PaintingNumber? _chosen;
        private CameraGlide _glide = CameraGlide.StillAt(new MapFrame(0f, 0f, 1f));

        /// <summary>The page's clock: how long the page has been the one shown, as the stage last told it.</summary>
        private float _now;
        private bool _hasOpened;

        /// <summary>The view the overlay that stands was built for; nothing while none stands.</summary>
        private MapView? _overlaidView;

        /// <summary>When on the page's clock the overlay began to fade in, or out while the camera leaves it.</summary>
        private float _overlayFadesInSince;
        private float? _overlayFadesOutSince;

        private MapPage(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit)
        {
            _art = art;
            _atlas = MapAtlas.Of(paintings, visit);
            _root = new GameObject("Map").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wall = Wall.Create(_root, art);
            _trim = RoomTrim.Create(_root, art);
            _plaque = MapPlaque.Create(_root, art);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);
            _drawing = new WorldMapDrawing(_root, art);
            _controls = new MapControls(_root, art);
            _strip = new EraStrip(_root, art, _atlas);
            for (int pin = 0; pin < _pins.Length; pin++)
            {
                _pins[pin] = new MapButton(string.Empty, drawing: null, point => _card?.Covers(point) ?? false) { IsShown = false };
            }

            _frame = SceneObjects.AddSprite("Frame", _root, art.Of(GalleryArt.PictureFrame), art.SpriteMaterial, DrawOrder.PictureFrame);
            _frame.drawMode = SpriteDrawMode.Tiled; // the middle of each side repeats, bead by bead; the corners keep their shape
            _frame.tileMode = SpriteTileMode.Adaptive; // ...and is stretched by less than half a bead to end on a whole one
            _plaque.Show(_view, _atlas);
            _strip.Choose(null);
            _controls.ShowTheWorld(true);
        }

        public Transform Root => _root;

        /// <summary>What the map shows: the whole world or an era.</summary>
        public MapView View => _view;

        /// <summary>The painting whose card is open; nothing while none is.</summary>
        public PaintingNumber? CardOf => _card?.Painting;

        /// <summary>What the open card's pill says; nothing while no card is open.</summary>
        public string? CardSays => _card?.Status;

        /// <summary>What the plaque says, for the walk's checks.</summary>
        public string PlaqueSays => _plaque.Says;

        /// <summary>True while the camera glides, or the pins of the view it came to have not faded in yet.</summary>
        public bool IsMoving => !_glide.IsOverAt(_now) || _overlaidView != _view || _now - _overlayFadesInSince < Tempo.MapPinsFade || _strip.Scroll.IsMovingAt(_now);

        /// <summary>True while a card is open: nothing on the page swipes then.</summary>
        public bool ShowsACard => _card != null;

        /// <summary>Every button of the page that a finger can press now, with what it says: for the walk's checks of the labels and the targets.</summary>
        public IReadOnlyList<MapButton> ButtonsShown
        {
            get
            {
                var shown = new List<MapButton>();
                foreach (MapButton button in _strip.Buttons)
                {
                    if (button.IsShown)
                    {
                        shown.Add(button);
                    }
                }

                shown.AddRange(Array.FindAll(_pins, button => button.IsShown));
                shown.Add(_controls.Globe);
                shown.Add(_controls.Target);
                if (_card != null)
                {
                    foreach ((_, MapButton button) in _card.Buttons)
                    {
                        shown.Add(button);
                    }
                }

                return shown;
            }
        }

        /// <exception cref="PlacesFormatException">The game carries no places, or a painting of its catalogue has none.</exception>
        public static MapPage Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit) =>
            new MapPage(parent, art, paintings, visit);

        public void Fit(ScreenShape screen)
        {
            MapLayout layout = MapLayout.Of(screen);
            _layout = layout;
            _wall.Fit(layout.Page.View);
            _trim.Show(layout.Page, floorLine: null);
            _plaque.Place(layout.Page);
            _frame.transform.localPosition = new Vector3(layout.Sheet.MiddleX, layout.Sheet.MiddleY, 0f);
            _frame.size = new Vector2(layout.Sheet.Width + FramedPainting.FramePictureMargin, layout.Sheet.Height + FramedPainting.FramePictureMargin);
            _wires.Show(Wires(layout));
            _drawing.Fit(layout.Sheet);
            _controls.Fit(layout);
            _strip.Fit(layout.Strip);
            if (_view.Era is RoomNumber era)
            {
                _strip.Scroll.ScrollTo(era.Value, _now, duration: 0f);
            }

            // A screen of another shape frames the view at once, as it is at rest.
            _glide = CameraGlide.StillAt(FrameOf(_view, layout));
            _drawing.Show(_glide.To);
            BuildTheOverlay(layout);
            _overlayFadesInSince = _now - Tempo.MapPinsFade;
            _overlay?.Fading.Show(1f);
            ShowTheCard();
            _strip.ScrollAt(_now);
        }

        public void AddPressables(PressSet<MuseumPress> presses)
        {
            foreach ((MapButton button, int era) in WithEras(_strip.Buttons))
            {
                var room = new RoomNumber(era);
                presses.Add(button, new MuseumPress.OnThePage(() => GoTo(MapView.Of(room), chosen: null)));
            }

            for (int pin = 0; pin < _pins.Length; pin++)
            {
                int place = pin + 1;
                presses.Add(_pins[pin], new MuseumPress.OnThePage(() => ChooseThePin(place)));
            }

            presses.Add(_controls.Globe, new MuseumPress.OnThePage(() => GoTo(MapView.World, chosen: null)));
            presses.Add(_controls.Target, new MuseumPress.OnThePage(GoToThePlayer));
            if (_card != null)
            {
                foreach ((MapCardPress press, MapButton button) in _card.Buttons)
                {
                    presses.Add(button, PressOf(press));
                }
            }
        }

        /// <summary>The way back closes the card; without one, the map has nothing of its own to go back from.</summary>
        public bool TryGoBack()
        {
            if (_card == null)
            {
                return false;
            }

            Choose(null);
            return true;
        }

        public void Move(float secondsOnThePage)
        {
            _now = secondsOnThePage;
            if (!_hasOpened && _now >= Tempo.MapOpensAfter)
            {
                _hasOpened = true;
                if (_view.IsWorld && _card == null)
                {
                    GoTo(MapView.Of(_atlas.Standing.Room), chosen: null);
                }
            }

            ShowTheMoment();
        }

        /// <summary>Whether a point of the page lies on the strip of eras, where a sideways drag scrolls the strip.</summary>
        public bool IsOnTheStrip(Vector2 point) => _strip.Box.Contains(point.x, point.y);

        /// <summary>A finger came down on the strip, at a place across.</summary>
        public void HoldTheStrip(float fingerX) => _strip.Scroll.Hold(fingerX, _now);

        /// <summary>The finger that holds the strip moved sideways.</summary>
        public void DragTheStrip(float fingerX)
        {
            _strip.Scroll.DragTo(fingerX, _now);
            _strip.ScrollAt(_now);
        }

        /// <summary>The finger let go of the strip: it runs on with the flick and settles within its ends.</summary>
        public void LetGoOfTheStrip() => _strip.Scroll.LetGo(_now, Tempo.MapStripScrolls);

        /// <summary>The touch on the strip ended without a lift the game saw: the strip stands within its ends at once.</summary>
        public void ForgetTheStripDrag()
        {
            _strip.Scroll.Forget();
            _strip.ScrollAt(_now);
        }

        /// <summary>The middle of an era's medallion, of a pin, of the globe or the target, or of a button of the card; nothing for one not shown.</summary>
        public Vector2? MiddleOfEra(RoomNumber era) => Middle(_strip.Buttons[era.Value - 1]);

        public Vector2? MiddleOfPin(PaintingNumber painting) => _view.Era == painting.Room ? Middle(_pins[painting.PlaceInRoom - 1]) : null;

        public Vector2? MiddleOfTheGlobe => Middle(_controls.Globe);

        public Vector2? MiddleOfTheTarget => Middle(_controls.Target);

        public Vector2? MiddleOnTheCard(MapCardPress press)
        {
            if (_card != null)
            {
                foreach ((MapCardPress pressed, MapButton button) in _card.Buttons)
                {
                    if (pressed == press)
                    {
                        return Middle(button);
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            _overlay?.Dispose();
            _card?.Dispose();
            _strip.Dispose();
            _controls.Dispose();
            _drawing.Dispose();
            _wall.Dispose();
            _trim.Dispose();
            _plaque.Dispose();
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>The camera goes to a view, the card of a painting open or none; the plaque and the strip say it at once.</summary>
        private void GoTo(MapView view, PaintingNumber? chosen)
        {
            if (!(_layout is MapLayout layout))
            {
                return;
            }

            _hasOpened = true;
            if (view == _view)
            {
                Choose(chosen);
                if (view.Era is RoomNumber here)
                {
                    _strip.Scroll.ScrollTo(here.Value, _now, Tempo.MapStripScrolls);
                }

                return;
            }

            MapFrame from = _glide.At(_now);
            _view = view;
            _glide = new CameraGlide(from, FrameOf(view, layout), _now, Tempo.MapGlides);
            if (_overlay != null && _overlayFadesOutSince == null)
            {
                _overlayFadesOutSince = _now;
            }

            HideThePins();
            _plaque.Show(view, _atlas);
            _strip.Choose(view.Era);
            _controls.ShowTheWorld(view.IsWorld);
            if (view.Era is RoomNumber era)
            {
                _strip.Scroll.ScrollTo(era.Value, _now, Tempo.MapStripScrolls);
            }

            _chosen = chosen;
            ShowTheCard();
        }

        private void GoToThePlayer() => GoTo(MapView.Of(_atlas.Standing.Room), _atlas.Standing.Here);

        /// <summary>A pin was pressed: its painting's card opens, or closes when it was the one open.</summary>
        private void ChooseThePin(int placeInRoom)
        {
            if (_view.Era is RoomNumber era)
            {
                var painting = new PaintingNumber((era.Value - 1) * MuseumPlan.PaintingsPerRoom + placeInRoom);
                Choose(painting == _chosen ? (PaintingNumber?)null : painting);
            }
        }

        /// <summary>Opens a painting's card, or closes the card; the pins show which is chosen at once.</summary>
        private void Choose(PaintingNumber? painting)
        {
            _chosen = painting;
            ShowTheCard();
            if (_layout is MapLayout layout && _glide.IsOverAt(_now) && _overlaidView == _view)
            {
                BuildTheOverlay(layout);
                _overlayFadesInSince = _now - Tempo.MapPinsFade; // already in full: nothing moved
                _overlay?.Fading.Show(1f);
            }
        }

        private void ShowTheCard()
        {
            _card?.Dispose();
            _card = null;
            if (_chosen is PaintingNumber painting && _layout is MapLayout layout && _view.Era == painting.Room && _atlas.ArtworkAt(painting) != null)
            {
                _card = MapCard.Show(_root, _art, _atlas, painting, layout.Sheet);
            }
        }

        /// <summary>The map at the page's moment: where the camera is, how strongly the pins show, where the strip is scrolled.</summary>
        private void ShowTheMoment()
        {
            if (!(_layout is MapLayout layout))
            {
                return;
            }

            _drawing.Show(_glide.At(_now));
            if (_overlayFadesOutSince is float since)
            {
                float left = 1f - (_now - since) / Tempo.MapPinsFade;
                _overlay?.Fading.Show(Mathf.Clamp01(left));
            }

            if (_glide.IsOverAt(_now) && _overlaidView != _view)
            {
                BuildTheOverlay(layout);
                _overlayFadesInSince = _now;
            }
            else if (_overlayFadesOutSince == null && _overlay != null)
            {
                _overlay.Fading.Show(Mathf.Clamp01((_now - _overlayFadesInSince) / Tempo.MapPinsFade));
            }

            _strip.ScrollAt(_now);
        }

        private void BuildTheOverlay(MapLayout layout)
        {
            _overlay?.Dispose();
            HideThePins();
            _overlay = MapOverlay.Build(_root, _art, _atlas, layout, _view, _glide.To, _chosen, _pins);
            _overlaidView = _view;
            _overlayFadesOutSince = null;
            _overlay.Fading.Show(0f);
        }

        private void HideThePins()
        {
            foreach (MapButton pin in _pins)
            {
                pin.IsShown = false;
            }
        }

        private MapFrame FrameOf(MapView view, MapLayout layout)
        {
            if (view.Era is RoomNumber era)
            {
                var places = new List<MapPoint>();
                foreach ((_, MapPoint place) in _atlas.PlacesIn(era))
                {
                    places.Add(place);
                }

                if (places.Count > 0)
                {
                    return MapCamera.OfAnEra(places, layout.MapWidth, layout.MapHeight);
                }
            }

            return MapCamera.OfTheWorld(layout.MapWidth, layout.MapHeight);
        }

        private MuseumPress PressOf(MapCardPress press)
        {
            switch (press)
            {
                case MapCardPress.Close:
                    return new MuseumPress.OnThePage(() => Choose(null));
                case MapCardPress.Previous:
                    return new MuseumPress.OnThePage(() => Step(-1));
                case MapCardPress.Next:
                    return new MuseumPress.OnThePage(() => Step(1));
                case MapCardPress.Play:
                    return new MuseumPress.Play();
                case MapCardPress.SeeInTheCollection:
                    return new MuseumPress.VisitTheCollection(_card?.Painting.Room ?? _atlas.Standing.Room);
                default:
                    throw new ArgumentOutOfRangeException(nameof(press), press, "A press of the card that leads nowhere.");
            }
        }

        /// <summary>The card steps to the era's painting before or after, round from the last to the first.</summary>
        private void Step(int by)
        {
            if (_chosen is PaintingNumber painting)
            {
                int place = (painting.PlaceInRoom - 1 + by + MuseumPlan.PaintingsPerRoom) % MuseumPlan.PaintingsPerRoom;
                Choose(new PaintingNumber((painting.Room.Value - 1) * MuseumPlan.PaintingsPerRoom + place + 1));
            }
        }

        private static Vector2? Middle(MapButton button) =>
            button.IsShown ? new Vector2(button.Target.MiddleX, button.Target.MiddleY) : (Vector2?)null;

        private static IEnumerable<(MapButton Button, int Era)> WithEras(IReadOnlyList<MapButton> buttons)
        {
            for (int era = 1; era <= buttons.Count; era++)
            {
                yield return (buttons[era - 1], era);
            }
        }

        /// <summary>The two wires from the picture rail down to the frame's top edge.</summary>
        private static Mesh Wires(MapLayout layout)
        {
            var sketch = new MeshSketch();
            Box frame = layout.Frame;
            PictureWire.Add(sketch, frame.Left + WireInFromCorner, frame.Top, layout.Page.RailBottom);
            PictureWire.Add(sketch, frame.Right - WireInFromCorner - PictureWire.Width, frame.Top, layout.Page.RailBottom);
            return sketch.ToMesh("Map wires");
        }
    }
}
