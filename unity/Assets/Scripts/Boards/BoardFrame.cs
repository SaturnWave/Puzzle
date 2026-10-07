#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Art;
using Puzzle.Client.Layout;
using Puzzle.Client.Painting;
using Puzzle.Core.Model;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// The stepped gilt frame around a board: rails of gilt and dark lines that follow the outline of the playable
    /// cells around every step and every hole, a row of beads on the widest rail, the frame's shadow on the wall and
    /// its shade on the tray. The frame is as wide on a board of small cells as on one of large cells, so it is drawn
    /// in points and built again whenever the size of a cell or of a pixel changes. All of it follows the web test
    /// page's frame (framed-board.js), which strokes one path with one line per rail: here the same path is the
    /// outline 6 points inside the cells, and a rail is a band at its distance from that path.
    /// </summary>
    public sealed class BoardFrame : IDisposable
    {
        /// <summary>The frame's path lies this far inside the cells, so the tray's own corners come out round (radius 6).</summary>
        private const float PathInsideCells = 6f;

        /// <summary>The beads' ring, past the cells: the middle of the widest rail.</summary>
        private const float BeadsPastCells = 5.6f;

        /// <summary>
        /// The rails from the tray outwards, each up to its outer edge in points past the cells, as the web page's
        /// strokes leave them visible (half a stroke's width less the 6 points the path lies inside the cells).
        /// </summary>
        private static readonly Rail[] Rails =
        {
            new Rail(RailPaint.InnerLine, 1f),
            new Rail(RailPaint.PaleGilt, 2.7f),
            new Rail(RailPaint.MiddleLine, 3.5f),
            new Rail(RailPaint.Gilt, 8.4f),
            new Rail(RailPaint.EdgeLight, 9.4f),
            new Rail(RailPaint.OuterLine, 10.6f),
        };

        private static readonly Color InnerLine = Tint.Of(0x6B3E08);
        private static readonly Color MiddleLine = Tint.Of(0x9C5C0B);
        private static readonly Color OuterLine = Tint.Of(0x5E3305);
        private static readonly Color Bronze = Tint.Of(0xB87412);
        private static readonly Color EdgeInLight = Tint.Of(0xFFF8D6);
        private static readonly Color EdgeInShade = Tint.Of(0x9C5C0B);

        /// <summary>Gilt is lighter at the top of the board than at its foot, as if lit from above.</summary>
        private static readonly ColourRamp Gilt = new ColourRamp((0f, Tint.Of(0xFFE892)), (0.45f, Tint.Of(0xF6B93C)), (1f, Tint.Of(0xDE9A22)));
        private static readonly ColourRamp PaleGilt = new ColourRamp((0f, Tint.Of(0xFFF7D2)), (0.5f, Tint.Of(0xFFDF7C)), (1f, Tint.Of(0xF3BC48)));

        /// <summary>The gilt gradients start this far above the cells and end as far below them.</summary>
        private const float GradientMargin = 10f;

        /// <summary>Two streaks of white light lie across the board from its top left to its bottom right; only the gilt rail shows them.</summary>
        private static readonly ColourRamp Sheen = new ColourRamp(
            (0.1f, Tint.Of(0xFFFFFF, 0f)), (0.2f, Tint.Of(0xFFFFFF, 0.65f)), (0.3f, Tint.Of(0xFFFFFF, 0f)),
            (0.62f, Tint.Of(0xFFFFFF, 0f)), (0.7f, Tint.Of(0xFFFFFF, 0.4f)), (0.78f, Tint.Of(0xFFFFFF, 0f)));

        private const float SheenStrength = 0.55f;

        /// <summary>The light comes from the upper left of the page (y grows downwards): sides that look that way catch it.</summary>
        private static readonly Vector2 TowardsLight = new Vector2(-0.8f, -1f).normalized;

        /// <summary>A side that looks this much towards the light is fully lit: the left side does, as the top side does.</summary>
        private const float FullyLitFrom = 0.6f;

        /// <summary>The shadow on the wall: full up to its first distance from the frame's path, gone at the second, and dropped.</summary>
        private static readonly Color WallShadow = Tint.Of(0x031E24, 0.4f);
        private const float WallShadowFullUntil = 11f;
        private const float WallShadowGoneAt = 23f;
        private const float WallShadowDrop = 9f;

        /// <summary>The shade the frame throws on the tray, from the cells' edge inwards.</summary>
        private static readonly Color TrayShade = Tint.Of(0x0B4C52);
        private static readonly float[] TrayShadeDistances = { 0f, -2.5f, -6.5f };
        private static readonly float[] TrayShadeStrengths = { 0.3f, 0.15f, 0f };

        private readonly IReadOnlyList<IReadOnlyList<GridCorner>> _outline;
        private readonly BoardShape _shape;
        private readonly Transform _root;
        private readonly PaintedShape _shadow;
        private readonly PaintedShape _trayShade;
        private readonly PaintedShape _rails;
        private readonly PaintedShape _beads;
        private readonly Texture2D _beadPicture;
        private (float CellSize, float PixelsPerPoint)? _builtFor;

        private BoardFrame(Transform parent, ArtCatalogue art, BoardShape shape)
        {
            _shape = shape;
            _outline = BoardOutline.LoopsOf(shape);
            _root = new GameObject("Frame").transform;
            _root.SetParent(parent, worldPositionStays: false);
            _beadPicture = FrameBeads.BuildPicture();
            _shadow = PaintedShape.Create("Shadow", _root, art.PaintMaterial, DrawOrder.FrameShadow);
            _shadow.Transform.localPosition = new Vector3(0f, -WallShadowDrop, 0f);
            _trayShade = PaintedShape.Create("Tray shade", _root, art.PaintMaterial, DrawOrder.TrayShade);
            _rails = PaintedShape.Create("Rails", _root, art.PaintMaterial, DrawOrder.Frame);
            _beads = PaintedShape.Create("Beads", _root, art.PaintMaterial, DrawOrder.FrameBeads, _beadPicture);
        }

        /// <summary>What a rail is painted with. A closed set: each has its own rule in <see cref="RailPainter"/>.</summary>
        private enum RailPaint
        {
            InnerLine,
            PaleGilt,
            MiddleLine,
            Gilt,
            EdgeLight,
            OuterLine,
        }

        public static BoardFrame Create(Transform parent, ArtCatalogue art, BoardShape shape) => new BoardFrame(parent, art, shape);

        /// <summary>Puts the frame around the board where the layout has it, and draws it for the layout's cells and pixels.</summary>
        public void Place(ScreenLayout layout)
        {
            _root.localPosition = new Vector3(layout.BoardLeft, layout.BoardTop, 0f);
            if (_builtFor == (layout.CellSize, layout.PixelsPerPoint))
            {
                return;
            }

            _builtFor = (layout.CellSize, layout.PixelsPerPoint);
            float cellSize = layout.CellSize;
            _shadow.Show(BuildShadow(cellSize));
            _trayShade.Show(BuildTrayShade(cellSize));
            _rails.Show(BuildRails(cellSize, softEdge: 1f / layout.PixelsPerPoint));
            _beads.Show(BuildBeads(cellSize));
        }

        public void Dispose()
        {
            _shadow.Dispose();
            _trayShade.Dispose();
            _rails.Dispose();
            _beads.Dispose();
            SceneObjects.Destroy(_beadPicture);
            SceneObjects.DestroyObjectOf(_root);
        }

        /// <summary>The spokes around the frame's path, with open corners turned round: the rails, the beads and the shadow follow them.</summary>
        /// <param name="bandReach">How far from the path the band on these spokes goes; only a band with steps along its sides needs to say.</param>
        private static List<Spoke> FrameSpokes(IReadOnlyList<GridCorner> loop, float cellSize, float longestStep, float bandReach = 0f) =>
            OutlineSpokes.Around(loop, cellSize, -PathInsideCells, CornerJoin.RoundWhereOpen, longestStep, bandReach);

        private Mesh BuildRails(float cellSize, float softEdge)
        {
            var painter = new RailPainter(_shape.Width * cellSize, _shape.Height * cellSize, softEdge);
            var sketch = new MeshSketch();
            foreach (IReadOnlyList<GridCorner> loop in _outline)
            {
                // The sheen changes along a side, so sides are cut into steps short enough to follow it.
                List<Spoke> spokes = FrameSpokes(loop, cellSize, longestStep: cellSize / 2f, bandReach: painter.Distances[painter.Distances.Count - 1]);
                OutlineBand.Add(sketch, spokes, painter.Distances, painter.ColourAt);
            }

            return sketch.ToMesh("Frame rails");
        }

        private Mesh BuildShadow(float cellSize)
        {
            float[] distances = { 0f, WallShadowFullUntil, WallShadowGoneAt };
            Color32 full = WallShadow;
            Color32 gone = new Color(WallShadow.r, WallShadow.g, WallShadow.b, 0f);
            var sketch = new MeshSketch();
            foreach (IReadOnlyList<GridCorner> loop in _outline)
            {
                OutlineBand.Add(sketch, FrameSpokes(loop, cellSize, float.PositiveInfinity), distances, (spoke, stop, point) => stop == 2 ? gone : full);
            }

            return sketch.ToMesh("Frame shadow");
        }

        private Mesh BuildTrayShade(float cellSize)
        {
            var sketch = new MeshSketch();
            foreach (IReadOnlyList<GridCorner> loop in _outline)
            {
                List<Spoke> spokes = OutlineSpokes.Around(loop, cellSize, movedOut: 0f, CornerJoin.Sharp, float.PositiveInfinity);
                OutlineBand.Add(sketch, spokes, TrayShadeDistances, (spoke, stop, point) =>
                    (Color32)new Color(TrayShade.r, TrayShade.g, TrayShade.b, TrayShadeStrengths[stop]));
            }

            return sketch.ToMesh("Tray shade");
        }

        private Mesh BuildBeads(float cellSize)
        {
            var sketch = new MeshSketch();
            foreach (IReadOnlyList<GridCorner> loop in _outline)
            {
                foreach (List<Vector2> stretch in BeadStretches(FrameSpokes(loop, cellSize, float.PositiveInfinity)))
                {
                    FrameBeads.AddStretch(sketch, stretch);
                }
            }

            return sketch.ToMesh("Frame beads");
        }

        /// <summary>
        /// The beads' ring around one loop, cut where its corners are: the turn of each corner (which has no length
        /// where the corner is sharp), then the straight side from it to the next corner.
        /// </summary>
        /// <param name="spokes">The loop's spokes with no steps along the sides: the spokes of a corner share their origin, and a side lies between two corners.</param>
        private static IEnumerable<List<Vector2>> BeadStretches(List<Spoke> spokes)
        {
            Vector2 BeadOn(Spoke spoke) => spoke.At(PathInsideCells + BeadsPastCells);

            int first = 0;
            while (first < spokes.Count)
            {
                var turn = new List<Vector2> { BeadOn(spokes[first]) };
                int last = first;
                while (last + 1 < spokes.Count && spokes[last + 1].Origin == spokes[first].Origin)
                {
                    last++;
                    turn.Add(BeadOn(spokes[last]));
                }

                yield return turn;
                yield return new List<Vector2> { BeadOn(spokes[last]), BeadOn(spokes[(last + 1) % spokes.Count]) };
                first = last + 1;
            }
        }

        /// <summary>A rail: its paint, and its outer edge in points past the cells. It begins where the rail before it ends.</summary>
        private readonly struct Rail
        {
            public Rail(RailPaint paint, float until)
            {
                Paint = paint;
                Until = until;
            }

            public RailPaint Paint { get; }

            public float Until { get; }
        }

        /// <summary>
        /// The rails as stops across the band, and the colour of each. A rail has a stop just inside each of its two
        /// edges, so it keeps its colour over its whole width and blends into its neighbour over the width of one
        /// pixel; one more stop at each end of the band fades the frame's own edges out, so they are smooth too.
        /// </summary>
        private sealed class RailPainter
        {
            private readonly float _boardWidth;
            private readonly float _boardHeight;
            private readonly List<float> _distances = new List<float>();
            private readonly List<(RailPaint Paint, bool IsPainted)> _stops = new List<(RailPaint, bool)>();

            public RailPainter(float boardWidth, float boardHeight, float softEdge)
            {
                _boardWidth = boardWidth;
                _boardHeight = boardHeight;
                float half = softEdge / 2f;
                AddStop(-half, Rails[0].Paint, isPainted: false);
                float from = 0f;
                foreach (Rail rail in Rails)
                {
                    AddStop(from + half, rail.Paint, isPainted: true);
                    AddStop(rail.Until - half, rail.Paint, isPainted: true);
                    from = rail.Until;
                }

                AddStop(from + half, Rails[Rails.Length - 1].Paint, isPainted: false);
            }

            /// <summary>The stops, as distances from the frame's path.</summary>
            public IReadOnlyList<float> Distances => _distances;

            public Color32 ColourAt(Spoke spoke, int stop, Vector2 pagePoint)
            {
                Color colour = PaintAt(_stops[stop].Paint, spoke, pagePoint);
                colour.a = _stops[stop].IsPainted ? 1f : 0f;
                return colour;
            }

            private void AddStop(float pastCells, RailPaint paint, bool isPainted)
            {
                _distances.Add(PathInsideCells + pastCells);
                _stops.Add((paint, isPainted));
            }

            private Color PaintAt(RailPaint paint, Spoke spoke, Vector2 pagePoint)
            {
                float downTheBoard = (pagePoint.y + GradientMargin) / (_boardHeight + 2f * GradientMargin);
                return paint switch
                {
                    RailPaint.InnerLine => InnerLine,
                    RailPaint.PaleGilt => PaleGilt.At(downTheBoard),
                    RailPaint.MiddleLine => MiddleLine,
                    RailPaint.Gilt => Tint.Over(SheenAt(pagePoint), Gilt.At(downTheBoard)),
                    RailPaint.EdgeLight => EdgeFacing(spoke.Facing),
                    RailPaint.OuterLine => OuterLine,
                    _ => throw new ArgumentOutOfRangeException(nameof(paint), paint, "A rail without a paint."),
                };
            }

            /// <summary>The white of the sheen at a point: it runs along the line from the cells' top left corner to their bottom right one.</summary>
            private Color SheenAt(Vector2 pagePoint)
            {
                float alongTheDiagonal = (pagePoint.x * _boardWidth + pagePoint.y * _boardHeight)
                    / (_boardWidth * _boardWidth + _boardHeight * _boardHeight);
                Color sheen = Sheen.At(alongTheDiagonal);
                sheen.a *= SheenStrength;
                return sheen;
            }

            /// <summary>The thin line outside the gilt: bright where the side looks towards the light, dark where it looks away.</summary>
            private static Color EdgeFacing(Vector2 facing)
            {
                float lit = Mathf.Clamp(Vector2.Dot(facing, TowardsLight) / FullyLitFrom, -1f, 1f);
                return lit >= 0f ? Color.Lerp(Bronze, EdgeInLight, lit) : Color.Lerp(Bronze, EdgeInShade, -lit);
            }
        }
    }
}
