using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.Painting
{
    /// <summary>
    /// A drawn line along a row of points: the cut lines of a jigsaw, the seams of a floor. The game's shapes are
    /// meshes of plain colour and nothing smooths their edges, so a line is a band of its colour between two narrow
    /// bands that fade it out; drawn over the edge of another shape it hides that edge's steps as well.
    /// </summary>
    public static class Stroke
    {
        /// <summary>A corner sharper than this is not drawn out to its point: the two sides' edges would meet far away.</summary>
        private const float LongestMitre = 2.5f;

        /// <summary>Adds a line to a sketch.</summary>
        /// <param name="points">The line's points in order; fewer than two draw nothing.</param>
        /// <param name="isClosed">True when the last point is joined to the first.</param>
        /// <param name="width">The width of the line's full colour.</param>
        /// <param name="softEdge">The width of the fade at each side: about a pixel.</param>
        public static void Add(MeshSketch sketch, IReadOnlyList<Vector2> points, bool isClosed, float width, float softEdge, Color colour)
        {
            if (points.Count < 2)
            {
                return;
            }

            Color32 full = colour;
            Color32 gone = new Color(colour.r, colour.g, colour.b, 0f);
            float half = width / 2f;
            float[] across = { -half - softEdge, -half, half, half + softEdge };
            Color32[] colours = { gone, full, full, gone };

            var firstCornerOf = new int[points.Count];
            for (int point = 0; point < points.Count; point++)
            {
                Vector2 sideways = SidewaysAt(points, point, isClosed);
                for (int band = 0; band < across.Length; band++)
                {
                    int corner = sketch.AddCorner(points[point] + sideways * across[band], colours[band]);
                    if (band == 0)
                    {
                        firstCornerOf[point] = corner;
                    }
                }
            }

            int stretches = isClosed ? points.Count : points.Count - 1;
            for (int stretch = 0; stretch < stretches; stretch++)
            {
                int here = firstCornerOf[stretch];
                int next = firstCornerOf[(stretch + 1) % points.Count];
                for (int band = 0; band + 1 < across.Length; band++)
                {
                    sketch.AddQuad(here + band, here + band + 1, next + band + 1, next + band);
                }
            }
        }

        /// <summary>Adds a broken line: dashes of one length with gaps of another between them, measured along the line.</summary>
        public static void AddDashed(MeshSketch sketch, IReadOnlyList<Vector2> points, float width, float softEdge, Color colour, float dash, float gap)
        {
            var dashPoints = new List<Vector2>();
            float untilNextChange = dash;
            bool isInADash = true;
            for (int point = 0; point + 1 < points.Count; point++)
            {
                Vector2 from = points[point];
                Vector2 to = points[point + 1];
                float left = Vector2.Distance(from, to);
                if (isInADash && dashPoints.Count == 0)
                {
                    dashPoints.Add(from);
                }

                while (left > untilNextChange)
                {
                    from = Vector2.MoveTowards(from, to, untilNextChange);
                    left -= untilNextChange;
                    if (isInADash)
                    {
                        dashPoints.Add(from);
                        Add(sketch, dashPoints, isClosed: false, width, softEdge, colour);
                        dashPoints.Clear();
                    }
                    else
                    {
                        dashPoints.Add(from);
                    }

                    isInADash = !isInADash;
                    untilNextChange = isInADash ? dash : gap;
                }

                untilNextChange -= left;
                if (isInADash)
                {
                    dashPoints.Add(to);
                }
            }

            Add(sketch, dashPoints, isClosed: false, width, softEdge, colour);
        }

        /// <summary>
        /// Which way is across the line at one of its points, and how far to go that way for one unit of width: at a
        /// bend the two sides' edges meet on the line that halves the bend, further out than the width itself.
        /// </summary>
        private static Vector2 SidewaysAt(IReadOnlyList<Vector2> points, int point, bool isClosed)
        {
            int last = points.Count - 1;
            bool hasBefore = isClosed || point > 0;
            bool hasAfter = isClosed || point < last;
            Vector2 arriving = hasBefore ? Direction(points[(point + last) % points.Count], points[point]) : Vector2.zero;
            Vector2 leaving = hasAfter ? Direction(points[point], points[(point + 1) % points.Count]) : Vector2.zero;
            Vector2 along = (arriving + leaving).normalized;
            if (along == Vector2.zero)
            {
                // The line turns straight back on itself, or two of its points lie on each other: any side will do.
                along = arriving != Vector2.zero ? arriving : leaving;
            }

            var sideways = new Vector2(-along.y, along.x);
            Vector2 side = arriving != Vector2.zero ? arriving : leaving;
            float cosineOfHalfTheBend = Mathf.Abs(Vector2.Dot(sideways, new Vector2(-side.y, side.x)));
            return sideways * Mathf.Min(LongestMitre, 1f / Mathf.Max(cosineOfHalfTheBend, 1e-4f));
        }

        private static Vector2 Direction(Vector2 from, Vector2 to) => (to - from).normalized;
    }
}
