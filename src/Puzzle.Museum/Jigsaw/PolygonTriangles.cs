using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>
    /// Cuts a piece's flattened outline into triangles, which is what a renderer fills. Ears are clipped one by one:
    /// a corner whose triangle with its two neighbours lies inside the outline and holds no other corner is cut off,
    /// until three corners are left.
    /// </summary>
    public static class PolygonTriangles
    {
        /// <summary>
        /// The triangles of a polygon, as corner numbers, three for each triangle, each triangle clockwise like the
        /// polygon. No triangle is flat: a corner that lies on a straight line between its neighbours is never the
        /// tip of one.
        /// </summary>
        /// <param name="polygon">
        /// Corners that run clockwise as the picture is seen (y downwards) along an outline that does not cross
        /// itself: what <see cref="PieceOutline.Flatten"/> gives.
        /// </param>
        /// <exception cref="ArgumentException">The corners do not run clockwise round a shape that does not cross itself.</exception>
        public static IReadOnlyList<int> Of(IReadOnlyList<Point2> polygon)
        {
            var left = new List<int>(polygon.Count);
            for (int corner = 0; corner < polygon.Count; corner++)
            {
                left.Add(corner);
            }

            var triangles = new List<int>(3 * Math.Max(0, polygon.Count - 2));
            int at = 0;
            int lookedAtSinceTheLastCut = 0;
            while (left.Count >= 3)
            {
                if (lookedAtSinceTheLastCut > left.Count)
                {
                    throw new ArgumentException("The corners do not run clockwise round a shape that does not cross itself.", nameof(polygon));
                }

                at %= left.Count;
                int before = left[(at + left.Count - 1) % left.Count];
                int corner = left[at];
                int after = left[(at + 1) % left.Count];
                double turn = Turn(polygon[before], polygon[corner], polygon[after]);
                bool isAnEar = turn > 0 && HoldsNoOtherCorner(polygon, left, before, corner, after);
                if (turn != 0 && !isAnEar)
                {
                    at++;
                    lookedAtSinceTheLastCut++;
                    continue;
                }

                if (isAnEar)
                {
                    triangles.Add(before);
                    triangles.Add(corner);
                    triangles.Add(after);
                }

                left.RemoveAt(at); // an ear is cut off; a corner on a straight line is dropped without a triangle, which would be flat
                lookedAtSinceTheLastCut = 0;
            }

            return triangles;
        }

        /// <summary>Positive where the way from a over b to c turns right as the picture is seen: a corner that points outwards on a clockwise outline. Zero on a straight line.</summary>
        private static double Turn(Point2 a, Point2 b, Point2 c) => (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);

        private static bool HoldsNoOtherCorner(IReadOnlyList<Point2> polygon, List<int> left, int before, int corner, int after)
        {
            Point2 a = polygon[before];
            Point2 b = polygon[corner];
            Point2 c = polygon[after];
            foreach (int other in left)
            {
                if (other == before || other == corner || other == after)
                {
                    continue;
                }

                Point2 point = polygon[other];
                bool isInside = Turn(a, b, point) > 0 && Turn(b, c, point) > 0 && Turn(c, a, point) > 0;
                if (isInside)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
