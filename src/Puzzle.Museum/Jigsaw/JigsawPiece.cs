using System;
using System.Collections.Generic;

namespace Puzzle.Museum.Jigsaw
{
    /// <summary>What a side of a piece is to the piece beyond it.</summary>
    public enum Joint
    {
        /// <summary>The picture's own straight edge: no piece lies beyond.</summary>
        Border,

        /// <summary>A straight stretch of cut line: the two pieces meet there over too short a length to carry a knob.</summary>
        Flat,

        /// <summary>This piece's knob reaches into the neighbour.</summary>
        Tab,

        /// <summary>The neighbour's knob reaches into this piece.</summary>
        Blank,
    }

    /// <summary>One side of a piece, or the part of a side it shares with one neighbour, walked clockwise as the picture is seen.</summary>
    public sealed class PieceEdge
    {
        internal PieceEdge(Joint joint, int? neighbour, OpenPath path)
        {
            Joint = joint;
            Neighbour = neighbour;
            Path = path;
        }

        public Joint Joint { get; }

        /// <summary>The number of the piece beyond this edge; nothing exactly when the edge is the picture's <see cref="Joint.Border"/>.</summary>
        public int? Neighbour { get; }

        public OpenPath Path { get; }
    }

    /// <summary>
    /// A piece's whole outline as one closed path, clockwise as the picture is seen, starting at the top left corner
    /// of the piece's cell. The last step ends where the path starts.
    /// </summary>
    public sealed class PieceOutline
    {
        internal PieceOutline(Point2 start, IReadOnlyList<PathStep> steps)
        {
            Start = start;
            Steps = steps;
        }

        public Point2 Start { get; }

        public IReadOnlyList<PathStep> Steps { get; }

        /// <summary>
        /// The outline as a polygon: every line gives its end point, every curve <paramref name="pointsPerCurve"/>
        /// points along it. The polygon's last point is joined to its first; the start is not repeated at the end.
        /// A knob is four curves, so 6 points per curve already draw it round at the size of a phone screen.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Fewer than one point per curve.</exception>
        public IReadOnlyList<Point2> Flatten(int pointsPerCurve)
        {
            if (pointsPerCurve < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerCurve), pointsPerCurve, "A curve is drawn with at least one point.");
            }

            var points = new List<Point2>(Steps.Count * pointsPerCurve);
            Point2 at = Start;
            foreach (PathStep step in Steps)
            {
                step.FlattenInto(points, at, pointsPerCurve);
                at = step.End;
            }

            // The path closes on its start, which FlattenInto added last; a polygon lists it once, and first.
            points.RemoveAt(points.Count - 1);
            points.Insert(0, Start);
            return points;
        }
    }

    /// <summary>One piece of a cut picture.</summary>
    public sealed class JigsawPiece
    {
        internal JigsawPiece(int number, PictureRect cell, IReadOnlyList<PieceEdge> edges)
        {
            Number = number;
            Cell = cell;
            Edges = edges;
            Outline = OutlineOf(edges);
            Neighbours = NeighboursOf(edges);
        }

        /// <summary>
        /// The piece's number in its picture, counted from 0 along the layout's bands: row by row from the top left in
        /// a landscape picture, column by column in a portrait one. Pieces are won in this order.
        /// </summary>
        public int Number { get; }

        /// <summary>The piece's rectangle in the layout, knobs and sockets aside. Its place in the picture never moves: the outline is drawn where the piece belongs.</summary>
        public PictureRect Cell { get; }

        /// <summary>Where the piece belongs: the middle of its cell. A piece dragged near enough to it snaps home.</summary>
        public Point2 Home => Cell.Centre;

        /// <summary>The sides in clockwise order, starting at the cell's top left corner. A side shared with two neighbours comes as two edges.</summary>
        public IReadOnlyList<PieceEdge> Edges { get; }

        public PieceOutline Outline { get; }

        /// <summary>The numbers of the pieces this one touches along a line, clockwise from the top left, each once.</summary>
        public IReadOnlyList<int> Neighbours { get; }

        private static PieceOutline OutlineOf(IReadOnlyList<PieceEdge> edges)
        {
            var steps = new List<PathStep>();
            foreach (PieceEdge edge in edges)
            {
                steps.AddRange(edge.Path.Steps);
            }

            return new PieceOutline(edges[0].Path.Start, steps);
        }

        private static List<int> NeighboursOf(IReadOnlyList<PieceEdge> edges)
        {
            var neighbours = new List<int>();
            foreach (PieceEdge edge in edges)
            {
                if (edge.Neighbour is int neighbour && !neighbours.Contains(neighbour))
                {
                    neighbours.Add(neighbour);
                }
            }

            return neighbours;
        }
    }
}
