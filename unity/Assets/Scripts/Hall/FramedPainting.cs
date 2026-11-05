#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Canvases;
using Puzzle.Client.Flow;
using Puzzle.Client.Gallery;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using UnityEngine;

namespace Puzzle.Client.Hall
{
    /// <summary>
    /// A painting as it hangs on the design canvas in the hall (HomeTR), on the jigsaw's screen (JigsawTR) and in
    /// front of the dark when it is looked at by itself: its canvas in a gilt frame that hangs on two wires, a brass
    /// picture light over the frame, and under it the brass plate with the painting's title and its painter. Which
    /// pieces the canvas shows in place can change while it hangs. Sizes are in points.
    /// </summary>
    public sealed class FramedPainting : IDisposable
    {
        /// <summary>The frame's picture is the frame and the room around it for its shadow: this much wider and higher than the picture it frames.</summary>
        public const float FramePictureMargin = 2f * 50.625f;

        /// <summary>The wires meet the frame's top edge this far in from its corners.</summary>
        private const float WireInFromCorner = 50f;

        /// <summary>The knob of the picture light sits this far under the frame's top edge.</summary>
        private const float LightKnobInFrame = 4f;

        private readonly Transform _root;
        private readonly PaintingLook _look;
        private readonly JigsawCanvas _canvas;
        private readonly PaintedShape _wires;
        private readonly SpriteRenderer _frame;
        private readonly Transform _light;
        private readonly CaptionPlate _plate;

        /// <summary>What the canvas shows, as it was last told; and where it hangs, once it does.</summary>
        private IReadOnlyCollection<int> _piecesInPlace = Array.Empty<int>();
        private CanvasHelp _help;
        private FramePlace? _place;
        private float _softEdge = 1f;

        private FramedPainting(Transform parent, ArtCatalogue art, PaintingLook look, int raisedBy)
        {
            _look = look;
            _root = new GameObject("Painting").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _wires = PaintedShape.Create("Wires", _root, art.PaintMaterial, DrawOrder.Wire + raisedBy);
            _canvas = JigsawCanvas.Create(_root, art, look, raisedBy);

            _frame = SceneObjects.AddSprite("Frame", _root, art.Of(GalleryArt.PictureFrame), art.SpriteMaterial, DrawOrder.PictureFrame + raisedBy);
            _frame.drawMode = SpriteDrawMode.Tiled; // the middle of each side repeats, bead by bead; the corners keep their shape
            _frame.tileMode = SpriteTileMode.Adaptive; // ...and is stretched by less than half a bead to end on a whole one
            _light = SceneObjects.AddSprite("Picture light", _root, art.Of(GalleryArt.PictureLight), art.SpriteMaterial, DrawOrder.PictureLight + raisedBy).transform;
            _plate = new CaptionPlate(_root, art, raisedBy);
        }

        /// <summary>What everything of the painting hangs under: whoever lets the painting swell or come forward moves this.</summary>
        public Transform Root => _root;

        /// <summary>The painting's canvas, for the light that runs over it and the glow of a piece that drops in.</summary>
        public JigsawCanvas Canvas => _canvas;

        /// <summary>Builds a painting as a player's progress has it. Call <see cref="Place"/> before the first picture.</summary>
        /// <param name="look">The painting's picture and cut.</param>
        /// <param name="painting">The painting as the player's progress has it.</param>
        public static FramedPainting Create(Transform parent, ArtCatalogue art, PaintingLook look, HungPainting painting)
        {
            var framed = new FramedPainting(parent, art, look, raisedBy: 0);
            framed.Show(painting.PiecesInPlace);
            return framed;
        }

        /// <summary>Builds a painting with no piece in place yet; <see cref="Show"/> says which are. Call <see cref="Place"/> before the first picture.</summary>
        /// <param name="raisedBy">Added to the painting's places in the drawing order: <see cref="DrawOrder.OverTheDark"/> for one that hangs in front of the dark.</param>
        public static FramedPainting Create(Transform parent, ArtCatalogue art, PaintingLook look, int raisedBy = 0) =>
            new FramedPainting(parent, art, look, raisedBy);

        /// <summary>Says which pieces the canvas shows in place, and what it shows to help the player.</summary>
        public void Show(IReadOnlyCollection<int> piecesInPlace, CanvasHelp help = default)
        {
            _piecesInPlace = piecesInPlace;
            _help = help;
            DrawTheCanvas();
        }

        /// <summary>Hangs the painting at a place; call again whenever the screen changes.</summary>
        public void Place(FramePlace place, ScreenView view)
        {
            _place = place;
            _softEdge = 1f / view.PixelsPerPoint;
            Box picture = place.Picture;
            var middle = new Vector3(picture.MiddleX, picture.MiddleY, 0f);
            _canvas.Transform.localPosition = middle;
            DrawTheCanvas();

            _frame.transform.localPosition = middle;
            _frame.size = new Vector2(picture.Width + FramePictureMargin, picture.Height + FramePictureMargin);
            _light.localPosition = new Vector3(picture.MiddleX, place.Frame.Top - LightKnobInFrame, 0f);
            _wires.Show(Wires(place));
            _plate.Place(
                new Vector2(place.Frame.MiddleX, place.PlateMiddle), place.WidestPlate,
                MuseumWords.TitleOf(_look.Number, _look.Artwork), MuseumWords.ByLineOf(_look.Artwork));
        }

        public void Dispose()
        {
            _canvas.Dispose();
            _wires.Dispose();
            SceneObjects.DestroyObjectOf(_root);
        }

        private void DrawTheCanvas()
        {
            if (_place is FramePlace place)
            {
                _canvas.Show(_piecesInPlace, new Vector2(place.Picture.Width, place.Picture.Height), _softEdge, _help);
            }
        }

        /// <summary>The two wires from where the painting hangs down to the frame's top edge.</summary>
        private static Mesh Wires(FramePlace place)
        {
            var sketch = new MeshSketch();
            Box frame = place.Frame;
            float inFromCorner = Mathf.Min(WireInFromCorner, frame.Width / 4f);
            PictureWire.Add(sketch, frame.Left + inFromCorner, frame.Top, place.WiresFrom);
            PictureWire.Add(sketch, frame.Right - inFromCorner - PictureWire.Width, frame.Top, place.WiresFrom);
            return sketch.ToMesh("Painting wires");
        }
    }
}
