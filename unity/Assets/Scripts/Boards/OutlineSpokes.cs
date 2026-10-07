using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Client.Boards
{
    /// <summary>
    /// A line across a band that runs around the board's outline, like a spoke across a rim: it starts on the outline
    /// and gives the point any distance out from it (or, with a negative distance, in from it). A band is drawn as
    /// the faces between neighbouring spokes. Spokes are in the board's page: points, the origin at the top left
    /// corner of the cells, y growing downwards.
    /// </summary>
    public readonly struct Spoke
    {
        public Spoke(Vector2 origin, Vector2 direction, Vector2 facing, float longestReach)
        {
            Origin = origin;
            Direction = direction;
            Facing = facing;
            LongestReach = longestReach;
        }

        /// <summary>Where the spoke meets the outline.</summary>
        public Vector2 Origin { get; }

        /// <summary>One step out: of length 1 along a side and around a round corner, the diagonal of both sides in a sharp corner.</summary>
        public Vector2 Direction { get; }

        /// <summary>Which way the side this spoke belongs to looks, of length 1: decides how the light falls on it.</summary>
        public Vector2 Facing { get; }

        /// <summary>
        /// A sharp corner that points into the band runs into the spokes of the corner across a narrow gap when it
        /// reaches too far; past this distance the spoke stays where it is.
        /// </summary>
        public float LongestReach { get; }

        public Vector2 At(float distance) => Origin + Direction * Mathf.Clamp(distance, -LongestReach, LongestReach);
    }

    /// <summary>How a band turns the outline's corners.</summary>
    public enum CornerJoin
    {
        /// <summary>Every corner is sharp: both sides run on until they meet.</summary>
        Sharp,

        /// <summary>A corner that opens outwards is turned in an arc; one that closes stays sharp. A stroked line does the same.</summary>
        RoundWhereOpen,
    }

    /// <summary>The spokes of a band around one loop of the board's outline.</summary>
    public static class OutlineSpokes
    {
        /// <summary>A round corner is turned in this many steps: at the frame's width no step is longer than two points.</summary>
        private const int StepsOfARoundCorner = 12;

        /// <param name="loop">A loop of <see cref="BoardOutline.LoopsOf"/>.</param>
        /// <param name="movedOut">How far outside the cells the band's own outline lies, in points; negative for inside.</param>
        /// <param name="longestStep">Sides are cut into steps no longer than this, so a colour can change along a side.</param>
        /// <param name="bandReach">
        /// How far out from its outline the band goes that will be drawn on these spokes. Beside a sharp corner the
        /// band of one side begins that far along the other, so the steps along a side keep that far from a sharp
        /// corner: a step nearer to it would lie behind the corner's own spoke, and the band would fold back over
        /// itself there. A band without steps along its sides (an endless <paramref name="longestStep"/>) needs none.
        /// </param>
        public static List<Spoke> Around(IReadOnlyList<GridCorner> loop, float cellSize, float movedOut, CornerJoin join, float longestStep, float bandReach = 0f)
        {
            int count = loop.Count;
            var outwards = new Vector2[count]; // of the side that starts at each corner
            var origins = new Vector2[count];
            for (int corner = 0; corner < count; corner++)
            {
                outwards[corner] = Outward(loop[corner], loop[(corner + 1) % count]);
            }

            for (int corner = 0; corner < count; corner++)
            {
                Vector2 sidesApart = outwards[(corner + count - 1) % count] + outwards[corner];
                origins[corner] = new Vector2(loop[corner].X, loop[corner].Y) * cellSize + sidesApart * movedOut;
            }

            bool IsSharp(int corner) =>
                join == CornerJoin.Sharp || !OpensOutwards(outwards[(corner + count - 1) % count], outwards[corner]);

            var spokes = new List<Spoke>();
            for (int corner = 0; corner < count; corner++)
            {
                int nextCorner = (corner + 1) % count;
                Vector2 before = outwards[(corner + count - 1) % count];
                Vector2 after = outwards[corner];
                Vector2 previous = origins[(corner + count - 1) % count];
                Vector2 next = origins[nextCorner];
                if (IsSharp(corner))
                {
                    float shorterSide = Mathf.Min(Vector2.Distance(previous, origins[corner]), Vector2.Distance(origins[corner], next));
                    AddSharpCorner(spokes, origins[corner], before, after, longestReach: shorterSide / 2f);
                }
                else
                {
                    AddArc(spokes, origins[corner], before, after);
                }

                AddSide(spokes, origins[corner], next, after, longestStep, IsSharp(corner) ? bandReach : 0f, IsSharp(nextCorner) ? bandReach : 0f);
            }

            return spokes;
        }

        /// <summary>Which way is out for the side from one corner to the next: the cells lie to the right of the walk.</summary>
        private static Vector2 Outward(GridCorner from, GridCorner to) =>
            new Vector2(Math.Sign(to.Y - from.Y), -Math.Sign(to.X - from.X));

        /// <summary>True for a corner of the board that points away from the cells: the two sides look apart.</summary>
        private static bool OpensOutwards(Vector2 before, Vector2 after) => before.x * after.y - before.y * after.x > 0f;

        private static void AddArc(List<Spoke> spokes, Vector2 origin, Vector2 before, Vector2 after)
        {
            for (int step = 0; step <= StepsOfARoundCorner; step++)
            {
                float angle = step * (Mathf.PI / 2f) / StepsOfARoundCorner;
                Vector2 direction = before * Mathf.Cos(angle) + after * Mathf.Sin(angle);
                spokes.Add(new Spoke(origin, direction, facing: direction, float.PositiveInfinity));
            }
        }

        /// <summary>
        /// One spoke along the corner's diagonal, which looks the way between its two sides. So the light of one
        /// side passes into the light of the other over the steps next to the corner, as it does around a round
        /// corner. Were each side to keep its own light up to the diagonal, a side in the light would meet a side in
        /// the shade there with a hard seam, and the dark line of the one would seem to run on past the other.
        /// </summary>
        private static void AddSharpCorner(List<Spoke> spokes, Vector2 origin, Vector2 before, Vector2 after, float longestReach)
        {
            Vector2 diagonal = before + after;
            spokes.Add(new Spoke(origin, diagonal, facing: diagonal.normalized, longestReach));
        }

        /// <summary>
        /// The spokes along a side between its two ends; the corners add the ends themselves. The stretch that is
        /// kept clear at an end belongs to that end's corner; where one is kept clear, the first spoke stands at its
        /// edge, so the side has its own light from there on.
        /// </summary>
        /// <param name="clearAtStart">How far from the side's beginning its first spoke must stay; nothing lets the steps begin at the corner.</param>
        /// <param name="clearAtEnd">The same at the side's end.</param>
        private static void AddSide(List<Spoke> spokes, Vector2 from, Vector2 to, Vector2 outward, float longestStep, float clearAtStart, float clearAtEnd)
        {
            float length = Vector2.Distance(from, to);
            float stepped = length - clearAtStart - clearAtEnd;
            if (stepped <= 0f || float.IsPositiveInfinity(longestStep))
            {
                return;
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(stepped / longestStep));
            int first = clearAtStart > 0f ? 0 : 1;
            int last = clearAtEnd > 0f ? steps : steps - 1;
            for (int step = first; step <= last; step++)
            {
                float along = (clearAtStart + stepped * step / steps) / length;
                spokes.Add(new Spoke(Vector2.Lerp(from, to, along), outward, facing: outward, float.PositiveInfinity));
            }
        }
    }
}
