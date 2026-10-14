#nullable enable
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Hall;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Client.Screens;
using UnityEngine;

namespace Puzzle.Client.Maps
{
    /// <summary>
    /// The museum's map, as the design canvas draws it (MapTR): under the plaque that sums the museum up hangs its
    /// floor plan, a sheet in the gilt frame the paintings have, on two wires from the picture rail. The page shows
    /// where the player stands; nothing on it is pressed.
    /// </summary>
    /// <remarks>
    /// Over the plaque the museum's stage shows the player's coins and lives (<see cref="WalletRow"/>). Left out of
    /// the design, because the game has nothing behind them yet: the wallet row's plus buttons and the settings'
    /// button. The design's rooms are also buttons that lead into a room; the game has one hall,
    /// the room the player is restoring, so the rooms here are not pressed.
    /// </remarks>
    public sealed class MapPage : IMuseumPage
    {
        /// <summary>The wires meet the frame's top edge this far in from its corners.</summary>
        private const float WireInFromCorner = 42f;

        private readonly Transform _root;
        private readonly MuseumVisit _visit;
        private readonly Wall _wall;
        private readonly RoomTrim _trim;
        private readonly MapPlaque _plaque;
        private readonly PaintedShape _wires;
        private readonly FloorPlanSheet _plan;
        private readonly SpriteRenderer _frame;

        private MapPage(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit)
        {
            _visit = visit;
            _root = new GameObject("Map").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wall = Wall.Create(_root, art);
            _trim = RoomTrim.Create(_root, art);
            _plaque = MapPlaque.Create(_root, art, visit.Room);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire);
            _plan = FloorPlanSheet.Create(_root, art, paintings, visit);
            _frame = SceneObjects.AddSprite("Frame", _root, art.Of(GalleryArt.PictureFrame), art.SpriteMaterial, DrawOrder.PictureFrame);
            _frame.drawMode = SpriteDrawMode.Tiled; // the middle of each side repeats, bead by bead; the corners keep their shape
            _frame.tileMode = SpriteTileMode.Adaptive; // ...and is stretched by less than half a bead to end on a whole one
        }

        public Transform Root => _root;

        public static MapPage Create(Transform parent, ArtCatalogue art, PaintingGallery paintings, MuseumVisit visit) =>
            new MapPage(parent, art, paintings, visit);

        public void Fit(ScreenShape screen)
        {
            MapLayout layout = MapLayout.Of(screen, _visit.Room);
            _wall.Fit(layout.Page.View);
            _trim.Show(layout.Page, floorLine: null);
            _plaque.Place(layout.Page);
            _plan.Show(layout);
            _frame.transform.localPosition = new Vector3(layout.Sheet.MiddleX, layout.Sheet.MiddleY, 0f);
            _frame.size = new Vector2(layout.Sheet.Width + FramedPainting.FramePictureMargin, layout.Sheet.Height + FramedPainting.FramePictureMargin);
            _wires.Show(Wires(layout));
        }

        /// <summary>Nothing on the map is pressed.</summary>
        public void AddPressables(PressSet<MuseumPress> presses)
        {
        }

        /// <summary>The map has nothing of its own to go back from.</summary>
        public bool TryGoBack() => false;

        public void Dispose()
        {
            _wall.Dispose();
            _trim.Dispose();
            _plaque.Dispose();
            _wires.Dispose();
            _plan.Dispose();
            SceneObjects.DestroyObjectOf(_root);
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
