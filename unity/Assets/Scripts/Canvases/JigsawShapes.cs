#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Puzzle.Client.Painting;
using Puzzle.Museum.Jigsaw;
using UnityEngine;

namespace Puzzle.Client.Canvases
{
    /// <summary>
    /// The shapes of a cut painting as the scene draws them. A cut is worked out in the picture's own frame: from its
    /// top left corner, y growing downwards. The scene draws a painting around its middle with y growing upwards,
    /// and takes the colour of a point from the place in the picture's texture that the point has in the picture.
    /// </summary>
    public static class JigsawShapes
    {
        /// <summary>A knob is four curves; six points along each draw it round at the size of a phone's screen.</summary>
        public const int PointsPerCurve = 6;

        /// <summary>
        /// The corners and triangles of each piece that was drawn, kept beside the piece: cutting an outline with
        /// its four knobs into triangles is the costly part of drawing a piece, and a canvas draws its pieces anew
        /// every time one of them is picked up or dropped in. A piece's entry lives exactly as long as the piece
        /// itself (the table holds neither alive), so what is kept is bounded by the cuts the paintings keep.
        /// </summary>
        private static readonly ConditionalWeakTable<JigsawPiece, PieceFace> Faces = new ConditionalWeakTable<JigsawPiece, PieceFace>();

        /// <summary>A piece's outline around the middle of the painting it is cut from, as a closed row of points.</summary>
        public static List<Vector2> OutlineOf(JigsawPiece piece, Vector2 paintingSize)
        {
            IReadOnlyList<Point2> corners = piece.Outline.Flatten(PointsPerCurve);
            var outline = new List<Vector2>(corners.Count);
            foreach (Point2 corner in corners)
            {
                outline.Add(InScene(corner, paintingSize));
            }

            return outline;
        }

        /// <summary>A stretch of a cut line around the middle of the painting, as a row of points from its start to its end.</summary>
        public static List<Vector2> LineOf(OpenPath path, Vector2 paintingSize)
        {
            var line = new List<Vector2> { InScene(path.Start, paintingSize) };
            Point2 from = path.Start;
            foreach (PathStep step in path.Steps)
            {
                if (step is PathStep.Curve curve)
                {
                    for (int point = 1; point < PointsPerCurve; point++)
                    {
                        line.Add(InScene(curve.At(from, (double)point / PointsPerCurve), paintingSize));
                    }
                }

                line.Add(InScene(step.End, paintingSize));
                from = step.End;
            }

            return line;
        }

        /// <summary>
        /// Adds a piece's face to a sketch: its outline, filled with the part of the picture that is the piece. The
        /// sketch is drawn with the painting's picture as its texture, or with none for a plain face.
        /// </summary>
        /// <param name="movedBy">How far from its own place in the painting the piece is drawn.</param>
        public static void AddFace(MeshSketch sketch, JigsawPiece piece, Vector2 paintingSize, Vector2 movedBy, Color32 colour)
        {
            PieceFace face = Faces.GetValue(piece, PieceFace.Of);
            IReadOnlyList<Point2> corners = face.Corners;
            int firstCorner = -1;
            for (int corner = 0; corner < corners.Count; corner++)
            {
                var inPicture = new Vector2((float)(corners[corner].X / paintingSize.x), 1f - (float)(corners[corner].Y / paintingSize.y));
                int added = sketch.AddCorner(InScene(corners[corner], paintingSize) + movedBy, colour, inPicture);
                firstCorner = corner == 0 ? added : firstCorner;
            }

            // The corners were added one after another, so a corner's number in the sketch is the first one's plus its own.
            IReadOnlyList<int> triangles = face.Triangles;
            for (int triangle = 0; triangle + 2 < triangles.Count; triangle += 3)
            {
                sketch.AddTriangle(firstCorner + triangles[triangle], firstCorner + triangles[triangle + 1], firstCorner + triangles[triangle + 2]);
            }
        }

        /// <summary>
        /// Adds a piece's shape in one colour that fades to nothing over a band around it: the shadow a piece throws
        /// on what it is held over.
        /// </summary>
        /// <param name="movedBy">How far from its own place in the painting the shape is drawn.</param>
        /// <param name="fade">The width of the band the colour fades out over.</param>
        public static void AddShade(MeshSketch sketch, JigsawPiece piece, Vector2 paintingSize, Vector2 movedBy, Color colour, float fade)
        {
            AddFace(sketch, piece, paintingSize, movedBy, colour);

            List<Vector2> outline = OutlineOf(piece, paintingSize);
            Color32 full = colour;
            Color32 gone = new Color(colour.r, colour.g, colour.b, 0f);
            float outwardTurn = TurnsLeft(outline) ? -1f : 1f;
            var onTheEdge = new int[outline.Count];
            var beyondIt = new int[outline.Count];
            for (int point = 0; point < outline.Count; point++)
            {
                Vector2 along = (outline[(point + 1) % outline.Count] - outline[(point + outline.Count - 1) % outline.Count]).normalized;
                Vector2 outward = outwardTurn * new Vector2(-along.y, along.x);
                onTheEdge[point] = sketch.AddCorner(outline[point] + movedBy, full);
                beyondIt[point] = sketch.AddCorner(outline[point] + movedBy + outward * fade, gone);
            }

            for (int point = 0; point < outline.Count; point++)
            {
                int next = (point + 1) % outline.Count;
                sketch.AddQuad(onTheEdge[point], beyondIt[point], beyondIt[next], onTheEdge[next]);
            }
        }

        /// <summary>Where a point of the picture lies in the scene, around the painting's middle.</summary>
        public static Vector2 InScene(Point2 inPicture, Vector2 paintingSize) =>
            new Vector2((float)inPicture.X - paintingSize.x / 2f, paintingSize.y / 2f - (float)inPicture.Y);

        /// <summary>A piece's outline as a row of corners, in the picture's own frame, and the triangles that fill it.</summary>
        private sealed class PieceFace
        {
            private PieceFace(IReadOnlyList<Point2> corners, IReadOnlyList<int> triangles)
            {
                Corners = corners;
                Triangles = triangles;
            }

            public IReadOnlyList<Point2> Corners { get; }

            /// <summary>Three corner numbers for each triangle.</summary>
            public IReadOnlyList<int> Triangles { get; }

            public static PieceFace Of(JigsawPiece piece)
            {
                IReadOnlyList<Point2> corners = piece.Outline.Flatten(PointsPerCurve);
                return new PieceFace(corners, PolygonTriangles.Of(corners));
            }
        }

        /// <summary>True when a closed row of points runs around its inside with the inside at its left (counter-clockwise, y growing upwards).</summary>
        private static bool TurnsLeft(List<Vector2> outline)
        {
            float twiceTheArea = 0f;
            for (int point = 0; point < outline.Count; point++)
            {
                Vector2 here = outline[point];
                Vector2 next = outline[(point + 1) % outline.Count];
                twiceTheArea += here.x * next.y - next.x * here.y;
            }

            return twiceTheArea > 0f;
        }
    }
}
