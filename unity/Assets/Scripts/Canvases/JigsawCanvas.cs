#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Painting;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// What a canvas shows to help the player put pieces in their place, as the design canvas draws the jigsaw's
    /// screen (JigsawTR): the picture as a faint ghost under the linen, and the places that are asked for (those of
    /// the pieces in the tray) marked in a broken gilt line. A canvas that only hangs on a wall shows neither.
    /// </summary>
    public readonly struct CanvasHelp
    {
        private readonly IReadOnlyCollection<int>? _asked;

        /// <param name="asked">The numbers of the pieces whose places are marked.</param>
        public CanvasHelp(bool showsGhost, IReadOnlyCollection<int> asked)
        {
            ShowsGhost = showsGhost;
            _asked = asked;
        }

        /// <summary>No help: the canvas as it hangs in the hall.</summary>
        public static CanvasHelp None => default;

        public bool ShowsGhost { get; }

        public IReadOnlyCollection<int> Asked => _asked ?? Array.Empty<int>();
    }

    /// <summary>
    /// The light on a piece that has just dropped into its place: its edge lit, its face washed with white. It is
    /// painted once; whoever dropped the piece lets it fade and takes it away.
    /// </summary>
    public sealed class PieceGlow : IDisposable
    {
        private readonly PaintedShape _shape;

        internal PieceGlow(PaintedShape shape)
        {
            _shape = shape;
        }

        /// <summary>How strong the light is: 1 as the piece drops in, 0 once it is gone.</summary>
        public void Show(float strength) => _shape.Fade(strength);

        public void Dispose() => _shape.Dispose();
    }

    /// <summary>
    /// A painting on its canvas while it is being restored, as the design canvas draws it in the hall's frame
    /// (HomeTR) and on the jigsaw's screen (JigsawTR): bare linen where pieces are missing, the picture where they
    /// are in place, and the lines of the cut between them. A painting whose every piece is in place is the picture
    /// alone: no line is left on it. The canvas is drawn around its own middle, at whatever size it is shown.
    /// </summary>
    public sealed class JigsawCanvas : IDisposable
    {
        /// <summary>Bare linen: the design's cream ground under its warm veil; over a ghost of the picture it lets a sixth of it through.</summary>
        private static readonly Color Linen = Tint.Of(0xDDCDAA);
        private const float LinenOverTheGhost = 0.84f;

        /// <summary>A piece in place on a canvas that has no picture: primed, a tone darker than the linen.</summary>
        private static readonly Color PrimedFace = Tint.Of(0xC9B78E);

        /// <summary>Between two pieces in place the cut is a hairline with a light edge beside it, as between two tiles laid close.</summary>
        private static readonly Color SeamLine = Tint.Of(0x2A1606, 0.42f);
        private static readonly Color SeamLight = Tint.Of(0xFFFFFF, 0.3f);
        private static readonly Vector2 SeamLightOffset = new Vector2(0.7f, -0.9f);
        private const float SeamWidth = 0.7f;

        /// <summary>Where a piece in place meets bare linen its edge is drawn stronger: the piece lies on the canvas.</summary>
        private static readonly Color EdgeLine = Tint.Of(0x3A2206, 0.6f);
        private const float EdgeWidth = 1.1f;

        /// <summary>Between two missing pieces the cut is only marked, in dashes.</summary>
        private static readonly Color MarkedLine = Tint.Of(0x7A6238, 0.7f);
        private const float MarkedWidth = 0.7f;
        private const float MarkedDash = 3f;
        private const float MarkedGap = 2.6f;

        /// <summary>A place that is asked for: a wash of light yellow in a broken gilt line on a darker one.</summary>
        private static readonly Color AskedWash = Tint.Of(0xFFE27A, 0.34f);
        private static readonly Color AskedLine = Tint.Of(0xFFD660);
        private static readonly Color AskedLineShade = Tint.Of(0x7A4A12, 0.7f);
        private const float AskedLineWidth = 1.7f;
        private const float AskedShadeWidth = 2.9f;
        private const float AskedDash = 5.5f;
        private const float AskedGap = 3.7f;

        /// <summary>The light that runs over a painting just restored: a slanted band of white, this wide, as strong as this in its middle.</summary>
        private const float LightBandShare = 0.34f;
        private const float LightStrength = 0.6f;
        private const float LightSlant = 0.45f;
        private const int LightColumns = 28;
        private const int LightRows = 6;

        /// <summary>A piece that has just dropped in: its edge lit, its face washed with white.</summary>
        private const float GlowLineWidth = 2.4f;
        private const float GlowLineStrength = 0.95f;
        private const float GlowWashStrength = 0.4f;

        private readonly PaintingLook _look;
        private readonly PaintedShape _ghost;
        private readonly PaintedShape _ground;
        private readonly PaintedShape _marks;
        private readonly PaintedShape _faces;
        private readonly PaintedShape _lines;
        private readonly PaintedShape _light;
        private readonly Material _paint;
        private readonly DrawOrder _shineOrder;

        /// <summary>The size and the pixel's width the canvas was last drawn at.</summary>
        private Vector2 _size;
        private float _softEdge;

        /// <summary>
        /// The net the running light is painted on, for the size the canvas has: its knots stay where they are
        /// and only their colours follow the light. Nothing until a light is first shown at that size.
        /// </summary>
        private Mesh? _lightNet;
        private Vector3[] _lightKnots = Array.Empty<Vector3>();
        private Color32[] _lightColours = Array.Empty<Color32>();

        /// <param name="raisedBy">Added to the canvas' places in the drawing order: <see cref="DrawOrder.OverTheDark"/> for a canvas in front of the dark.</param>
        private JigsawCanvas(Transform parent, ArtCatalogue art, PaintingLook look, int raisedBy)
        {
            _look = look;
            _paint = art.PaintMaterial;
            _shineOrder = DrawOrder.CanvasShine + raisedBy;
            Transform = new GameObject("Canvas").transform;
            Transform.SetParent(parent, worldPositionStays: false);
            _ghost = PaintedShape.Create("Ghost of the picture", Transform, art.PaintMaterial, DrawOrder.CanvasGhost + raisedBy, look.Picture);
            _ground = PaintedShape.Create("Linen", Transform, art.PaintMaterial, DrawOrder.Canvas + raisedBy);
            _marks = PaintedShape.Create("Places asked for", Transform, art.PaintMaterial, DrawOrder.CanvasMark + raisedBy);
            _faces = PaintedShape.Create("Pieces in place", Transform, art.PaintMaterial, DrawOrder.CanvasPicture + raisedBy, look.Picture);
            _lines = PaintedShape.Create("Cut lines", Transform, art.PaintMaterial, DrawOrder.CanvasLines + raisedBy);
            _light = PaintedShape.Create("Light", Transform, art.PaintMaterial, _shineOrder);
        }

        /// <summary>The canvas' place is its middle.</summary>
        public Transform Transform { get; }

        /// <summary>Builds the canvas of a painting. Call <see cref="Show"/> before the first picture.</summary>
        /// <param name="raisedBy">Added to the canvas' places in the drawing order; nothing for a canvas that hangs in the lit room.</param>
        public static JigsawCanvas Create(Transform parent, ArtCatalogue art, PaintingLook look, int raisedBy = 0) =>
            new JigsawCanvas(parent, art, look, raisedBy);

        /// <summary>Draws the canvas at a size with some of its pieces in place.</summary>
        /// <param name="piecesInPlace">The numbers of the pieces that are in place.</param>
        /// <param name="softEdge">The width of a pixel, in the canvas' own units: lines fade out over it.</param>
        /// <param name="help">What the canvas shows to help the player; nothing on a canvas that only hangs.</param>
        public void Show(IReadOnlyCollection<int> piecesInPlace, Vector2 size, float softEdge, CanvasHelp help = default)
        {
            if (size != _size)
            {
                ShowLight(0f);
                _lightNet = null; // the light's shape has let go of it; a net for the new size is made when a light next runs
            }

            _size = size;
            _softEdge = softEdge;
            JigsawCut cut = _look.CutAt(size);
            var inPlace = new HashSet<int>(piecesInPlace);
            bool isWhole = inPlace.Count == cut.Pieces.Count;
            bool hasGhost = help.ShowsGhost && _look.Picture != null && !isWhole;

            _ghost.Show(hasGhost ? WholePicture(size) : Nothing("No ghost"));
            var ground = new MeshSketch();
            ground.AddRectangle(new Rect(-size / 2f, size), hasGhost ? new Color(Linen.r, Linen.g, Linen.b, LinenOverTheGhost) : Linen);
            _ground.Show(ground.ToMesh("Linen"));

            _faces.Show(FacesInPlace(cut, inPlace, size));
            _lines.Show(isWhole ? Nothing("No cut lines") : CutLines(cut, inPlace, size, softEdge));
            _marks.Show(Marks(cut, help.Asked, inPlace, size, softEdge));
        }

        /// <summary>
        /// The light that runs over a painting just restored, as far as it has come: 0 is before it enters at the
        /// left, 1 after it has left at the right; at both ends nothing of it is seen.
        /// </summary>
        public void ShowLight(float across)
        {
            bool isSeen = across > 0f && across < 1f;
            _light.Transform.gameObject.SetActive(isSeen);
            if (!isSeen)
            {
                return;
            }

            Mesh net = _lightNet ??= StretchTheLightNet();
            float band = LightBandShare * _size.x;
            float reach = _size.x / 2f + band + LightSlant * _size.y / 2f;
            float middle = Mathf.Lerp(-reach, reach, across);
            for (int knot = 0; knot < _lightKnots.Length; knot++)
            {
                float fromBand = Mathf.Abs(_lightKnots[knot].x + LightSlant * _lightKnots[knot].y - middle) / band;
                _lightColours[knot] = new Color(1f, 1f, 1f, LightStrength * Mathf.Clamp01(1f - fromBand));
            }

            net.colors32 = _lightColours;
        }

        /// <summary>
        /// Lights the edge and the face of a piece in its place. The light is painted once, at its full strength;
        /// the caller fades it (<see cref="PieceGlow.Show"/>) and takes it away when it is gone. Several pieces
        /// may be lit at once, each by a light of its own.
        /// </summary>
        public PieceGlow LightUp(int pieceNumber)
        {
            JigsawPiece piece = _look.CutAt(_size).Pieces[pieceNumber];
            var sketch = new MeshSketch();
            JigsawShapes.AddFace(sketch, piece, _size, Vector2.zero, new Color(1f, 1f, 1f, GlowWashStrength));
            Stroke.Add(sketch, JigsawShapes.OutlineOf(piece, _size), isClosed: true, GlowLineWidth, _softEdge, new Color(1f, 1f, 1f, GlowLineStrength));
            PaintedShape shape = PaintedShape.Create($"Glow of piece {pieceNumber}", Transform, _paint, _shineOrder);
            shape.Show(sketch.ToMesh("Glow"));
            return new PieceGlow(shape);
        }

        public void Dispose()
        {
            _ghost.Dispose();
            _ground.Dispose();
            _marks.Dispose();
            _faces.Dispose();
            _lines.Dispose();
            _light.Dispose();
            SceneObjects.DestroyObjectOf(Transform);
        }

        private static Mesh Nothing(string name) => new MeshSketch().ToMesh(name);

        /// <summary>
        /// Stretches a net over the canvas for the running light and gives it to the light's shape, which owns it
        /// from then on. The knots are remembered, so each step of the light only paints them anew.
        /// </summary>
        private Mesh StretchTheLightNet()
        {
            var sketch = new MeshSketch();
            sketch.AddNet(new Rect(-_size / 2f, _size), LightColumns, LightRows, _ => Color.clear);
            Mesh net = sketch.ToMesh("Light");
            net.MarkDynamic(); // its colours change with every step of the light
            _lightKnots = net.vertices;
            _lightColours = new Color32[_lightKnots.Length];
            _light.Show(net);
            return net;
        }

        /// <summary>The whole picture as one face.</summary>
        private static Mesh WholePicture(Vector2 size)
        {
            var sketch = new MeshSketch();
            sketch.AddRectangle(new Rect(-size / 2f, size), Color.white, Color.white, new Rect(0f, 0f, 1f, 1f));
            return sketch.ToMesh("Ghost of the picture");
        }

        private Mesh FacesInPlace(JigsawCut cut, HashSet<int> inPlace, Vector2 size)
        {
            Color32 face = _look.Picture != null ? Color.white : PrimedFace;
            var faces = new MeshSketch();
            foreach (JigsawPiece piece in cut.Pieces)
            {
                if (inPlace.Contains(piece.Number))
                {
                    JigsawShapes.AddFace(faces, piece, size, Vector2.zero, face);
                }
            }

            return faces.ToMesh("Pieces in place");
        }

        /// <summary>The places that are asked for and still empty: a wash over each, and its outline in a broken gilt line.</summary>
        private static Mesh Marks(JigsawCut cut, IReadOnlyCollection<int> asked, HashSet<int> inPlace, Vector2 size, float softEdge)
        {
            var sketch = new MeshSketch();
            foreach (int number in asked)
            {
                if (inPlace.Contains(number))
                {
                    continue;
                }

                JigsawPiece piece = cut.Pieces[number];
                JigsawShapes.AddFace(sketch, piece, size, Vector2.zero, AskedWash);
                List<Vector2> outline = JigsawShapes.OutlineOf(piece, size);
                outline.Add(outline[0]); // a broken line is drawn along an open row of points: it comes back to its start
                Stroke.AddDashed(sketch, outline, AskedShadeWidth, softEdge, AskedLineShade, AskedDash, AskedGap);
                Stroke.AddDashed(sketch, outline, AskedLineWidth, softEdge, AskedLine, AskedDash, AskedGap);
            }

            return sketch.ToMesh("Places asked for");
        }

        /// <summary>Every stretch of the cut once, drawn as what lies at its two sides asks.</summary>
        private static Mesh CutLines(JigsawCut cut, HashSet<int> inPlace, Vector2 size, float softEdge)
        {
            var sketch = new MeshSketch();
            foreach (JigsawPiece piece in cut.Pieces)
            {
                foreach (PieceEdge edge in piece.Edges)
                {
                    // Two neighbours share a stretch of the cut; the one with the lower number draws it.
                    if (edge.Neighbour is int neighbour && neighbour > piece.Number)
                    {
                        List<Vector2> line = JigsawShapes.LineOf(edge.Path, size);
                        AddCutLine(sketch, line, inPlace.Contains(piece.Number), inPlace.Contains(neighbour), softEdge);
                    }
                }
            }

            return sketch.ToMesh("Cut lines");
        }

        private static void AddCutLine(MeshSketch sketch, List<Vector2> line, bool isOneInPlace, bool isOtherInPlace, float softEdge)
        {
            if (isOneInPlace && isOtherInPlace)
            {
                Stroke.Add(sketch, Moved(line, SeamLightOffset), isClosed: false, SeamWidth, softEdge, SeamLight);
                Stroke.Add(sketch, line, isClosed: false, SeamWidth, softEdge, SeamLine);
            }
            else if (isOneInPlace || isOtherInPlace)
            {
                Stroke.Add(sketch, line, isClosed: false, EdgeWidth, softEdge, EdgeLine);
            }
            else
            {
                Stroke.AddDashed(sketch, line, MarkedWidth, softEdge, MarkedLine, MarkedDash, MarkedGap);
            }
        }

        private static List<Vector2> Moved(List<Vector2> line, Vector2 by)
        {
            var moved = new List<Vector2>(line.Count);
            foreach (Vector2 point in line)
            {
                moved.Add(point + by);
            }

            return moved;
        }
    }
}
